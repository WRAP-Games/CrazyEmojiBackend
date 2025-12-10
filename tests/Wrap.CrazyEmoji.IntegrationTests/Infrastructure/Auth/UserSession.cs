namespace Wrap.CrazyEmoji.IntegrationTests.Infrastructure.Auth;

internal sealed record UserSession(string Email, string Password, Token Token);