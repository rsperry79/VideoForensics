using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace VideoForensics.Client.Hosting.Updates
{
    /// <summary>
    /// Verifies a detached Ed25519 signature over a package's manifest.json. The manifest lists the SHA-256 of every file,
    /// so one signature covers the whole package. The signature is stored beside it as manifest.json.sig (64 raw bytes).
    /// Why: .NET has no built-in Ed25519, so BouncyCastle does the check.
    /// </summary>
    public sealed class Ed25519PackageSignatureVerifier : IPackageSignatureVerifier
    {
        /// <summary>Public half of the release signing key, as an SPKI DER key in base64. The private half lives only in CI.</summary>
        private const string EmbeddedPublicKeySpkiBase64 = "MCowBQYDK2VwAyEAFL1xisuyLhKmrO+JNUAm9yNCmQ0NmPGb3+9I2NSDusc=";

        /// <summary>Raw 32-byte Ed25519 public key compiled into the client.</summary>
        public static byte[] EmbeddedPublicKey { get; } = ExtractRawKey(Convert.FromBase64String(EmbeddedPublicKeySpkiBase64));

        private const string SignatureFileName = UpdateManifest.FileName + ".sig";
        private const int SignatureLength = 64;
        private readonly byte[] _publicKey;

        /// <summary>Creates the verifier for a raw 32-byte Ed25519 public key.</summary>
        public Ed25519PackageSignatureVerifier(byte[] publicKey)
        {
            ArgumentNullException.ThrowIfNull(publicKey);
            if (publicKey.Length != 32)
            {
                throw new ArgumentException("An Ed25519 public key is 32 bytes.", nameof(publicKey));
            }

            _publicKey = publicKey;
        }

        /// <summary>Creates the verifier with the key compiled into the client.</summary>
        public Ed25519PackageSignatureVerifier()
            : this(EmbeddedPublicKey)
        {
        }

        /// <inheritdoc />
        public async Task<bool> VerifyAsync(string packageDir, UpdateManifest manifest, CancellationToken ct)
        {
            string manifestPath = Path.Combine(packageDir, UpdateManifest.FileName);
            string signaturePath = Path.Combine(packageDir, SignatureFileName);
            if (!File.Exists(manifestPath) || !File.Exists(signaturePath))
            {
                return false;
            }

            byte[] manifestBytes = await File.ReadAllBytesAsync(manifestPath, ct);
            byte[] signature = await File.ReadAllBytesAsync(signaturePath, ct);
            if (signature.Length != SignatureLength)
            {
                return false;
            }

            var verifier = new Ed25519Signer();
            verifier.Init(false, new Ed25519PublicKeyParameters(_publicKey, 0));
            verifier.BlockUpdate(manifestBytes, 0, manifestBytes.Length);
            return verifier.VerifySignature(signature);
        }

        // SPKI for Ed25519 is a fixed 12-byte header followed by the 32-byte key.
        private static byte[] ExtractRawKey(byte[] spki)
        {
            if (spki.Length != 44)
            {
                throw new InvalidOperationException("The embedded update key is not an Ed25519 SPKI key.");
            }

            return spki[12..];
        }
    }
}
