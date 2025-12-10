namespace Wrap.CrazyEmoji.IntegrationTests.Infrastructure.Auth;

internal record Token(string AccessToken, DateTime ExpiresAt, string RefreshToken);