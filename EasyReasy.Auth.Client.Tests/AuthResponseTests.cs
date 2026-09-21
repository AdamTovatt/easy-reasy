using System.Text.Json;

namespace EasyReasy.Auth.Client.Tests
{
    [TestClass]
    public class AuthResponseTests
    {
        private const string ValidExpiresAt = "2026-01-01T00:00:00.0000000Z";

        [TestMethod]
        public void FromJson_ValidJson_ReturnsTheResponse()
        {
            // Arrange
            string json = new AuthResponse("a-token", ValidExpiresAt, "a-refresh-token").ToJson();

            // Act
            AuthResponse response = AuthResponse.FromJson(json);

            // Assert
            Assert.AreEqual("a-token", response.Token);
            Assert.AreEqual(ValidExpiresAt, response.ExpiresAt);
            Assert.AreEqual("a-refresh-token", response.RefreshToken);
        }

        [TestMethod]
        public void FromJson_NoRefreshToken_ReturnsTheResponse()
        {
            // Arrange — the only field an auth response is allowed to leave out.
            string json = $"{{\"token\":\"a-token\",\"expiresAt\":\"{ValidExpiresAt}\"}}";

            // Act
            AuthResponse response = AuthResponse.FromJson(json);

            // Assert
            Assert.IsNull(response.RefreshToken);
        }

        [TestMethod]
        public void FromJson_ExplicitlyNullRefreshToken_ReturnsTheResponse()
        {
            // Arrange — the field is declared nullable, so a server writing the null out is saying the same thing
            // as leaving it out.
            string json = $"{{\"token\":\"a-token\",\"expiresAt\":\"{ValidExpiresAt}\",\"refreshToken\":null}}";

            // Act
            AuthResponse response = AuthResponse.FromJson(json);

            // Assert
            Assert.IsNull(response.RefreshToken);
        }

        [TestMethod]
        public void FromJson_NotJson_ThrowsCarryingTheParserFailure()
        {
            // Act
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => AuthResponse.FromJson("<!DOCTYPE html><html></html>"));

            // Assert — without the inner exception, even a caller willing to dig has nothing to dig into.
            Assert.IsInstanceOfType(exception.InnerException, typeof(JsonException));
        }

        [TestMethod]
        public void FromJson_NullLiteral_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => AuthResponse.FromJson("null"));
        }

        [TestMethod]
        public void FromJson_JsonWithoutAToken_ThrowsNamingTheMissingField()
        {
            // Arrange — only the token is missing, so naming it is the whole of what this test can be about.
            string json = $"{{\"expiresAt\":\"{ValidExpiresAt}\"}}";

            // Act
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => AuthResponse.FromJson(json));

            // Assert — the properties are declared non-nullable, so returning this would hand out an instance that
            // breaks its own contract and fails later, somewhere that can no longer say what was wrong with it.
            StringAssert.Contains(exception.Message, "token");
            Assert.IsFalse(exception.Message.Contains("expiresAt"), exception.Message);
            Assert.IsInstanceOfType(exception.InnerException, typeof(JsonException));
        }

        [TestMethod]
        public void FromJson_JsonWithoutAnExpiresAt_ThrowsNamingTheMissingField()
        {
            // Arrange
            string json = "{\"token\":\"a-token\"}";

            // Act
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => AuthResponse.FromJson(json));

            // Assert
            StringAssert.Contains(exception.Message, "expiresAt");
        }

        [TestMethod]
        public void FromJson_JsonWithoutEitherField_ThrowsNamingBoth()
        {
            // Arrange — what an error body from something that is not an auth server looks like.
            string json = "{\"error\":\"unknown route\"}";

            // Act
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => AuthResponse.FromJson(json));

            // Assert
            StringAssert.Contains(exception.Message, "token");
            StringAssert.Contains(exception.Message, "expiresAt");
        }

        [TestMethod]
        public void FromJson_ExplicitlyNullToken_Throws()
        {
            // Arrange — a field written out as null is *present*, so a check that only asks whether it was supplied
            // lets this through and hands back an instance whose non-nullable property is null.
            string json = $"{{\"token\":null,\"expiresAt\":\"{ValidExpiresAt}\"}}";

            // Act
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => AuthResponse.FromJson(json));

            // Assert
            StringAssert.Contains(exception.Message, "token");
        }

        [TestMethod]
        public void FromJson_ExplicitlyNullExpiresAt_Throws()
        {
            // Arrange — see FromJson_ExplicitlyNullToken_Throws.
            string json = "{\"token\":\"a-token\",\"expiresAt\":null}";

            // Act
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => AuthResponse.FromJson(json));

            // Assert
            StringAssert.Contains(exception.Message, "expiresAt");
        }

        [TestMethod]
        public void FromJson_ExpiresAtIsNotADateAndTime_ThrowsNamingTheField()
        {
            // Arrange — present, a string, and unreadable. Accepted here, it becomes a FormatException from
            // wherever the expiry is first used, carrying nothing about where the value came from.
            string json = "{\"token\":\"a-token\",\"expiresAt\":\"tomorrow\"}";

            // Act
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => AuthResponse.FromJson(json));

            // Assert
            StringAssert.Contains(exception.Message, "expiresAt");
            StringAssert.Contains(exception.Message, "tomorrow");
        }

        [TestMethod]
        [DataRow("12:30", DisplayName = "a time of day alone")]
        [DataRow("1/2/2026", DisplayName = "a date in a local convention")]
        [DataRow("2026-13-01T00:00:00Z", DisplayName = "a month that does not exist")]
        [DataRow("January 1, 2026", DisplayName = "a date written out")]
        public void FromJson_ExpiresAtIsNotAnIsoDateAndTime_ThrowsNamingTheField(string expiresAt)
        {
            // Arrange — each of these is something a general date parser will happily read as *some* point in
            // time, which is worse than rejecting it: "12:30" becomes today at half past twelve, and an expiry
            // that means nothing is indistinguishable from one that means something.
            string json = $"{{\"token\":\"a-token\",\"expiresAt\":\"{expiresAt}\"}}";

            // Act
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => AuthResponse.FromJson(json));

            // Assert
            StringAssert.Contains(exception.Message, "expiresAt");
        }

        [TestMethod]
        public void FromJson_ExpiresAtIsEmpty_ThrowsNamingTheField()
        {
            // Arrange
            string json = "{\"token\":\"a-token\",\"expiresAt\":\"\"}";

            // Act
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => AuthResponse.FromJson(json));

            // Assert
            StringAssert.Contains(exception.Message, "expiresAt");
        }

        [TestMethod]
        public void ExpirationTime_ReadableExpiresAt_ReturnsThePointInTime()
        {
            // Arrange
            AuthResponse response = AuthResponse.FromJson($"{{\"token\":\"a-token\",\"expiresAt\":\"{ValidExpiresAt}\"}}");

            // Act
            DateTime expirationTime = response.ExpirationTime;

            // Assert
            Assert.AreEqual(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), expirationTime);
            Assert.AreEqual(DateTimeKind.Utc, expirationTime.Kind);
        }

        [TestMethod]
        public void ExpirationTime_ExpiresAtCarriesAnOffset_ReturnsTheSameInstantInUtc()
        {
            // Arrange — the same moment as ValidExpiresAt, written from a clock two hours ahead. A server writing
            // a non-UTC DateTime with the round-trip format sends exactly this.
            AuthResponse response = AuthResponse.FromJson(
                "{\"token\":\"a-token\",\"expiresAt\":\"2026-01-01T02:00:00.0000000+02:00\"}");

            // Act
            DateTime expirationTime = response.ExpirationTime;

            // Assert — kept as a local time it would be compared against a UTC clock, and the token would read as
            // valid for two hours after it expired on a machine two hours ahead.
            Assert.AreEqual(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), expirationTime);
            Assert.AreEqual(DateTimeKind.Utc, expirationTime.Kind);
        }

        [TestMethod]
        public void ExpirationTime_ExpiresAtCarriesNoOffset_ReadsItAsUtc()
        {
            // Arrange — the field is documented as UTC, so a value that says nothing about its offset is taken at
            // its word rather than as the reading machine's local time.
            AuthResponse response = AuthResponse.FromJson("{\"token\":\"a-token\",\"expiresAt\":\"2026-01-01T00:00:00\"}");

            // Act
            DateTime expirationTime = response.ExpirationTime;

            // Assert
            Assert.AreEqual(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), expirationTime);
            Assert.AreEqual(DateTimeKind.Utc, expirationTime.Kind);
        }

        [TestMethod]
        public void ExpirationTime_ExpiresAtBuiltFromABadValue_ThrowsFormatException()
        {
            // Arrange — the only way to hold an unreadable expiry is to build one, since FromJson rejects it.
            AuthResponse response = new AuthResponse("a-token", "tomorrow");

            // Act & Assert
            Assert.ThrowsException<FormatException>(() => response.ExpirationTime);
        }

        [TestMethod]
        public void ToString_ResponseWithBothSecrets_RedactsThem()
        {
            // Arrange
            AuthResponse response = new AuthResponse("header.payload.signature", ValidExpiresAt, "a-refresh-token");

            // Act
            string text = response.ToString();

            // Assert — this is what a log line of an auth response is allowed to say.
            Assert.IsFalse(text.Contains("header.payload.signature"), text);
            Assert.IsFalse(text.Contains("a-refresh-token"), text);
            StringAssert.Contains(text, ValidExpiresAt);
            StringAssert.Contains(text, "[REDACTED]");
        }
    }
}
