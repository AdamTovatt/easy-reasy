# EasyReasy.EnvironmentVariable

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
    public static readonly string DatabaseUrl = "DATABASE_URL";
    
    [EnvironmentVariableName(minLength: 20)]
    public static readonly string ApiKey = "API_KEY";
    
    [EnvironmentVariableName]
    public static readonly string DebugMode = "DEBUG_MODE";
}
```

### Startup Validation

Validate all environment variables at application startup:

```csharp
// In Program.cs or Startup.cs
EnvironmentVariables.ValidateVariableNamesIn(typeof(EnvironmentVariable));
```

This validates all environment variables defined in the `EnvironmentVariable` class. You can pass any number of configuration classes, but it's recommended to use only one to keep all environment variable definitions in one place.

This will throw an `InvalidOperationException` with detailed error messages if any required environment variables are missing or don't meet minimum length requirements.

### Safe Environment Variable Retrieval

Get environment variables with built-in validation:

```csharp
string databaseUrl = EnvironmentVariables.GetVariable("DATABASE_URL", minLength: 10);
string apiKey = EnvironmentVariables.GetVariable("API_KEY");
```

> **Note**: The `minLength` parameter in `GetVariable()` will override any minimum length requirement set on the variable definition in your configuration class.

### Loading from Files

Load environment variables from `.env` files and set them in the running program:

```csharp
EnvironmentVariables.LoadFromFile("config.env");
```

File format:
```
DATABASE_URL=postgresql://localhost:5432/mydb
API_KEY=my-secret-key
DEBUG_MODE=true
# Comments are supported
```

> **Note**: This is particularly useful in unit tests where environment variables need to be configured for testing but can't be in the code, and there's no `launchSettings.json` file or built-in way like ASP.NET Core web API applications have.

## Attributes

### EnvironmentVariableNameContainerAttribute

Marks a class as a container for environment variable definitions. Required for validation.

### EnvironmentVariableNameAttribute

Marks individual fields as environment variable names with optional minimum length validation:

```csharp
[EnvironmentVariableName] // No minimum length
public static readonly string DebugMode = "DEBUG_MODE";

[EnvironmentVariableName(minLength: 10)] // Minimum 10 characters
public static readonly string ApiKey = "API_KEY";
```

> **Note**: Minimum length validation is useful for both security (ensuring sensitive variables meet length requirements) and validation (preventing placeholder values like `"url"` for a URL that should be a proper URL). Empty strings are never valid regardless of the minimum length setting.

## Error Handling

The library provides clear, actionable error messages:

```
Environment variable validation failed:
---> Environment Variable 'DATABASE_URL' (EnvironmentVariable.DatabaseUrl): Environment variable 'DATABASE_URL' is not set or is empty.
---> Environment Variable 'API_KEY' (EnvironmentVariable.ApiKey): Environment variable 'API_KEY' has length 8 but minimum required length is 20.

This validation ensures all required environment variables are properly configured before the application starts.
Please check your environment configuration and ensure all required variables are set.
```

## Best Practices

1. **Validate early**: Call `ValidateVariableNamesIn()` at application startup
2. **Use meaningful names**: Define environment variable names as constants
3. **Set minimum lengths**: Use `minLength` parameter for security-sensitive variables
4. **Group related variables**: Use separate configuration classes for different concerns
5. **Document requirements**: Add XML comments to explain what each variable is for

 