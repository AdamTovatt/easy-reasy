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
    [EnvironmentVariableName(minLength: 10)]
    public static readonly VariableName DatabaseUrl = new VariableName("DATABASE_URL");
    
    [EnvironmentVariableName(minLength: 20)]
    public static readonly VariableName ApiKey = new VariableName("API_KEY");
    
    [EnvironmentVariableName]
    public static readonly VariableName DebugMode = new VariableName("DEBUG_MODE");
}
```

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
    [EnvironmentVariableNameRange(minCount: 2)]
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

> **Note:** This is particularly useful in unit tests where environment variables need to be configured for testing but can't be in the code, and there's no `launchSettings.json` file or built-in way like ASP.NET Core web API applications have.