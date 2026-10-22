---
paths:
  - "src/providers/ring/auth/**"
---

# PBKDF2 iterations (credential encryption)

AES-256-CBC + PBKDF2-HMAC-SHA256. Iterations rise yearly per OWASP guidance.

Schedule: 600,000 (current baseline) → 750,000 (2026) → 1,000,000 (2027) → +25%/yr after.

Yearly action (January):
1. Check the OWASP Cryptographic Storage Cheat Sheet.
2. Update `Iterations` in `AesEncryption.cs` (`src/providers/ring/auth/src/Implementations/`).
3. Update the code comment with the next target year.
4. Add a test in `AesEncryptionTests.cs` asserting the new count.
5. Run `dotnet test --filter "Class=AesEncryptionTests"`.
6. No credential re-encryption needed (backward-compatible).
7. Commit message: "Security: Increase PBKDF2 iterations from X to Y per OWASP 2026 guidance".

Full audit: `AES_ENCRYPTION_AUDIT_REPORT.md` (Section 10).
