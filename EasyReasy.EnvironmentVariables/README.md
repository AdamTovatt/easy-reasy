# EasyReasy.EnvironmentVariables

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

### Loading from Files

Load environment variables from `.env` files and set them in the running program:

```csharp
EnvironmentVariableHelper.LoadVariablesFromFile("config.env");
```

File format:
```