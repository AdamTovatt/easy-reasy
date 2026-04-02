namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class AuthRequestSecurityTests
    {
        [TestMethod]
        public void LoginAuthRequest_ToString_ShouldRedactPassword()
        {
            // Arrange
            LoginAuthRequest request = new LoginAuthRequest("testuser", "secret-password-123");

            // Act
            string result = request.ToString();

            // Assert
            Assert.IsTrue(result.Contains("testuser"));
            Assert.IsTrue(result.Contains("[REDACTED]"));
            Assert.IsFalse(result.Contains("secret-password-123"));
        }

        [TestMethod]
        public void LoginAuthRequest_ToString_ShouldEscapeUsername()
        {
            // Arrange — username containing JSON-breaking characters
            LoginAuthRequest request = new LoginAuthRequest("user\"with\\quotes", "password");

            // Act
            string result = request.ToString();

            // Assert — the result should be valid JSON and contain a redacted password
            Assert.IsTrue(result.Contains("[REDACTED]"));

            // Verify the output is parseable JSON (would fail if username wasn't properly escaped)
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(result);
            string? parsedUsername = document.RootElement.GetProperty("username").GetString();
            Assert.AreEqual("user\"with\\quotes", parsedUsername);
        }

        [TestMethod]
        public void LoginAuthRequest_ToJson_ShouldStillContainPassword()
        {
            // Arrange
            LoginAuthRequest request = new LoginAuthRequest("testuser", "secret-password-123");

            // Act
            string json = request.ToJson();

            // Assert — ToJson is for serialization, not logging
            Assert.IsTrue(json.Contains("secret-password-123"));
        }

        [TestMethod]
        public void LoginAuthRequest_FromJson_ShouldNotLeakInputInException()
        {
            // Arrange
            string sensitiveJson = "{\"username\":\"user\",\"password\":\"my-secret-pw\",invalid}";

            // Act & Assert
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => LoginAuthRequest.FromJson(sensitiveJson));

            Assert.IsFalse(exception.Message.Contains("my-secret-pw"));
            Assert.IsFalse(exception.Message.Contains(sensitiveJson));
            Assert.IsNull(exception.InnerException);
        }

        [TestMethod]
        public void ApiKeyAuthRequest_ToString_ShouldRedactApiKey()
        {
            // Arrange
            ApiKeyAuthRequest request = new ApiKeyAuthRequest("super-secret-api-key");

            // Act
            string result = request.ToString();

            // Assert
            Assert.IsTrue(result.Contains("[REDACTED]"));
            Assert.IsFalse(result.Contains("super-secret-api-key"));
        }

        [TestMethod]
        public void ApiKeyAuthRequest_ToJson_ShouldStillContainApiKey()
        {
            // Arrange
            ApiKeyAuthRequest request = new ApiKeyAuthRequest("super-secret-api-key");

            // Act
            string json = request.ToJson();

            // Assert — ToJson is for serialization, not logging
            Assert.IsTrue(json.Contains("super-secret-api-key"));
        }

        [TestMethod]
        public void ApiKeyAuthRequest_FromJson_ShouldNotLeakInputInException()
        {
            // Arrange
            string sensitiveJson = "{\"apiKey\":\"secret-key-value\",invalid}";

            // Act & Assert
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => ApiKeyAuthRequest.FromJson(sensitiveJson));

            Assert.IsFalse(exception.Message.Contains("secret-key-value"));
            Assert.IsFalse(exception.Message.Contains(sensitiveJson));
            Assert.IsNull(exception.InnerException);
        }

        [TestMethod]
        public void ApiKeyAuthRequest_ToString_WithClientId_ShouldShowClientIdAndRedactApiKey()
        {
            // Arrange
            ApiKeyAuthRequest request = new ApiKeyAuthRequest("super-secret-api-key", "my-client");

            // Act
            string result = request.ToString();

            // Assert
            Assert.IsTrue(result.Contains("my-client"));
            Assert.IsTrue(result.Contains("[REDACTED]"));
            Assert.IsFalse(result.Contains("super-secret-api-key"));
        }

        [TestMethod]
        public void ApiKeyAuthRequest_ToString_WithoutClientId_ShouldOmitClientId()
        {
            // Arrange
            ApiKeyAuthRequest request = new ApiKeyAuthRequest("super-secret-api-key");

            // Act
            string result = request.ToString();

            // Assert
            Assert.IsFalse(result.Contains("clientId"));
        }

        [TestMethod]
        public void ApiKeyAuthRequest_ToString_ShouldEscapeClientId()
        {
            // Arrange — clientId containing JSON-breaking characters
            ApiKeyAuthRequest request = new ApiKeyAuthRequest("key", "client\"with\\quotes");

            // Act
            string result = request.ToString();

            // Assert — the result should be valid JSON
            Assert.IsTrue(result.Contains("[REDACTED]"));

            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(result);
            string? parsedClientId = document.RootElement.GetProperty("clientId").GetString();
            Assert.AreEqual("client\"with\\quotes", parsedClientId);
        }

        [TestMethod]
        public void ApiKeyAuthRequest_ToJson_WithClientId_ShouldContainClientId()
        {
            // Arrange
            ApiKeyAuthRequest request = new ApiKeyAuthRequest("super-secret-api-key", "my-client");

            // Act
            string json = request.ToJson();

            // Assert
            Assert.IsTrue(json.Contains("my-client"));
            Assert.IsTrue(json.Contains("clientId"));
        }

        [TestMethod]
        public void ApiKeyAuthRequest_ToJson_WithoutClientId_ShouldOmitClientId()
        {
            // Arrange
            ApiKeyAuthRequest request = new ApiKeyAuthRequest("super-secret-api-key");

            // Act
            string json = request.ToJson();

            // Assert
            Assert.IsFalse(json.Contains("clientId"));
        }

        [TestMethod]
        public void ApiKeyAuthRequest_FromJson_ShouldDeserializeClientId()
        {
            // Arrange
            string json = "{\"apiKey\":\"test-key\",\"clientId\":\"my-client\"}";

            // Act
            ApiKeyAuthRequest request = ApiKeyAuthRequest.FromJson(json);

            // Assert
            Assert.AreEqual("test-key", request.ApiKey);
            Assert.AreEqual("my-client", request.ClientId);
        }

        [TestMethod]
        public void ApiKeyAuthRequest_FromJson_ShouldDefaultClientIdToNull()
        {
            // Arrange
            string json = "{\"apiKey\":\"test-key\"}";

            // Act
            ApiKeyAuthRequest request = ApiKeyAuthRequest.FromJson(json);

            // Assert
            Assert.AreEqual("test-key", request.ApiKey);
            Assert.IsNull(request.ClientId);
        }

        [TestMethod]
        public void ApiKeyAuthRequest_Constructor_ShouldDefaultClientIdToNull()
        {
            // Arrange & Act
            ApiKeyAuthRequest request = new ApiKeyAuthRequest("test-key");

            // Assert
            Assert.IsNull(request.ClientId);
        }

        [TestMethod]
        public void AuthResponse_FromJson_ShouldNotLeakInputInException()
        {
            // Arrange
            string sensitiveJson = "{\"token\":\"eyJhbGciOi.secret.token\",invalid}";

            // Act & Assert
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => AuthResponse.FromJson(sensitiveJson));

            Assert.IsFalse(exception.Message.Contains("eyJhbGciOi"));
            Assert.IsFalse(exception.Message.Contains(sensitiveJson));
            Assert.IsNull(exception.InnerException);
        }

        [TestMethod]
        public void AuthResponse_ToString_ShouldRedactToken()
        {
            // Arrange
            AuthResponse response = new AuthResponse("eyJhbGciOi.secret.token", "2026-12-31T00:00:00Z");

            // Act
            string result = response.ToString();

            // Assert
            Assert.IsTrue(result.Contains("[REDACTED]"));
            Assert.IsTrue(result.Contains("2026-12-31T00:00:00Z"));
            Assert.IsFalse(result.Contains("eyJhbGciOi"));
        }

        [TestMethod]
        public void AuthResponse_ToString_ShouldRedactRefreshToken()
        {
            // Arrange
            AuthResponse response = new AuthResponse("eyJhbGciOi.secret.token", "2026-12-31T00:00:00Z", "refresh-secret-123");

            // Act
            string result = response.ToString();

            // Assert
            Assert.IsTrue(result.Contains("[REDACTED]"));
            Assert.IsFalse(result.Contains("eyJhbGciOi"));
            Assert.IsFalse(result.Contains("refresh-secret-123"));
        }

        [TestMethod]
        public void AuthResponse_ToString_ShouldOmitRefreshTokenWhenNull()
        {
            // Arrange
            AuthResponse response = new AuthResponse("token", "2026-12-31T00:00:00Z");

            // Act
            string result = response.ToString();

            // Assert
            Assert.IsFalse(result.Contains("refreshToken"));
        }
    }
}
