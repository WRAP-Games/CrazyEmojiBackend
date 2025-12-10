using System.Net;
using System.Net.Http.Json;

namespace Wrap.CrazyEmoji.IntegrationTests.Infrastructure.Auth;

internal static class TokenProvider
{
    internal static async Task<Token> RegisterThenLoginAsync(
        HttpClient httpClient,
        string registerPath,
        string loginPath,
        string email,
        string password)
    {
        await TryRegisterAsync(httpClient, registerPath, email, password);
        return await LoginAndGetAccessTokenAsync(httpClient, loginPath, email, password);
    }

    internal static async Task<Token> RefreshTokenAsync(
        HttpClient httpClient,
        string refreshPath,
        string refreshToken)
    {
        var response = await httpClient.PostAsJsonAsync(refreshPath, new { RefreshToken = refreshToken });
        response.EnsureSuccessStatusCode();

        var refreshResponse = await response.Content.ReadFromJsonAsync<LoginResponse>()
            ?? throw new InvalidOperationException("Refresh token response is null");

        return new Token(
            AccessToken: refreshResponse.AccessToken,
            ExpiresAt: DateTime.UtcNow.AddSeconds(refreshResponse.ExpiresIn),
            RefreshToken: refreshResponse.RefreshToken);
    }

    private static async Task TryRegisterAsync(
        HttpClient httpClient,
        string registerPath,
        string email,
        string password)
    {
        var response = await httpClient.PostAsJsonAsync(registerPath, new
        {
            Email = email,
            Password = password
        });

        if (response.IsSuccessStatusCode || response.StatusCode is HttpStatusCode.Conflict) return;

        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"Register failed {(int)response.StatusCode} {response.StatusCode}\n{body}");
    }

    private static async Task<Token> LoginAndGetAccessTokenAsync(
        HttpClient httpClient,
        string loginPath,
        string email,
        string password)
    {
        var response = await httpClient.PostAsJsonAsync(loginPath, new { email, password });
        response.EnsureSuccessStatusCode();

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>()
            ?? throw new InvalidOperationException("Login response is null");

        return new Token(
            AccessToken: loginResponse.AccessToken,
            ExpiresAt: DateTime.UtcNow.AddSeconds(loginResponse.ExpiresIn),
            RefreshToken: loginResponse.RefreshToken);
    }
}