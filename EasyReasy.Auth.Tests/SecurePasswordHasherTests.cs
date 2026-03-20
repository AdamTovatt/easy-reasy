using System.Text;

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
        public void HashPassword_WithValidPassword_ShouldReturnBase64Hash()
        {
            // Arrange
            string password = "testpassword123";

            // Act
            string hash = _passwordHasher.HashPassword(password);

            // Assert
            Assert.IsNotNull(hash);
            Assert.IsFalse(string.IsNullOrEmpty(hash));
            Assert.AreNotEqual(password, hash);

            byte[] hashBytes = Convert.FromBase64String(hash);
            Assert.IsTrue(hashBytes.Length > 13);
        }

        [TestMethod]
        public void HashPassword_WithSamePassword_ShouldReturnDifferentHashes()
        {
            // Arrange
            string password = "testpassword123";

            // Act
            string hash1 = _passwordHasher.HashPassword(password);
            string hash2 = _passwordHasher.HashPassword(password);

            // Assert
            Assert.AreNotEqual(hash1, hash2);
            Assert.IsTrue(_passwordHasher.ValidatePassword(password, hash1));
            Assert.IsTrue(_passwordHasher.ValidatePassword(password, hash2));
        }

        [TestMethod]
        public void HashPassword_WithDifferentPasswords_ShouldReturnDifferentHashes()
        {
            // Arrange
            string password1 = "password1";
            string password2 = "password2";

            // Act
            string hash1 = _passwordHasher.HashPassword(password1);
            string hash2 = _passwordHasher.HashPassword(password2);

            // Assert
            Assert.AreNotEqual(hash1, hash2);
        }

        [TestMethod]
        public void HashPassword_WithEmptyPassword_ShouldThrowArgumentException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _passwordHasher.HashPassword(""));
        }

        [TestMethod]
        public void HashPassword_WithWhitespacePassword_ShouldThrowArgumentException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _passwordHasher.HashPassword("   "));
        }

        [TestMethod]
        public void HashPassword_WithNullPassword_ShouldThrowArgumentException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _passwordHasher.HashPassword(null!));
        }

        [TestMethod]
        public void HashPassword_WithPasswordExceedingMaxLength_ShouldThrowArgumentException()
        {
            // Arrange - create a password that exceeds 1024 UTF-8 bytes
            string password = new string('a', 1025);

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _passwordHasher.HashPassword(password));
        }

        [TestMethod]
        public void HashPassword_WithPasswordAtMaxLength_ShouldSucceed()
        {
            // Arrange - exactly 1024 ASCII characters = 1024 UTF-8 bytes
            string password = new string('a', 1024);

            // Act
            string hash = _passwordHasher.HashPassword(password);

            // Assert
            Assert.IsNotNull(hash);
            Assert.IsTrue(_passwordHasher.ValidatePassword(password, hash));
        }

        [TestMethod]
        public void HashPassword_WithMultiByteCharacters_ShouldEnforceByteLimit()
        {
            // Arrange - CJK characters are 3 UTF-8 bytes each
            // 342 CJK chars = 1026 bytes, which exceeds the 1024 byte limit
            // but is only 342 characters long
            string password = new string('\u4e00', 342);
            Assert.IsTrue(Encoding.UTF8.GetByteCount(password) > 1024);
            Assert.IsTrue(password.Length < 1024);

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _passwordHasher.HashPassword(password));
        }

        [TestMethod]
        public void HashPassword_ShouldProduceV4FormatMarker()
        {
            // Arrange
            string password = "testpassword";

            // Act
            string hash = _passwordHasher.HashPassword(password);

            // Assert
            byte[] hashBytes = Convert.FromBase64String(hash);
            Assert.AreEqual(0x02, hashBytes[0]);
        }

        [TestMethod]
        public void HashPassword_ShouldGenerateUniqueSalts()
        {
            // Arrange
            string password = "testpassword";

            // Act
            string hash1 = _passwordHasher.HashPassword(password);
            string hash2 = _passwordHasher.HashPassword(password);

            // Assert
            byte[] hashBytes1 = Convert.FromBase64String(hash1);
            byte[] hashBytes2 = Convert.FromBase64String(hash2);

            int saltLength1 = (int)ReadNetworkByteOrder(hashBytes1, 9);
            int saltLength2 = (int)ReadNetworkByteOrder(hashBytes2, 9);

            byte[] salt1 = new byte[saltLength1];
            byte[] salt2 = new byte[saltLength2];
            Buffer.BlockCopy(hashBytes1, 13, salt1, 0, saltLength1);
            Buffer.BlockCopy(hashBytes2, 13, salt2, 0, saltLength2);

            Assert.AreNotEqual(Convert.ToBase64String(salt1), Convert.ToBase64String(salt2));
        }

        [TestMethod]
        public void ValidatePassword_WithCorrectPassword_ShouldReturnTrue()
        {
            // Arrange
            string password = "testpassword123";
            string hash = _passwordHasher.HashPassword(password);

            // Act
            bool isValid = _passwordHasher.ValidatePassword(password, hash);

            // Assert
            Assert.IsTrue(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithIncorrectPassword_ShouldReturnFalse()
        {
            // Arrange
            string password = "testpassword123";
            string hash = _passwordHasher.HashPassword(password);

            // Act
            bool isValid = _passwordHasher.ValidatePassword("wrongpassword", hash);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithEmptyPassword_ShouldReturnFalse()
        {
            // Arrange
            string hash = _passwordHasher.HashPassword("somepassword");

            // Act
            bool isValid = _passwordHasher.ValidatePassword("", hash);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithEmptyHash_ShouldReturnFalse()
        {
            // Act
            bool isValid = _passwordHasher.ValidatePassword("somepassword", "");

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithNullPassword_ShouldReturnFalse()
        {
            // Arrange
            string hash = _passwordHasher.HashPassword("somepassword");

            // Act
            bool isValid = _passwordHasher.ValidatePassword(null!, hash);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithNullHash_ShouldReturnFalse()
        {
            // Act
            bool isValid = _passwordHasher.ValidatePassword("somepassword", null!);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithInvalidBase64Hash_ShouldReturnFalse()
        {
            // Act
            bool isValid = _passwordHasher.ValidatePassword("somepassword", "invalid.hash.format");

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithV3FormatHash_ShouldReturnFalse()
        {
            // Arrange - craft a hash with V3 marker (0x01)
            byte[] fakeV3Hash = new byte[61]; // 13-byte header + 16-byte salt + 32-byte subkey
            fakeV3Hash[0] = 0x01; // V3 marker
            string hash = Convert.ToBase64String(fakeV3Hash);

            // Act
            bool isValid = _passwordHasher.ValidatePassword("somepassword", hash);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithV2FormatHash_ShouldReturnFalse()
        {
            // Arrange - craft a hash with V2 marker (0x00)
            byte[] fakeV2Hash = new byte[49]; // 1-byte marker + 16-byte salt + 32-byte subkey
            fakeV2Hash[0] = 0x00; // V2 marker
            string hash = Convert.ToBase64String(fakeV2Hash);

            // Act
            bool isValid = _passwordHasher.ValidatePassword("somepassword", hash);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithUnknownFormatMarker_ShouldReturnFalse()
        {
            // Arrange
            byte[] fakeHash = new byte[61];
            fakeHash[0] = 0xFF; // Unknown marker
            string hash = Convert.ToBase64String(fakeHash);

            // Act
            bool isValid = _passwordHasher.ValidatePassword("somepassword", hash);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithTruncatedHash_ShouldReturnFalse()
        {
            // Arrange - only a few bytes, too short for V4 header
            byte[] truncatedHash = new byte[] { 0x02, 0x00, 0x00 };
            string hash = Convert.ToBase64String(truncatedHash);

            // Act
            bool isValid = _passwordHasher.ValidatePassword("somepassword", hash);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithTamperedHash_ShouldReturnFalse()
        {
            // Arrange
            string password = "testpassword";
            string hash = _passwordHasher.HashPassword(password);
            byte[] hashBytes = Convert.FromBase64String(hash);

            // Flip a byte in the subkey area
            hashBytes[hashBytes.Length - 1] ^= 0xFF;
            string tamperedHash = Convert.ToBase64String(hashBytes);

            // Act
            bool isValid = _passwordHasher.ValidatePassword(password, tamperedHash);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidatePassword_WithPasswordExceedingMaxLength_ShouldReturnFalse()
        {
            // Arrange
            string longPassword = new string('a', 1025);
            string hash = _passwordHasher.HashPassword("somepassword");

            // Act
            bool isValid = _passwordHasher.ValidatePassword(longPassword, hash);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidatePassword_ShouldUseConstantTimeComparison()
        {
            // Arrange - this is a functional test, not a timing test
            string password = "testpassword";
            string hash = _passwordHasher.HashPassword(password);

            // Act
            bool validResult = _passwordHasher.ValidatePassword(password, hash);
            bool invalidResult = _passwordHasher.ValidatePassword("wrongpassword", hash);

            // Assert
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
            return ((uint)buffer[offset + 0] << 24)
                | ((uint)buffer[offset + 1] << 16)
                | ((uint)buffer[offset + 2] << 8)
                | ((uint)buffer[offset + 3]);
        }
    }
}
