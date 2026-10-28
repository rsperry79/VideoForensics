---
name: pbkdf2-yearly-bump
description: Yearly PBKDF2 iteration increase for Ring credential encryption (AesEncryption.cs), per OWASP guidance. Use in January or when asked to raise PBKDF2 iterations.
---

# PBKDF2 yearly bump

AES-256-CBC + PBKDF2-HMAC-SHA256. Iterations rise yearly per OWASP guidance.

Schedule: 600,000 (current baseline) → 750,000 (2026) → 1,000,000 (2027) → +25%/yr after.

Steps:
1. Check the OWASP Cryptographic Storage Cheat Sheet.
2. Update `Iterations` in `AesEncryption.cs` (`src/providers/ring/auth/src/Implementations/`).
3. Update the code comment with the next target year.
4. Add a test in `AesEncryptionTests.cs` asserting the new count.
5. Run `dotnet test --filter "Class=AesEncryptionTests"`.
6. No credential re-encryption needed (backward-compatible).
7. Commit message: "Security: Increase PBKDF2 iterations from X to Y per OWASP 2026 guidance".
