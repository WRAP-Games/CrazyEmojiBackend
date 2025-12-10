namespace Wrap.CrazyEmoji.IntegrationTests.Infrastructure.Auth;

internal sealed record LoginResponse(string AccessToken, int ExpiresIn, string RefreshToken, string? TokenType);