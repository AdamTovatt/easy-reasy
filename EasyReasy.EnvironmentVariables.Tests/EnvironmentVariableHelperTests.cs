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
Description=My Application Service
After=network.target

[Service]
Type=simple
User=myapp
Environment=""DATABASE_URL=postgresql://localhost:5432/mydb""
Environment=""API_KEY=my-secret-key""
Environment=""DEBUG_MODE=true""
ExecStart=/usr/bin/myapp
Restart=always

[Install]
WantedBy=multi-user.target";

            SystemdServiceFilePreprocessor preprocessor = new SystemdServiceFilePreprocessor();

            // Act
            string result = preprocessor.Preprocess(systemdContent);

            // Assert
            string expected = @"DATABASE_URL=postgresql://localhost:5432/mydb
API_KEY=my-secret-key
DEBUG_MODE=true
";
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void SystemdServiceFilePreprocessor_WithMultipleVariablesOnOneLine_HandlesCorrectly()
        {
            // Arrange
            string systemdContent = @"[Service]
Environment=""VAR1=value1"" ""VAR2=value2"" ""VAR3=value3""
ExecStart=/usr/bin/myapp";

            SystemdServiceFilePreprocessor preprocessor = new SystemdServiceFilePreprocessor();

            // Act
            string result = preprocessor.Preprocess(systemdContent);

            // Assert
            string expected = @"VAR1=value1
VAR2=value2
VAR3=value3
";
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void SystemdServiceFilePreprocessor_WithSingleQuotes_HandlesCorrectly()
        {
            // Arrange
            string systemdContent = @"[Service]
Environment='DATABASE_URL=postgresql://localhost:5432/mydb'
Environment='API_KEY=my-secret-key'
ExecStart=/usr/bin/myapp";

            SystemdServiceFilePreprocessor preprocessor = new SystemdServiceFilePreprocessor();

            // Act
            string result = preprocessor.Preprocess(systemdContent);

            // Assert
            string expected = @"DATABASE_URL=postgresql://localhost:5432/mydb
API_KEY=my-secret-key
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
Environment=""DATABASE_URL=postgresql://localhost:5432/mydb""
// Another comment
Environment=""API_KEY=my-secret-key""
ExecStart=/usr/bin/myapp";

            SystemdServiceFilePreprocessor preprocessor = new SystemdServiceFilePreprocessor();

            // Act
            string result = preprocessor.Preprocess(systemdContent);

            // Assert
            string expected = @"DATABASE_URL=postgresql://localhost:5432/mydb
API_KEY=my-secret-key
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
Environment=""DATABASE_URL=postgresql://localhost:5432/mydb""
Environment=""API_KEY=my-secret-key""
Environment=""DEBUG_MODE=true""
ExecStart=/usr/bin/myapp";

            SystemdServiceFilePreprocessor preprocessor = new SystemdServiceFilePreprocessor();

            // Act
            EnvironmentVariableHelper.LoadVariablesFromString(systemdContent, preprocessor);

            // Assert
            Assert.AreEqual("postgresql://localhost:5432/mydb", Environment.GetEnvironmentVariable("DATABASE_URL"));
            Assert.AreEqual("my-secret-key", Environment.GetEnvironmentVariable("API_KEY"));
            Assert.AreEqual("true", Environment.GetEnvironmentVariable("DEBUG_MODE"));
        }

        [TestMethod]
        public void LoadVariablesFromStream_WithSystemdPreprocessor_LoadsCorrectly()
        {
            // Arrange
            string systemdContent = @"[Service]
Environment=""DATABASE_URL=postgresql://localhost:5432/mydb""
Environment=""API_KEY=my-secret-key""
ExecStart=/usr/bin/myapp";
            using MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(systemdContent));
            SystemdServiceFilePreprocessor preprocessor = new SystemdServiceFilePreprocessor();

            // Act
            EnvironmentVariableHelper.LoadVariablesFromStream(stream, preprocessor);

            // Assert
            Assert.AreEqual("postgresql://localhost:5432/mydb", Environment.GetEnvironmentVariable("DATABASE_URL"));
            Assert.AreEqual("my-secret-key", Environment.GetEnvironmentVariable("API_KEY"));
        }

        [TestMethod]
        public void LoadVariablesFromFile_WithSystemdPreprocessor_LoadsCorrectly()
        {
            // Arrange
            string systemdContent = @"[Service]
Environment=""DATABASE_URL=postgresql://localhost:5432/mydb""
Environment=""API_KEY=my-secret-key""
ExecStart=/usr/bin/myapp";
            File.WriteAllText(TestConfigFile, systemdContent);
            SystemdServiceFilePreprocessor preprocessor = new SystemdServiceFilePreprocessor();

            // Act
            EnvironmentVariableHelper.LoadVariablesFromFile(TestConfigFile, preprocessor);

            // Assert
            Assert.AreEqual("postgresql://localhost:5432/mydb", Environment.GetEnvironmentVariable("DATABASE_URL"));
            Assert.AreEqual("my-secret-key", Environment.GetEnvironmentVariable("API_KEY"));
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
}