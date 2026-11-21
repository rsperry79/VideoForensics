using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

using VideoForensics.Client.Hosting.Updates;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests.Updates;

public class Ed25519PackageSignatureVerifierTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"ed25519-pkg-{Guid.NewGuid()}");
    private static readonly UpdateManifest Manifest = new("1.2.3", [new UpdateManifestFile("app.dll", "abc", 3)]);

    public Ed25519PackageSignatureVerifierTests()
    {
        _ = Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch
        {
            // Swallow cleanup errors
        }
    }

    private static (byte[] PublicKey, Ed25519PrivateKeyParameters Private) NewKeyPair()
    {
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        AsymmetricCipherKeyPair pair = generator.GenerateKeyPair();
        return (((Ed25519PublicKeyParameters)pair.Public).GetEncoded(), (Ed25519PrivateKeyParameters)pair.Private);
    }

    private static byte[] Sign(Ed25519PrivateKeyParameters key, byte[] data)
    {
        var signer = new Ed25519Signer();
        signer.Init(true, key);
        signer.BlockUpdate(data, 0, data.Length);
        return signer.GenerateSignature();
    }

    private void WriteManifest(string json)
    {
        File.WriteAllText(Path.Combine(_dir, UpdateManifest.FileName), json);
    }

    [Fact]
    public async Task VerifyAsync_SignatureOverManifest_ReturnsTrue()
    {
        (byte[] publicKey, Ed25519PrivateKeyParameters privateKey) = NewKeyPair();
        byte[] manifestBytes = System.Text.Encoding.UTF8.GetBytes(Manifest.ToJson());
        File.WriteAllBytes(Path.Combine(_dir, UpdateManifest.FileName), manifestBytes);
        File.WriteAllBytes(Path.Combine(_dir, UpdateManifest.FileName + ".sig"), Sign(privateKey, manifestBytes));
        var verifier = new Ed25519PackageSignatureVerifier(publicKey);

        Assert.True(await verifier.VerifyAsync(_dir, Manifest, CancellationToken.None));
    }

    [Fact]
    public async Task VerifyAsync_ManifestChangedAfterSigning_ReturnsFalse()
    {
        (byte[] publicKey, Ed25519PrivateKeyParameters privateKey) = NewKeyPair();
        byte[] manifestBytes = System.Text.Encoding.UTF8.GetBytes(Manifest.ToJson());
        File.WriteAllBytes(Path.Combine(_dir, UpdateManifest.FileName + ".sig"), Sign(privateKey, manifestBytes));
        WriteManifest(Manifest.ToJson().Replace("1.2.3", "9.9.9", StringComparison.Ordinal));
        var verifier = new Ed25519PackageSignatureVerifier(publicKey);

        Assert.False(await verifier.VerifyAsync(_dir, Manifest, CancellationToken.None));
    }

    [Fact]
    public async Task VerifyAsync_NoSignatureFile_ReturnsFalse()
    {
        (byte[] publicKey, _) = NewKeyPair();
        WriteManifest(Manifest.ToJson());
        var verifier = new Ed25519PackageSignatureVerifier(publicKey);

        Assert.False(await verifier.VerifyAsync(_dir, Manifest, CancellationToken.None));
    }

    [Fact]
    public async Task VerifyAsync_SignedByAnotherKey_ReturnsFalse()
    {
        (byte[] publicKey, _) = NewKeyPair();
        (_, Ed25519PrivateKeyParameters attacker) = NewKeyPair();
        byte[] manifestBytes = System.Text.Encoding.UTF8.GetBytes(Manifest.ToJson());
        WriteManifest(Manifest.ToJson());
        File.WriteAllBytes(Path.Combine(_dir, UpdateManifest.FileName + ".sig"), Sign(attacker, manifestBytes));
        var verifier = new Ed25519PackageSignatureVerifier(publicKey);

        Assert.False(await verifier.VerifyAsync(_dir, Manifest, CancellationToken.None));
    }

    [Fact]
    public void EmbeddedPublicKey_IsA32ByteEd25519Key()
    {
        Assert.Equal(32, Ed25519PackageSignatureVerifier.EmbeddedPublicKey.Length);
    }
}
