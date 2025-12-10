# [MongoDbTokenManager](https://www.nuget.org/packages/MongoDbTokenManager)

**MongoDbTokenManager** is an open-source C# class library designed to create, manage, and verify One-Time Tokens (OTTs). These tokens are stored in a MongoDB database, leveraging the [MongoDbService](https://www.nuget.org/packages/MongoDbService) package for configuration and access.

## Features

- **Token Generation**: Generate numeric codes (e.g., for SMS) or GUID-based tokens (e.g., for email links).
- **Secure Validation**: Verify tokens with built-in expiration checks.
- **Automatic Cleanup**: Expired tokens are automatically deleted using MongoDB TTL indexes (default: 24 hours after expiry).
- **Distributed & Scalable**: Uses MongoDB for storage, allowing different service instances to generate and verify tokens independently.
- **Strongly Typed IDs**: Uses `TokenIdentifier` to ensure type safety for user or resource IDs.

## Getting Started

### 1. Install the Package

Install the NuGet package via the .NET CLI:

```bash
dotnet add package MongoDbTokenManager
```

### 2. Configuration

Configure your MongoDB connection settings in your `appsettings.json` or environment variables. The library uses `MongoDbService` which requires the following structure:

```json
{
  "MongoDbSettings": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "YourDatabaseName"
  }
}
```

### 3. Dependency Injection

Register the services in your `Program.cs` or `Startup.cs`:

```csharp
using MongoDbTokenManager;
using MongoDbService; // Ensure this is referenced

var builder = WebApplication.CreateBuilder(args);

// Register MongoDbService (required dependency)
builder.Services.AddMongoDbService(); 

// Register MongoDbTokenManager services
builder.Services.AddMongoDbTokenServices();

var app = builder.Build();
```

## Usage Example

Inject `MongoDbTokenService` into your class to generate and verify tokens.

```csharp
using MongoDbTokenManager;
using MongoDbTokenManager.Database;

public class AuthenticationService
{
    private readonly MongoDbTokenService _tokenService;

    public AuthenticationService(MongoDbTokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public async Task<string> SendVerificationCode(string userId)
    {
        // Generate a 6-digit code valid for 5 minutes (300 seconds)
        var token = await _tokenService.Generate(
            logId: "UserLogin", 
            id: new TokenIdentifier(userId), 
            validityInSeconds: 300, 
            numberOfDigits: 6
        );

        // If numberOfDigits is 0 (default), a GUID-based token is generated.
        
        return token; // Send this token via SMS/Email
    }

    public async Task<bool> VerifyCode(string userId, string code)
    {
        // Verify the token.
        // Returns false if token is expired or does not match.
        bool isValid = await _tokenService.Validate(new TokenIdentifier(userId), code);

        if (isValid)
        {
            // Ideally, consume the token so it cannot be used again
            await _tokenService.Consume(new TokenIdentifier(userId));
        }

        return isValid;
    }
    
    public async Task<bool> VerifyAndConsume(string userId, string code)
    {
        // Convenience method to validate and consume in one step
        return await _tokenService.ConsumeAndValidate(new TokenIdentifier(userId), code);
    }
}
```

## API Reference

### `Generate`
Creates a new token or updates an existing one if valid.
- `logId`: A string for logging purposes.
- `id`: The unique `TokenIdentifier` for the user/resource.
- `validityInSeconds`: How long the token remains valid.
- `numberOfDigits`: (Optional) Length of the numeric code. If `0`, generates a GUID string.

### `Validate`
Checks if a token is valid.
- Returns `true` if the token matches and is not expired.
- **Note**: This package does not include brute-force protection. Implement rate limiting at your API layer if needed.

### `Consume`
Deletes the token associated with the identifier, preventing further use.

### `ConsumeAndValidate`
Validates the token and immediately consumes it (deletes it) regardless of the result. Useful for strict one-time-use scenarios.

## Configuration Options

### Automatic Token Cleanup

Expired tokens are automatically cleaned up by MongoDB using a TTL index. By default, tokens are deleted **24 hours after expiry**. You can customize this when registering the service:

```csharp
// Custom cleanup: delete tokens 1 hour after expiry
builder.Services.AddSingleton(sp =>
    new MongoDbTokenService(
        sp.GetRequiredService<MongoService>(),
        cleanupAfterExpiry: TimeSpan.FromHours(1)
    ));
```

Set to `TimeSpan.Zero` to delete tokens immediately upon expiry.

## Contributing

We welcome contributions! If you find a bug or have an idea for improvement, please submit an issue or a pull request on GitHub.

## License

This project is licensed under the GNU GENERAL PUBLIC LICENSE.

Happy coding! 🚀
