using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Wrap.CrazyEmoji.Api.Data;
using Xunit;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Wrap.CrazyEmoji.IntegrationTests;

public class CrazyEmojiIntegrationTests : IAsyncLifetime
{
    private HubConnection? _connection1;
    private HubConnection? _connection2;
    private HubConnection? _connection3;
    private string? _roomCode;
    private readonly string _hubUrl = "http://localhost:5026/roomHub";
    private readonly string _apiBaseUrl = "http://localhost:5026";
    private GameDbContext? _db;
    private readonly HttpClient _httpClient = new();

    public async ValueTask InitializeAsync()
    {
        // Setup database for cleanup
        var basePath = AppContext.BaseDirectory;
        
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<CrazyEmojiIntegrationTests>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found. " +
                "Create appsettings.json in test project with ConnectionStrings:DefaultConnection");
        }
        
        var optionsBuilder = new DbContextOptionsBuilder<GameDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        
        _db = new GameDbContext(optionsBuilder.Options);
        
        // Cleanup any previous test data
        await CleanupTestData(_db);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection1 != null) await _connection1.DisposeAsync();
        if (_connection2 != null) await _connection2.DisposeAsync();
        if (_connection3 != null) await _connection3.DisposeAsync();
        
        // Cleanup test data
        if (_db != null)
        {
            await CleanupTestData(_db);
            await _db.DisposeAsync();
        }
        
        _httpClient.Dispose();
    }

    private async Task CleanupTestData(GameDbContext db)
    {
        var testUsers = await db.Users
            .Where(u => u.Username.StartsWith("integrationtest_"))
            .ToListAsync();

        if (testUsers.Any())
        {
            var testRoomCodes = await db.ActiveRooms
                .Where(r => testUsers.Select(u => u.Username).Contains(r.RoomCreator))
                .Select(r => r.RoomCode)
                .ToListAsync();

            var roomMembers = await db.RoomMembers
                .Where(rm => testRoomCodes.Contains(rm.RoomCode))
                .ToListAsync();

            if (roomMembers.Any())
                db.RoomMembers.RemoveRange(roomMembers);

            var activeRooms = await db.ActiveRooms
                .Where(r => testRoomCodes.Contains(r.RoomCode))
                .ToListAsync();

            if (activeRooms.Any())
                db.ActiveRooms.RemoveRange(activeRooms);

            db.Users.RemoveRange(testUsers);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task FullGameFlow_CreateRoomStartGamePlayRound_Success()
    {
        // Arrange - Create 3 users
        _connection1 = await CreateAuthenticatedConnection();
        _connection2 = await CreateAuthenticatedConnection();
        _connection3 = await CreateAuthenticatedConnection();

        var user1CreatedTcs = new TaskCompletionSource<string>();
        var user2CreatedTcs = new TaskCompletionSource<string>();
        var user3CreatedTcs = new TaskCompletionSource<string>();

        _connection1.On<string>("createdUser", username => user1CreatedTcs.SetResult(username));
        _connection2.On<string>("createdUser", username => user2CreatedTcs.SetResult(username));
        _connection3.On<string>("createdUser", username => user3CreatedTcs.SetResult(username));

        // Act - Create users
        await _connection1.InvokeAsync("CreateUser", "integrationtest_user1", "password123");
        await _connection2.InvokeAsync("CreateUser", "integrationtest_user2", "password123");
        await _connection3.InvokeAsync("CreateUser", "integrationtest_user3", "password123");

        var user1 = await user1CreatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var user2 = await user2CreatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var user3 = await user3CreatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert - Users created
        Assert.Equal("integrationtest_user1", user1);
        Assert.Equal("integrationtest_user2", user2);
        Assert.Equal("integrationtest_user3", user3);

        // Arrange - Room creation
        var roomCreatedTcs = new TaskCompletionSource<string>();
        var user1JoinedTcs = new TaskCompletionSource<object>();
        var user2JoinedTcs = new TaskCompletionSource<object>();
        var user3JoinedTcs = new TaskCompletionSource<object>();

        _connection1.On<string>("createdRoom", roomCode => roomCreatedTcs.SetResult(roomCode));
        _connection1.On<object>("joinedRoom", data => user1JoinedTcs.SetResult(data));
        _connection2.On<object>("joinedRoom", data => user2JoinedTcs.SetResult(data));
        _connection3.On<object>("joinedRoom", data => user3JoinedTcs.SetResult(data));

        // Act - Create room (user1 is creator)
        await _connection1.InvokeAsync("CreateRoom", "Integration Test Room", "Animals", 10, 30);
        _roomCode = await roomCreatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await user1JoinedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert - Room created
        Assert.NotNull(_roomCode);
        Assert.Equal(6, _roomCode.Length);

        // Act - Users 2 and 3 join room
        await _connection2.InvokeAsync("JoinRoom", _roomCode);
        await _connection3.InvokeAsync("JoinRoom", _roomCode);
        
        await user2JoinedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await user3JoinedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Arrange - Game start
        var gameStartedTcs1 = new TaskCompletionSource<bool>();
        var gameStartedTcs2 = new TaskCompletionSource<bool>();
        var gameStartedTcs3 = new TaskCompletionSource<bool>();

        _connection1.On("gameStarted", () => gameStartedTcs1.SetResult(true));
        _connection2.On("gameStarted", () => gameStartedTcs2.SetResult(true));
        _connection3.On("gameStarted", () => gameStartedTcs3.SetResult(true));

        // Act - Start game
        await _connection1.InvokeAsync("StartGame");

        await gameStartedTcs1.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await gameStartedTcs2.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await gameStartedTcs3.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert - Game started for all players
        Assert.True(gameStartedTcs1.Task.Result);
        Assert.True(gameStartedTcs2.Task.Result);
        Assert.True(gameStartedTcs3.Task.Result);

        // Arrange - Get commander
        var commanderTcs = new TaskCompletionSource<string>();
        _connection1.On<string>("commanderSelected", commander => commanderTcs.SetResult(commander));

        // Act - Get commander
        await _connection1.InvokeAsync("GetCommander");
        var commander = await commanderTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert - Commander selected
        Assert.Contains(commander, new[] { "integrationtest_user1", "integrationtest_user2", "integrationtest_user3" });

        // Arrange - Get word (commander only)
        var wordTcs = new TaskCompletionSource<string>();
        HubConnection commanderConnection = commander switch
        {
            "integrationtest_user1" => _connection1,
            "integrationtest_user2" => _connection2,
            "integrationtest_user3" => _connection3,
            _ => throw new Exception("Unknown commander")
        };

        commanderConnection.On<string>("recivedWord", word => wordTcs.SetResult(word));

        // Act - Get word
        await commanderConnection.InvokeAsync("GetWord");
        var word = await wordTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert - Word received
        Assert.NotNull(word);
        Assert.NotEmpty(word);

        // Arrange - Send emojis
        var emojisReceivedTcs = new TaskCompletionSource<bool>();
        var player1EmojisTcs = new TaskCompletionSource<List<string>>();
        var player2EmojisTcs = new TaskCompletionSource<List<string>>();

        commanderConnection.On("emojisRecieved", () => emojisReceivedTcs.SetResult(true));

        // Setup emoji receivers (non-commanders)
        var connections = new[] { _connection1, _connection2, _connection3 };
        var nonCommanderConnections = connections.Where(c => c != commanderConnection).ToList();

        nonCommanderConnections[0].On<List<string>>("recieveEmojis", emojis => player1EmojisTcs.SetResult(emojis));
        nonCommanderConnections[1].On<List<string>>("recieveEmojis", emojis => player2EmojisTcs.SetResult(emojis));

        // Act - Send emojis
        var emojis = new List<string> { "🐶", "🐕", "🦴" };
        await commanderConnection.InvokeAsync("SendEmojis", emojis);

        await emojisReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await player1EmojisTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await player2EmojisTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert - Emojis sent and received
        Assert.True(emojisReceivedTcs.Task.Result);
        Assert.NotEmpty(player1EmojisTcs.Task.Result);
        Assert.NotEmpty(player2EmojisTcs.Task.Result);

        // Arrange - Check word (players only)
        var player1CheckTcs = new TaskCompletionSource<bool>();
        var player2CheckTcs = new TaskCompletionSource<bool>();

        nonCommanderConnections[0].On<bool>("wordChecked", isCorrect => player1CheckTcs.SetResult(isCorrect));
        nonCommanderConnections[1].On<bool>("wordChecked", isCorrect => player2CheckTcs.SetResult(isCorrect));

        // Act - Players guess the word
        await nonCommanderConnections[0].InvokeAsync("CheckWord", word);
        await nonCommanderConnections[1].InvokeAsync("CheckWord", word);

        var player1Correct = await player1CheckTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var player2Correct = await player2CheckTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert - Both players guessed correctly
        Assert.True(player1Correct);
        Assert.True(player2Correct);

        // Test complete - cleanup will happen in DisposeAsync
    }

    [Fact]
    public async Task CreateUser_InvalidUsername_ReturnsError()
    {
        // Arrange
        _connection1 = await CreateAuthenticatedConnection();
        var errorTcs = new TaskCompletionSource<string>();
        _connection1.On<string>("Error", error => errorTcs.SetResult(error));

        // Act - Try to create user with invalid username (too short)
        await _connection1.InvokeAsync("CreateUser", "ab", "password123");
        var error = await errorTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("incorrectUsername", error);
    }

    [Fact]
    public async Task CreateRoom_InvalidCategory_ReturnsError()
    {
        // Arrange
        _connection1 = await CreateAuthenticatedConnection();
        var userCreatedTcs = new TaskCompletionSource<string>();
        var errorTcs = new TaskCompletionSource<string>();

        _connection1.On<string>("createdUser", username => userCreatedTcs.SetResult(username));
        _connection1.On<string>("Error", error => errorTcs.SetResult(error));

        // Act - Create user
        await _connection1.InvokeAsync("CreateUser", "integrationtest_badcat", "password123");
        await userCreatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act - Try to create room with invalid category
        await _connection1.InvokeAsync("CreateRoom", "Test Room", "InvalidCategory", 10, 30);
        var error = await errorTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("incorrectRoomCategory", error);
    }

    [Fact]
    public async Task JoinRoom_InvalidRoomCode_ReturnsError()
    {
        // Arrange
        _connection1 = await CreateAuthenticatedConnection();
        var userCreatedTcs = new TaskCompletionSource<string>();
        var errorTcs = new TaskCompletionSource<string>();

        _connection1.On<string>("createdUser", username => userCreatedTcs.SetResult(username));
        _connection1.On<string>("Error", error => errorTcs.SetResult(error));

        // Act - Create user
        await _connection1.InvokeAsync("CreateUser", "integrationtest_noroom", "password123");
        await userCreatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act - Try to join non-existent room
        await _connection1.InvokeAsync("JoinRoom", "XXXXXX");
        var error = await errorTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("incorrectRoomCode", error);
    }

    [Fact]
    public async Task StartGame_NotEnoughPlayers_ReturnsError()
    {
        // Arrange
        _connection1 = await CreateAuthenticatedConnection();
        _connection2 = await CreateAuthenticatedConnection();

        var user1CreatedTcs = new TaskCompletionSource<string>();
        var user2CreatedTcs = new TaskCompletionSource<string>();
        var roomCreatedTcs = new TaskCompletionSource<string>();
        var user1JoinedTcs = new TaskCompletionSource<object>();
        var user2JoinedTcs = new TaskCompletionSource<object>();
        var errorTcs = new TaskCompletionSource<string>();

        _connection1.On<string>("createdUser", username => user1CreatedTcs.SetResult(username));
        _connection2.On<string>("createdUser", username => user2CreatedTcs.SetResult(username));
        _connection1.On<string>("createdRoom", roomCode => roomCreatedTcs.SetResult(roomCode));
        _connection1.On<object>("joinedRoom", data => user1JoinedTcs.SetResult(data));
        _connection2.On<object>("joinedRoom", data => user2JoinedTcs.SetResult(data));
        _connection1.On<string>("Error", error => errorTcs.SetResult(error));

        // Act - Create users
        await _connection1.InvokeAsync("CreateUser", "integrationtest_few1", "password123");
        await _connection2.InvokeAsync("CreateUser", "integrationtest_few2", "password123");
        await user1CreatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await user2CreatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act - Create room and join
        await _connection1.InvokeAsync("CreateRoom", "Few Players", "Animals", 10, 30);
        _roomCode = await roomCreatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await user1JoinedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await _connection2.InvokeAsync("JoinRoom", _roomCode);
        await user2JoinedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act - Try to start game with only 2 players
        await _connection1.InvokeAsync("StartGame");
        var error = await errorTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("notEnoughPlayers", error);
    }

    [Fact]
    public async Task LoginUser_ValidCredentials_Success()
    {
        // Arrange
        _connection1 = await CreateAuthenticatedConnection();
        _connection2 = await CreateAuthenticatedConnection();

        var userCreatedTcs = new TaskCompletionSource<string>();
        var loginTcs = new TaskCompletionSource<bool>();

        _connection1.On<string>("createdUser", username => userCreatedTcs.SetResult(username));
        _connection2.On("userLoggedIn", () => loginTcs.SetResult(true));

        // Act - Create user
        await _connection1.InvokeAsync("CreateUser", "integrationtest_login", "password123");
        await userCreatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await _connection1.StopAsync();

        // Act - Login with same credentials from new connection (already started)
        await _connection2.InvokeAsync("LoginUser", "integrationtest_login", "password123");
        var loggedIn = await loginTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(loggedIn);
    }

    private async Task<string> RegisterAndGetToken(string email, string password)
    {
        // Register user via Identity API
        var registerResponse = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/api/auth/register", new
        {
            email = email,
            password = password
        });

        if (!registerResponse.IsSuccessStatusCode)
        {
            // User might already exist, try login
            var loginResponse = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/api/auth/login", new
            {
                email = email,
                password = password,
                twoFactorCode = (string?)null,
                twoFactorRecoveryCode = (string?)null
            });

            if (!loginResponse.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to login: {await loginResponse.Content.ReadAsStringAsync()}");
            }

            var loginResult = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
            return loginResult.GetProperty("accessToken").GetString()!;
        }

        // Login after successful registration
        var loginAfterRegisterResponse = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/api/auth/login", new
        {
            email = email,
            password = password,
            twoFactorCode = (string?)null,
            twoFactorRecoveryCode = (string?)null
        });

        if (!loginAfterRegisterResponse.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to login after registration: {await loginAfterRegisterResponse.Content.ReadAsStringAsync()}");
        }

        var result = await loginAfterRegisterResponse.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("accessToken").GetString()!;
    }

    private async Task<HubConnection> CreateAuthenticatedConnection()
    {
        // Create a unique Identity user for this connection
        var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var email = $"test_{uniqueId}@test.com";
        var password = "Test123!@#";
        
        var token = await RegisterAndGetToken(email, password);
        
        var connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token)!;
            })
            .WithAutomaticReconnect()
            .Build();

        await connection.StartAsync();
        return connection;
    }
}