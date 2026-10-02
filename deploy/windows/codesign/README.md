# Windows installer code signing

`VideoForensicsSetup.exe` is signed at build time (`.github/workflows/publish-testing.yml`,
`Sign installer with self-signed certificate` step) with a **self-signed** certificate. The
private key exists only as GitHub Actions secrets (`WINDOWS_CODESIGN_PFX_BASE64`,
`WINDOWS_CODESIGN_PFX_PASSWORD`) - it is never checked into this repo.

## What this does and does not give you

- **Tamper-evidence**: the signature proves the exe wasn't modified after signing, and that it
  was signed by the holder of this specific private key.
- **Not OS-level trust**: because the certificate doesn't chain to a public root CA, Windows
  SmartScreen will still show an "Unknown Publisher" warning on first run. Self-signing does not
  suppress that warning - only a certificate from a trusted CA (or an EV cert) does.

## Verifying a downloaded installer

Check the signer's certificate matches this repo's published certificate before trusting it:

```powershell
(Get-AuthenticodeSignature .\VideoForensicsSetup.exe).SignerCertificate.Thumbprint
# Expected: 8B4907F1FFA309878A3CFED8ACD09D478FDB568C
```

Or compare the checked-in public certificate file's fingerprint directly:

```
openssl x509 -in VideoForensics-CodeSigning.cer -inform DER -noout -fingerprint -sha256
# sha256 Fingerprint=90:C9:7B:36:3A:F4:3B:EB:08:B1:8D:77:61:9A:7F:2B:03:56:19:9F:6F:6F:06:3D:69:B9:95:23:3E:E0:AC:BD
```

If either value doesn't match, do not run the installer - it wasn't produced by this project's
release pipeline.

## Certificate details

- Subject / Issuer: `O=DV Victim Protection Team, CN=VideoForensics`
- Key: RSA 4096, SHA256
- Validity: 2026-09-29 to 2036-09-29 (10 years)
- File: `VideoForensics-CodeSigning.cer` (public certificate only, DER format, no private key)

## Rotating the certificate

If the private key is ever compromised, generate a new self-signed cert, export the new public
`.cer` over this one, and replace the `WINDOWS_CODESIGN_PFX_BASE64` /
`WINDOWS_CODESIGN_PFX_PASSWORD` GitHub Actions secrets. Update the thumbprint/fingerprint values
above and in `.github/workflows/publish-testing.yml`'s release notes.
