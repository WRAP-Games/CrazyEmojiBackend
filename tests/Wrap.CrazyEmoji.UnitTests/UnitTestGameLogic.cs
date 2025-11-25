using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Wrap.CrazyEmoji.Api.Abstractions;
using Wrap.CrazyEmoji.Api.Constants;
using Wrap.CrazyEmoji.Api.GameLogic;
using Wrap.CrazyEmoji.Api.GameLogic.Exceptions;
using Wrap.CrazyEmoji.Api.Services;


namespace Wrap.CrazyEmoji.UnitTests;

public class UnitTestGameLogic
{
    
    //points tests
    [Theory]
    [InlineData(-1)]
    [InlineData(-20)]
    public void Constructor_NegativeValue_ShouldThrowArgumentException(int invalidValue)
    {
        Assert.Throws<ArgumentException>(() => new Points(invalidValue));
    }
    
    [Fact]
    public void Constructor_ValidValue_ShouldSetValue()
    {
        int value = 5;
        var points = new Points(value);
        Assert.Equal(value, points.Value);
    }

    [Fact]
    public void IsZero_WhenValueIsZero_ShouldReturnTrue()
    {
        var points = new Points(0);
        bool result = points.IsZero();
        Assert.True(result);
    }
    
    [Fact]
    public void IsZero_WhenValueIsNotZero_ShouldReturnFalse()
    {
        var points = new Points(3);
        bool result = points.IsZero();
        Assert.False(result);
    }
    
    [Fact]
    public void AdditionOperator_ShouldReturnCorrectSum()
    {
        var p1 = new Points(4);
        var p2 = new Points(6);
        var result = p1 + p2;
        Assert.Equal(10, result.Value);
    }
    
    [Fact]
    public void CompareTo_ShouldReturnCorrectComparisons()
    {
        var smaller = new Points(3);
        var larger = new Points(8);
        Assert.True(smaller.CompareTo(larger) < 0);
        Assert.True(larger.CompareTo(smaller) > 0);
        Assert.Equal(0, smaller.CompareTo(new Points(3)));
    }
    
    [Fact]
    public void ToString_ShouldReturnValueAsString()
    { 
        var points = new Points(15);
        var result = points.ToString();
        Assert.Equal("15", result);
    }
    
    
    //player role tests
    
    [Fact]
    public void DefaultRole_ShouldBePlayer()
    {
        var player = new Player();
        Assert.Equal(PlayerRole.Player, player.Role);
    }
    
    [Fact]
    public void CanAssignCommanderRole()
    {
        var player = new Player();
        player.Role = PlayerRole.Commander;
        Assert.Equal(PlayerRole.Commander, player.Role);
    }
    
    [Fact]
    public void PlayerRoleEnum_ShouldHaveExpectedValues()
    {
        var commander = PlayerRole.Commander;
        var playerRole = PlayerRole.Player;
        Assert.Equal(0, (int)commander);
        Assert.Equal(1, (int)playerRole);
    }
    
    
    //player tests
    [Fact]
    public void Constructor_ValidValues_ShouldAssignProperties()
    {
        string username = "sussie";
        string connectionId = "abc123";

        var player = new Player(username, connectionId);

        Assert.Equal(username, player.Username);
        Assert.Equal(connectionId, player.ConnectionId);
        Assert.Equal(0, player.Points.Value);
        Assert.Equal(PlayerRole.Player, player.Role);
        Assert.False(player.HasGuessed);
        Assert.False(player.GuessedRight);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_InvalidUsername_ShouldThrow(string? username)
    {
        string connectionId = "conn-1";
        Assert.Throws<ArgumentException>(() => new Player(username!, connectionId));
    }
    
    
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_InvalidConnectionId_ShouldThrow(string? connectionId)
    {
        string username = "sussie";
        Assert.Throws<ArgumentException>(() => new Player(username, connectionId!));
    }
    
    [Fact]
    public void Username_SetValidValue_ShouldUpdateProperty()
    {
        var player = new Player("sussie", "id");
        player.Username = "Bob";

        Assert.Equal("Bob", player.Username);
    }
    
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Username_SetInvalidValue_ShouldThrow(string? invalidValue)
    {
        var player = new Player("sussie", "id");

        Assert.Throws<ArgumentException>(() => player.Username = invalidValue!);
    }
    
    [Fact]
    public void ConnectionId_SetValidValue_ShouldUpdateProperty()
    {
        var player = new Player("sussie", "id");
        player.ConnectionId = "new-id";
        Assert.Equal("new-id", player.ConnectionId);
    }
    
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void ConnectionId_SetInvalidValue_ShouldThrow(string? invalidValue)
    {
        var player = new Player("sussie", "id");
        Assert.Throws<ArgumentException>(() => player.ConnectionId = invalidValue!);
    }
    
    [Fact]
    public void Points_InitialValue_ShouldBeZero()
    {
        var player = new Player();
        Assert.Equal(0, player.Points.Value);
    }
    
    [Fact]
    public void CanSetRole()
    {
        var player = new Player();
        player.Role = PlayerRole.Commander;
        Assert.Equal(PlayerRole.Commander, player.Role);
    }
    
    [Fact]
    public void GuessFlags_CanBeUpdated()
    {
        var player = new Player();
        player.HasGuessed = true;
        player.GuessedRight = true;
        Assert.True(player.HasGuessed);
        Assert.True(player.GuessedRight);
    }
    
    // roomManager test
    
    private Mock<IHubContext<RoomHub>> CreateMockHubContext(
        out Mock<IGroupManager> mockGroups,
        out Mock<IHubClients> mockClients,
        out Mock<ISingleClientProxy> mockSingleClientProxy,
        out Mock<IClientProxy> mockClientProxy)
    {
        var mockHub = new Mock<IHubContext<RoomHub>>();
        mockGroups = new Mock<IGroupManager>();
        mockClients = new Mock<IHubClients>();
        mockSingleClientProxy = new Mock<ISingleClientProxy>();
        mockClientProxy = new Mock<IClientProxy>();

        mockHub.Setup(x => x.Groups).Returns(mockGroups.Object);
        mockHub.Setup(x => x.Clients).Returns(mockClients.Object);
        mockClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        mockClients.Setup(x => x.Client(It.IsAny<string>())).Returns(mockSingleClientProxy.Object);
        
        return mockHub;
    }

    [Fact]
    public async Task RoomManager_CreateRoomAsync_ShouldReturnUniqueSixCharacterCode()
    {
        var hubContext = CreateMockHubContext(out _, out _, out _, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);

        var roomCode = await manager.CreateRoomAsync("TestRoom");

        Assert.NotNull(roomCode);
        Assert.Equal(6, roomCode!.Length);
        Assert.Matches(@"^[A-Z0-9]{6}$", roomCode);
    }

    [Fact]
    public async Task RoomManager_CreateRoomAsync_CalledMultipleTimes_ShouldReturnDifferentCodes()
    {
        var hubContext = CreateMockHubContext(out _, out _, out _, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);

        var code1 = await manager.CreateRoomAsync("Room1");
        var code2 = await manager.CreateRoomAsync("Room2");
        var code3 = await manager.CreateRoomAsync("Room3");

        Assert.NotEqual(code1, code2);
        Assert.NotEqual(code2, code3);
        Assert.NotEqual(code1, code3);
    }
    
    [Fact]
    public async Task RoomManager_AddPlayerAsync_WithValidRoomCode_ShouldAddPlayerToGroup()
    {
        var hubContext = CreateMockHubContext(out var mockGroups, out _, out _, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        var roomCode = await manager.CreateRoomAsync("Room1");
        var player = new Player("Alice", "conn-alice");

        await manager.AddPlayerAsync(roomCode!, player);

        mockGroups.Verify(g => g.AddToGroupAsync("conn-alice", roomCode!, default), Times.Once);
    }

    [Fact]
    public async Task RoomManager_AddPlayerAsync_WithInvalidRoomCode_ShouldThrowRoomNotFoundException()
    {
        var hubContext = CreateMockHubContext(out _, out _, out _, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        var player = new Player("Bob", "conn-bob");

        var exception = await Assert.ThrowsAsync<RoomNotFoundException>(() =>
            manager.AddPlayerAsync("INVALID", player));

        Assert.Contains("INVALID", exception.Message);
    }
    
    [Fact]
    public async Task RoomManager_AddPlayerAsync_MultiplePlayersToSameRoom_ShouldAddAll()
    {
        var hubContext = CreateMockHubContext(out var mockGroups, out _, out _, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        var roomCode = await manager.CreateRoomAsync("Room1");
        var player1 = new Player("Alice", "conn-1");
        var player2 = new Player("Bob", "conn-2");
        var player3 = new Player("Charlie", "conn-3");

        var result1 = await manager.AddPlayerAsync(roomCode!, player1);
        var result2 = await manager.AddPlayerAsync(roomCode!, player2);
        var result3 = await manager.AddPlayerAsync(roomCode!, player3);

        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);
        mockGroups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), roomCode!, default), Times.Exactly(3));
    }
    
    [Fact]
    public async Task RoomManager_RemovePlayerAsync_ShouldRemovePlayerFromRoom()
    {
        var hubContext = CreateMockHubContext(out _, out _, out _, out var mockClientProxy);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        var roomCode = await manager.CreateRoomAsync("Room1");
        var player = new Player("Alice", "conn-alice");

        await manager.AddPlayerAsync(roomCode!, player);
        await manager.RemovePlayerAsync("conn-alice");

        mockClientProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.PlayerLeft,
            It.Is<object[]>(args => args[0].ToString() == "conn-alice"),
            default), Times.Once);
    }
    
    public async Task RoomManager_RemovePlayerAsync_WithNonExistentPlayer_ShouldNotThrow()
    {
        var hubContext = CreateMockHubContext(out _, out _, out _, out var mockClientProxy);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        await manager.CreateRoomAsync("Room1");

        // Should not throw exception
        await manager.RemovePlayerAsync("non-existent-connection");

        mockClientProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.PlayerLeft,
            It.IsAny<object[]>(),
            default), Times.Never);
    }
    
    [Fact]
    public async Task RoomManager_RemovePlayerAsync_ShouldRemoveFromCorrectRoomOnly()
    {
        var hubContext = CreateMockHubContext(out _, out _, out _, out var mockClientProxy);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        var roomCode1 = await manager.CreateRoomAsync("Room1");
        var roomCode2 = await manager.CreateRoomAsync("Room2");

        var player1 = new Player("Alice", "conn-1");
        var player2 = new Player("Bob", "conn-2");

        await manager.AddPlayerAsync(roomCode1!, player1);
        await manager.AddPlayerAsync(roomCode2!, player2);

        await manager.RemovePlayerAsync("conn-1");

        mockClientProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.PlayerLeft,
            It.Is<object[]>(args => args[0].ToString() == "conn-1"),
            default), Times.Once);
    }
    
    [Fact]
    public async Task RoomManager_StartGameAsync_WithLessThanThreePlayers_ShouldThrowNotEnoughPlayersException()
    {
        var hubContext = CreateMockHubContext(out _, out _, out _, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        var roomCode = await manager.CreateRoomAsync("Room1");

        var player1 = new Player("Alice", "conn-1");
        var player2 = new Player("Bob", "conn-2");

        await manager.AddPlayerAsync(roomCode!, player1);
        await manager.AddPlayerAsync(roomCode!, player2);

        var exception = await Assert.ThrowsAsync<NotEnoughPlayersException>(() =>
            manager.StartGameAsync(roomCode!));

        Assert.Contains(roomCode!, exception.Message);
    }
    
    [Fact]
    public async Task RoomManager_StartGameAsync_WithInvalidRoomCode_ShouldThrowNotEnoughPlayersException()
    {
        var hubContext = CreateMockHubContext(out _, out _, out _, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);

        await Assert.ThrowsAsync<NotEnoughPlayersException>(() =>
            manager.StartGameAsync("INVALID"));
    }


    [Fact]
    public async Task RoomManager_SendEmojisAsync_WithInvalidRoomCode_ShouldThrowRoomNotFoundException()
    {
        var hubContext = CreateMockHubContext(out _, out _, out _, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);

        await Assert.ThrowsAsync<RoomNotFoundException>(() =>
            manager.SendEmojisAsync("INVALID", "conn-1", "😀😃😄"));
    }
    
    [Fact]
    public async Task RoomManager_SendEmojisAsync_WhenNotCommander_ShouldSendError()
    {
        var hubContext = CreateMockHubContext(out _, out _, out var mockSingleClientProxy, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        var roomCode = await manager.CreateRoomAsync("Room1");

        var player = new Player("Alice", "conn-1") { Role = PlayerRole.Player };
        await manager.AddPlayerAsync(roomCode!, player);

        await manager.SendEmojisAsync(roomCode!, "conn-1", "😀😃😄");

        mockSingleClientProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.Error,
            It.Is<object[]>(args => args[0].ToString()!.Contains("commander")),
            default), Times.Once);
    }
    
    
    [Fact]
    public async Task RoomManager_SendEmojisAsync_WithNonExistentPlayer_ShouldSendError()
    {
        var hubContext = CreateMockHubContext(out _, out _, out var mockSingleClientProxy, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        var roomCode = await manager.CreateRoomAsync("Room1");

        var player = new Player("Alice", "conn-1");
        await manager.AddPlayerAsync(roomCode!, player);

        await manager.SendEmojisAsync(roomCode!, "conn-nonexistent", "😀😃😄");

        mockSingleClientProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.Error,
            It.IsAny<object[]>(),
            default), Times.Once);
    }
    
    [Fact]
    public async Task RoomManager_CheckWordAsync_WithInvalidRoomCode_ShouldThrowRoomNotFoundException()
    {
        var hubContext = CreateMockHubContext(out _, out _, out _, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        
        await Assert.ThrowsAsync<RoomNotFoundException>(() =>
            manager.CheckWordAsync("INVALID", "conn-1", "apple"));
    }

    [Fact]
    public async Task RoomManager_CheckWordAsync_BeforeEmojisAreSent_ShouldSendError()
    {
        var hubContext = CreateMockHubContext(out _, out _, out var mockSingleClientProxy, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        var roomCode = await manager.CreateRoomAsync("Room1");

        var player = new Player("Alice", "conn-1");
        await manager.AddPlayerAsync(roomCode!, player);

        await manager.CheckWordAsync(roomCode!, "conn-1", "apple");

        mockSingleClientProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.Error,
            It.Is<object[]>(args => args[0].ToString()!.Contains("emojis")),
            default), Times.Once);
    }
    
    [Fact]
    public async Task RoomManager_CheckWordAsync_WithCorrectWord_ShouldMarkPlayerAsCorrect()
    {
        var hubContext = CreateMockHubContext(out _, out _, out _, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        var roomCode = await manager.CreateRoomAsync("Room1");

        var player = new Player("Alice", "conn-1");
        await manager.AddPlayerAsync(roomCode!, player);

        var emojisSent = new ConcurrentDictionary<string, bool> { [roomCode!] = true };
        var currentWords = new ConcurrentDictionary<string, string> { [roomCode!] = "apple" };
        
        manager.GetType()
            .GetField("_emojisSent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(manager, emojisSent);
        manager.GetType()
            .GetField("_currentWords", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(manager, currentWords);

        await manager.CheckWordAsync(roomCode!, "conn-1", "apple");

        Assert.True(player.HasGuessed);
        Assert.True(player.GuessedRight);
    }
    
    [Fact]
    public async Task RoomManager_CheckWordAsync_WithIncorrectWord_ShouldMarkPlayerAsWrong()
    {
        var hubContext = CreateMockHubContext(out _, out _, out _, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        var roomCode = await manager.CreateRoomAsync("Room1");

        var player = new Player("Alice", "conn-1");
        await manager.AddPlayerAsync(roomCode!, player);

        var emojisSent = new ConcurrentDictionary<string, bool> { [roomCode!] = true };
        var currentWords = new ConcurrentDictionary<string, string> { [roomCode!] = "apple" };
        
        manager.GetType()
            .GetField("_emojisSent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(manager, emojisSent);
        manager.GetType()
            .GetField("_currentWords", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(manager, currentWords);

        await manager.CheckWordAsync(roomCode!, "conn-1", "banana");

        Assert.True(player.HasGuessed);
        Assert.False(player.GuessedRight);
    }
    
    [Fact]
    public async Task RoomManager_CheckWordAsync_CaseInsensitive_ShouldMarkAsCorrect()
    {
        var hubContext = CreateMockHubContext(out _, out _, out _, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        var roomCode = await manager.CreateRoomAsync("Room1");

        var player = new Player("Alice", "conn-1");
        await manager.AddPlayerAsync(roomCode!, player);

        var emojisSent = new ConcurrentDictionary<string, bool> { [roomCode!] = true };
        var currentWords = new ConcurrentDictionary<string, string> { [roomCode!] = "apple" };
        
        manager.GetType()
            .GetField("_emojisSent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(manager, emojisSent);
        manager.GetType()
            .GetField("_currentWords", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(manager, currentWords);

        await manager.CheckWordAsync(roomCode!, "conn-1", "APPLE");

        Assert.True(player.GuessedRight);
    }
    
    [Fact]
    public async Task RoomManager_CheckWordAsync_WhenPlayerAlreadyGuessed_ShouldSendError()
    {
        var hubContext = CreateMockHubContext(out _, out _, out var mockSingleClientProxy, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        var roomCode = await manager.CreateRoomAsync("Room1");

        var player = new Player("Alice", "conn-1") { HasGuessed = true };
        await manager.AddPlayerAsync(roomCode!, player);

        var emojisSent = new ConcurrentDictionary<string, bool> { [roomCode!] = true };
        var currentWords = new ConcurrentDictionary<string, string> { [roomCode!] = "apple" };
        
        manager.GetType()
            .GetField("_emojisSent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(manager, emojisSent);
        manager.GetType()
            .GetField("_currentWords", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(manager, currentWords);

        await manager.CheckWordAsync(roomCode!, "conn-1", "apple");

        mockSingleClientProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.Error,
            It.Is<object[]>(args => args[0].ToString()!.Contains("already guessed")),
            default), Times.Once);
    }
    
    [Fact]
    public async Task RoomManager_CheckWordAsync_AsCommander_ShouldSendError()
    {
        var hubContext = CreateMockHubContext(out _, out _, out var mockSingleClientProxy, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        var roomCode = await manager.CreateRoomAsync("Room1");

        var commander = new Player("Alice", "conn-1") { Role = PlayerRole.Commander };
        await manager.AddPlayerAsync(roomCode!, commander);

        var emojisSent = new ConcurrentDictionary<string, bool> { [roomCode!] = true };
        var currentWords = new ConcurrentDictionary<string, string> { [roomCode!] = "apple" };
        
        manager.GetType()
            .GetField("_emojisSent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(manager, emojisSent);
        manager.GetType()
            .GetField("_currentWords", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(manager, currentWords);

        await manager.CheckWordAsync(roomCode!, "conn-1", "apple");

        mockSingleClientProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.Error,
            It.Is<object[]>(args => args[0].ToString()!.Contains("Commanders cannot guess")),
            default), Times.Once);
    }
    
    [Fact]
    public async Task RoomManager_CheckWordAsync_WithNonExistentPlayer_ShouldSendError()
    {
        var hubContext = CreateMockHubContext(out _, out _, out var mockSingleClientProxy, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        var roomCode = await manager.CreateRoomAsync("Room1");

        var player = new Player("Alice", "conn-1");
        await manager.AddPlayerAsync(roomCode!, player);

        var emojisSent = new ConcurrentDictionary<string, bool> { [roomCode!] = true };
        var currentWords = new ConcurrentDictionary<string, string> { [roomCode!] = "apple" };
        
        manager.GetType()
            .GetField("_emojisSent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(manager, emojisSent);
        manager.GetType()
            .GetField("_currentWords", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(manager, currentWords);

        await manager.CheckWordAsync(roomCode!, "conn-nonexistent", "apple");

        mockSingleClientProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.Error,
            It.Is<object[]>(args => args[0].ToString()!.Contains("not found")),
            default), Times.Once);
    }
    
    [Fact]
    public async Task RoomManager_CheckWordAsync_WhenNoWordIsSet_ShouldSendError()
    {
        var hubContext = CreateMockHubContext(out _, out _, out var mockSingleClientProxy, out _);
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        var roomCode = await manager.CreateRoomAsync("Room1");

        var player = new Player("Alice", "conn-1");
        await manager.AddPlayerAsync(roomCode!, player);

        var emojisSent = new ConcurrentDictionary<string, bool> { [roomCode!] = true };
        
        manager.GetType()
            .GetField("_emojisSent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(manager, emojisSent);

        await manager.CheckWordAsync(roomCode!, "conn-1", "apple");

        mockSingleClientProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.Error,
            It.Is<object[]>(args => args[0].ToString()!.Contains("No word set")),
            default), Times.Once);
    }
    
    
    
    [Fact]
    public async Task RoomManager_CreateRoomAsync_ShouldReturnValidRoomCode()
    {
        var hubContext = new Mock<IHubContext<RoomHub>>();
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();

        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);

        var code = await manager.CreateRoomAsync("testRoom");

        Assert.NotNull(code);
        Assert.Equal(6, code!.Length);
    }

    [Fact]
    public async Task RoomManager_AddPlayerAsync_ShouldAddPlayerToRoom()
    {
        var hubContext = new Mock<IHubContext<RoomHub>>();
        var groups = new Mock<IGroupManager>();
        hubContext.Setup(x => x.Groups).Returns(groups.Object);

        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();

        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);

        var roomCode = await manager.CreateRoomAsync("Room1");
        var player = new Player("Alice", "C1");

        var added = await manager.AddPlayerAsync(roomCode!, player);

        Assert.True(added);
        groups.Verify(g => g.AddToGroupAsync("C1", roomCode!, default), Times.Once);
    }
    
    [Fact]
    public async Task RoomManager_AddPlayerAsync_ShouldThrow_WhenRoomNotFound()
    {
        var hubContext = new Mock<IHubContext<RoomHub>>();
        var wordService = new Mock<IWordService>();
        var logger = new LoggerFactory().CreateLogger<RoomManager>();

        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);

        var player = new Player("Bob", "C22");

        await Assert.ThrowsAsync<RoomNotFoundException>(() =>
            manager.AddPlayerAsync("INVALID", player));
    }
    
    //RoomHub tests
    
    
    
    

    //WordService tests
    [Fact]
    public async Task WordService_LoadWordsAsync_LoadsWordsCorrectly()
    {
        var service = new WordService();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("apple\nbanana\napple"));
        await service.LoadWordsAsync(stream);

        Assert.Equal(2, service.Count());
    }

    [Fact]
    public async Task WordService_GetRandomWordAsync_Throws_WhenEmpty()
    {
        var service = new WordService();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetRandomWordAsync());
    }

    [Fact]
    public async Task WordService_GetRandomWordAsync_ReturnsWord_WhenLoaded()
    {
        var service = new WordService();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("apple\nbanana"));
        await service.LoadWordsAsync(stream);

        var word = await service.GetRandomWordAsync();
        Assert.Contains(word, new[] { "apple", "banana" });
    }

    


}