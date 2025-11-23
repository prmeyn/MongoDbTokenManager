# [MongoDbTokenManager](https://www.nuget.org/packages/MongoDbTokenManager)

**MongoDbTokenManager** is an open-source C# class library designed to create, manage, and verify One-Time Tokens (OTTs). These tokens are stored in a MongoDB database, leveraging the [MongoDbService](https://www.nuget.org/packages/MongoDbService) package for configuration and access.

## Features

- **Token Generation**: Generate numeric codes (e.g., for SMS) or GUID-based tokens (e.g., for email links).
- **Secure Validation**: Verify tokens with built-in expiration and maximum attempt limits (default: 5 attempts).
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
    "MongoDatabaseName": "YourDatabaseName"
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
        // Returns false if:
        // - Token is expired
        // - Token does not match
        // - Maximum validation attempts (5) have been exceeded
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
- Returns `true` if the token matches, is not expired, and attempt limit is not reached.
- **Note**: Allows up to **5 validation attempts**. After that, the token becomes invalid regardless of correctness.

### `Consume`
Deletes the token associated with the identifier, preventing further use.

### `ConsumeAndValidate`
Validates the token and immediately consumes it (deletes it) regardless of the result. Useful for strict one-time-use scenarios.

## Contributing

We welcome contributions! If you find a bug or have an idea for improvement, please submit an issue or a pull request on GitHub.

## License

This project is licensed under the GNU GENERAL PUBLIC LICENSE.

Happy coding! 🚀
