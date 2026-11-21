#!/usr/bin/env bash
# Builds the in-place update zip for a release and signs its manifest.
#
# Usage: build-update-package.sh <version> <publish-dir> <out-dir>
# Reads the Ed25519 private key (PEM) from VF_UPDATE_SIGNING_KEY.
#
# Why: the client refuses any package whose manifest.json lacks a valid detached signature
# (Ed25519PackageSignatureVerifier). The signature covers manifest.json, and the manifest lists the
# SHA-256 of every other file, so one signature covers the whole package.
set -euo pipefail

VERSION="${1:?version required}"
PUBLISH_DIR="${2:?publish dir required}"
OUT_DIR="${3:?output dir required}"
: "${VF_UPDATE_SIGNING_KEY:?VF_UPDATE_SIGNING_KEY secret is not set}"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
STAGE="$WORK/package"
KEY="$WORK/signing-key.pem"
mkdir -p "$STAGE" "$OUT_DIR"
# Resolve before the cd into the stage folder, so a relative out-dir still points at the caller's folder.
OUT_DIR="$(cd "$OUT_DIR" && pwd)"
cp -a "$PUBLISH_DIR"/. "$STAGE"/

# The key must be the one the client embeds. Compare the public half with the constant in the client source.
printf '%s\n' "$VF_UPDATE_SIGNING_KEY" > "$KEY"
chmod 600 "$KEY"
PUB_B64="$(openssl pkey -in "$KEY" -pubout -outform DER | base64 -w0)"
EMBEDDED_B64="$(sed -n 's/.*EmbeddedPublicKeySpkiBase64 = "\([^"]*\)".*/\1/p' src/client/host/VideoForensics.Hosting/src/Updates/Ed25519PackageSignatureVerifier.cs)"
if [ "$PUB_B64" != "$EMBEDDED_B64" ]; then
  echo "ERROR: the signing key does not match the public key embedded in the client." >&2
  exit 1
fi

cd "$STAGE"
find . -type f ! -name manifest.json ! -name manifest.json.sig | sed 's#^\./##' | LC_ALL=C sort > "$WORK/files.txt"
while IFS= read -r path; do
  printf '%s\t%s\t%s\n' "$path" "$(sha256sum "$path" | cut -d' ' -f1)" "$(stat -c %s "$path")"
done < "$WORK/files.txt" \
  | jq -R -s --arg version "$VERSION" '
      split("\n") | map(select(length > 0) | split("\t")
        | {path: .[0], sha256: .[1], size: (.[2] | tonumber)}) as $files
      | {version: $version, files: $files}' > manifest.json

openssl pkeyutl -sign -inkey "$KEY" -rawin -in manifest.json -out manifest.json.sig
SIG_SIZE="$(stat -c %s manifest.json.sig)"
if [ "$SIG_SIZE" != "64" ]; then
  echo "ERROR: Ed25519 signature is $SIG_SIZE bytes, expected 64." >&2
  exit 1
fi

# Check the signature the way the client will, before publishing anything.
openssl pkey -in "$KEY" -pubout -out "$WORK/public.pem"
openssl pkeyutl -verify -pubin -inkey "$WORK/public.pem" -rawin -in manifest.json -sigfile manifest.json.sig >/dev/null

ZIP="$OUT_DIR/VideoForensics-$VERSION-update-win-x64.zip"
rm -f "$ZIP"
zip -qr "$ZIP" .
echo "Wrote $ZIP"
