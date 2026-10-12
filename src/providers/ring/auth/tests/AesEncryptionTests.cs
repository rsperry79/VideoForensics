namespace VideoForensics.Providers.Ring.Auth.Tests
{
    using System.Reflection;
    using System.Text;
    using VideoForensics.Providers.Ring.Implementations;

    public class AesEncryptionTests
    {
        private readonly string _tempSaltDir = Path.Combine(Path.GetTempPath(), $"aes-test-{Guid.NewGuid()}");

        private void SetupTestEnvironment()
        {
            if (!Directory.Exists(_tempSaltDir))
            {
                Directory.CreateDirectory(_tempSaltDir);
            }
        }

        private void CleanupTestEnvironment()
        {
            try
            {
                if (Directory.Exists(_tempSaltDir))
                {
                    Directory.Delete(_tempSaltDir, true);
                }
            }
            catch
            {
                // Swallow cleanup errors
            }
        }

        #region Null/Empty Handling Tests

        [Fact]
        public void AesEncryption_Encrypt_Null_ReturnsNull()
        {
            // Arrange
            var aes = new AesEncryption();

            // Act
            string result = aes.Encrypt(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void AesEncryption_Encrypt_EmptyString_ReturnsNull()
        {
            // Arrange
            var aes = new AesEncryption();

            // Act
            string result = aes.Encrypt("");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void AesEncryption_Decrypt_Null_ReturnsNull()
        {
            // Arrange
            var aes = new AesEncryption();

            // Act
            string result = aes.Decrypt(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void AesEncryption_Decrypt_EmptyString_ReturnsNull()
        {
            // Arrange
            var aes = new AesEncryption();

            // Act
            string result = aes.Decrypt("");

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region Round-Trip Encryption/Decryption Tests

        [Fact]
        public void AesEncryption_RoundTrip_PlaintextRecovered_WithASCII()
        {
            // Arrange
            var aes = new AesEncryption();
            string plaintext = "Hello, World! This is a test message.";

            // Act
            string ciphertext = aes.Encrypt(plaintext);
            string decrypted = aes.Decrypt(ciphertext);

            // Assert
            Assert.NotNull(ciphertext);
            Assert.NotEqual(plaintext, ciphertext);
            Assert.Equal(plaintext, decrypted);
        }

        [Fact]
        public void AesEncryption_RoundTrip_PlaintextRecovered_WithUnicode()
        {
            // Arrange
            var aes = new AesEncryption();
            string plaintext = "Unicode test: 你好世界 🚀 Привет мир";

            // Act
            string ciphertext = aes.Encrypt(plaintext);
            string decrypted = aes.Decrypt(ciphertext);

            // Assert
            Assert.NotNull(ciphertext);
            Assert.Equal(plaintext, decrypted);
        }

        [Fact]
        public void AesEncryption_RoundTrip_PlaintextRecovered_WithSpecialCharacters()
        {
            // Arrange
            var aes = new AesEncryption();
            string plaintext = "Special chars: !@#$%^&*()_+-=[]{}|;:',.<>?/~`";

            // Act
            string ciphertext = aes.Encrypt(plaintext);
            string decrypted = aes.Decrypt(ciphertext);

            // Assert
            Assert.NotNull(ciphertext);
            Assert.Equal(plaintext, decrypted);
        }

        [Fact]
        public void AesEncryption_RoundTrip_PlaintextRecovered_WithLargeString()
        {
            // Arrange
            var aes = new AesEncryption();
            string plaintext = new string('A', 10000); // 10KB of data

            // Act
            string ciphertext = aes.Encrypt(plaintext);
            string decrypted = aes.Decrypt(ciphertext);

            // Assert
            Assert.NotNull(ciphertext);
            Assert.Equal(plaintext, decrypted);
        }

        #endregion

        #region IV Uniqueness Tests

        [Fact]
        public void AesEncryption_IVUniqueness_MultipleEncryptions_ProduceDifferentCiphertexts()
        {
            // Arrange
            var aes = new AesEncryption();
            string plaintext = "Test message for IV uniqueness";
            var ciphertexts = new HashSet<string>();

            // Act
            for (int i = 0; i < 10; i++)
            {
                string ciphertext = aes.Encrypt(plaintext);
                ciphertexts.Add(ciphertext);
            }

            // Assert
            // All 10 encryptions should produce different ciphertexts due to unique IVs
            Assert.Equal(10, ciphertexts.Count);
        }

        #endregion

        #region Error Scenario Tests

        [Fact]
        public void AesEncryption_Decrypt_InvalidBase64_ReturnsNull()
        {
            // Arrange
            var aes = new AesEncryption();
            string invalidBase64 = "This is not valid base64!!!@@@###";

            // Act
            string result = aes.Decrypt(invalidBase64);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void AesEncryption_Decrypt_CorruptedCiphertext_ReturnsNull()
        {
            // Arrange
            var aes = new AesEncryption();
            string plaintext = "Test message";
            string ciphertext = aes.Encrypt(plaintext);

            // Corrupt the ciphertext by truncating it
            string truncated = ciphertext.Substring(0, ciphertext.Length - 10);

            // Act
            string result = aes.Decrypt(truncated);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void AesEncryption_Decrypt_InvalidCiphertext_ReturnsNull()
        {
            // Arrange
            var aes = new AesEncryption();
            // Create a valid base64 string but invalid ciphertext
            string invalidCiphertext = Convert.ToBase64String(Encoding.UTF8.GetBytes("This is not encrypted data"));

            // Act
            string result = aes.Decrypt(invalidCiphertext);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region PBKDF2 Configuration Tests

        [Fact]
        public void AesEncryption_PBKDF2_Iterations_Equals_600000()
        {
            // Arrange - use reflection to get the private constant
            var aesType = typeof(AesEncryption);
            var iterationsField = aesType.GetField("Iterations", BindingFlags.NonPublic | BindingFlags.Static);

            // Act
            var iterationsValue = iterationsField?.GetValue(null);

            // Assert
            Assert.NotNull(iterationsValue);
            Assert.Equal(600000, iterationsValue);
        }

        #endregion

        #region Salt Tests

        [Fact]
        public void AesEncryption_Salt_Size_Is_SixteenBytes()
        {
            // Arrange - use reflection to get the private constant
            var aesType = typeof(AesEncryption);
            var saltSizeField = aesType.GetField("SaltSize", BindingFlags.NonPublic | BindingFlags.Static);

            // Act
            var saltSizeValue = saltSizeField?.GetValue(null);

            // Assert
            Assert.NotNull(saltSizeValue);
            Assert.Equal(16, saltSizeValue);
        }

        [Fact]
        public void AesEncryption_Salt_Persists_AfterFirstCall()
        {
            SetupTestEnvironment();
            try
            {
                // Arrange
                var aes = new AesEncryption();
                string plaintext = "Test message";

                // Act
                string ciphertext = aes.Encrypt(plaintext);

                // Call DeriveKey via encryption to trigger salt creation
                // The salt should be created in the default location
                string saltPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoForensics",
                    "ring-credential-salt.bin");

                // Assert
                Assert.True(File.Exists(saltPath), "Salt file should exist after encryption");
            }
            finally
            {
                CleanupTestEnvironment();
            }
        }

        [Fact]
        public void AesEncryption_Salt_Reuse_SameAcrossCalls()
        {
            SetupTestEnvironment();
            try
            {
                // Arrange
                var aes = new AesEncryption();
                string plaintext1 = "First message";
                string plaintext2 = "Second message";

                // Act
                // Get the salt path that will be used
                string saltPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoForensics",
                    "ring-credential-salt.bin");

                // Ensure clean state
                if (File.Exists(saltPath))
                {
                    File.Delete(saltPath);
                }

                // First encryption
                string ciphertext1 = aes.Encrypt(plaintext1);
                byte[] salt1 = File.ReadAllBytes(saltPath);

                // Second encryption
                string ciphertext2 = aes.Encrypt(plaintext2);
                byte[] salt2 = File.ReadAllBytes(saltPath);

                // Assert - both encryptions should use the same salt
                Assert.Equal(salt1, salt2);
            }
            finally
            {
                CleanupTestEnvironment();
            }
        }

        #endregion

        #region Whitespace Handling Tests

        [Fact]
        public void AesEncryption_RoundTrip_WithWhitespace()
        {
            // Arrange
            var aes = new AesEncryption();
            string plaintext = "  \n\t  Message with whitespace  \r\n  ";

            // Act
            string ciphertext = aes.Encrypt(plaintext);
            string decrypted = aes.Decrypt(ciphertext);

            // Assert
            Assert.NotNull(ciphertext);
            Assert.Equal(plaintext, decrypted);
        }

        #endregion

        #region JSON Credentials Tests

        [Fact]
        public void AesEncryption_RoundTrip_WithJsonData()
        {
            // Arrange
            var aes = new AesEncryption();
            string jsonData = @"{""username"":""user@example.com"",""password"":""SecurePass123!""}";

            // Act
            string ciphertext = aes.Encrypt(jsonData);
            string decrypted = aes.Decrypt(ciphertext);

            // Assert
            Assert.NotNull(ciphertext);
            Assert.NotEqual(jsonData, ciphertext);
            Assert.Equal(jsonData, decrypted);
        }

        #endregion

        #region Encryption Determinism Tests

        [Fact]
        public void AesEncryption_SameKeyDifferentIV_ProducesDifferentCiphertexts()
        {
            // Arrange
            var aes = new AesEncryption();
            string plaintext = "Same plaintext, different IVs";

            // Act
            string cipher1 = aes.Encrypt(plaintext);
            string cipher2 = aes.Encrypt(plaintext);

            // The IVs are different, so ciphertexts should be different
            // even though the key derivation and plaintext are the same
            Assert.NotEqual(cipher1, cipher2);

            // But both should decrypt to the same plaintext
            string decrypted1 = aes.Decrypt(cipher1);
            string decrypted2 = aes.Decrypt(cipher2);

            Assert.Equal(plaintext, decrypted1);
            Assert.Equal(plaintext, decrypted2);
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public void AesEncryption_RoundTrip_SingleCharacter()
        {
            // Arrange
            var aes = new AesEncryption();
            string plaintext = "A";

            // Act
            string ciphertext = aes.Encrypt(plaintext);
            string decrypted = aes.Decrypt(ciphertext);

            // Assert
            Assert.NotNull(ciphertext);
            Assert.Equal(plaintext, decrypted);
        }

        [Fact]
        public void AesEncryption_RoundTrip_Newlines()
        {
            // Arrange
            var aes = new AesEncryption();
            string plaintext = "Line1\r\nLine2\nLine3\r";

            // Act
            string ciphertext = aes.Encrypt(plaintext);
            string decrypted = aes.Decrypt(ciphertext);

            // Assert
            Assert.NotNull(ciphertext);
            Assert.Equal(plaintext, decrypted);
        }

        [Fact]
        public void AesEncryption_RoundTrip_NullCharacters()
        {
            // Arrange
            var aes = new AesEncryption();
            // Note: strings in .NET don't naturally contain null terminators,
            // but we can test with embedded null-like sequences
            string plaintext = "Before\0After";

            // Act
            string ciphertext = aes.Encrypt(plaintext);
            string decrypted = aes.Decrypt(ciphertext);

            // Assert
            Assert.NotNull(ciphertext);
            Assert.Equal(plaintext, decrypted);
        }

        #endregion
    }
}
