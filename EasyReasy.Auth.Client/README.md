# EasyReasy.Auth.Client

[← Back to EasyReasy System](../README.md)

[![NuGet](https://img.shields.io/badge/nuget-EasyReasy.Auth.Client-blue.svg)](https://www.nuget.org/packages/EasyReasy.Auth.Client)

A lightweight .NET client library for authenticating with EasyReasy.Auth servers, designed for simplicity and automatic token management.

## Overview

EasyReasy.Auth.Client provides a simple HTTP client wrapper that automatically handles authentication with EasyReasy.Auth servers. It supports both API key and username/password authentication, with automatic token refresh and retry logic.

**Why Use EasyReasy.Auth.Client?**

- **Automatic authentication**: Handles JWT token acquisition and renewal transparently
- **Multiple auth methods**: Support for API key and username/password authentication
- **Token management**: Automatic token refresh before expiration (5-minute buffer)
- **Retry logic**: Automatically retries requests on 401 Unauthorized with fresh tokens
- **Simple API**: Drop-in replacement for HttpClient with minimal code changes
- **Flexible configuration**: Customizable auth endpoints and HTTP client settings

## Quick Start

### 1. Add to your project

Install via NuGet:
```sh
dotnet add package EasyReasy.Auth.Client
```

### 2. Create an authorized client

#### API Key Authentication
```csharp
using (HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/"))
{
    AuthorizedHttpClient authorizedClient = new AuthorizedHttpClient(httpClient, "your-api-key-here");

    // The client will automatically authenticate on first use
    HttpResponseMessage response = await authorizedClient.GetAsync("api/data");
}
```

#### Username/Password Authentication
```csharp
using (HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/"))
{
    AuthorizedHttpClient authorizedClient = new AuthorizedHttpClient(
        httpClient, 
        username: "your-username", 
        password: "your-password");

    // The client will automatically authenticate on first use
    HttpResponseMessage response = await authorizedClient.GetAsync("api/data");
}
```

### 3. Use the client like a regular HttpClient

```csharp
using (HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/"))
{
    AuthorizedHttpClient authorizedClient = new AuthorizedHttpClient(httpClient, "your-api-key");

    // GET requests
    HttpResponseMessage response = await authorizedClient.GetAsync("api/users");

    // POST requests
    StringContent content = new StringContent("{\"name\":\"John\"}", Encoding.UTF8, "application/json");
    HttpResponseMessage response = await authorizedClient.PostAsync("api/users", content);

    // Custom requests
    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, "api/users/123");
    request.Content = new StringContent("{\"name\":\"Jane\"}", Encoding.UTF8, "application/json");
    HttpResponseMessage response = await authorizedClient.SendAsync(request);
}
```

## Advanced Usage

### Custom Auth Endpoints

By default, the client uses standard EasyReasy.Auth endpoints:
- API Key: `/api/auth/apikey`
- Username/Password: `/api/auth/login`

You can customize these endpoints:

```csharp
using (HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/"))
{
    // Custom API key endpoint
    AuthorizedHttpClient apiKeyClient = new AuthorizedHttpClient(
        httpClient, 
        "your-api-key", 
        authEndpoint: "custom/auth/apikey");

    // Custom login endpoint
    AuthorizedHttpClient loginClient = new AuthorizedHttpClient(
        httpClient, 
        "username", 
        "password", 
        authEndpoint: "custom/auth/login");
}
```

### Manual Authentication Control

You can manually control when authentication happens:

```csharp
using (HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/"))
{
    AuthorizedHttpClient client = new AuthorizedHttpClient(httpClient, "api-key");

    // Force authentication now
    await client.EnsureAuthorizedAsync();

    // Check authentication type
    if (client.AuthenticationType == AuthorizedHttpClient.AuthType.ApiKey)
    {
        Console.WriteLine("Using API key authentication");
    }
}
```

### Token Expiration
The client automatically handles token expiration:
1. Detects when token expires within 5 minutes
2. Automatically re-authenticates before making requests
3. Retries failed requests once with a fresh token

## Best Practices

### 1. HttpClient Lifecycle Management

The `AuthorizedHttpClient` doesn't dispose the underlying `HttpClient`. Manage the `HttpClient` lifecycle according to your application's needs:

```csharp
// For long-lived applications
HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/");

// Reuse the same AuthorizedHttpClient instance
AuthorizedHttpClient authorizedClient = new AuthorizedHttpClient(httpClient, "api-key");

// Use throughout your application
// Don't dispose the AuthorizedHttpClient unless you're done with the HttpClient
```

### 2. Error Handling

Always handle authentication and network errors:

```csharp
using (HttpClient httpClient = AuthorizedHttpClient.CreateHttpClient("https://api.example.com/"))
{
    AuthorizedHttpClient authorizedClient = new AuthorizedHttpClient(httpClient, "api-key");
    
    try
    {
        HttpResponseMessage response = await authorizedClient.GetAsync("api/data");
        response.EnsureSuccessStatusCode();
        
        string content = await response.Content.ReadAsStringAsync();
        // Process response
    }
    catch (UnauthorizedAccessException)
    {
        // Handle authentication errors
    }
    catch (HttpRequestException)
    {
        // Handle network/server errors
    }
}
```