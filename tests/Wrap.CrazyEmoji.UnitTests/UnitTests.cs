using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Wrap.CrazyEmoji.Api.Abstractions;
using Wrap.CrazyEmoji.Api.Constants;
using Wrap.CrazyEmoji.Api.GameLogic;
using Wrap.CrazyEmoji.Api.GameLogic.Exceptions;
using Wrap.CrazyEmoji.Api.Infrastructure;
using Wrap.CrazyEmoji.Api.Services;
using Xunit;


namespace Wrap.CrazyEmoji.UnitTests;

public class UnitTests
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

    // RoomManager tests

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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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

    [Fact]
    public async Task RoomManager_RemovePlayerAsync_WithNonExistentPlayer_ShouldNotThrow()
    {
        var hubContext = CreateMockHubContext(out _, out _, out _, out var mockClientProxy);
        var wordService = new Mock<IWordService>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);
        await manager.CreateRoomAsync("Room1");

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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);

        await Assert.ThrowsAsync<NotEnoughPlayersException>(() =>
            manager.StartGameAsync("INVALID"));
    }


    [Fact]
    public async Task RoomManager_SendEmojisAsync_WithInvalidRoomCode_ShouldThrowRoomNotFoundException()
    {
        var hubContext = CreateMockHubContext(out _, out _, out _, out _);
        var wordService = new Mock<IWordService>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);

        await Assert.ThrowsAsync<RoomNotFoundException>(() =>
            manager.SendEmojisAsync("INVALID", "conn-1", "😀😃😄"));
    }

    [Fact]
    public async Task RoomManager_SendEmojisAsync_WhenNotCommander_ShouldSendError()
    {
        var hubContext = CreateMockHubContext(out _, out _, out var mockSingleClientProxy, out _);
        var wordService = new Mock<IWordService>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
        var manager = new RoomManager(hubContext.Object, wordService.Object, logger);

        await Assert.ThrowsAsync<RoomNotFoundException>(() =>
            manager.CheckWordAsync("INVALID", "conn-1", "apple"));
    }

    [Fact]
    public async Task RoomManager_CheckWordAsync_BeforeEmojisAreSent_ShouldSendError()
    {
        var hubContext = CreateMockHubContext(out _, out _, out var mockSingleClientProxy, out _);
        var wordService = new Mock<IWordService>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomManager>.Instance;
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


    //RoomHub tests

    private RoomHub CreateRoomHub(
        out Mock<RoomManager> mockRoomManager,
        out Mock<HubCallerContext> mockContext,
        out Mock<IHubCallerClients> mockClients,
        out Mock<ISingleClientProxy> mockCallerProxy,
        out Mock<IClientProxy> mockGroupProxy,
        out Mock<IGroupManager> mockGroups,
        out Dictionary<object, object?> contextItems)
    {
        mockRoomManager = new Mock<RoomManager>(
            Mock.Of<IHubContext<RoomHub>>(),
            Mock.Of<IWordService>(),
            Mock.Of<ILogger<RoomManager>>())
        {
            CallBase = false
        };

        mockContext = new Mock<HubCallerContext>();
        mockClients = new Mock<IHubCallerClients>();
        mockCallerProxy = new Mock<ISingleClientProxy>();
        mockGroupProxy = new Mock<IClientProxy>();
        mockGroups = new Mock<IGroupManager>();
        contextItems = new Dictionary<object, object?>();

        mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id");
        mockContext.Setup(c => c.Items).Returns(contextItems);

        mockClients.Setup(c => c.Caller).Returns(mockCallerProxy.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockGroupProxy.Object);

        var hub = new RoomHub(mockRoomManager.Object)
        {
            Context = mockContext.Object,
            Clients = mockClients.Object,
            Groups = mockGroups.Object
        };

        return hub;
    }



    [Fact]
    public async Task RoomHub_SetUsername_WithValidUsername_ShouldSetInContextAndNotifyCaller()
    {
        var hub = CreateRoomHub(out _, out _, out _, out var mockCallerProxy, out _, out _, out var contextItems);
        var username = "TestUser";

        await hub.SetUsername(username);

        Assert.Equal(username, contextItems[RoomHubConstants.Username]);
        mockCallerProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.UsernameSet,
            It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == username),
            default), Times.Once);
    }

    [Fact]
    public async Task RoomHub_CreateRoom_WithNullRoomName_ShouldThrowArgumentNullException()
    {
        var hub = CreateRoomHub(out _, out _, out _, out _, out _, out _, out _);

        await Assert.ThrowsAsync<ArgumentNullException>(() => hub.CreateRoom(null!));
    }

    [Fact]
    public async Task RoomHub_CreateRoom_WithEmptyRoomName_ShouldThrowArgumentException()
    {
        var hub = CreateRoomHub(out _, out _, out _, out _, out _, out _, out _);

        await Assert.ThrowsAsync<ArgumentException>(() => hub.CreateRoom(""));
    }

    [Fact]
    public async Task RoomHub_CreateRoom_WithWhitespaceRoomName_ShouldThrowArgumentException()
    {
        var hub = CreateRoomHub(out _, out _, out _, out _, out _, out _, out _);

        await Assert.ThrowsAsync<ArgumentException>(() => hub.CreateRoom("   "));
    }

    [Fact]
    public async Task RoomHub_SetUsername_CalledMultipleTimes_ShouldUpdateUsername()
    {
        var hub = CreateRoomHub(out _, out _, out _, out var mockCallerProxy, out _, out _, out var contextItems);

        await hub.SetUsername("FirstName");
        await hub.SetUsername("SecondName");

        Assert.Equal("SecondName", contextItems[RoomHubConstants.Username]);
        mockCallerProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.UsernameSet,
            It.IsAny<object[]>(),
            default), Times.Exactly(2));
    }

    [Fact]
    public async Task RoomHub_CreateRoom_WithValidRoomName_ShouldCreateRoomAndJoin()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out var mockCallerProxy,
            out _, out var mockGroups, out var contextItems);
        var roomName = "TestRoom";
        var roomCode = "ABC123";

        mockRoomManager.Setup(m => m.CreateRoomAsync(roomName))
            .ReturnsAsync(roomCode);
        mockRoomManager.Setup(m => m.AddPlayerAsync(roomCode, It.IsAny<Player>()))
            .ReturnsAsync(true);

        await hub.CreateRoom(roomName);

        mockRoomManager.Verify(m => m.CreateRoomAsync(roomName), Times.Once);
        mockGroups.Verify(g => g.AddToGroupAsync("test-connection-id", roomCode, default), Times.Once);
        Assert.Equal(roomCode, contextItems[RoomHubConstants.RoomCode]);
        mockCallerProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.CreatedRoom,
            It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == roomCode),
            default), Times.Once);
        mockRoomManager.Verify(m => m.AddPlayerAsync(roomCode, It.IsAny<Player>()), Times.Once);
    }

    [Fact]
    public async Task RoomHub_CreateRoom_WhenCreationFails_ShouldSendError()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out var mockCallerProxy,
            out _, out _, out _);
        var roomName = "TestRoom";

        mockRoomManager.Setup(m => m.CreateRoomAsync(roomName))
            .ReturnsAsync((string?)null);

        await hub.CreateRoom(roomName);

        mockCallerProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.Error,
            It.Is<object[]>(args => args.Length == 1 && args[0].ToString()!.Contains("Failed to create room")),
            default), Times.Once);
    }


    [Fact]
    public async Task RoomHub_JoinRoom_WithValidRoomCode_ShouldJoinRoom()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out var mockCallerProxy,
            out _, out _, out var contextItems);
        var roomCode = "ABC123";
        var username = "TestUser";
        contextItems[RoomHubConstants.Username] = username;

        mockRoomManager.Setup(m => m.AddPlayerAsync(roomCode, It.IsAny<Player>()))
            .ReturnsAsync(true);

        await hub.JoinRoom(roomCode);

        mockRoomManager.Verify(m => m.AddPlayerAsync(roomCode,
            It.Is<Player>(p => p.ConnectionId == "test-connection-id"
                               && p.Username == username
                               && p.Role == PlayerRole.Player)), Times.Once);
        Assert.Equal(roomCode, contextItems[RoomHubConstants.RoomCode]);
        mockCallerProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.JoinedRoom,
            It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == roomCode),
            default), Times.Once);
    }

    [Fact]
    public async Task RoomHub_JoinRoom_WithoutUsername_ShouldUseConnectionIdAsUsername()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out _, out _, out _, out _);
        var roomCode = "ABC123";

        mockRoomManager.Setup(m => m.AddPlayerAsync(roomCode, It.IsAny<Player>()))
            .ReturnsAsync(true);

        await hub.JoinRoom(roomCode);

        mockRoomManager.Verify(m => m.AddPlayerAsync(roomCode,
            It.Is<Player>(p => p.Username == "test-connection-id")), Times.Once);
    }

    [Fact]
    public async Task RoomHub_JoinRoom_WhenRoomNotFound_ShouldSendError()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out var mockCallerProxy,
            out _, out _, out var contextItems);
        var roomCode = "INVALID";

        mockRoomManager.Setup(m => m.AddPlayerAsync(roomCode, It.IsAny<Player>()))
            .ReturnsAsync(false);

        await hub.JoinRoom(roomCode);

        mockCallerProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.Error,
            It.Is<object[]>(args => args.Length == 1 && args[0].ToString()!.Contains("Room not found")),
            default), Times.Once);
        Assert.False(contextItems.ContainsKey(RoomHubConstants.RoomCode));
    }

    [Fact]
    public async Task RoomHub_JoinRoom_WithNullRoomCode_ShouldThrowArgumentNullException()
    {
        var hub = CreateRoomHub(out _, out _, out _, out _, out _, out _, out var contextItems);
        contextItems[RoomHubConstants.Username] = "TestUser";

        await Assert.ThrowsAsync<ArgumentNullException>(() => hub.JoinRoom(null!));
    }

    [Fact]
    public async Task RoomHub_JoinRoom_WithEmptyRoomCode_ShouldThrowArgumentException()
    {
        var hub = CreateRoomHub(out _, out _, out _, out _, out _, out _, out var contextItems);
        contextItems[RoomHubConstants.Username] = "TestUser";

        await Assert.ThrowsAsync<ArgumentException>(() => hub.JoinRoom(""));
    }

    [Fact]
    public async Task RoomHub_JoinRoom_WithWhitespaceRoomCode_ShouldThrowArgumentException()
    {
        var hub = CreateRoomHub(out _, out _, out _, out _, out _, out _, out var contextItems);
        contextItems[RoomHubConstants.Username] = "TestUser";

        await Assert.ThrowsAsync<ArgumentException>(() => hub.JoinRoom("   "));
    }

    [Fact]
    public async Task RoomHub_JoinRoom_WhenAlreadyInRoom_ShouldUpdateRoomCode()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out _,
            out _, out _, out var contextItems);
        var firstRoomCode = "ROOM01";
        var secondRoomCode = "ROOM02";

        contextItems[RoomHubConstants.RoomCode] = firstRoomCode;

        mockRoomManager.Setup(m => m.AddPlayerAsync(It.IsAny<string>(), It.IsAny<Player>()))
            .ReturnsAsync(true);

        await hub.JoinRoom(secondRoomCode);

        Assert.Equal(secondRoomCode, contextItems[RoomHubConstants.RoomCode]);
    }

    [Fact]
    public async Task RoomHub_StartGame_WhenInRoom_ShouldStartGameAndNotifyGroup()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out _,
            out var mockGroupProxy, out _, out var contextItems);
        var roomCode = "ABC123";
        contextItems[RoomHubConstants.RoomCode] = roomCode;

        mockRoomManager.Setup(m => m.StartGameAsync(roomCode))
            .ReturnsAsync(true);

        await hub.StartGame();

        mockRoomManager.Verify(m => m.StartGameAsync(roomCode), Times.Once);
        mockGroupProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.GameStarted,
            It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == roomCode),
            default), Times.Once);
    }

    [Fact]
    public async Task RoomHub_StartGame_WhenNotInRoom_ShouldSendError()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out var mockCallerProxy,
            out _, out _, out _);

        await hub.StartGame();

        mockCallerProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.Error,
            It.Is<object[]>(args => args.Length == 1 && args[0].ToString()!.Contains("not in a room")),
            default), Times.Once);
        mockRoomManager.Verify(m => m.StartGameAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RoomHub_StartGame_WhenStartFails_ShouldNotNotifyGroup()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out _,
            out var mockGroupProxy, out _, out var contextItems);
        var roomCode = "ABC123";
        contextItems[RoomHubConstants.RoomCode] = roomCode;

        mockRoomManager.Setup(m => m.StartGameAsync(roomCode))
            .ReturnsAsync(false);

        await hub.StartGame();

        mockGroupProxy.Verify(c => c.SendCoreAsync(
            RoomHubConstants.GameStarted,
            It.IsAny<object[]>(),
            default), Times.Never);
    }

    [Fact]
    public async Task RoomHub_GetAndSendEmojis_WhenInRoom_ShouldSendEmojisToRoomManager()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out _,
            out _, out _, out var contextItems);
        var roomCode = "ABC123";
        var emojis = "😀😃😄";
        contextItems[RoomHubConstants.RoomCode] = roomCode;

        await hub.GetAndSendEmojis(emojis);

        mockRoomManager.Verify(m => m.SendEmojisAsync(roomCode, "test-connection-id", emojis), Times.Once);
    }

    [Fact]
    public async Task RoomHub_GetAndSendEmojis_WhenNotInRoom_ShouldNotCallRoomManager()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out _, out _, out _, out _);
        var emojis = "😀😃😄";

        await hub.GetAndSendEmojis(emojis);

        mockRoomManager.Verify(m => m.SendEmojisAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RoomHub_GetAndSendEmojis_WithEmptyEmojis_ShouldStillCallRoomManager()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out _,
            out _, out _, out var contextItems);
        var roomCode = "ABC123";
        contextItems[RoomHubConstants.RoomCode] = roomCode;

        await hub.GetAndSendEmojis("");

        mockRoomManager.Verify(m => m.SendEmojisAsync(roomCode, "test-connection-id", ""), Times.Once);
    }


    [Fact]
    public async Task RoomHub_CheckWord_WhenInRoom_ShouldCheckWordWithRoomManager()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out _,
            out _, out _, out var contextItems);
        var roomCode = "ABC123";
        var word = "apple";
        contextItems[RoomHubConstants.RoomCode] = roomCode;

        await hub.CheckWord(word);

        mockRoomManager.Verify(m => m.CheckWordAsync(roomCode, "test-connection-id", word), Times.Once);
    }

    [Fact]
    public async Task RoomHub_CheckWord_WhenNotInRoom_ShouldNotCallRoomManager()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out _, out _, out _, out _);
        var word = "apple";

        await hub.CheckWord(word);

        mockRoomManager.Verify(m => m.CheckWordAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RoomHub_CheckWord_WithEmptyWord_ShouldStillCallRoomManager()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out _,
            out _, out _, out var contextItems);
        var roomCode = "ABC123";
        contextItems[RoomHubConstants.RoomCode] = roomCode;

        await hub.CheckWord("");

        mockRoomManager.Verify(m => m.CheckWordAsync(roomCode, "test-connection-id", ""), Times.Once);
    }

    [Fact]
    public async Task RoomHub_OnDisconnectedAsync_ShouldRemovePlayerFromRoomManager()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out _, out _, out _, out _);

        await hub.OnDisconnectedAsync(null);

        mockRoomManager.Verify(m => m.RemovePlayerAsync("test-connection-id"), Times.Once);
    }

    [Fact]
    public async Task RoomHub_OnDisconnectedAsync_WithException_ShouldStillRemovePlayer()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out _, out _, out _, out _);
        var exception = new Exception("Test exception");

        await hub.OnDisconnectedAsync(exception);

        mockRoomManager.Verify(m => m.RemovePlayerAsync("test-connection-id"), Times.Once);
    }

    [Fact]
    public async Task RoomHub_OnDisconnectedAsync_ShouldCallBaseMethod()
    {
        var hub = CreateRoomHub(out var mockRoomManager, out _, out _, out _, out _, out _, out _);

        var exception = await Record.ExceptionAsync(() => hub.OnDisconnectedAsync(null));

        Assert.Null(exception);
        mockRoomManager.Verify(m => m.RemovePlayerAsync("test-connection-id"), Times.Once);
    }

    // GameCache tests

    private class TestCacheItem
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    [Fact]
    public void GameCache_Add_ShouldStoreItem()
    {
        var cache = new GameCache<TestCacheItem>();
        var item = new TestCacheItem { Name = "Test", Value = 42 };

        cache.Add("key1", item);

        var retrieved = cache.Get("key1");
        Assert.NotNull(retrieved);
        Assert.Equal("Test", retrieved.Name);
        Assert.Equal(42, retrieved.Value);
    }

    [Fact]
    public void GameCache_Get_WithNonExistentKey_ShouldReturnNull()
    {
        var cache = new GameCache<TestCacheItem>();

        var result = cache.Get("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public void GameCache_Add_WithSameKey_ShouldOverwriteValue()
    {
        var cache = new GameCache<TestCacheItem>();
        var item1 = new TestCacheItem { Name = "First", Value = 1 };
        var item2 = new TestCacheItem { Name = "Second", Value = 2 };

        cache.Add("key1", item1);
        cache.Add("key1", item2);

        var retrieved = cache.Get("key1");
        Assert.NotNull(retrieved);
        Assert.Equal("Second", retrieved.Name);
        Assert.Equal(2, retrieved.Value);
    }

    [Fact]
    public void GameCache_Add_MultipleDifferentKeys_ShouldStoreAll()
    {
        var cache = new GameCache<TestCacheItem>();
        var item1 = new TestCacheItem { Name = "First", Value = 1 };
        var item2 = new TestCacheItem { Name = "Second", Value = 2 };
        var item3 = new TestCacheItem { Name = "Third", Value = 3 };

        cache.Add("key1", item1);
        cache.Add("key2", item2);
        cache.Add("key3", item3);

        Assert.Equal("First", cache.Get("key1")!.Name);
        Assert.Equal("Second", cache.Get("key2")!.Name);
        Assert.Equal("Third", cache.Get("key3")!.Name);
    }

    [Fact]
    public void GameCache_GetBest_WithIntegers_ShouldReturnMaximum()
    {
        var cache = new GameCache<TestCacheItem>();
        var numbers = new[] { 5, 12, 3, 8, 15, 1 };

        var result = cache.GetBest(numbers);

        Assert.Equal(15, result);
    }

    [Fact]
    public void GameCache_GetBest_WithStrings_ShouldReturnLastAlphabetically()
    {
        var cache = new GameCache<TestCacheItem>();
        var strings = new[] { "apple", "banana", "zebra", "cherry" };

        var result = cache.GetBest(strings);

        Assert.Equal("zebra", result);
    }

    [Fact]
    public void GameCache_GetBest_WithSingleItem_ShouldReturnThatItem()
    {
        var cache = new GameCache<TestCacheItem>();
        var numbers = new[] { 42 };

        var result = cache.GetBest(numbers);

        Assert.Equal(42, result);
    }

    [Fact]
    public void GameCache_GetBest_WithPoints_ShouldReturnMaximum()
    {
        var cache = new GameCache<TestCacheItem>();
        var points = new[] { new Points(10), new Points(25), new Points(5), new Points(20) };

        var result = cache.GetBest(points);

        Assert.Equal(25, result.Value);
    }

    [Fact]
    public void GameCache_GetBest_WithNegativeNumbers_ShouldReturnLargest()
    {
        var cache = new GameCache<TestCacheItem>();
        var numbers = new[] { -5, -12, -3, -8 };

        var result = cache.GetBest(numbers);

        Assert.Equal(-3, result);
    }


    [Fact]
    public void GameCache_StoreValue_ShouldCreateWrapper()
    {
        var cache = new GameCache<TestCacheItem>();

        cache.StoreValue("key1", 42);

        var result = cache.Get("key1");
        Assert.NotNull(result);
    }

    [Fact]
    public void GameCache_StoreValue_WithDifferentValueTypes_ShouldStoreAll()
    {
        var cache = new GameCache<TestCacheItem>();

        cache.StoreValue("int", 42);
        cache.StoreValue("double", 3.14);
        cache.StoreValue("bool", true);

        Assert.NotNull(cache.Get("int"));
        Assert.NotNull(cache.Get("double"));
        Assert.NotNull(cache.Get("bool"));
    }

    [Fact]
    public void GameCache_Add_WithNullKey_ShouldThrowArgumentNullException()
    {
        var cache = new GameCache<TestCacheItem>();
        var item = new TestCacheItem();

        Assert.Throws<ArgumentNullException>(() => cache.Add(null!, item));
    }

    [Fact]
    public void GameCache_Get_WithNullKey_ShouldThrowArgumentNullException()
    {
        var cache = new GameCache<TestCacheItem>();

        Assert.Throws<ArgumentNullException>(() => cache.Get(null!));
    }

    [Fact]
    public void GameCache_Add_WithEmptyKey_ShouldStoreItem()
    {
        var cache = new GameCache<TestCacheItem>();
        var item = new TestCacheItem { Name = "Empty Key Test" };

        cache.Add("", item);

        var retrieved = cache.Get("");
        Assert.NotNull(retrieved);
        Assert.Equal("Empty Key Test", retrieved.Name);
    }


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


    // GlobalExceptionHandler Tests

    [Fact]
    public async Task GlobalExceptionHandler_TryHandleAsync_ShouldReturnTrue()
    {
        var mockLogger = new Mock<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(mockLogger.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var exception = new Exception("Test exception");

        var result = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task GlobalExceptionHandler_TryHandleAsync_ShouldSetStatusCodeTo500()
    {
        var mockLogger = new Mock<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(mockLogger.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var exception = new Exception("Test exception");

        await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        Assert.Equal(500, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task GlobalExceptionHandler_TryHandleAsync_ShouldWriteJsonResponse()
    {
        var mockLogger = new Mock<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(mockLogger.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var exception = new Exception("Test exception");

        await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var responseText = await reader.ReadToEndAsync();

        Assert.Contains("\"status\":500", responseText);
        Assert.Contains("An unexpected error occurred", responseText);

    }

    [Fact]
    public async Task GlobalExceptionHandler_TryHandleAsync_ShouldLogError()
    {
        var mockLogger = new Mock<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(mockLogger.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var exception = new Exception("Test exception message");

        await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unhandled exception caught by global handler")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GlobalExceptionHandler_TryHandleAsync_ShouldWriteProblemDetailsToResponse()
    {
        var mockLogger = new Mock<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(mockLogger.Object);
        var httpContext = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;
        var exception = new Exception("Test exception");

        await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        responseBody.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseBody);
        var responseText = await reader.ReadToEndAsync();

        Assert.Contains("\"status\":500", responseText);
        Assert.Contains("\"title\":\"An unexpected error occurred.\"", responseText);
        Assert.Contains("\"type\":\"https://datatracker.ietf.org/doc/html/rfc9110#name-500-internal-server-error\"", responseText);
    }

    [Fact]
    public async Task GlobalExceptionHandler_TryHandleAsync_WithDifferentExceptionTypes_ShouldHandleAll()
    {
        var mockLogger = new Mock<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(mockLogger.Object);

        var exceptions = new Exception[]
        {
            new InvalidOperationException("Invalid operation"),
            new ArgumentException("Bad argument"),
            new NullReferenceException("Null reference"),
            new Exception("Generic exception")
        };

        foreach (var exception in exceptions)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();

            var result = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

            Assert.True(result);
            Assert.Equal(500, httpContext.Response.StatusCode);
        }
    }

    [Fact]
    public async Task GlobalExceptionHandler_TryHandleAsync_WithCancellationToken_ShouldPassTokenToWriteAsync()
    {
        var mockLogger = new Mock<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(mockLogger.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var exception = new Exception("Test exception");
        using var cts = new CancellationTokenSource();

        var result = await handler.TryHandleAsync(httpContext, exception, cts.Token);

        Assert.True(result);
        Assert.Equal(500, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task GlobalExceptionHandler_TryHandleAsync_ShouldIncludeCorrectProblemDetailsProperties()
    {
        var mockLogger = new Mock<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(mockLogger.Object);
        var httpContext = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;
        var exception = new Exception("Test exception");

        await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        responseBody.Seek(0, SeekOrigin.Begin);
        using var jsonDoc = await JsonDocument.ParseAsync(responseBody);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("status", out var status));
        Assert.Equal(500, status.GetInt32());

        Assert.True(root.TryGetProperty("title", out var title));
        Assert.Equal("An unexpected error occurred.", title.GetString());

        Assert.True(root.TryGetProperty("type", out var type));
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc9110#name-500-internal-server-error", type.GetString());
    }

    [Fact]
    public async Task GlobalExceptionHandler_TryHandleAsync_WithExceptionContainingInnerException_ShouldLogBoth()
    {
        var mockLogger = new Mock<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(mockLogger.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var innerException = new InvalidOperationException("Inner exception");
        var exception = new Exception("Outer exception", innerException);

        await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}