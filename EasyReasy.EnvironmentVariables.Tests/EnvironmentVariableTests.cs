namespace EasyReasy.EnvironmentVariables.Tests
{
    [TestClass]
    public class EnvironmentVariableTests
    {
        private const string TestVariableName = "TEST_ENV_VARIABLE";
        private const string TestConfigFile = "test_config.env";

        [TestCleanup]
        public void Cleanup()
        {
            Environment.SetEnvironmentVariable(TestVariableName, null);
            Environment.SetEnvironmentVariable("TEST_VAR_1", null);
            Environment.SetEnvironmentVariable("TEST_VAR_2", null);
            Environment.SetEnvironmentVariable("TEST_VAR_5", null);
            Environment.SetEnvironmentVariable("TEST_VAR_6", null);
            Environment.SetEnvironmentVariable("DATABASE_URL", null);
            Environment.SetEnvironmentVariable("API_KEY", null);
            Environment.SetEnvironmentVariable("DEBUG_MODE", null);
            Environment.SetEnvironmentVariable("EMPTY_VAR", null);

            // Clean up test file
            if (File.Exists(TestConfigFile))
            {
                File.Delete(TestConfigFile);
            }
        }

        [TestMethod]
        public void GetEnvironmentVariable_WithValidVariable_ReturnsValue()
        {
            // Arrange
            string expectedValue = "test-value";
            Environment.SetEnvironmentVariable(TestVariableName, expectedValue);

            // Act
            string result = EnvironmentVariables.GetVariable(TestVariableName);

            // Assert
            Assert.AreEqual(expectedValue, result);
        }

        [TestMethod]
        public void GetEnvironmentVariable_WithMissingVariable_ThrowsInvalidOperationException()
        {
            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariables.GetVariable("NON_EXISTENT_VARIABLE"));
        }

        [TestMethod]
        public void GetEnvironmentVariable_WithEmptyVariable_ThrowsInvalidOperationException()
        {
            // Arrange
            Environment.SetEnvironmentVariable(TestVariableName, "");

            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariables.GetVariable(TestVariableName));
        }

        [TestMethod]
        public void GetEnvironmentVariable_WithWhitespaceVariable_ThrowsInvalidOperationException()
        {
            // Arrange
            Environment.SetEnvironmentVariable(TestVariableName, "   ");

            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariables.GetVariable(TestVariableName));
        }

        [TestMethod]
        public void GetEnvironmentVariable_WithMinLength_ValidLength_ReturnsValue()
        {
            // Arrange
            string expectedValue = "test-value";
            Environment.SetEnvironmentVariable(TestVariableName, expectedValue);

            // Act
            string result = EnvironmentVariables.GetVariable(TestVariableName, 5);

            // Assert
            Assert.AreEqual(expectedValue, result);
        }

        [TestMethod]
        public void GetEnvironmentVariable_WithMinLength_TooShort_ThrowsInvalidOperationException()
        {
            // Arrange
            Environment.SetEnvironmentVariable(TestVariableName, "short");

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariables.GetVariable(TestVariableName, 10));
            Assert.IsTrue(exception.Message.Contains("minimum required length is 10"));
        }

        [TestMethod]
        public void GetEnvironmentVariable_WithMinLength_ExactLength_ReturnsValue()
        {
            // Arrange
            string expectedValue = "exact";
            Environment.SetEnvironmentVariable(TestVariableName, expectedValue);

            // Act
            string result = EnvironmentVariables.GetVariable(TestVariableName, 5);

            // Assert
            Assert.AreEqual(expectedValue, result);
        }

        [TestMethod]
        public void LoadFromFile_WithValidFile_SetsEnvironmentVariables()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://localhost:5432/mydb
API_KEY=my-secret-key
DEBUG_MODE=true";
            File.WriteAllText(TestConfigFile, configContent);

            // Act
            EnvironmentVariables.LoadFromFile(TestConfigFile);

            // Assert
            Assert.AreEqual("postgresql://localhost:5432/mydb", Environment.GetEnvironmentVariable("DATABASE_URL"));
            Assert.AreEqual("my-secret-key", Environment.GetEnvironmentVariable("API_KEY"));
            Assert.AreEqual("true", Environment.GetEnvironmentVariable("DEBUG_MODE"));
        }

        [TestMethod]
        public void LoadFromFile_WithComments_SkipsCommentLines()
        {
            // Arrange
            string configContent = @"# This is a comment
DATABASE_URL=postgresql://localhost:5432/mydb
// Another comment
API_KEY=my-secret-key
# Comment at end";
            File.WriteAllText(TestConfigFile, configContent);

            // Act
            EnvironmentVariables.LoadFromFile(TestConfigFile);

            // Assert
            Assert.AreEqual("postgresql://localhost:5432/mydb", Environment.GetEnvironmentVariable("DATABASE_URL"));
            Assert.AreEqual("my-secret-key", Environment.GetEnvironmentVariable("API_KEY"));
            Assert.IsNull(Environment.GetEnvironmentVariable("This"));
            Assert.IsNull(Environment.GetEnvironmentVariable("Another"));
        }

        [TestMethod]
        public void LoadFromFile_WithEmptyLines_SkipsEmptyLines()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://localhost:5432/mydb

API_KEY=my-secret-key

DEBUG_MODE=true";
            File.WriteAllText(TestConfigFile, configContent);

            // Act
            EnvironmentVariables.LoadFromFile(TestConfigFile);

            // Assert
            Assert.AreEqual("postgresql://localhost:5432/mydb", Environment.GetEnvironmentVariable("DATABASE_URL"));
            Assert.AreEqual("my-secret-key", Environment.GetEnvironmentVariable("API_KEY"));
            Assert.AreEqual("true", Environment.GetEnvironmentVariable("DEBUG_MODE"));
        }

        [TestMethod]
        public void LoadFromFile_WithWhitespaceOnlyLines_SkipsWhitespaceLines()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://localhost:5432/mydb
   
API_KEY=my-secret-key
	 
DEBUG_MODE=true";
            File.WriteAllText(TestConfigFile, configContent);

            // Act
            EnvironmentVariables.LoadFromFile(TestConfigFile);

            // Assert
            Assert.AreEqual("postgresql://localhost:5432/mydb", Environment.GetEnvironmentVariable("DATABASE_URL"));
            Assert.AreEqual("my-secret-key", Environment.GetEnvironmentVariable("API_KEY"));
            Assert.AreEqual("true", Environment.GetEnvironmentVariable("DEBUG_MODE"));
        }

        [TestMethod]
        public void LoadFromFile_WithTrimmedValues_TrimsWhitespace()
        {
            // Arrange
            string configContent = @"DATABASE_URL = postgresql://localhost:5432/mydb 
API_KEY = my-secret-key 
DEBUG_MODE = true ";
            File.WriteAllText(TestConfigFile, configContent);

            // Act
            EnvironmentVariables.LoadFromFile(TestConfigFile);

            // Assert
            Assert.AreEqual("postgresql://localhost:5432/mydb", Environment.GetEnvironmentVariable("DATABASE_URL"));
            Assert.AreEqual("my-secret-key", Environment.GetEnvironmentVariable("API_KEY"));
            Assert.AreEqual("true", Environment.GetEnvironmentVariable("DEBUG_MODE"));
        }

        [TestMethod]
        public void LoadFromFile_WithEmptyValue_SetsEmptyValue()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://localhost:5432/mydb
EMPTY_VAR=
DEBUG_MODE=true";
            File.WriteAllText(TestConfigFile, configContent);

            // Act
            EnvironmentVariables.LoadFromFile(TestConfigFile);

            // Assert
            Assert.AreEqual("postgresql://localhost:5432/mydb", Environment.GetEnvironmentVariable("DATABASE_URL"));
            // Environment.SetEnvironmentVariable with empty string may return null, so we check for either empty string or null
            string? emptyVarValue = Environment.GetEnvironmentVariable("EMPTY_VAR");
            Assert.IsTrue(string.IsNullOrEmpty(emptyVarValue), $"Expected empty or null, but got: '{emptyVarValue}'");
            Assert.AreEqual("true", Environment.GetEnvironmentVariable("DEBUG_MODE"));
        }

        [TestMethod]
        public void LoadFromFile_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            // Act & Assert
            FileNotFoundException exception = Assert.ThrowsException<FileNotFoundException>(() => EnvironmentVariables.LoadFromFile("non-existent-file.env"));
            Assert.IsTrue(exception.Message.Contains("non-existent-file.env"));
        }

        [TestMethod]
        public void LoadFromFile_WithInvalidFormat_ThrowsInvalidOperationException()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://localhost:5432/mydb
INVALID_LINE_WITHOUT_EQUALS
API_KEY=my-secret-key";
            File.WriteAllText(TestConfigFile, configContent);

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariables.LoadFromFile(TestConfigFile));
            Assert.IsTrue(exception.Message.Contains("Invalid format at line 2"));
            Assert.IsTrue(exception.Message.Contains("Expected format: VARIABLE_NAME=value"));
        }

        [TestMethod]
        public void LoadFromFile_WithEmptyVariableName_ThrowsInvalidOperationException()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://localhost:5432/mydb
=some-value
API_KEY=my-secret-key";
            File.WriteAllText(TestConfigFile, configContent);

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariables.LoadFromFile(TestConfigFile));
            Assert.IsTrue(exception.Message.Contains("Invalid variable name at line 2"));
            Assert.IsTrue(exception.Message.Contains("Variable name cannot be empty"));
        }

        [TestMethod]
        public void LoadFromFile_WithWhitespaceVariableName_ThrowsInvalidOperationException()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://localhost:5432/mydb
   =some-value
API_KEY=my-secret-key";
            File.WriteAllText(TestConfigFile, configContent);

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariables.LoadFromFile(TestConfigFile));
            Assert.IsTrue(exception.Message.Contains("Invalid variable name at line 2"));
            Assert.IsTrue(exception.Message.Contains("Variable name cannot be empty"));
        }

        [TestMethod]
        public void LoadFromFile_WithComplexValues_HandlesSpecialCharacters()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://user:pass@localhost:5432/mydb?sslmode=require
API_KEY=my-secret-key-with-special-chars!@#$%^&*()
DEBUG_MODE=true
PATH_VAR=C:\Program Files\MyApp\bin";
            File.WriteAllText(TestConfigFile, configContent);

            // Act
            EnvironmentVariables.LoadFromFile(TestConfigFile);

            // Assert
            Assert.AreEqual("postgresql://user:pass@localhost:5432/mydb?sslmode=require", Environment.GetEnvironmentVariable("DATABASE_URL"));
            Assert.AreEqual("my-secret-key-with-special-chars!@#$%^&*()", Environment.GetEnvironmentVariable("API_KEY"));
            Assert.AreEqual("true", Environment.GetEnvironmentVariable("DEBUG_MODE"));
            Assert.AreEqual(@"C:\Program Files\MyApp\bin", Environment.GetEnvironmentVariable("PATH_VAR"));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithValidConfiguration_DoesNotThrow()
        {
            // Arrange
            Environment.SetEnvironmentVariable("TEST_VAR_1", "valid-value");
            Environment.SetEnvironmentVariable("TEST_VAR_2", "another-valid-value");

            // Act & Assert
            EnvironmentVariables.ValidateVariableNamesIn(typeof(TestConfiguration));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithMissingVariable_ThrowsInvalidOperationException()
        {
            // Arrange
            Environment.SetEnvironmentVariable("TEST_VAR_1", "valid-value");
            // TEST_VAR_2 is not set

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariables.ValidateVariableNamesIn(typeof(TestConfiguration)));
            Assert.IsTrue(exception.Message.Contains("TEST_VAR_2"));
            Assert.IsTrue(exception.Message.Contains("is not set or is empty"));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithEmptyVariable_ThrowsInvalidOperationException()
        {
            // Arrange
            Environment.SetEnvironmentVariable("TEST_VAR_1", "valid-value");
            Environment.SetEnvironmentVariable("TEST_VAR_2", "");

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariables.ValidateVariableNamesIn(typeof(TestConfiguration)));
            Assert.IsTrue(exception.Message.Contains("TEST_VAR_2"));
            Assert.IsTrue(exception.Message.Contains("is not set or is empty"));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithWhitespaceVariable_ThrowsInvalidOperationException()
        {
            // Arrange
            Environment.SetEnvironmentVariable("TEST_VAR_1", "valid-value");
            Environment.SetEnvironmentVariable("TEST_VAR_2", "   ");

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariables.ValidateVariableNamesIn(typeof(TestConfiguration)));
            Assert.IsTrue(exception.Message.Contains("TEST_VAR_2"));
            Assert.IsTrue(exception.Message.Contains("is not set or is empty"));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithMinLengthRequirement_ValidLength_DoesNotThrow()
        {
            // Arrange
            Environment.SetEnvironmentVariable("TEST_VAR_5", "valid-long-value");
            Environment.SetEnvironmentVariable("TEST_VAR_6", "another-long-value");

            // Act & Assert
            EnvironmentVariables.ValidateVariableNamesIn(typeof(TestConfigurationWithMinLength));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithMinLengthRequirement_TooShort_ThrowsInvalidOperationException()
        {
            // Arrange
            Environment.SetEnvironmentVariable("TEST_VAR_5", "short");
            Environment.SetEnvironmentVariable("TEST_VAR_6", "another-long-value");

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariables.ValidateVariableNamesIn(typeof(TestConfigurationWithMinLength)));
            Assert.IsTrue(exception.Message.Contains("TEST_VAR_5"));
            Assert.IsTrue(exception.Message.Contains("minimum required length is 10"));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithMinLengthRequirement_ExactLength_DoesNotThrow()
        {
            // Arrange
            Environment.SetEnvironmentVariable("TEST_VAR_5", "exactlyten");
            Environment.SetEnvironmentVariable("TEST_VAR_6", "another-long-value");

            // Act & Assert
            EnvironmentVariables.ValidateVariableNamesIn(typeof(TestConfigurationWithMinLength));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithMultipleConfigurations_AllValid_DoesNotThrow()
        {
            // Arrange
            Environment.SetEnvironmentVariable("TEST_VAR_1", "valid-value");
            Environment.SetEnvironmentVariable("TEST_VAR_2", "another-valid-value");
            Environment.SetEnvironmentVariable("TEST_VAR_5", "valid-long-value");
            Environment.SetEnvironmentVariable("TEST_VAR_6", "another-long-value");

            // Act & Assert
            EnvironmentVariables.ValidateVariableNamesIn(typeof(TestConfiguration), typeof(TestConfigurationWithMinLength));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithMultipleConfigurations_OneInvalid_ThrowsInvalidOperationException()
        {
            // Arrange
            Environment.SetEnvironmentVariable("TEST_VAR_1", "valid-value");
            Environment.SetEnvironmentVariable("TEST_VAR_2", "another-valid-value");
            Environment.SetEnvironmentVariable("TEST_VAR_5", "valid-long-value");
            // TEST_VAR_6 is not set

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariables.ValidateVariableNamesIn(typeof(TestConfiguration), typeof(TestConfigurationWithMinLength)));
            Assert.IsTrue(exception.Message.Contains("TEST_VAR_6"));
            Assert.IsTrue(exception.Message.Contains("is not set or is empty"));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithMultipleConfigurations_MultipleInvalid_ThrowsInvalidOperationException()
        {
            // Arrange
            Environment.SetEnvironmentVariable("TEST_VAR_1", "valid-value");
            // TEST_VAR_2 is not set
            Environment.SetEnvironmentVariable("TEST_VAR_5", "short"); // Too short
            // TEST_VAR_6 is not set

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariables.ValidateVariableNamesIn(typeof(TestConfiguration), typeof(TestConfigurationWithMinLength)));
            Assert.IsTrue(exception.Message.Contains("TEST_VAR_2"));
            Assert.IsTrue(exception.Message.Contains("TEST_VAR_5"));
            Assert.IsTrue(exception.Message.Contains("TEST_VAR_6"));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithTypeNotMarkedWithContainerAttribute_ThrowsArgumentException()
        {
            // Act & Assert
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => EnvironmentVariables.ValidateVariableNamesIn(typeof(TestConfigurationWithoutAttribute)));
            Assert.IsTrue(exception.Message.Contains("is not marked with EnvironmentVariableNameContainerAttribute"));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithEmptyConfiguration_DoesNotThrow()
        {
            // Act & Assert
            EnvironmentVariables.ValidateVariableNamesIn(typeof(TestEmptyConfiguration));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithConfigurationWithoutAttributeFields_DoesNotThrow()
        {
            // Act & Assert
            EnvironmentVariables.ValidateVariableNamesIn(typeof(TestConfigurationWithoutAttributeFields));
        }
    }

    [EnvironmentVariableNameContainer]
    public static class TestConfiguration
    {
        [EnvironmentVariableName]
        public static readonly string Variable1 = "TEST_VAR_1";

        [EnvironmentVariableName]
        public static readonly string Variable2 = "TEST_VAR_2";
    }

    [EnvironmentVariableNameContainer]
    public static class TestConfigurationWithMinLength
    {
        [EnvironmentVariableName(10)]
        public static readonly string Variable5 = "TEST_VAR_5";

        [EnvironmentVariableName(10)]
        public static readonly string Variable6 = "TEST_VAR_6";
    }

    [EnvironmentVariableNameContainer]
    public static class TestEmptyConfiguration
    {
        // No fields with EnvironmentVariableNameAttribute
    }

    [EnvironmentVariableNameContainer]
    public static class TestConfigurationWithoutAttributeFields
    {
        public static readonly string VariableWithoutAttribute = "TEST_VAR_WITHOUT_ATTRIBUTE";
    }

    public static class TestConfigurationWithoutAttribute
    {
        [EnvironmentVariableName]
        public static readonly string Variable1 = "TEST_VAR_1";
    }
}