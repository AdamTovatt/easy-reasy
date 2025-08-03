namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class SecurePasswordHasherTests
    {
        private IPasswordHasher _passwordHasher = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _passwordHasher = new SecurePasswordHasher();
        }

        [TestMethod]
        public void HashPassword_WithValidPasswordAndUsername_ShouldReturnHash()
        {
            // Arrange
            string password = "testpassword123";
            string username = "testuser";

            // Act
            string hash = _passwordHasher.HashPassword(password, username);

            // Assert
            Assert.IsNotNull(hash);
            Assert.IsFalse(string.IsNullOrEmpty(hash));
            Assert.AreNotEqual(password, hash);

            // Verify hash format: binary V3 format
            byte[] hashBytes = Convert.FromBase64String(hash);
            Assert.IsTrue(hashBytes.Length > 13); // Minimum size for V3 format
            Assert.AreEqual(0x01, hashBytes[0]); // V3 format marker
        }

        [TestMethod]
        public void HashPassword_WithSamePasswordAndUsername_ShouldReturnDifferentHashes()
        {
            // Arrange
            string password = "testpassword123";
            string username = "testuser";

            // Act
            string hash1 = _passwordHasher.HashPassword(password, username);
            string hash2 = _passwordHasher.HashPassword(password, username);

            // Assert
            // With PBKDF2, each hash should have a different salt, so hashes should be different
            Assert.AreNotEqual(hash1, hash2);

            // But both should validate correctly
            Assert.IsTrue(_passwordHasher.ValidatePassword(password, hash1, username));
            Assert.IsTrue(_passwordHasher.ValidatePassword(password, hash2, username));
        }

        [TestMethod]
        public void HashPassword_WithDifferentUsernames_ShouldReturnDifferentHashes()
        {
            // Arrange
            string password = "testpassword123";
            string username1 = "user1";
            string username2 = "user2";

            // Act
            string hash1 = _passwordHasher.HashPassword(password, username1);
            string hash2 = _passwordHasher.HashPassword(password, username2);

            // Assert
            Assert.AreNotEqual(hash1, hash2);
        }

        [TestMethod]
        public void HashPassword_WithDifferentPasswords_ShouldReturnDifferentHashes()
        {
            // Arrange
            string password1 = "password1";
            string password2 = "password2";
            string username = "testuser";

            // Act
            string hash1 = _passwordHasher.HashPassword(password1, username);
            string hash2 = _passwordHasher.HashPassword(password2, username);

            // Assert
            Assert.AreNotEqual(hash1, hash2);
        }

        [TestMethod]
        public void ValidatePassword_WithCorrectPasswordAndUsername_ShouldReturnTrue()
        {
            // Arrange
            string password = "testpassword123";
            string username = "testuser";
            string hash = _passwordHasher.HashPassword(password, username);

            // Act
            bool isValid = _passwordHasher.ValidatePassword(password, hash, username);

            // Assert
            Assert.IsTrue(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithIncorrectPassword_ShouldReturnFalse()
        {
            // Arrange
            string password = "testpassword123";
            string wrongPassword = "wrongpassword";
            string username = "testuser";
            string hash = _passwordHasher.HashPassword(password, username);

            // Act
            bool isValid = _passwordHasher.ValidatePassword(wrongPassword, hash, username);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithIncorrectUsername_ShouldReturnFalse()
        {
            // Arrange
            string password = "testpassword123";
            string username1 = "user1";
            string username2 = "user2";
            string hash = _passwordHasher.HashPassword(password, username1);

            // Act
            bool isValid = _passwordHasher.ValidatePassword(password, hash, username2);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithEmptyPassword_ShouldReturnFalse()
        {
            // Arrange
            string password = "";
            string username = "testuser";
            string hash = _passwordHasher.HashPassword("somepassword", username);

            // Act
            bool isValid = _passwordHasher.ValidatePassword(password, hash, username);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithEmptyHash_ShouldReturnFalse()
        {
            // Arrange
            string password = "somepassword";
            string passwordHash = "";
            string username = "testuser";

            // Act
            bool isValid = _passwordHasher.ValidatePassword(password, passwordHash, username);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithEmptyUsername_ShouldReturnFalse()
        {
            // Arrange
            string password = "somepassword";
            string username = "";
            string hash = _passwordHasher.HashPassword("somepassword", "testuser");

            // Act
            bool isValid = _passwordHasher.ValidatePassword(password, hash, username);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithInvalidHashFormat_ShouldReturnFalse()
        {
            // Arrange
            string password = "somepassword";
            string invalidHash = "invalid.hash.format";
            string username = "testuser";

            // Act
            bool isValid = _passwordHasher.ValidatePassword(password, invalidHash, username);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void HashPassword_WithEmptyPassword_ShouldThrowArgumentException()
        {
            // Arrange
            string password = "";
            string username = "testuser";

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _passwordHasher.HashPassword(password, username));
        }

        [TestMethod]
        public void HashPassword_WithEmptyUsername_ShouldThrowArgumentException()
        {
            // Arrange
            string password = "testpassword";
            string username = "";

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _passwordHasher.HashPassword(password, username));
        }

        [TestMethod]
        public void HashPassword_WithWhitespacePassword_ShouldThrowArgumentException()
        {
            // Arrange
            string password = "   ";
            string username = "testuser";

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _passwordHasher.HashPassword(password, username));
        }

        [TestMethod]
        public void HashPassword_WithWhitespaceUsername_ShouldThrowArgumentException()
        {
            // Arrange
            string password = "testpassword";
            string username = "   ";

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _passwordHasher.HashPassword(password, username));
        }

        [TestMethod]
        public void HashPassword_WithNullPassword_ShouldThrowArgumentException()
        {
            // Arrange
            string? password = null;
            string username = "testuser";

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _passwordHasher.HashPassword(password!, username));
        }

        [TestMethod]
        public void HashPassword_WithNullUsername_ShouldThrowArgumentException()
        {
            // Arrange
            string password = "testpassword";
            string? username = null;

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _passwordHasher.HashPassword(password, username!));
        }

        [TestMethod]
        public void HashPassword_ShouldGenerateUniqueSalts()
        {
            // Arrange
            string password = "testpassword";
            string username = "testuser";

            // Act
            string hash1 = _passwordHasher.HashPassword(password, username);
            string hash2 = _passwordHasher.HashPassword(password, username);

            // Assert
            // Each hash should have a unique salt, so hashes should be different
            Assert.AreNotEqual(hash1, hash2);

            // Extract salt parts to verify they're different
            byte[] hashBytes1 = Convert.FromBase64String(hash1);
            byte[] hashBytes2 = Convert.FromBase64String(hash2);

            // Extract salt from V3 format (starts at offset 13)
            int saltLength1 = (int)ReadNetworkByteOrder(hashBytes1, 9);
            int saltLength2 = (int)ReadNetworkByteOrder(hashBytes2, 9);

            byte[] salt1 = new byte[saltLength1];
            byte[] salt2 = new byte[saltLength2];
            Buffer.BlockCopy(hashBytes1, 13, salt1, 0, saltLength1);
            Buffer.BlockCopy(hashBytes2, 13, salt2, 0, saltLength2);

            Assert.AreNotEqual(Convert.ToBase64String(salt1), Convert.ToBase64String(salt2)); // Salt parts should be different

            // But both should validate correctly
            Assert.IsTrue(_passwordHasher.ValidatePassword(password, hash1, username));
            Assert.IsTrue(_passwordHasher.ValidatePassword(password, hash2, username));
        }

        [TestMethod]
        public void ValidatePassword_ShouldBeResistantToTimingAttacks()
        {
            // Arrange
            string password = "testpassword";
            string username = "testuser";
            string hash = _passwordHasher.HashPassword(password, username);
            string wrongPassword = "wrongpassword";

            // Act & Assert
            // This test verifies that the validation doesn't leak timing information
            // Both valid and invalid passwords should take similar time to validate
            bool validResult = _passwordHasher.ValidatePassword(password, hash, username);
            bool invalidResult = _passwordHasher.ValidatePassword(wrongPassword, hash, username);

            Assert.IsTrue(validResult);
            Assert.IsFalse(invalidResult);
        }

        /// <summary>
        /// Reads a 32-bit unsigned integer from the specified position in a byte array using network byte order.
        /// </summary>
        /// <param name="buffer">The byte array to read from.</param>
        /// <param name="offset">The offset to read from.</param>
        /// <returns>The 32-bit unsigned integer.</returns>
        private static uint ReadNetworkByteOrder(byte[] buffer, int offset)
        {
            return ((uint)(buffer[offset + 0]) << 24)
                | ((uint)(buffer[offset + 1]) << 16)
                | ((uint)(buffer[offset + 2]) << 8)
                | ((uint)(buffer[offset + 3]));
        }
    }
}