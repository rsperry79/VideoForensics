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
# Expected: 102E1E3120A226AB239B3677348CD33675106D3D
```

Or compare the checked-in public certificate file's fingerprint directly:

```
openssl x509 -in VideoForensics-CodeSigning.cer -inform DER -noout -fingerprint -sha256
# sha256 Fingerprint=E7:90:DB:39:E5:E8:43:7F:5D:07:FF:7C:4B:46:A6:26:FD:3D:05:43:D6:EE:F0:39:B6:0D:01:CC:4D:9E:B9:16
```

If either value doesn't match, do not run the installer - it wasn't produced by this project's
release pipeline.

## Certificate details

- Subject / Issuer: `O=DV Victim Protection Team, CN=VideoForensics`
- Key: RSA 4096, SHA256
- Validity: 2026-09-28 to 2036-09-28 (10 years)
- File: `VideoForensics-CodeSigning.cer` (public certificate only, DER format, no private key)

## Rotating the certificate

If the private key is ever compromised, generate a new self-signed cert, export the new public
`.cer` over this one, and replace the `WINDOWS_CODESIGN_PFX_BASE64` /
`WINDOWS_CODESIGN_PFX_PASSWORD` GitHub Actions secrets. Update the thumbprint/fingerprint values
above and in `.github/workflows/publish-testing.yml`'s release notes.
