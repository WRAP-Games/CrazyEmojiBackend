using System.Collections.Concurrent;
using Wrap.CrazyEmoji.IntegrationTests.Infrastructure.Auth;

namespace Wrap.CrazyEmoji.IntegrationTests.Infrastructure;

internal class HttpClientProvider(ApiApplicationFactory factory, string registerPath, string loginPath, string refreshPath)
{
    private readonly ApiApplicationFactory _factory = factory;
    private readonly string _registerPath = registerPath;
    private readonly string _loginPath = loginPath;
    private readonly string _refreshPath = refreshPath;
    private readonly ConcurrentDictionary<(string email, string baseAddress, bool includeAccessToken), Task<HttpClient>> _clientsCache = new();
    private readonly ConcurrentDictionary<string, Token> _sessions = new();

    internal async Task<HttpClient> GetHttpClientAsync(
        UserCredentials user,
        string baseAddress,
        bool includeAccessToken)
    {
        var key = (user.Email, baseAddress, includeAccessToken);

        return await _clientsCache.GetOrAdd(key, async _ =>
        {
            var client = _factory.CreateClient(new() { BaseAddress = new Uri(baseAddress) });

            if (includeAccessToken)
            {
                var accessToken = await GetOrRefreshTokenAsync(client, user.Email, user.Password);
                client.DefaultRequestHeaders.Authorization = new(scheme: "Bearer", parameter: accessToken.AccessToken);
            }

            return client;
        });
    }

    private async Task<Token> GetOrRefreshTokenAsync(HttpClient client, string email, string password)
    {
        if (_sessions.TryGetValue(email, out var existingToken))
        {
            if (existingToken.ExpiresAt > DateTime.UtcNow.AddMinutes(5))
            {
                return existingToken;
            }

            if (!string.IsNullOrEmpty(existingToken.RefreshToken))
            {
                var refreshedToken = await TokenProvider.RefreshTokenAsync(
                    client,
                    _refreshPath,
                    existingToken.RefreshToken);

                _sessions.TryAdd(email, refreshedToken);

                return refreshedToken;
            }
        }

        // No existing token or refresh failed - register and login
        var token = await TokenProvider.RegisterThenLoginAsync(
            client,
            _registerPath,
            _loginPath,
            email,
            password);

        _sessions[email] = token;
        return token;
    }
}