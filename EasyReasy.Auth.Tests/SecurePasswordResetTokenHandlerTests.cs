using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class SecurePasswordResetTokenHandlerTests
    {
        private IPasswordResetTokenHandler _tokenHandler = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _tokenHandler = new SecurePasswordResetTokenHandler();
        }

        [TestMethod]
        public void GenerateResetToken_ShouldReturnNonEmptyToken()
        {
            // Act
            PasswordResetToken result = _tokenHandler.GenerateResetToken();

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(result.Token));
        }

        [TestMethod]
        public void GenerateResetToken_ShouldReturnNonEmptyTokenHash()
        {
            // Act
            PasswordResetToken result = _tokenHandler.GenerateResetToken();

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(result.TokenHash));
        }

        [TestMethod]
        public void GenerateResetToken_ShouldReturnBase64UrlEncodedToken()
        {
            // Act
            PasswordResetToken result = _tokenHandler.GenerateResetToken();

            // Assert - base64url should not contain +, /, or =
            Assert.IsFalse(result.Token.Contains('+'));
            Assert.IsFalse(result.Token.Contains('/'));
            Assert.IsFalse(result.Token.Contains('='));
        }

        [TestMethod]
        public void GenerateResetToken_ShouldReturnLowercaseHexHash()
        {
            // Act
            PasswordResetToken result = _tokenHandler.GenerateResetToken();

            // Assert - SHA-256 produces 64 hex characters
            Assert.IsTrue(Regex.IsMatch(result.TokenHash, "^[0-9a-f]{64}$"));
        }

        [TestMethod]
        public void GenerateResetToken_ShouldReturnUniqueTokens()
        {
            // Act
            PasswordResetToken result1 = _tokenHandler.GenerateResetToken();
            PasswordResetToken result2 = _tokenHandler.GenerateResetToken();

            // Assert
            Assert.AreNotEqual(result1.Token, result2.Token);
        }

        [TestMethod]
        public void GenerateResetToken_ShouldReturnUniqueHashes()
        {
            // Act
            PasswordResetToken result1 = _tokenHandler.GenerateResetToken();
            PasswordResetToken result2 = _tokenHandler.GenerateResetToken();

            // Assert
            Assert.AreNotEqual(result1.TokenHash, result2.TokenHash);
        }

        [TestMethod]
        public void GenerateResetToken_TokenHashShouldMatchSha256OfToken()
        {
            // Act
            PasswordResetToken result = _tokenHandler.GenerateResetToken();

            // Assert - manually compute SHA-256 of the token and compare
            byte[] tokenBytes = Encoding.UTF8.GetBytes(result.Token);
            byte[] hash = SHA256.HashData(tokenBytes);
            string expectedHash = Convert.ToHexString(hash).ToLowerInvariant();

            Assert.AreEqual(expectedHash, result.TokenHash);
        }

        [TestMethod]
        public void ValidateResetToken_WithMatchingTokenAndHash_ShouldReturnTrue()
        {
            // Arrange
            PasswordResetToken result = _tokenHandler.GenerateResetToken();

            // Act
            bool isValid = _tokenHandler.ValidateResetToken(result.Token, result.TokenHash);

            // Assert
            Assert.IsTrue(isValid);
        }

        [TestMethod]
        public void ValidateResetToken_WithMismatchedToken_ShouldReturnFalse()
        {
            // Arrange
            PasswordResetToken result1 = _tokenHandler.GenerateResetToken();
            PasswordResetToken result2 = _tokenHandler.GenerateResetToken();

            // Act
            bool isValid = _tokenHandler.ValidateResetToken(result1.Token, result2.TokenHash);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidateResetToken_WithEmptyToken_ShouldReturnFalse()
        {
            // Arrange
            PasswordResetToken result = _tokenHandler.GenerateResetToken();

            // Act
            bool isValid = _tokenHandler.ValidateResetToken("", result.TokenHash);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidateResetToken_WithEmptyHash_ShouldReturnFalse()
        {
            // Arrange
            PasswordResetToken result = _tokenHandler.GenerateResetToken();

            // Act
            bool isValid = _tokenHandler.ValidateResetToken(result.Token, "");

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidateResetToken_WithNullToken_ShouldReturnFalse()
        {
            // Arrange
            PasswordResetToken result = _tokenHandler.GenerateResetToken();

            // Act
            bool isValid = _tokenHandler.ValidateResetToken(null!, result.TokenHash);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidateResetToken_WithNullHash_ShouldReturnFalse()
        {
            // Arrange
            PasswordResetToken result = _tokenHandler.GenerateResetToken();

            // Act
            bool isValid = _tokenHandler.ValidateResetToken(result.Token, null!);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void ValidateResetToken_WithUpperCaseHash_ShouldReturnTrue()
        {
            // Arrange
            PasswordResetToken result = _tokenHandler.GenerateResetToken();
            string upperCaseHash = result.TokenHash.ToUpperInvariant();

            // Act
            bool isValid = _tokenHandler.ValidateResetToken(result.Token, upperCaseHash);

            // Assert
            Assert.IsTrue(isValid);
        }

        [TestMethod]
        public void ValidateResetToken_WithModifiedToken_ShouldReturnFalse()
        {
            // Arrange
            PasswordResetToken result = _tokenHandler.GenerateResetToken();
            char[] tokenChars = result.Token.ToCharArray();
            tokenChars[0] = tokenChars[0] == 'A' ? 'B' : 'A';
            string modifiedToken = new string(tokenChars);

            // Act
            bool isValid = _tokenHandler.ValidateResetToken(modifiedToken, result.TokenHash);

            // Assert
            Assert.IsFalse(isValid);
        }
    }
}
