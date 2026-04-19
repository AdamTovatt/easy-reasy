namespace EasyReasy.EnvironmentVariables.Tests
{
    [TestClass]
    public class EnvironmentVariableHelperTests
    {
        private const string TestVariableName = "TEST_ENV_VARIABLE";
        private const string TestConfigFile = "test_config.env";

        private static readonly VariableName TestVariable = new VariableName(TestVariableName);

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
            Environment.SetEnvironmentVariable("REQUIRED_VAR", null);
            Environment.SetEnvironmentVariable("OPTIONAL_VAR", null);
            Environment.SetEnvironmentVariable("OPTIONAL_MIN_VAR", null);

            // Clean up test files
            if (File.Exists(TestConfigFile))
            {
                File.Delete(TestConfigFile);
            }
            if (File.Exists("test_example.env"))
            {
                File.Delete("test_example.env");
            }
        }

        [TestMethod]
        public void GetEnvironmentVariable_WithValidVariable_ReturnsValue()
        {
            // Arrange
            string expectedValue = "test-value";
            Environment.SetEnvironmentVariable(TestVariableName, expectedValue);

            // Act
            string result = EnvironmentVariableHelper.GetVariableValue(TestVariable);

            // Assert
            Assert.AreEqual(expectedValue, result);
        }

        [TestMethod]
        public void GetEnvironmentVariable_WithMissingVariable_ThrowsInvalidOperationException()
        {
            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariableHelper.GetVariableValue(new VariableName("NON_EXISTENT_VARIABLE")));
        }

        [TestMethod]
        public void GetEnvironmentVariable_WithEmptyVariable_ThrowsInvalidOperationException()
        {
            // Arrange
            Environment.SetEnvironmentVariable(TestVariableName, "");

            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariableHelper.GetVariableValue(TestVariable));
        }

        [TestMethod]
        public void GetEnvironmentVariable_WithWhitespaceVariable_ThrowsInvalidOperationException()
        {
            // Arrange
            Environment.SetEnvironmentVariable(TestVariableName, "   ");

            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariableHelper.GetVariableValue(TestVariable));
        }

        [TestMethod]
        public void GetEnvironmentVariable_WithMinLength_ValidLength_ReturnsValue()
        {
            // Arrange
            string expectedValue = "test-value";
            Environment.SetEnvironmentVariable(TestVariableName, expectedValue);

            // Act
            string result = EnvironmentVariableHelper.GetVariableValue(TestVariable, 5);

            // Assert
            Assert.AreEqual(expectedValue, result);
        }

        [TestMethod]
        public void GetEnvironmentVariable_WithMinLength_TooShort_ThrowsInvalidOperationException()
        {
            // Arrange
            Environment.SetEnvironmentVariable(TestVariableName, "short");

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariableHelper.GetVariableValue(TestVariable, 10));
            Assert.IsTrue(exception.Message.Contains("minimum required length is 10"));
        }

        [TestMethod]
        public void GetEnvironmentVariable_WithMinLength_ExactLength_ReturnsValue()
        {
            // Arrange
            string expectedValue = "exact";
            Environment.SetEnvironmentVariable(TestVariableName, expectedValue);

            // Act
            string result = EnvironmentVariableHelper.GetVariableValue(TestVariable, 5);

            // Assert
            Assert.AreEqual(expectedValue, result);
        }

        [TestMethod]
        public void LoadVariablesFromFile_WithValidFile_SetsEnvironmentVariables()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://localhost:5432/mydb
API_KEY=my-secret-key
DEBUG_MODE=true";
            File.WriteAllText(TestConfigFile, configContent);

            // Act
            EnvironmentVariableHelper.LoadVariablesFromFile(TestConfigFile);

            // Assert
            Assert.AreEqual("postgresql://localhost:5432/mydb", Environment.GetEnvironmentVariable("DATABASE_URL"));
            Assert.AreEqual("my-secret-key", Environment.GetEnvironmentVariable("API_KEY"));
            Assert.AreEqual("true", Environment.GetEnvironmentVariable("DEBUG_MODE"));
        }

        [TestMethod]
        public void LoadVariablesFromFile_WithComments_SkipsCommentLines()
        {
            // Arrange
            string configContent = @"# This is a comment
DATABASE_URL=postgresql://localhost:5432/mydb
// Another comment
API_KEY=my-secret-key
# Comment at end";
            File.WriteAllText(TestConfigFile, configContent);

            // Act
            EnvironmentVariableHelper.LoadVariablesFromFile(TestConfigFile);

            // Assert
            Assert.AreEqual("postgresql://localhost:5432/mydb", Environment.GetEnvironmentVariable("DATABASE_URL"));
            Assert.AreEqual("my-secret-key", Environment.GetEnvironmentVariable("API_KEY"));
            Assert.IsNull(Environment.GetEnvironmentVariable("This"));
            Assert.IsNull(Environment.GetEnvironmentVariable("Another"));
        }

        [TestMethod]
        public void LoadVariablesFromFile_WithEmptyLines_SkipsEmptyLines()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://localhost:5432/mydb

API_KEY=my-secret-key

DEBUG_MODE=true";
            File.WriteAllText(TestConfigFile, configContent);

            // Act
            EnvironmentVariableHelper.LoadVariablesFromFile(TestConfigFile);

            // Assert
            Assert.AreEqual("postgresql://localhost:5432/mydb", Environment.GetEnvironmentVariable("DATABASE_URL"));
            Assert.AreEqual("my-secret-key", Environment.GetEnvironmentVariable("API_KEY"));
            Assert.AreEqual("true", Environment.GetEnvironmentVariable("DEBUG_MODE"));
        }

        [TestMethod]
        public void LoadVariablesFromFile_WithWhitespaceOnlyLines_SkipsWhitespaceLines()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://localhost:5432/mydb
   
API_KEY=my-secret-key
	 
DEBUG_MODE=true";
            File.WriteAllText(TestConfigFile, configContent);

            // Act
            EnvironmentVariableHelper.LoadVariablesFromFile(TestConfigFile);

            // Assert
            Assert.AreEqual("postgresql://localhost:5432/mydb", Environment.GetEnvironmentVariable("DATABASE_URL"));
            Assert.AreEqual("my-secret-key", Environment.GetEnvironmentVariable("API_KEY"));
            Assert.AreEqual("true", Environment.GetEnvironmentVariable("DEBUG_MODE"));
        }

        [TestMethod]
        public void LoadVariablesFromFile_WithTrimmedValues_TrimsWhitespace()
        {
            // Arrange
            string configContent = @"DATABASE_URL = postgresql://localhost:5432/mydb 
API_KEY = my-secret-key 
DEBUG_MODE = true ";
            File.WriteAllText(TestConfigFile, configContent);

            // Act
            EnvironmentVariableHelper.LoadVariablesFromFile(TestConfigFile);

            // Assert
            Assert.AreEqual("postgresql://localhost:5432/mydb", Environment.GetEnvironmentVariable("DATABASE_URL"));
            Assert.AreEqual("my-secret-key", Environment.GetEnvironmentVariable("API_KEY"));
            Assert.AreEqual("true", Environment.GetEnvironmentVariable("DEBUG_MODE"));
        }

        [TestMethod]
        public void LoadVariablesFromFile_WithEmptyValue_SetsEmptyValue()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://localhost:5432/mydb
EMPTY_VAR=
DEBUG_MODE=true";
            File.WriteAllText(TestConfigFile, configContent);

            // Act
            EnvironmentVariableHelper.LoadVariablesFromFile(TestConfigFile);

            // Assert
            Assert.AreEqual("postgresql://localhost:5432/mydb", Environment.GetEnvironmentVariable("DATABASE_URL"));
            // Environment.SetEnvironmentVariable with empty string may return null, so we check for either empty string or null
            string? emptyVarValue = Environment.GetEnvironmentVariable("EMPTY_VAR");
            Assert.IsTrue(string.IsNullOrEmpty(emptyVarValue), $"Expected empty or null, but got: '{emptyVarValue}'");
            Assert.AreEqual("true", Environment.GetEnvironmentVariable("DEBUG_MODE"));
        }

        [TestMethod]
        public void LoadVariablesFromFile_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            // Act & Assert
            FileNotFoundException exception = Assert.ThrowsException<FileNotFoundException>(() => EnvironmentVariableHelper.LoadVariablesFromFile("non-existent-file.env"));
            Assert.IsTrue(exception.Message.Contains("non-existent-file.env"));
        }

        [TestMethod]
        public void LoadVariablesFromFile_WithInvalidFormat_ThrowsInvalidOperationException()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://localhost:5432/mydb
INVALID_LINE_WITHOUT_EQUALS
API_KEY=my-secret-key";
            File.WriteAllText(TestConfigFile, configContent);

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariableHelper.LoadVariablesFromFile(TestConfigFile));
            Assert.IsTrue(exception.Message.Contains("Invalid format at line 2"));
            Assert.IsTrue(exception.Message.Contains("Expected format: VARIABLE_NAME=value"));
        }

        [TestMethod]
        public void LoadVariablesFromFile_WithEmptyVariableName_ThrowsInvalidOperationException()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://localhost:5432/mydb
=some-value
API_KEY=my-secret-key";
            File.WriteAllText(TestConfigFile, configContent);

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariableHelper.LoadVariablesFromFile(TestConfigFile));
            Assert.IsTrue(exception.Message.Contains("Invalid variable name at line 2"));
            Assert.IsTrue(exception.Message.Contains("Variable name cannot be empty"));
        }

        [TestMethod]
        public void LoadVariablesFromFile_WithWhitespaceVariableName_ThrowsInvalidOperationException()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://localhost:5432/mydb
   =some-value
API_KEY=my-secret-key";
            File.WriteAllText(TestConfigFile, configContent);

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariableHelper.LoadVariablesFromFile(TestConfigFile));
            Assert.IsTrue(exception.Message.Contains("Invalid variable name at line 2"));
            Assert.IsTrue(exception.Message.Contains("Variable name cannot be empty"));
        }

        [TestMethod]
        public void LoadVariablesFromFile_WithComplexValues_HandlesSpecialCharacters()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://user:pass@localhost:5432/mydb?sslmode=require
API_KEY=my-secret-key-with-special-chars!@#$%^&*()
DEBUG_MODE=true
PATH_VAR=C:\Program Files\MyApp\bin";
            File.WriteAllText(TestConfigFile, configContent);

            // Act
            EnvironmentVariableHelper.LoadVariablesFromFile(TestConfigFile);

            // Assert
            Assert.AreEqual("postgresql://user:pass@localhost:5432/mydb?sslmode=require", Environment.GetEnvironmentVariable("DATABASE_URL"));
            Assert.AreEqual("my-secret-key-with-special-chars!@#$%^&*()", Environment.GetEnvironmentVariable("API_KEY"));
            Assert.AreEqual("true", Environment.GetEnvironmentVariable("DEBUG_MODE"));
            Assert.AreEqual(@"C:\Program Files\MyApp\bin", Environment.GetEnvironmentVariable("PATH_VAR"));
        }

        [TestMethod]
        public void LoadVariablesFromString_WithValidContent_SetsEnvironmentVariables()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://localhost:5432/mydb
API_KEY=my-secret-key
DEBUG_MODE=true";

            // Act
            EnvironmentVariableHelper.LoadVariablesFromString(configContent);

            // Assert
            Assert.AreEqual("postgresql://localhost:5432/mydb", Environment.GetEnvironmentVariable("DATABASE_URL"));
            Assert.AreEqual("my-secret-key", Environment.GetEnvironmentVariable("API_KEY"));
            Assert.AreEqual("true", Environment.GetEnvironmentVariable("DEBUG_MODE"));
        }

        [TestMethod]
        public void LoadVariablesFromString_WithEmptyContent_DoesNothing()
        {
            // Act & Assert - should not throw
            EnvironmentVariableHelper.LoadVariablesFromString("");
            EnvironmentVariableHelper.LoadVariablesFromString(null!);
        }

        [TestMethod]
        public void LoadVariablesFromStream_WithValidContent_SetsEnvironmentVariables()
        {
            // Arrange
            string configContent = @"DATABASE_URL=postgresql://localhost:5432/mydb
API_KEY=my-secret-key
DEBUG_MODE=true";
            using MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(configContent));

            // Act
            EnvironmentVariableHelper.LoadVariablesFromStream(stream);

            // Assert
            Assert.AreEqual("postgresql://localhost:5432/mydb", Environment.GetEnvironmentVariable("DATABASE_URL"));
            Assert.AreEqual("my-secret-key", Environment.GetEnvironmentVariable("API_KEY"));
            Assert.AreEqual("true", Environment.GetEnvironmentVariable("DEBUG_MODE"));
        }

        [TestMethod]
        public void LoadVariablesFromStream_WithNullStream_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => EnvironmentVariableHelper.LoadVariablesFromStream(null!));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithValidConfiguration_DoesNotThrow()
        {
            // Arrange
            Environment.SetEnvironmentVariable("TEST_VAR_1", "valid-value");
            Environment.SetEnvironmentVariable("TEST_VAR_2", "another-valid-value");

            // Act & Assert
            EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestConfiguration));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithMissingVariable_ThrowsInvalidOperationException()
        {
            // Arrange
            Environment.SetEnvironmentVariable("TEST_VAR_1", "valid-value");
            // TEST_VAR_2 is not set

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestConfiguration)));
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
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestConfiguration)));
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
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestConfiguration)));
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
            EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestConfigurationWithMinLength));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithMinLengthRequirement_TooShort_ThrowsInvalidOperationException()
        {
            // Arrange
            Environment.SetEnvironmentVariable("TEST_VAR_5", "short");
            Environment.SetEnvironmentVariable("TEST_VAR_6", "another-long-value");

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestConfigurationWithMinLength)));
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
            EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestConfigurationWithMinLength));
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
            EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestConfiguration), typeof(TestConfigurationWithMinLength));
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
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestConfiguration), typeof(TestConfigurationWithMinLength)));
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
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestConfiguration), typeof(TestConfigurationWithMinLength)));
            Assert.IsTrue(exception.Message.Contains("TEST_VAR_2"));
            Assert.IsTrue(exception.Message.Contains("TEST_VAR_5"));
            Assert.IsTrue(exception.Message.Contains("TEST_VAR_6"));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithTypeNotMarkedWithContainerAttribute_ThrowsArgumentException()
        {
            // Act & Assert
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestConfigurationWithoutAttribute)));
            Assert.IsTrue(exception.Message.Contains("is not marked with EnvironmentVariableNameContainerAttribute"));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithEmptyConfiguration_DoesNotThrow()
        {
            // Act & Assert
            EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestEmptyConfiguration));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithConfigurationWithoutAttributeFields_DoesNotThrow()
        {
            // Act & Assert
            EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestConfigurationWithoutAttributeFields));
        }

        [TestMethod]
        public void GetAllVariableValuesInRange_ReturnsAllMatchingValues()
        {
            // Arrange
            Environment.SetEnvironmentVariable("RANGE_TEST_A", "valueA");
            Environment.SetEnvironmentVariable("RANGE_TEST_B", "valueB");
            Environment.SetEnvironmentVariable("RANGE_TEST_C", "valueC");
            VariableNameRange range = new VariableNameRange("RANGE_TEST");

            // Act
            List<string> values = EnvironmentVariableHelper.GetAllVariableValuesInRange(range);

            // Assert
            CollectionAssert.AreEquivalent(new[] { "valueA", "valueB", "valueC" }, values);
        }

        [TestMethod]
        public void VariableNameRange_GetAllValues_ExtensionMethod_Works()
        {
            // Arrange
            Environment.SetEnvironmentVariable("RANGE_EXT_1", "foo");
            Environment.SetEnvironmentVariable("RANGE_EXT_2", "bar");
            VariableNameRange range = new VariableNameRange("RANGE_EXT");

            // Act
            List<string> values = range.GetAllValues();

            // Assert
            CollectionAssert.AreEquivalent(new[] { "foo", "bar" }, values);
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithRange_MinimumCountEnforced()
        {
            // Arrange
            Environment.SetEnvironmentVariable("RANGE_MIN_1", "one");
            // Only one variable set, but minCount is 2

            // Define a config class for this test
            Type configType = typeof(TestRangeConfig);

            // Act & Assert
            InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariableHelper.ValidateVariableNamesIn(configType));
            Assert.IsTrue(ex.Message.Contains("RANGE_MIN"));
            Assert.IsTrue(ex.Message.Contains("Minimum count of 2 not met"));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithRange_MinimumCountSatisfied_DoesNotThrow()
        {
            // Arrange
            Environment.SetEnvironmentVariable("RANGE_MIN_1", "one");
            Environment.SetEnvironmentVariable("RANGE_MIN_2", "two");

            // Act & Assert
            EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestRangeConfig));
        }

        [EnvironmentVariableNameContainer]
        public static class TestRangeConfig
        {
            [EnvironmentVariableNameRange(2)]
            public static readonly VariableNameRange Range = new VariableNameRange("RANGE_MIN");
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithMixedConfig_BothValid_DoesNotThrow()
        {
            // Arrange
            Environment.SetEnvironmentVariable("MIXED_SINGLE", "single-value");
            Environment.SetEnvironmentVariable("MIXED_RANGE_1", "range1");
            Environment.SetEnvironmentVariable("MIXED_RANGE_2", "range2");

            // Act & Assert
            EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(MixedConfig));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithMixedConfig_MissingSingle_ThrowsForSingleOnly()
        {
            // Arrange
            Environment.SetEnvironmentVariable("MIXED_SINGLE", null);
            Environment.SetEnvironmentVariable("MIXED_RANGE_1", "range1");
            Environment.SetEnvironmentVariable("MIXED_RANGE_2", "range2");

            // Act & Assert
            InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(MixedConfig)));
            Assert.IsTrue(ex.Message.Contains("MIXED_SINGLE"));
            Assert.IsFalse(ex.Message.Contains("MIXED_RANGE"));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithMixedConfig_MissingRange_ThrowsForRangeOnly()
        {
            // Arrange
            Environment.SetEnvironmentVariable("MIXED_SINGLE", "single-value");
            Environment.SetEnvironmentVariable("MIXED_RANGE_1", null);
            Environment.SetEnvironmentVariable("MIXED_RANGE_2", null);

            // Act & Assert
            InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(() => EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(MixedConfig)));
            Assert.IsTrue(ex.Message.Contains("MIXED_RANGE"));
            Assert.IsFalse(ex.Message.Contains("MIXED_SINGLE"));
        }

        [EnvironmentVariableNameContainer]
        public static class MixedConfig
        {
            [EnvironmentVariableName]
            public static readonly VariableName Single = new VariableName("MIXED_SINGLE");

            [EnvironmentVariableNameRange(2)]
            public static readonly VariableNameRange Range = new VariableNameRange("MIXED_RANGE");
        }

        [TestMethod]
        public void SystemdServiceFilePreprocessor_WithValidSystemdFile_ExtractsEnvironmentVariables()
        {
            // Arrange
            string systemdContent = @"[Unit]
Description=Byte-Shelf Service
After=nginx.service

[Service]
Type=simple
User=pi
WorkingDirectory=/opt/byte-shelf
ExecStart=/opt/byte-shelf/ByteShelf
Restart=always
RestartSec=2
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5002
Environment=BYTESHELF_TENANT_CONFIG_PATH=/var/lib/byte-shelf/tentants.json
Environment=BYTESHELF_STORAGE_PATH=/mnt/ssd1/byte-shelf/storage
Environment=BYTESHELF_CHUNK_SIZE_BYTES=27336576";

            SystemdServiceFilePreprocessor preprocessor = new SystemdServiceFilePreprocessor();

            // Act
            string result = preprocessor.Preprocess(systemdContent);

            // Assert
            string expected = @"ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://localhost:5002
BYTESHELF_TENANT_CONFIG_PATH=/var/lib/byte-shelf/tentants.json
BYTESHELF_STORAGE_PATH=/mnt/ssd1/byte-shelf/storage
BYTESHELF_CHUNK_SIZE_BYTES=27336576
";
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void SystemdServiceFilePreprocessor_WithNoEnvironmentLines_ReturnsEmpty()
        {
            // Arrange
            string systemdContent = @"[Unit]
Description=My Application Service

[Service]
Type=simple
ExecStart=/usr/bin/myapp

[Install]
WantedBy=multi-user.target";

            SystemdServiceFilePreprocessor preprocessor = new SystemdServiceFilePreprocessor();

            // Act
            string result = preprocessor.Preprocess(systemdContent);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void SystemdServiceFilePreprocessor_WithComments_SkipsComments()
        {
            // Arrange
            string systemdContent = @"[Service]
# This is a comment
Environment=ASPNETCORE_ENVIRONMENT=Production
// Another comment
Environment=ASPNETCORE_URLS=http://localhost:5002
ExecStart=/usr/bin/myapp";

            SystemdServiceFilePreprocessor preprocessor = new SystemdServiceFilePreprocessor();

            // Act
            string result = preprocessor.Preprocess(systemdContent);

            // Assert
            string expected = @"ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://localhost:5002
";
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void SystemdServiceFilePreprocessor_WithEmptyContent_ReturnsEmpty()
        {
            // Arrange
            SystemdServiceFilePreprocessor preprocessor = new SystemdServiceFilePreprocessor();

            // Act
            string result = preprocessor.Preprocess("");

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void LoadVariablesFromString_WithSystemdPreprocessor_LoadsCorrectly()
        {
            // Arrange
            string systemdContent = @"[Service]
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5002
Environment=BYTESHELF_STORAGE_PATH=/mnt/ssd1/byte-shelf/storage
ExecStart=/usr/bin/myapp";

            SystemdServiceFilePreprocessor preprocessor = new SystemdServiceFilePreprocessor();

            // Act
            EnvironmentVariableHelper.LoadVariablesFromString(systemdContent, preprocessor);

            // Assert
            Assert.AreEqual("Production", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
            Assert.AreEqual("http://localhost:5002", Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));
            Assert.AreEqual("/mnt/ssd1/byte-shelf/storage", Environment.GetEnvironmentVariable("BYTESHELF_STORAGE_PATH"));
        }

        [TestMethod]
        public void LoadVariablesFromStream_WithSystemdPreprocessor_LoadsCorrectly()
        {
            // Arrange
            string systemdContent = @"[Service]
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5002
ExecStart=/usr/bin/myapp";
            using MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(systemdContent));
            SystemdServiceFilePreprocessor preprocessor = new SystemdServiceFilePreprocessor();

            // Act
            EnvironmentVariableHelper.LoadVariablesFromStream(stream, preprocessor);

            // Assert
            Assert.AreEqual("Production", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
            Assert.AreEqual("http://localhost:5002", Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));
        }

        [TestMethod]
        public void LoadVariablesFromFile_WithSystemdPreprocessor_LoadsCorrectly()
        {
            // Arrange
            string systemdContent = @"[Service]
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5002
ExecStart=/usr/bin/myapp";
            File.WriteAllText(TestConfigFile, systemdContent);
            SystemdServiceFilePreprocessor preprocessor = new SystemdServiceFilePreprocessor();

            // Act
            EnvironmentVariableHelper.LoadVariablesFromFile(TestConfigFile, preprocessor);

            // Assert
            Assert.AreEqual("Production", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
            Assert.AreEqual("http://localhost:5002", Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));
        }

        [TestMethod]
        public void SystemdServiceFilePreprocessor_WithRealSystemdFile_ExtractsCorrectly()
        {
            // Arrange - Using the exact format from the user's example
            string systemdContent = @"[Unit]
Description=Byte-Shelf Service
After=nginx.service

[Service]
Type=simple
User=pi
WorkingDirectory=/opt/byte-shelf
ExecStart=/opt/byte-shelf/ByteShelf
Restart=always
RestartSec=2
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5002
Environment=BYTESHELF_TENANT_CONFIG_PATH=/var/lib/byte-shelf/tentants.json
Environment=BYTESHELF_STORAGE_PATH=/mnt/ssd1/byte-shelf/storage
Environment=BYTESHELF_CHUNK_SIZE_BYTES=27336576";

            SystemdServiceFilePreprocessor preprocessor = new SystemdServiceFilePreprocessor();

            // Act
            string result = preprocessor.Preprocess(systemdContent);

            // Assert
            string expected = @"ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://localhost:5002
BYTESHELF_TENANT_CONFIG_PATH=/var/lib/byte-shelf/tentants.json
BYTESHELF_STORAGE_PATH=/mnt/ssd1/byte-shelf/storage
BYTESHELF_CHUNK_SIZE_BYTES=27336576
";
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void GetExampleContent_WithNoParameters_ReturnsDefaultContent()
        {
            // Act
            string result = EnvironmentVariableHelper.GetExampleContent();

            // Assert
            string expected = @"# Use ""#"" to comment
EXAMPLE_KEY1=example_value1
EXAMPLE_KEY2=example_value2
";
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void GetExampleContent_WithOneKeyValue_ReturnsContentWithOneExample()
        {
            // Arrange
            string key = "TEST_KEY";
            string value = "test_value";

            // Act
            string result = EnvironmentVariableHelper.GetExampleContent(key, value);

            // Assert
            string expected = $@"# Use ""#"" to comment
{key}={value}
";
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void GetExampleContent_WithTwoKeyValues_ReturnsContentWithTwoExamples()
        {
            // Arrange
            string key1 = "TEST_KEY1";
            string value1 = "test_value1";
            string key2 = "TEST_KEY2";
            string value2 = "test_value2";

            // Act
            string result = EnvironmentVariableHelper.GetExampleContent(key1, value1, key2, value2);

            // Assert
            string expected = $@"# Use ""#"" to comment
{key1}={value1}
{key2}={value2}
";
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void WriteExampleFile_WithNoParameters_WritesDefaultContent()
        {
            // Arrange
            string testFile = "test_example.env";

            // Act
            EnvironmentVariableHelper.WriteExampleFile(testFile);

            // Assert
            Assert.IsTrue(File.Exists(testFile));
            string content = File.ReadAllText(testFile);
            string expected = @"# Use ""#"" to comment
EXAMPLE_KEY1=example_value1
EXAMPLE_KEY2=example_value2
";
            Assert.AreEqual(expected, content);

            // Cleanup
            if (File.Exists(testFile))
                File.Delete(testFile);
        }

        [TestMethod]
        public void WriteExampleFile_WithOneKeyValue_WritesContentWithOneExample()
        {
            // Arrange
            string testFile = "test_example.env";
            string key = "DATABASE_URL";
            string value = "postgres://localhost:5432/mydb";

            // Act
            EnvironmentVariableHelper.WriteExampleFile(testFile, key, value);

            // Assert
            Assert.IsTrue(File.Exists(testFile));
            string content = File.ReadAllText(testFile);
            string expected = $@"# Use ""#"" to comment
{key}={value}
";
            Assert.AreEqual(expected, content);

            // Cleanup
            if (File.Exists(testFile))
                File.Delete(testFile);
        }

        [TestMethod]
        public void WriteExampleFile_WithTwoKeyValues_WritesContentWithTwoExamples()
        {
            // Arrange
            string testFile = "test_example.env";
            string key1 = "DATABASE_URL";
            string value1 = "postgres://localhost:5432/mydb";
            string key2 = "API_KEY";
            string value2 = "my-secret-key";

            // Act
            EnvironmentVariableHelper.WriteExampleFile(testFile, key1, value1, key2, value2);

            // Assert
            Assert.IsTrue(File.Exists(testFile));
            string content = File.ReadAllText(testFile);
            string expected = $@"# Use ""#"" to comment
{key1}={value1}
{key2}={value2}
";
            Assert.AreEqual(expected, content);

            // Cleanup
            if (File.Exists(testFile))
                File.Delete(testFile);
        }

        [TestMethod]
        public void WriteExampleToStream_WithNoParameters_WritesDefaultContent()
        {
            // Arrange
            using MemoryStream stream = new MemoryStream();

            // Act
            EnvironmentVariableHelper.WriteExampleToStream(stream);

            // Assert
            stream.Position = 0;
            using StreamReader reader = new StreamReader(stream);
            string content = reader.ReadToEnd();
            string expected = @"# Use ""#"" to comment
EXAMPLE_KEY1=example_value1
EXAMPLE_KEY2=example_value2
";
            Assert.AreEqual(expected, content);
        }

        [TestMethod]
        public void WriteExampleToStream_WithOneKeyValue_WritesContentWithOneExample()
        {
            // Arrange
            using MemoryStream stream = new MemoryStream();
            string key = "DATABASE_URL";
            string value = "postgres://localhost:5432/mydb";

            // Act
            EnvironmentVariableHelper.WriteExampleToStream(stream, key, value);

            // Assert
            stream.Position = 0;
            using StreamReader reader = new StreamReader(stream);
            string content = reader.ReadToEnd();
            string expected = $@"# Use ""#"" to comment
{key}={value}
";
            Assert.AreEqual(expected, content);
        }

        [TestMethod]
        public void WriteExampleToStream_WithTwoKeyValues_WritesContentWithTwoExamples()
        {
            // Arrange
            using MemoryStream stream = new MemoryStream();
            string key1 = "DATABASE_URL";
            string value1 = "postgres://localhost:5432/mydb";
            string key2 = "API_KEY";
            string value2 = "my-secret-key";

            // Act
            EnvironmentVariableHelper.WriteExampleToStream(stream, key1, value1, key2, value2);

            // Assert
            stream.Position = 0;
            using StreamReader reader = new StreamReader(stream);
            string content = reader.ReadToEnd();
            string expected = $@"# Use ""#"" to comment
{key1}={value1}
{key2}={value2}
";
            Assert.AreEqual(expected, content);
        }

        [TestMethod]
        public void WriteExampleToStream_WithNullStream_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => EnvironmentVariableHelper.WriteExampleToStream(null!));
        }

        [TestMethod]
        public void WriteExampleToStream_WithNullStreamAndOneKeyValue_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => EnvironmentVariableHelper.WriteExampleToStream(null!, "KEY", "VALUE"));
        }

        [TestMethod]
        public void WriteExampleToStream_WithNullStreamAndTwoKeyValues_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => EnvironmentVariableHelper.WriteExampleToStream(null!, "KEY1", "VALUE1", "KEY2", "VALUE2"));
        }

        [TestMethod]
        public void GetExampleContent_WithContainerType_ReturnsExpectedContent()
        {
            // Act
            string result = EnvironmentVariableHelper.GetExampleContent(typeof(TestConfiguration));

            // Assert
            Assert.IsTrue(result.Contains("# Use \"#\" to comment"));
            Assert.IsTrue(result.Contains("TEST_VAR_1=<YOUR_TEST_VAR_1>"));
            Assert.IsTrue(result.Contains("TEST_VAR_2=<YOUR_TEST_VAR_2>"));
        }

        [TestMethod]
        public void GetExampleContent_WithContainerTypeWithDescription_IncludesDescriptionComment()
        {
            // Act
            string result = EnvironmentVariableHelper.GetExampleContent(typeof(TestConfigurationWithDescription), respectMinLength: false);

            // Assert
            Assert.IsTrue(result.Contains("# Database connection string"));
            Assert.IsTrue(result.Contains("DATABASE_URL=<YOUR_DATABASE_URL>"));
            Assert.IsTrue(result.Contains("# Secret key for JWT token signing"));
            Assert.IsTrue(result.Contains("JWT_SECRET=<YOUR_JWT_SECRET>"));
        }

        [TestMethod]
        public void GetExampleContent_WithContainerTypeWithMinLength_IncludesMinLengthComment()
        {
            // Act
            string result = EnvironmentVariableHelper.GetExampleContent(typeof(TestConfigurationWithDescription));

            // Assert
            Assert.IsTrue(result.Contains("# Min length: 32"));
        }

        [TestMethod]
        public void GetExampleContent_WithContainerTypeWithRange_GeneratesMultipleEntries()
        {
            // Act
            string result = EnvironmentVariableHelper.GetExampleContent(typeof(TestConfigurationWithRangeNoMinCount));

            // Assert
            Assert.IsTrue(result.Contains("# API endpoints"));
            Assert.IsTrue(result.Contains("API_ENDPOINT_1=<YOUR_API_ENDPOINT_1>"));
            Assert.IsTrue(result.Contains("API_ENDPOINT_2=<YOUR_API_ENDPOINT_2>"));
        }

        [TestMethod]
        public void GetExampleContent_WithContainerTypeWithRangeMinCount_GeneratesMinCountEntries()
        {
            // Act
            string result = EnvironmentVariableHelper.GetExampleContent(typeof(TestConfigurationWithRangeAndDescription));

            // Assert
            Assert.IsTrue(result.Contains("# File storage paths"));
            Assert.IsTrue(result.Contains("# Min count: 3"));
            Assert.IsTrue(result.Contains("FILE_PATH_1=<YOUR_FILE_PATH_1>"));
            Assert.IsTrue(result.Contains("FILE_PATH_2=<YOUR_FILE_PATH_2>"));
            Assert.IsTrue(result.Contains("FILE_PATH_3=<YOUR_FILE_PATH_3>"));
        }

        [TestMethod]
        public void GetExampleContent_WithContainerTypeWithoutAttribute_ThrowsArgumentException()
        {
            // Act & Assert
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => EnvironmentVariableHelper.GetExampleContent(typeof(TestConfigurationWithoutAttribute)));
            Assert.IsTrue(exception.Message.Contains("is not marked with EnvironmentVariableNameContainerAttribute"));
        }

        [TestMethod]
        public void WriteExampleFile_WithContainerType_WritesExpectedContent()
        {
            // Arrange
            string testFile = "test_container_example.env";

            try
            {
                // Act
                EnvironmentVariableHelper.WriteExampleFile(testFile, typeof(TestConfigurationWithDescription), respectMinLength: false);

                // Assert
                Assert.IsTrue(File.Exists(testFile));
                string content = File.ReadAllText(testFile);
                Assert.IsTrue(content.Contains("# Database connection string"));
                Assert.IsTrue(content.Contains("DATABASE_URL=<YOUR_DATABASE_URL>"));
                Assert.IsTrue(content.Contains("# Min length: 32"));
                Assert.IsTrue(content.Contains("JWT_SECRET=<YOUR_JWT_SECRET>"));
            }
            finally
            {
                // Cleanup
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
        }

        [TestMethod]
        public void WriteExampleToStream_WithContainerType_WritesExpectedContent()
        {
            // Arrange
            using MemoryStream stream = new MemoryStream();

            // Act
            EnvironmentVariableHelper.WriteExampleToStream(stream, typeof(TestConfigurationWithDescription), respectMinLength: false);

            // Assert
            stream.Position = 0;
            using StreamReader reader = new StreamReader(stream);
            string content = reader.ReadToEnd();
            Assert.IsTrue(content.Contains("# Database connection string"));
            Assert.IsTrue(content.Contains("DATABASE_URL=<YOUR_DATABASE_URL>"));
            Assert.IsTrue(content.Contains("# Min length: 32"));
            Assert.IsTrue(content.Contains("JWT_SECRET=<YOUR_JWT_SECRET>"));
        }

        [TestMethod]
        public void WriteExampleToStream_WithContainerTypeAndNullStream_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => EnvironmentVariableHelper.WriteExampleToStream(null!, typeof(TestConfiguration)));
        }

        [TestMethod]
        public void GetExampleContent_WithEmptyContainer_ReturnsHeaderOnly()
        {
            // Act
            string result = EnvironmentVariableHelper.GetExampleContent(typeof(TestEmptyConfiguration));

            // Assert
            string expected = "# Use \"#\" to comment" + Environment.NewLine;
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void GetExampleContent_WithMixedConfig_ReturnsCorrectContent()
        {
            // Act
            string result = EnvironmentVariableHelper.GetExampleContent(typeof(TestConfigurationWithRangeAndDescription));

            // Assert
            Assert.IsTrue(result.Contains("# Single variable"));
            Assert.IsTrue(result.Contains("SINGLE_VAR=<YOUR_SINGLE_VAR>"));
            Assert.IsTrue(result.Contains("# File storage paths"));
            Assert.IsTrue(result.Contains("# Min count: 3"));
            Assert.IsTrue(result.Contains("FILE_PATH_1=<YOUR_FILE_PATH_1>"));
            Assert.IsTrue(result.Contains("FILE_PATH_2=<YOUR_FILE_PATH_2>"));
            Assert.IsTrue(result.Contains("FILE_PATH_3=<YOUR_FILE_PATH_3>"));
        }

        [TestMethod]
        public void GetExampleContent_WithRespectMinLengthTrue_IncludesMinLengthSuffix()
        {
            // Act
            string result = EnvironmentVariableHelper.GetExampleContent(typeof(TestConfigurationWithDescription), respectMinLength: true);

            // Assert - JWT_SECRET has min length 32, so value should include suffix
            Assert.IsTrue(result.Contains("JWT_SECRET=<YOUR_JWT_SECRET_min_length_32>"));
        }

        [TestMethod]
        public void GetExampleContent_WithRespectMinLengthTrue_PadsValueToMeetMinLength()
        {
            // Act
            string result = EnvironmentVariableHelper.GetExampleContent(typeof(TestConfigurationWithDescription), respectMinLength: true);

            // Assert - Extract the JWT_SECRET value and verify it meets min length
            string[] lines = result.Split('\n');
            string? jwtLine = lines.FirstOrDefault(l => l.StartsWith("JWT_SECRET="));
            Assert.IsNotNull(jwtLine);

            string value = jwtLine.Substring("JWT_SECRET=".Length);
            Assert.IsTrue(value.Length >= 32, $"Value '{value}' should be at least 32 characters but was {value.Length}");
        }

        [TestMethod]
        public void GetExampleContent_WithRespectMinLengthDefault_RespectsMinLength()
        {
            // Act - default should be respectMinLength: true
            string result = EnvironmentVariableHelper.GetExampleContent(typeof(TestConfigurationWithDescription));

            // Assert
            Assert.IsTrue(result.Contains("_min_length_32"));
        }

        [TestMethod]
        public void GetExampleContent_WithNoMinLength_DoesNotAddSuffix()
        {
            // Act
            string result = EnvironmentVariableHelper.GetExampleContent(typeof(TestConfigurationWithDescription), respectMinLength: true);

            // Assert - DATABASE_URL has no min length, should not have suffix
            Assert.IsTrue(result.Contains("DATABASE_URL=<YOUR_DATABASE_URL>"));
            Assert.IsFalse(result.Contains("DATABASE_URL=<YOUR_DATABASE_URL_min_length"));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithOptionalVariableMissing_DoesNotThrow()
        {
            // Arrange
            Environment.SetEnvironmentVariable("REQUIRED_VAR", "required-value");
            // OPTIONAL_VAR is not set

            // Act & Assert
            EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestConfigurationWithOptional));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithOptionalVariableSet_DoesNotThrow()
        {
            // Arrange
            Environment.SetEnvironmentVariable("REQUIRED_VAR", "required-value");
            Environment.SetEnvironmentVariable("OPTIONAL_VAR", "optional-value");

            // Act & Assert
            EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestConfigurationWithOptional));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithOptionalVariableSetButRequiredMissing_ThrowsForRequiredOnly()
        {
            // Arrange - required is not set, optional is set
            Environment.SetEnvironmentVariable("OPTIONAL_VAR", "optional-value");

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestConfigurationWithOptional)));
            Assert.IsTrue(exception.Message.Contains("REQUIRED_VAR"));
            Assert.IsFalse(exception.Message.Contains("OPTIONAL_VAR"));
        }

        [TestMethod]
        public void ValidateVariableNamesIn_WithOptionalMinLengthVariable_SkipsValidation()
        {
            // Arrange
            Environment.SetEnvironmentVariable("REQUIRED_VAR", "required-value");
            // OPTIONAL_MIN_VAR is not set - should not cause validation failure

            // Act & Assert
            EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(TestConfigurationWithOptionalMinLength));
        }

        [TestMethod]
        public void GetValueOrDefault_WithSetVariable_ReturnsValue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("OPTIONAL_VAR", "some-value");
            VariableName variable = new VariableName("OPTIONAL_VAR");

            // Act
            string? result = variable.GetValueOrDefault();

            // Assert
            Assert.AreEqual("some-value", result);
        }

        [TestMethod]
        public void GetValueOrDefault_WithMissingVariable_ReturnsNull()
        {
            // Arrange
            VariableName variable = new VariableName("NON_EXISTENT_OPTIONAL_VAR");

            // Act
            string? result = variable.GetValueOrDefault();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetValueOrDefault_WithEmptyVariable_ReturnsNull()
        {
            // Arrange
            Environment.SetEnvironmentVariable("OPTIONAL_VAR", "");
            VariableName variable = new VariableName("OPTIONAL_VAR");

            // Act
            string? result = variable.GetValueOrDefault();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetValueOrDefault_WithWhitespaceVariable_ReturnsNull()
        {
            // Arrange
            Environment.SetEnvironmentVariable("OPTIONAL_VAR", "   ");
            VariableName variable = new VariableName("OPTIONAL_VAR");

            // Act
            string? result = variable.GetValueOrDefault();

            // Assert
            Assert.IsNull(result);
        }
    }

    [EnvironmentVariableNameContainer]
    public static class TestConfiguration
    {
        [EnvironmentVariableName]
        public static readonly VariableName Variable1 = new VariableName("TEST_VAR_1");
        [EnvironmentVariableName]
        public static readonly VariableName Variable2 = new VariableName("TEST_VAR_2");
    }

    [EnvironmentVariableNameContainer]
    public static class TestConfigurationWithMinLength
    {
        [EnvironmentVariableName(10)]
        public static readonly VariableName Variable5 = new VariableName("TEST_VAR_5");
        [EnvironmentVariableName(10)]
        public static readonly VariableName Variable6 = new VariableName("TEST_VAR_6");
    }

    [EnvironmentVariableNameContainer]
    public static class TestEmptyConfiguration
    {
    }

    [EnvironmentVariableNameContainer]
    public static class TestConfigurationWithoutAttributeFields
    {
        public static readonly VariableName VariableWithoutAttribute = new VariableName("TEST_VAR_WITHOUT_ATTRIBUTE");
    }

    public static class TestConfigurationWithoutAttribute
    {
        [EnvironmentVariableName]
        public static readonly VariableName Variable1 = new VariableName("TEST_VAR_1");
    }

    [EnvironmentVariableNameContainer]
    public static class TestConfigurationWithDescription
    {
        [EnvironmentVariableName(description: "Database connection string")]
        public static readonly VariableName DatabaseUrl = new VariableName("DATABASE_URL");

        [EnvironmentVariableName(32, "Secret key for JWT token signing")]
        public static readonly VariableName JwtSecret = new VariableName("JWT_SECRET");
    }

    [EnvironmentVariableNameContainer]
    public static class TestConfigurationWithRangeAndDescription
    {
        [EnvironmentVariableName(description: "Single variable")]
        public static readonly VariableName SingleVar = new VariableName("SINGLE_VAR");

        [EnvironmentVariableNameRange(3, "File storage paths")]
        public static readonly VariableNameRange FilePaths = new VariableNameRange("FILE_PATH");
    }

    [EnvironmentVariableNameContainer]
    public static class TestConfigurationWithRangeNoMinCount
    {
        [EnvironmentVariableNameRange(description: "API endpoints")]
        public static readonly VariableNameRange ApiEndpoints = new VariableNameRange("API_ENDPOINT");
    }

    [EnvironmentVariableNameContainer]
    public static class TestConfigurationWithOptional
    {
        [EnvironmentVariableName]
        public static readonly VariableName RequiredVar = new VariableName("REQUIRED_VAR");

        [EnvironmentVariableName(optional: true)]
        public static readonly VariableName OptionalVar = new VariableName("OPTIONAL_VAR");
    }

    [EnvironmentVariableNameContainer]
    public static class TestConfigurationWithOptionalMinLength
    {
        [EnvironmentVariableName]
        public static readonly VariableName RequiredVar = new VariableName("REQUIRED_VAR");

        [EnvironmentVariableName(minLength: 10, optional: true)]
        public static readonly VariableName OptionalMinVar = new VariableName("OPTIONAL_MIN_VAR");
    }
}