using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Wrap.CrazyEmoji.Api.Abstractions;
using Wrap.CrazyEmoji.Api.GameLogic;
using Wrap.CrazyEmoji.Api.GameLogic.Exceptions;
using Xunit;

namespace Wrap.CrazyEmoji.IntegrationTests;

#region Test Infrastructure

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Jwt:Key"] = "ThisIsATestSecretKeyForJwtTokenGenerationWithAtLeast32CharactersLong",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["ConnectionStrings:DefaultConnection"] = "" // Disable database
            }!);
        });
        
        builder.UseTestServer();
        
        builder.ConfigureTestServices(services =>
        {
            // Remove any existing IWordService registrations
            var wordServiceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IWordService));
            if (wordServiceDescriptor != null)
            {
                services.Remove(wordServiceDescriptor);
            }
            
            // Add our test word service
            services.AddSingleton<IWordService>(new TestWordService());
            
            // Disable authentication for testing
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = "Test";
            }).AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
        });
    }
}

// Test authentication handler that always succeeds
public class TestAuthHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "TestUser") };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(principal, "Test");
        return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(ticket));
    }
}

public class TestWordService : IWordService
{
    private readonly List<string> _words = new()
    {
        "Apple", "Banana", "Cherry", "Dragon", "Elephant"
    };

    private int _currentIndex = 0;
    private bool _isLoaded = true; // Mark as already loaded

    public Task<string> GetRandomWordAsync()
    {
        if (!_isLoaded || _words.Count == 0)
            throw new InvalidOperationException("Word list not loaded.");
            
        var word = _words[_currentIndex % _words.Count];
        _currentIndex++;
        return Task.FromResult(word);
    }

    public Task LoadWordsAsync(Stream wordStream)
    {
        _isLoaded = true;
        return Task.CompletedTask;
    }

    public void Reset() => _currentIndex = 0;
}

public class SignalRTestClient : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly ConcurrentDictionary<string, List<object?[]>> _receivedMessages = new();

    public HubConnection Connection => _connection;
    public string ConnectionId => _connection.ConnectionId ?? string.Empty;

    public SignalRTestClient(string hubUrl, HttpMessageHandler httpMessageHandler)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => httpMessageHandler;
            })
            .Build();
    }

    public async Task StartAsync() => await _connection.StartAsync();

    public void On<T>(string methodName, Action<T> handler)
    {
        _connection.On<T>(methodName, message =>
        {
            CaptureMessage(methodName, message);
            handler(message);
        });
    }

    public void On<T1, T2>(string methodName, Action<T1, T2> handler)
    {
        _connection.On<T1, T2>(methodName, (arg1, arg2) =>
        {
            CaptureMessage(methodName, arg1, arg2);
            handler(arg1, arg2);
        });
    }

    public void OnCapture(string methodName)
    {
        _connection.On<object>(methodName, message => CaptureMessage(methodName, message));
    }

    public void OnString(string methodName)
    {
        _connection.On<string>(methodName, message => CaptureMessage(methodName, message));
    }

    public void OnStringInt(string methodName)
    {
        _connection.On<string, int>(methodName, (msg, value) => CaptureMessage(methodName, msg, value));
    }

    private void CaptureMessage(string methodName, params object?[] args)
    {
        if (!_receivedMessages.ContainsKey(methodName))
            _receivedMessages[methodName] = new List<object?[]>();
        _receivedMessages[methodName].Add(args);
    }

    public List<object?[]> GetMessages(string methodName)
    {
        return _receivedMessages.TryGetValue(methodName, out var messages) 
            ? messages : new List<object?[]>();
    }

    public object?[]? GetFirstMessage(string methodName)
    {
        var messages = GetMessages(methodName);
        return messages.Count > 0 ? messages[0] : null;
    }

    public object?[]? GetLastMessage(string methodName)
    {
        var messages = GetMessages(methodName);
        return messages.Count > 0 ? messages[^1] : null;
    }

    public async Task<bool> WaitForMessageAsync(string methodName, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            if (GetMessages(methodName).Count > 0)
                return true;
            await Task.Delay(100);
        }
        return false;
    }

    public async Task<bool> WaitForMessagesAsync(string methodName, int count, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            if (GetMessages(methodName).Count >= count)
                return true;
            await Task.Delay(100);
        }
        return false;
    }

    public void ClearMessages() => _receivedMessages.Clear();

    public async Task InvokeAsync(string methodName, params object[] args)
    {
        await _connection.InvokeAsync(methodName, args);
    }

    public async Task<T> InvokeAsync<T>(string methodName, params object[] args)
    {
        return await _connection.InvokeAsync<T>(methodName, args);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection.State == HubConnectionState.Connected)
            await _connection.StopAsync();
        await _connection.DisposeAsync();
    }
}

#endregion

#region Hub Integration Tests

public class RoomHubIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly List<SignalRTestClient> _clients = new();

    public RoomHubIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var client in _clients)
            await client.DisposeAsync();
        _clients.Clear();
    }

    private SignalRTestClient CreateClient()
    {
        var client = new SignalRTestClient(
            _factory.Server.BaseAddress + "roomhub", 
            _factory.Server.CreateHandler());
        _clients.Add(client);
        return client;
    }

    [Fact]
    public async Task SetUsername_ShouldSetUsernameSuccessfully()
    {
        var client = CreateClient();
        client.OnString("UsernameSet");
        await client.StartAsync();

        await client.InvokeAsync("SetUsername", "TestPlayer");
        var received = await client.WaitForMessageAsync("UsernameSet", TimeSpan.FromSeconds(10));

        received.Should().BeTrue("UsernameSet message should be received");
        var messages = client.GetMessages("UsernameSet");
        messages.Should().HaveCount(1);
        messages[0][0].Should().Be("TestPlayer");
    }

    [Fact]
    public async Task CreateRoom_ShouldCreateRoomAndReturnRoomCode()
    {
        var client = CreateClient();
        client.OnString("CreatedRoom");
        client.OnString("JoinedRoom");
        await client.StartAsync();
        await client.InvokeAsync("SetUsername", "Host");

        await client.InvokeAsync("CreateRoom", "Test Room");
        await client.WaitForMessageAsync("CreatedRoom");

        var createdMessages = client.GetMessages("CreatedRoom");
        createdMessages.Should().HaveCount(1);
        var roomCode = createdMessages[0][0] as string;
        roomCode.Should().NotBeNullOrEmpty();
        roomCode.Should().HaveLength(6);
    }

    [Fact]
    public async Task JoinRoom_WithValidRoomCode_ShouldJoinSuccessfully()
    {
        var hostClient = CreateClient();
        var playerClient = CreateClient();
        
        hostClient.OnString("CreatedRoom");
        hostClient.OnString("JoinedRoom");
        playerClient.OnString("JoinedRoom");
        
        await hostClient.StartAsync();
        await playerClient.StartAsync();
        
        await hostClient.InvokeAsync("SetUsername", "Host");
        await hostClient.InvokeAsync("CreateRoom", "Test Room");
        await hostClient.WaitForMessageAsync("CreatedRoom");
        
        var roomCode = hostClient.GetFirstMessage("CreatedRoom")?[0] as string;

        await playerClient.InvokeAsync("SetUsername", "Player1");
        await playerClient.InvokeAsync("JoinRoom", roomCode);
        await playerClient.WaitForMessageAsync("JoinedRoom");

        var joinedMessages = playerClient.GetMessages("JoinedRoom");
        joinedMessages.Should().HaveCount(1);
        joinedMessages[0][0].Should().Be(roomCode);
    }

    [Fact]
    public async Task JoinRoom_WithInvalidRoomCode_ShouldReturnError()
    {
        var client = CreateClient();
        client.OnString("Error");
        await client.StartAsync();
        await client.InvokeAsync("SetUsername", "Player");

        await client.InvokeAsync("JoinRoom", "INVALID");
        await client.WaitForMessageAsync("Error", TimeSpan.FromSeconds(3));

        var errorMessages = client.GetMessages("Error");
        errorMessages.Should().HaveCount(1);
        errorMessages[0][0].Should().Be("Room not found");
    }

    [Fact]
    public async Task StartGame_WithLessThan3Players_ShouldReturnError()
    {
        var hostClient = CreateClient();
        var player1Client = CreateClient();
        
        hostClient.OnString("CreatedRoom");
        hostClient.OnString("JoinedRoom");
        hostClient.OnString("Error");
        player1Client.OnString("JoinedRoom");
        
        await hostClient.StartAsync();
        await player1Client.StartAsync();
        
        await hostClient.InvokeAsync("SetUsername", "Host");
        await hostClient.InvokeAsync("CreateRoom", "Test Room");
        await hostClient.WaitForMessageAsync("CreatedRoom");
        
        var roomCode = hostClient.GetFirstMessage("CreatedRoom")?[0] as string;
        
        await player1Client.InvokeAsync("SetUsername", "Player1");
        await player1Client.InvokeAsync("JoinRoom", roomCode);
        await player1Client.WaitForMessageAsync("JoinedRoom");

        await hostClient.InvokeAsync("StartGame");
        await hostClient.WaitForMessageAsync("Error", TimeSpan.FromSeconds(3));

        var errorMessages = hostClient.GetMessages("Error");
        errorMessages.Should().HaveCountGreaterThan(0);
        var errorMessage = errorMessages[0][0] as string;
        errorMessage.Should().Contain("players");
    }

    [Fact]
    public async Task StartGame_With3Players_ShouldStartGameSuccessfully()
    {
        var hostClient = CreateClient();
        var player1Client = CreateClient();
        var player2Client = CreateClient();
        
        hostClient.OnString("CreatedRoom");
        hostClient.OnString("JoinedRoom");
        hostClient.OnString("GameStarted");
        player1Client.OnString("JoinedRoom");
        player1Client.OnString("GameStarted");
        player2Client.OnString("JoinedRoom");
        player2Client.OnString("GameStarted");
        
        await hostClient.StartAsync();
        await player1Client.StartAsync();
        await player2Client.StartAsync();
        
        await hostClient.InvokeAsync("SetUsername", "Host");
        await hostClient.InvokeAsync("CreateRoom", "Test Room");
        await hostClient.WaitForMessageAsync("CreatedRoom");
        
        var roomCode = hostClient.GetFirstMessage("CreatedRoom")?[0] as string;
        
        await player1Client.InvokeAsync("SetUsername", "Player1");
        await player1Client.InvokeAsync("JoinRoom", roomCode);
        await player1Client.WaitForMessageAsync("JoinedRoom");
        
        await player2Client.InvokeAsync("SetUsername", "Player2");
        await player2Client.InvokeAsync("JoinRoom", roomCode);
        await player2Client.WaitForMessageAsync("JoinedRoom");

        await hostClient.InvokeAsync("StartGame");
        await Task.WhenAll(
            hostClient.WaitForMessageAsync("GameStarted"),
            player1Client.WaitForMessageAsync("GameStarted"),
            player2Client.WaitForMessageAsync("GameStarted")
        );

        hostClient.GetMessages("GameStarted").Should().HaveCount(1);
        player1Client.GetMessages("GameStarted").Should().HaveCount(1);
        player2Client.GetMessages("GameStarted").Should().HaveCount(1);
    }

    [Fact]
    public async Task GameRound_ShouldSelectCommanderAndSendWord()
    {
        var clients = new[] { CreateClient(), CreateClient(), CreateClient() };
        
        foreach (var client in clients)
        {
            client.OnString("CreatedRoom");
            client.OnString("JoinedRoom");
            client.OnString("GameStarted");
            client.OnString("CommanderSelected");
            client.OnString("CommanderAnnounced");
            client.OnString("ReceiveWord");
            await client.StartAsync();
        }
        
        await clients[0].InvokeAsync("SetUsername", "Player1");
        await clients[0].InvokeAsync("CreateRoom", "Test Room");
        await clients[0].WaitForMessageAsync("CreatedRoom");
        var roomCode = clients[0].GetFirstMessage("CreatedRoom")?[0] as string;
        
        await clients[1].InvokeAsync("SetUsername", "Player2");
        await clients[1].InvokeAsync("JoinRoom", roomCode);
        await clients[1].WaitForMessageAsync("JoinedRoom");
        
        await clients[2].InvokeAsync("SetUsername", "Player3");
        await clients[2].InvokeAsync("JoinRoom", roomCode);
        await clients[2].WaitForMessageAsync("JoinedRoom");

        await clients[0].InvokeAsync("StartGame");
        await Task.Delay(1000);

        var commanderSelectedCount = clients.Count(c => c.GetMessages("CommanderSelected").Count > 0);
        var commanderAnnouncedCount = clients.Count(c => c.GetMessages("CommanderAnnounced").Count > 0);
        var receiveWordCount = clients.Count(c => c.GetMessages("ReceiveWord").Count > 0);
        
        commanderSelectedCount.Should().Be(1, "exactly one player should be selected as commander");
        commanderAnnouncedCount.Should().Be(2, "two players should be notified about commander");
        receiveWordCount.Should().Be(1, "exactly one player (commander) should receive word");
    }

    [Fact]
    public async Task PlayerDisconnect_ShouldRemovePlayerFromRoom()
    {
        var hostClient = CreateClient();
        var playerClient = CreateClient();
        
        hostClient.OnString("CreatedRoom");
        hostClient.OnString("JoinedRoom");
        hostClient.OnString("PlayerLeft");
        playerClient.OnString("JoinedRoom");
        
        await hostClient.StartAsync();
        await playerClient.StartAsync();
        
        await hostClient.InvokeAsync("SetUsername", "Host");
        await hostClient.InvokeAsync("CreateRoom", "Test Room");
        await hostClient.WaitForMessageAsync("CreatedRoom");
        
        var roomCode = hostClient.GetFirstMessage("CreatedRoom")?[0] as string;
        
        await playerClient.InvokeAsync("SetUsername", "Player1");
        await playerClient.InvokeAsync("JoinRoom", roomCode);
        await playerClient.WaitForMessageAsync("JoinedRoom");

        await playerClient.DisposeAsync();
        _clients.Remove(playerClient);
        await hostClient.WaitForMessageAsync("PlayerLeft", TimeSpan.FromSeconds(3));

        var playerLeftMessages = hostClient.GetMessages("PlayerLeft");
        playerLeftMessages.Should().HaveCount(1);
    }
}

#endregion

#region Room Manager Integration Tests

public class RoomManagerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RoomManagerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private RoomManager CreateRoomManager(IWordService? wordService = null)
    {
        var scope = _factory.Services.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<RoomHub>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RoomManager>>();
        wordService ??= scope.ServiceProvider.GetRequiredService<IWordService>();
        
        return new RoomManager(hubContext, wordService, logger);
    }

    [Fact]
    public async Task CreateRoomAsync_ShouldReturnUniqueRoomCode()
    {
        var roomManager = CreateRoomManager();
        var roomCode = await roomManager.CreateRoomAsync("Test Room");

        roomCode.Should().NotBeNullOrEmpty();
        roomCode.Should().HaveLength(6);
        roomCode.Should().MatchRegex("^[A-Z0-9]{6}$");
    }

    [Fact]
    public async Task CreateRoomAsync_ShouldCreateMultipleUniqueRooms()
    {
        var roomManager = CreateRoomManager();
        var roomCodes = new HashSet<string>();

        for (int i = 0; i < 10; i++)
        {
            var roomCode = await roomManager.CreateRoomAsync($"Room {i}");
            roomCodes.Add(roomCode!);
        }

        roomCodes.Should().HaveCount(10, "all room codes should be unique");
    }

    [Fact]
    public async Task AddPlayerAsync_WithValidRoomCode_ShouldAddPlayer()
    {
        var roomManager = CreateRoomManager();
        var roomCode = await roomManager.CreateRoomAsync("Test Room");
        var player = new Player("TestPlayer", "conn-123");

        var result = await roomManager.AddPlayerAsync(roomCode!, player);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AddPlayerAsync_WithInvalidRoomCode_ShouldThrowException()
    {
        var roomManager = CreateRoomManager();
        var player = new Player("TestPlayer", "conn-123");

        var act = async () => await roomManager.AddPlayerAsync("INVALID", player);

        await act.Should().ThrowAsync<RoomNotFoundException>()
            .WithMessage("*INVALID*");
    }

    [Fact]
    public async Task StartGameAsync_WithLessThan3Players_ShouldThrowException()
    {
        var roomManager = CreateRoomManager();
        var roomCode = await roomManager.CreateRoomAsync("Test Room");
        await roomManager.AddPlayerAsync(roomCode!, new Player("Player1", "conn-1"));
        await roomManager.AddPlayerAsync(roomCode!, new Player("Player2", "conn-2"));

        var act = async () => await roomManager.StartGameAsync(roomCode!);

        await act.Should().ThrowAsync<NotEnoughPlayersException>()
            .WithMessage("*at least 3 players*");
    }

    [Fact]
    public async Task StartGameAsync_With3OrMorePlayers_ShouldStartGame()
    {
        var roomManager = CreateRoomManager();
        var roomCode = await roomManager.CreateRoomAsync("Test Room");
        await roomManager.AddPlayerAsync(roomCode!, new Player("Player1", "conn-1"));
        await roomManager.AddPlayerAsync(roomCode!, new Player("Player2", "conn-2"));
        await roomManager.AddPlayerAsync(roomCode!, new Player("Player3", "conn-3"));

        var result = await roomManager.StartGameAsync(roomCode!);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RemovePlayerAsync_ShouldRemovePlayerFromRoom()
    {
        var roomManager = CreateRoomManager();
        var roomCode = await roomManager.CreateRoomAsync("Test Room");
        var player = new Player("TestPlayer", "conn-123");
        await roomManager.AddPlayerAsync(roomCode!, player);

        await roomManager.RemovePlayerAsync("conn-123");

        var act = async () => await roomManager.StartGameAsync(roomCode!);
        await act.Should().ThrowAsync<NotEnoughPlayersException>();
    }
}

#endregion

#region Complete Game Flow Tests

public class CompleteGameFlowTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly List<SignalRTestClient> _clients = new();

    public CompleteGameFlowTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var client in _clients)
            await client.DisposeAsync();
        _clients.Clear();
    }

    private SignalRTestClient CreateClient()
    {
        var client = new SignalRTestClient(
            _factory.Server.BaseAddress + "roomhub",
            _factory.Server.CreateHandler());
        _clients.Add(client);
        return client;
    }

    [Fact]
    public async Task CompleteGameRound_WithCorrectGuess_ShouldAwardPoints()
    {
        var player1 = CreateClient();
        var player2 = CreateClient();
        var player3 = CreateClient();
        var clients = new[] { player1, player2, player3 };
        
        foreach (var client in clients)
        {
            client.OnString("CreatedRoom");
            client.OnString("JoinedRoom");
            client.OnString("GameStarted");
            client.OnString("CommanderSelected");
            client.OnString("CommanderAnnounced");
            client.OnString("ReceiveWord");
            client.OnString("ReceiveEmojis");
            client.OnStringInt("CorrectGuess");
            client.OnStringInt("IncorrectGuess");
            client.OnString("RoundEnded");
            await client.StartAsync();
        }
        
        await player1.InvokeAsync("SetUsername", "Alice");
        await player1.InvokeAsync("CreateRoom", "Game Room");
        await player1.WaitForMessageAsync("CreatedRoom");
        
        var roomCode = player1.GetFirstMessage("CreatedRoom")?[0] as string;
        
        await player2.InvokeAsync("SetUsername", "Bob");
        await player2.InvokeAsync("JoinRoom", roomCode);
        await player2.WaitForMessageAsync("JoinedRoom");
        
        await player3.InvokeAsync("SetUsername", "Charlie");
        await player3.InvokeAsync("JoinRoom", roomCode);
        await player3.WaitForMessageAsync("JoinedRoom");

        await player1.InvokeAsync("StartGame");
        await Task.WhenAll(
            player1.WaitForMessageAsync("GameStarted", TimeSpan.FromSeconds(5)),
            player2.WaitForMessageAsync("GameStarted", TimeSpan.FromSeconds(5)),
            player3.WaitForMessageAsync("GameStarted", TimeSpan.FromSeconds(5))
        );
        
        await Task.Delay(1000);
        
        SignalRTestClient? commander = null;
        var nonCommanders = new List<SignalRTestClient>();
        
        foreach (var client in clients)
        {
            if (client.GetMessages("CommanderSelected").Count > 0)
                commander = client;
            else
                nonCommanders.Add(client);
        }
        
        commander.Should().NotBeNull("exactly one player should be commander");
        nonCommanders.Should().HaveCount(2, "two players should be non-commanders");
        
        var wordMessage = commander!.GetFirstMessage("ReceiveWord");
        wordMessage.Should().NotBeNull();
        var word = wordMessage![0] as string;
        word.Should().NotBeNullOrEmpty();
        
        await commander.InvokeAsync("GetAndSendEmojis", "🍎🔴");
        await Task.WhenAll(
            nonCommanders.Select(c => c.WaitForMessageAsync("ReceiveEmojis", TimeSpan.FromSeconds(3)))
        );
        
        foreach (var guesser in nonCommanders)
            await guesser.InvokeAsync("CheckWord", word);
        
        await Task.Delay(2000);
        
        foreach (var guesser in nonCommanders)
        {
            var correctMessages = guesser.GetMessages("CorrectGuess");
            var incorrectMessages = guesser.GetMessages("IncorrectGuess");
            
            (correctMessages.Count + incorrectMessages.Count).Should().BeGreaterThan(0, 
                "each guesser should receive feedback");
            
            if (correctMessages.Count > 0)
            {
                var points = correctMessages[0][1];
                points.Should().Be(100, "correct guess should award 100 points");
            }
        }
    }

    [Fact]
    public async Task CompleteGameRound_WithIncorrectGuess_ShouldNotAwardPoints()
    {
        var player1 = CreateClient();
        var player2 = CreateClient();
        var player3 = CreateClient();
        var clients = new[] { player1, player2, player3 };
        
        foreach (var client in clients)
        {
            client.OnString("CreatedRoom");
            client.OnString("JoinedRoom");
            client.OnString("GameStarted");
            client.OnString("CommanderSelected");
            client.OnString("CommanderAnnounced");
            client.OnString("ReceiveWord");
            client.OnString("ReceiveEmojis");
            client.OnStringInt("CorrectGuess");
            client.OnStringInt("IncorrectGuess");
            await client.StartAsync();
        }
        
        await player1.InvokeAsync("SetUsername", "Alice");
        await player1.InvokeAsync("CreateRoom", "Game Room");
        await player1.WaitForMessageAsync("CreatedRoom");
        
        var roomCode = player1.GetFirstMessage("CreatedRoom")?[0] as string;
        
        await player2.InvokeAsync("SetUsername", "Bob");
        await player2.InvokeAsync("JoinRoom", roomCode);
        await player2.WaitForMessageAsync("JoinedRoom");
        
        await player3.InvokeAsync("SetUsername", "Charlie");
        await player3.InvokeAsync("JoinRoom", roomCode);
        await player3.WaitForMessageAsync("JoinedRoom");

        await player1.InvokeAsync("StartGame");
        await Task.Delay(1000);
        
        SignalRTestClient? commander = null;
        var nonCommanders = new List<SignalRTestClient>();
        
        foreach (var client in clients)
        {
            if (client.GetMessages("CommanderSelected").Count > 0)
                commander = client;
            else
                nonCommanders.Add(client);
        }
        
        await commander!.InvokeAsync("GetAndSendEmojis", "🍎🔴");
        await Task.Delay(500);
        
        foreach (var guesser in nonCommanders)
            await guesser.InvokeAsync("CheckWord", "WrongWord");
        
        await Task.Delay(2000);
        
        foreach (var guesser in nonCommanders)
        {
            var incorrectMessages = guesser.GetMessages("IncorrectGuess");
            incorrectMessages.Should().HaveCountGreaterThan(0, 
                "guesser should receive incorrect guess message");
            
            if (incorrectMessages.Count > 0)
            {
                var points = incorrectMessages[0][1];
                points.Should().Be(0, "incorrect guess should not award points");
            }
        }
    }

    [Fact]
    public async Task CommanderCannotGuess_ShouldReceiveError()
    {
        var player1 = CreateClient();
        var player2 = CreateClient();
        var player3 = CreateClient();
        var clients = new[] { player1, player2, player3 };
        
        foreach (var client in clients)
        {
            client.OnString("CreatedRoom");
            client.OnString("JoinedRoom");
            client.OnString("GameStarted");
            client.OnString("CommanderSelected");
            client.OnString("ReceiveWord");
            client.OnString("ReceiveEmojis");
            client.OnString("Error");
            await client.StartAsync();
        }
        
        await player1.InvokeAsync("SetUsername", "Alice");
        await player1.InvokeAsync("CreateRoom", "Game Room");
        await player1.WaitForMessageAsync("CreatedRoom");
        
        var roomCode = player1.GetFirstMessage("CreatedRoom")?[0] as string;
        
        await player2.InvokeAsync("SetUsername", "Bob");
        await player2.InvokeAsync("JoinRoom", roomCode);
        await player2.WaitForMessageAsync("JoinedRoom");
        
        await player3.InvokeAsync("SetUsername", "Charlie");
        await player3.InvokeAsync("JoinRoom", roomCode);
        await player3.WaitForMessageAsync("JoinedRoom");

        await player1.InvokeAsync("StartGame");
        await Task.Delay(1000);
        
        SignalRTestClient? commander = clients.FirstOrDefault(c => 
            c.GetMessages("CommanderSelected").Count > 0);
        
        commander.Should().NotBeNull();
        
        await commander!.InvokeAsync("GetAndSendEmojis", "🍎");
        await Task.Delay(500);
        
        await commander.InvokeAsync("CheckWord", "Apple");
        await commander.WaitForMessageAsync("Error", TimeSpan.FromSeconds(3));

        var errors = commander.GetMessages("Error");
        errors.Should().HaveCountGreaterThan(0);
        var errorMessage = errors[0][0] as string;
        errorMessage.Should().Contain("cannot guess");
    }
}

#endregion