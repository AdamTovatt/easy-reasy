# EasyReasy.EnvironmentVariables

[← Back to EasyReasy System](../README.md)

[![NuGet](https://img.shields.io/badge/nuget-EasyReasy.EnvironmentVariables-blue.svg)](https://www.nuget.org/packages/EasyReasy.EnvironmentVariables)

A lightweight .NET library for environment variable validation and management with startup-time safety.

## Overview

EasyReasy.EnvironmentVariable provides a structured way to define, validate, and retrieve environment variables with early error detection and type safety.

**Why Use EasyReasy.EnvironmentVariable?**

- **Startup-time safety**: Environment variable names are defined as constants and validated at startup
- **Early validation**: Catch missing variables at startup, not during execution
- **Clear error messages**: Detailed feedback about what's missing or invalid
- **Type safety**: Strongly typed environment variable access with IntelliSense support, making it easy to find and get suggestions for available environment variables
- **Static analysis**: Compiler can find all references to environment variables, making it easy to see where each variable is used and identify unused variables
- **Minimum length validation**: Ensure variables meet length requirements for both security and validation purposes (empty strings are never valid)

## Core Features

### Environment Variable Validation

Define your environment variables in configuration classes and validate them at startup:

```csharp
[EnvironmentVariableNameContainer]
public static class EnvironmentVariable
{
    [EnvironmentVariableName(minLength: 10, description: "PostgreSQL connection string")]
    public static readonly VariableName DatabaseUrl = new VariableName("DATABASE_URL");

    [EnvironmentVariableName(minLength: 20, description: "API key for external service")]
    public static readonly VariableName ApiKey = new VariableName("API_KEY");

    [EnvironmentVariableName]
    public static readonly VariableName DebugMode = new VariableName("DEBUG_MODE");
}
```

The optional `description` parameter is used when generating example content (see [Example File Generation](#example-file-generation)).

### Optional Variables

> **Most variables should not be optional.** The core value of this library is catching missing configuration at startup rather than at runtime. Only mark a variable as optional when there is a genuine reason — for example, when the application has a built-in fallback or auto-discovery mechanism and the variable only serves as an explicit override.

For the rare cases where a variable is truly optional, you can skip startup validation for it:

```csharp
[EnvironmentVariableNameContainer]
public static class EnvironmentVariable
{
    [EnvironmentVariableName(minLength: 10)]
    public static readonly VariableName DatabaseUrl = new VariableName("DATABASE_URL");

    // Optional: the application can auto-discover the CLI, but this allows an explicit override
    [EnvironmentVariableName(optional: true)]
    public static readonly VariableName CliPath = new VariableName("CLI_PATH");
}
```

Use `GetValueOrDefault()` to retrieve optional variables without throwing:

```csharp
string? cliPath = EnvironmentVariable.CliPath.GetValueOrDefault();
if (cliPath != null)
{
    // Use the explicit override
}
```

`GetValueOrDefault()` returns `null` if the variable is not set, empty, or whitespace.

### Startup Validation

Validate all environment variables at application startup:

```csharp
// In Program.cs or Startup.cs
EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(EnvironmentVariable));
```

This validates all environment variables defined in the `EnvironmentVariable` class. You can pass any number of configuration classes, but it's recommended to use only one to keep all environment variable definitions in one place.

This will throw an `InvalidOperationException` with detailed error messages if any required environment variables are missing or don't meet minimum length requirements.

### Safe Environment Variable Retrieval

Get environment variables with built-in validation:

```csharp
string databaseUrl = EnvironmentVariable.DatabaseUrl.GetValue(minLength: 10);
string apiKey = EnvironmentVariable.ApiKey.GetValue();
```

> **Note:** The `GetValue()` method is an extension method for `VariableName` that internally calls `EnvironmentVariableHelper.GetVariableValue`. If you prefer, you can also call `EnvironmentVariableHelper.GetVariableValue(EnvironmentVariable.DatabaseUrl, minLength: 10)` directly.

### Environment Variable Ranges

You can declare a range of environment variables that share a common prefix. This is useful for cases like multiple file paths, API keys, etc.

```csharp
[EnvironmentVariableNameContainer]
public static class EnvironmentVariable
{
    // This declares a range of names (use with VariableNameRange)
    [EnvironmentVariableNameRange(minCount: 2, description: "File storage paths")]
    public static readonly VariableNameRange FilePaths = new VariableNameRange("FILE_PATH");

    // "Normal" variable names can also exist in the same file
    [EnvironmentVariableName(minLength: 10)]
    public static readonly VariableName DatabaseUrl = new VariableName("DATABASE_URL");
}
```

This will match all environment variables whose names start with `FILE_PATH` (e.g., `FILE_PATH1`, `FILE_PATH_A`, `FILE_PATH_01`, etc.).

> Both `[EnvironmentVariableNameRange]` and `[EnvironmentVariableName]` can of course be used in the same file. Just make sure to use the correct types (`VariableNameRange` for the ranges and `VariableName` for the normal names).

#### Retrieving All Values in a Range

You can retrieve all values for a range using either the helper or the extension method:

```csharp
List<string> filePaths = EnvironmentVariableHelper.GetAllVariableValuesInRange(EnvironmentVariable.FilePaths);
// or
List<string> filePaths = EnvironmentVariable.FilePaths.GetAllValues();
```

#### Validation

If you specify `minCount` in the attribute, validation will ensure at least that many variables with the prefix are present and non-empty. If not, a clear error message will be thrown at startup.

### Loading from Files

Load environment variables from `.env` files and set them in the running program:

```csharp
EnvironmentVariableHelper.LoadVariablesFromFile("config.env");
```

File format:
```
DATABASE_URL=postgresql://localhost:5432/mydb
API_KEY=my-secret-key
DEBUG_MODE=true
FILE_PATH1=/path/to/file1
FILE_PATH2=/path/to/file2
# Comments are supported
```

### Example File Generation

You can create example files programmatically:

```csharp
// Create an example file with default examples
EnvironmentVariableHelper.WriteExampleFile("config.example.env");

// Or with custom examples
EnvironmentVariableHelper.WriteExampleFile("config.example.env", "DATABASE_URL", "postgres://localhost:5432/mydb");

// Get example content as a string
string exampleContent1 = EnvironmentVariableHelper.GetExampleContent();
string exampleContent2 = EnvironmentVariableHelper.GetExampleContent("DATABASE_URL", "postgres://localhost:5432/mydb");

// Write example content to a stream
EnvironmentVariableHelper.WriteExampleToStream(stream, "DATABASE_URL", "postgres://localhost:5432/mydb");
```

#### Container-Based Example Generation

You can also generate example content directly from your environment variable container. This automatically includes descriptions and requirement comments based on your attribute definitions:

```csharp
// Generate example content from a container type
string exampleContent = EnvironmentVariableHelper.GetExampleContent(typeof(EnvironmentVariable));

// Write directly to a file
EnvironmentVariableHelper.WriteExampleFile("config.example.env", typeof(EnvironmentVariable));

// Write to a stream
EnvironmentVariableHelper.WriteExampleToStream(stream, typeof(EnvironmentVariable));
```

Given this container definition:

```csharp
[EnvironmentVariableNameContainer]
public static class EnvironmentVariable
{
    [EnvironmentVariableName(32, description: "Secret key for JWT token signing")]
    public static readonly VariableName JwtSecret = new VariableName("JWT_SECRET");

    [EnvironmentVariableNameRange(2, description: "File storage paths")]
    public static readonly VariableNameRange FilePaths = new VariableNameRange("FILE_PATH");
}
```

The generated example content would be:

```
# Use "#" to comment

# Secret key for JWT token signing
# Min length: 32
JWT_SECRET=<YOUR_JWT_SECRET>

# File storage paths
# Min count: 2
FILE_PATH_1=<YOUR_FILE_PATH_1>
FILE_PATH_2=<YOUR_FILE_PATH_2>
```

For ranges, at least 2 example entries are always generated (or more if `minCount` is higher) to show that multiple entries are possible.

> **Note:** This is particularly useful in unit tests where environment variables need to be configured for testing but can't be in the code, and there's no `launchSettings.json` file or built-in way like ASP.NET Core web API applications have.

### Loading from Strings and Streams

You can also load environment variables from strings or streams:

```csharp
// Load from a string
string configContent = @"DATABASE_URL=postgresql://localhost:5432/mydb
API_KEY=my-secret-key";
EnvironmentVariableHelper.LoadVariablesFromString(configContent);

// Load from a stream
using Stream stream = File.OpenRead("config.env");
EnvironmentVariableHelper.LoadVariablesFromStream(stream);
```

### Loading from Linux systemd Service Files

Load environment variables from Linux systemd service files using the built-in preprocessor:

```csharp
// Load from a systemd service file
EnvironmentVariableHelper.LoadVariablesFromFile("/etc/systemd/system/myapp.service", new SystemdServiceFilePreprocessor());

// Or load from a string containing systemd service content
string systemdContent = @"[Service]
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5002
Environment=BYTESHELF_STORAGE_PATH=/mnt/ssd1/byte-shelf/storage
ExecStart=/usr/bin/myapp";
EnvironmentVariableHelper.LoadVariablesFromString(systemdContent, new SystemdServiceFilePreprocessor());
```

The `SystemdServiceFilePreprocessor` extracts all `Environment=` lines from the service file and converts them to standard environment variable format. It supports:

- Standard systemd `Environment=KEY=value` format
- Comments and other systemd directives are automatically ignored

### Custom Preprocessors

You can create custom preprocessors by implementing the `IFileContentPreprocessor` interface:

```csharp
public class MyCustomPreprocessor : IFileContentPreprocessor
{
    public string Preprocess(string content)
    {
        // Transform the content as needed
        return transformedContent;
    }
}

// Use your custom preprocessor
EnvironmentVariableHelper.LoadVariablesFromString(content, new MyCustomPreprocessor());
```