using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Wrap.CrazyEmoji.Api.Abstractions;
using Wrap.CrazyEmoji.Api.Constants;
using Wrap.CrazyEmoji.Api.Data;
using Wrap.CrazyEmoji.Api.Data.Entities;
using Wrap.CrazyEmoji.Api.GameLogic;
using Wrap.CrazyEmoji.Api.GameLogic.Exceptions;
using Wrap.CrazyEmoji.Api.Infrastructure;

namespace Wrap.CrazyEmoji.UnitTests;

public class TestDbContextFactory : IDbContextFactory<GameDbContext>
{
    private readonly DbContextOptions<GameDbContext> _options;

    public TestDbContextFactory(DbContextOptions<GameDbContext> options)
    {
        _options = options;
    }

    public GameDbContext CreateDbContext()
    {
        return new GameDbContext(_options);
    }

    public Task<GameDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GameDbContext(_options));
    }
}

public class RoomManagerTests : IDisposable
{
    private readonly DbContextOptions<GameDbContext> _dbOptions;
    private readonly IDbContextFactory<GameDbContext> _dbFactory;
    private readonly Mock<IHubContext<RoomHub>> _mockHubContext;
    private readonly Mock<ILogger<RoomManager>> _mockLogger;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IDbWordService> _mockWordService;
    private readonly RoomManager _roomManager;

    public RoomManagerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var factory = new TestDbContextFactory(_dbOptions);
        _dbFactory = factory;

        _mockHubContext = new Mock<IHubContext<RoomHub>>();
        _mockLogger = new Mock<ILogger<RoomManager>>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockWordService = new Mock<IDbWordService>();

        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        
        mockServiceProvider
            .Setup(x => x.GetService(typeof(IDbWordService)))
            .Returns(_mockWordService.Object);
        
        mockScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);

        _roomManager = new RoomManager(
            _mockHubContext.Object,
            _mockLogger.Object,
            _dbFactory,
            _mockScopeFactory.Object
        );
    }

    public void Dispose()
    {
        using var context = _dbFactory.CreateDbContext();
        context.Database.EnsureDeleted();
    }

    private async Task<User> CreateTestUserAsync(string username = "testuser", string connectionId = "conn-123")
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        
        var user = new User
        {
            Username = username,
            Password = "Password123!",
            ConnectionId = connectionId
        };
        
        db.Users.Add(user);
        await db.SaveChangesAsync();
        
        return user;
    }

    private async Task<Category> CreateTestCategoryAsync(string name = "General")
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        
        var category = new Category { Name = name };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        
        return category;
    }

    #region CreateUser Tests

    [Fact]
    public async Task CreateUser_WithValidCredentials_ShouldCreateUser()
    {
        var username = "newuser";
        var password = "Password123!";
        var connectionId = "conn-new";

        await _roomManager.CreateUser(connectionId, username, password);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
        Assert.NotNull(user);
        Assert.Equal(username, user.Username);
        Assert.Equal(password, user.Password);
        Assert.Equal(connectionId, user.ConnectionId);
    }

    [Theory]
    [InlineData("ab")] 
    [InlineData("a")] 
    [InlineData("thisusernameiswaytoolongandexceedsthirtytwocharacters")] 
    public async Task CreateUser_WithInvalidUsernameLength_ShouldThrowInvalidUsernameException(string username)
    {
        var password = "Password123!";
        var connectionId = "conn-test";

        await Assert.ThrowsAsync<InvalidUsernameException>(() =>
            _roomManager.CreateUser(connectionId, username, password));
    }

    [Theory]
    [InlineData("user@name")] 
    [InlineData("user name")] 
    [InlineData("user#123")]
    public async Task CreateUser_WithInvalidUsernameFormat_ShouldThrowInvalidUsernameException(string username)
    {
        var password = "Password123!";
        var connectionId = "conn-test";

        await Assert.ThrowsAsync<InvalidUsernameException>(() =>
            _roomManager.CreateUser(connectionId, username, password));
    }

    [Theory]
    [InlineData("Pass12!")] 
    [InlineData("Short1!")] 
    [InlineData("thispasswordiswaytoolongandexceedsthirtytwocharacterseasily")] 
    public async Task CreateUser_WithInvalidPasswordLength_ShouldThrowInvalidPasswordException(string password)
    {
        var username = "testuser";
        var connectionId = "conn-test";

        await Assert.ThrowsAsync<InvalidPasswordException>(() =>
            _roomManager.CreateUser(connectionId, username, password));
    }

    [Theory]
    [InlineData("Password 123")] 
    [InlineData("Password#123")] 
    public async Task CreateUser_WithInvalidPasswordFormat_ShouldThrowInvalidPasswordException(string password)
    {
        var username = "testuser";
        var connectionId = "conn-test";

        await Assert.ThrowsAsync<InvalidPasswordException>(() =>
            _roomManager.CreateUser(connectionId, username, password));
    }

    [Fact]
    public async Task CreateUser_WithExistingUsername_ShouldThrowUsernameTakenException()
    {
        var username = "existinguser";
        await CreateTestUserAsync(username, "conn-existing");

        await Assert.ThrowsAsync<UsernameTakenException>(() =>
            _roomManager.CreateUser("conn-new", username, "Password123!"));
    }

    #endregion

    #region LoginUser Tests

    [Fact]
    public async Task LoginUser_WithValidCredentials_ShouldUpdateConnectionId()
    {
        var username = "loginuser";
        var password = "Password123!";
        var oldConnectionId = "conn-old";
        var newConnectionId = "conn-new";
        
        await CreateTestUserAsync(username, oldConnectionId);
        await using var db = await _dbFactory.CreateDbContextAsync();
        var user = await db.Users.FirstAsync(u => u.Username == username);
        user.Password = password;
        await db.SaveChangesAsync();
        
        await _roomManager.LoginUser(newConnectionId, username, password);

        await using var db2 = await _dbFactory.CreateDbContextAsync();
        var updatedUser = await db2.Users.FirstAsync(u => u.Username == username);
        Assert.Equal(newConnectionId, updatedUser.ConnectionId);
    }

    [Fact]
    public async Task LoginUser_WithInvalidCredentials_ShouldThrowInvalidUsernameException()
    {
        var username = "loginuser";
        var correctPassword = "Password123!";
        var wrongPassword = "WrongPassword123!";
        
        await CreateTestUserAsync(username, "conn-old");
        await using var db = await _dbFactory.CreateDbContextAsync();
        var user = await db.Users.FirstAsync(u => u.Username == username);
        user.Password = correctPassword;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidUsernameException>(() =>
            _roomManager.LoginUser("conn-new", username, wrongPassword));
    }

    [Fact]
    public async Task LoginUser_WithNonExistentUser_ShouldThrowInvalidUsernameException()
    {
        await Assert.ThrowsAsync<InvalidUsernameException>(() =>
            _roomManager.LoginUser("conn-new", "nonexistent", "Password123!"));
    }

    #endregion

    #region GetCurrentUserDataAsync Tests

    [Fact]
    public async Task GetCurrentUserDataAsync_WithValidConnectionId_ShouldReturnUserData()
    {
        var username = "testuser";
        var connectionId = "conn-test";
        await CreateTestUserAsync(username, connectionId);

        var (returnedUsername, roomCode) = await _roomManager.GetCurrentUserDataAsync(connectionId);

        Assert.Equal(username, returnedUsername);
        Assert.Equal("-1", roomCode);
    }

    [Fact]
    public async Task GetCurrentUserDataAsync_WithInvalidConnectionId_ShouldThrowInvalidConnectionIdException()
    {
        await Assert.ThrowsAsync<InvalidConnectionIdException>(() =>
            _roomManager.GetCurrentUserDataAsync("invalid-connection"));
    }

    [Fact]
    public async Task GetCurrentUserDataAsync_WhenUserInRoom_ShouldReturnRoomCode()
    {
        var username = "testuser";
        var connectionId = "conn-test";
        var roomCode = "ROOM01";
        
        var user = await CreateTestUserAsync(username, connectionId);
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = roomCode,
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = username
        };
        db.ActiveRooms.Add(room);
        
        var member = new RoomMember
        {
            RoomCode = roomCode,
            Username = username,
            Role = "Player",
            GameScore = 0
        };
        db.RoomMembers.Add(member);
        await db.SaveChangesAsync();

        var (returnedUsername, returnedRoomCode) = await _roomManager.GetCurrentUserDataAsync(connectionId);

        Assert.Equal(username, returnedUsername);
        Assert.Equal(roomCode, returnedRoomCode);
    }

    #endregion

    #region CreateRoom Tests

    [Fact]
    public async Task CreateRoom_WithValidParameters_ShouldCreateRoomAndReturnCode()
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();
        _mockWordService.Setup(x => x.LoadWordsForRoomAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var roomCode = await _roomManager.CreateRoom(
            user.ConnectionId, 
            "Test Room", 
            category.Name, 
            10, 
            30
        );

        Assert.NotNull(roomCode);
        Assert.Equal(6, roomCode.Length);
        Assert.Matches(@"^[A-Z0-9]{6}$", roomCode);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = await db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
        Assert.NotNull(room);
        Assert.Equal("Test Room", room.RoomName);
        Assert.Equal(user.Username, room.RoomCreator);
    }

    [Fact]
    public async Task CreateRoom_WithoutExistingUser_ShouldThrowForbiddenException()
    {
        var category = await CreateTestCategoryAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _roomManager.CreateRoom("invalid-conn", "Test Room", category.Name, 10, 30));
    }

    [Fact]
    public async Task CreateRoom_WhenUserAlreadyInRoom_ShouldThrowJoinedDifferentRoomException()
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existingRoom = new ActiveRoom
        {
            RoomCode = "EXIST1",
            RoomName = "Existing Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = user.Username
        };
        db.ActiveRooms.Add(existingRoom);
        
        var member = new RoomMember
        {
            RoomCode = "EXIST1",
            Username = user.Username,
            Role = "Player",
            GameScore = 0
        };
        db.RoomMembers.Add(member);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<JoinedDifferentRoomException>(() =>
            _roomManager.CreateRoom(user.ConnectionId, "New Room", category.Name, 10, 30));
    }

    [Theory]
    [InlineData("AB")] 
    [InlineData("thisroomnameiswaytoolongandexceedsthirtytwocharacterseasily")] 
    public async Task CreateRoom_WithInvalidRoomNameLength_ShouldThrowIncorrectRoomNameException(string roomName)
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();

        await Assert.ThrowsAsync<IncorrectRoomNameException>(() =>
            _roomManager.CreateRoom(user.ConnectionId, roomName, category.Name, 10, 30));
    }

    [Theory]
    [InlineData("Room@Name")] 
    [InlineData("Room#123")] 
    public async Task CreateRoom_WithInvalidRoomNameFormat_ShouldThrowIncorrectRoomNameException(string roomName)
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();

        await Assert.ThrowsAsync<IncorrectRoomNameException>(() =>
            _roomManager.CreateRoom(user.ConnectionId, roomName, category.Name, 10, 30));
    }

    [Fact]
    public async Task CreateRoom_WithNonExistentCategory_ShouldThrowIncorrectRoomCategoryException()
    {
        var user = await CreateTestUserAsync();

        await Assert.ThrowsAsync<IncorrectRoomCategoryException>(() =>
            _roomManager.CreateRoom(user.ConnectionId, "Test Room", "NonExistent", 10, 30));
    }

    [Theory]
    [InlineData(9)] 
    [InlineData(31)] 
    public async Task CreateRoom_WithInvalidRounds_ShouldThrowIncorrectRoundAmountException(int rounds)
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();

        await Assert.ThrowsAsync<IncorrectRoundAmountException>(() =>
            _roomManager.CreateRoom(user.ConnectionId, "Test Room", category.Name, rounds, 30));
    }

    [Theory]
    [InlineData(14)] 
    [InlineData(46)] 
    public async Task CreateRoom_WithInvalidRoundDuration_ShouldThrowIncorrectRoundDurationException(int duration)
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();

        await Assert.ThrowsAsync<IncorrectRoundDurationException>(() =>
            _roomManager.CreateRoom(user.ConnectionId, "Test Room", category.Name, 10, duration));
    }

    #endregion

    #region JoinRoom Tests

    [Fact]
    public async Task JoinRoom_WithValidRoomCode_ShouldAddUserToRoom()
    {
        var creator = await CreateTestUserAsync("creator", "conn-creator");
        var joiner = await CreateTestUserAsync("joiner", "conn-joiner");
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = creator.Username,
            GameStarted = false
        };
        db.ActiveRooms.Add(room);
        await db.SaveChangesAsync();

        var result = await _roomManager.JoinRoom(joiner.ConnectionId, "ROOM01");

        Assert.Equal(joiner.Username, result.username);
        Assert.Equal("Test Room", result.roomName);
        
        await using var db2 = await _dbFactory.CreateDbContextAsync();
        var member = await db2.RoomMembers.FirstOrDefaultAsync(
            m => m.RoomCode == "ROOM01" && m.Username == joiner.Username);
        Assert.NotNull(member);
        Assert.Equal("Player", member.Role);
    }

    [Fact]
    public async Task JoinRoom_WithInvalidUser_ShouldThrowForbiddenException()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _roomManager.JoinRoom("invalid-conn", "ROOM01"));
    }

    [Fact]
    public async Task JoinRoom_WhenAlreadyInRoom_ShouldThrowJoinedDifferentRoomException()
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existingRoom = new ActiveRoom
        {
            RoomCode = "EXIST1",
            RoomName = "Existing Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = user.Username
        };
        db.ActiveRooms.Add(existingRoom);
        
        var member = new RoomMember
        {
            RoomCode = "EXIST1",
            Username = user.Username,
            Role = "Player",
            GameScore = 0
        };
        db.RoomMembers.Add(member);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<JoinedDifferentRoomException>(() =>
            _roomManager.JoinRoom(user.ConnectionId, "ROOM02"));
    }

    [Fact]
    public async Task JoinRoom_WithInvalidRoomCode_ShouldThrowIncorrectRoomCodeException()
    {
        var user = await CreateTestUserAsync();

        await Assert.ThrowsAsync<IncorrectRoomCodeException>(() =>
            _roomManager.JoinRoom(user.ConnectionId, "INVALID"));
    }

    [Fact]
    public async Task JoinRoom_WhenGameAlreadyStarted_ShouldThrowRoomGameStartedException()
    {
        var creator = await CreateTestUserAsync("creator", "conn-creator");
        var joiner = await CreateTestUserAsync("joiner", "conn-joiner");
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = creator.Username,
            GameStarted = true 
        };
        db.ActiveRooms.Add(room);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<RoomGameStartedException>(() =>
            _roomManager.JoinRoom(joiner.ConnectionId, "ROOM01"));
    }

    #endregion

    #region StartGame Tests

    [Fact]
    public async Task StartGame_WithEnoughPlayers_ShouldStartGame()
    {
        var creator = await CreateTestUserAsync("creator", "conn-creator");
        var player2 = await CreateTestUserAsync("player2", "conn-2");
        var player3 = await CreateTestUserAsync("player3", "conn-3");
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = creator.Username,
            GameStarted = false
        };
        db.ActiveRooms.Add(room);
        
        db.RoomMembers.AddRange(
            new RoomMember { RoomCode = "ROOM01", Username = creator.Username, Role = "Player", GameScore = 0 },
            new RoomMember { RoomCode = "ROOM01", Username = player2.Username, Role = "Player", GameScore = 0 },
            new RoomMember { RoomCode = "ROOM01", Username = player3.Username, Role = "Player", GameScore = 0 }
        );
        await db.SaveChangesAsync();

        var result = await _roomManager.StartGame(creator.ConnectionId);

        Assert.Equal("ROOM01", result);
        
        await using var db2 = await _dbFactory.CreateDbContextAsync();
        var updatedRoom = await db2.ActiveRooms.FirstAsync(r => r.RoomCode == "ROOM01");
        Assert.True(updatedRoom.GameStarted);
    }

    [Fact]
    public async Task StartGame_WithLessThanThreePlayers_ShouldThrowNotEnoughPlayersException()
    {
        var creator = await CreateTestUserAsync("creator", "conn-creator");
        var player2 = await CreateTestUserAsync("player2", "conn-2");
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = creator.Username,
            GameStarted = false
        };
        db.ActiveRooms.Add(room);
        
        db.RoomMembers.AddRange(
            new RoomMember { RoomCode = "ROOM01", Username = creator.Username, Role = "Player", GameScore = 0 },
            new RoomMember { RoomCode = "ROOM01", Username = player2.Username, Role = "Player", GameScore = 0 }
        );
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotEnoughPlayersException>(() =>
            _roomManager.StartGame(creator.ConnectionId));
    }

    [Fact]
    public async Task StartGame_WhenNotRoomCreator_ShouldThrowForbiddenException()
    {
        var creator = await CreateTestUserAsync("creator", "conn-creator");
        var player2 = await CreateTestUserAsync("player2", "conn-2");
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = creator.Username,
            GameStarted = false
        };
        db.ActiveRooms.Add(room);
        
        db.RoomMembers.AddRange(
            new RoomMember { RoomCode = "ROOM01", Username = creator.Username, Role = "Player", GameScore = 0 },
            new RoomMember { RoomCode = "ROOM01", Username = player2.Username, Role = "Player", GameScore = 0 }
        );
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _roomManager.StartGame(player2.ConnectionId));
    }

    [Fact]
    public async Task StartGame_WhenGameAlreadyStarted_ShouldThrowRoomGameStartedException()
    {
        var creator = await CreateTestUserAsync("creator", "conn-creator");
        var category = await CreateTestCategoryAsync();
    
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = creator.Username,
            GameStarted = true
        };
        db.ActiveRooms.Add(room);
    
        var member = new RoomMember
        {
            RoomCode = "ROOM01",
            Username = creator.Username,
            Role = "Player",
            GameScore = 0
        };
        db.RoomMembers.Add(member);
    
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<RoomGameStartedException>(() =>
            _roomManager.StartGame(creator.ConnectionId));
    }

    #endregion

    #region CheckWord Tests

    [Fact]
    public async Task CheckWord_WithCorrectWord_ShouldMarkAsCorrectAndAddScore()
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = "creator",
            GameStarted = true,
            EmojisSent = true,
            RoundWord = "apple",
            RoundEnded = false
        };
        db.ActiveRooms.Add(room);
        
        var member = new RoomMember
        {
            RoomCode = "ROOM01",
            Username = user.Username,
            Role = "Player",
            GameScore = 0
        };
        db.RoomMembers.Add(member);
        await db.SaveChangesAsync();

        var (isCorrect, roomCode) = await _roomManager.CheckWord(user.ConnectionId, "apple");

        Assert.True(isCorrect);
        Assert.Equal("ROOM01", roomCode);
        
        await using var db2 = await _dbFactory.CreateDbContextAsync();
        var updatedMember = await db2.RoomMembers.FirstAsync(
            m => m.RoomCode == "ROOM01" && m.Username == user.Username);
        Assert.True(updatedMember.GuessedRight);
        Assert.Equal(100, updatedMember.GameScore);
    }

    [Fact]
    public async Task CheckWord_WithIncorrectWord_ShouldNotAddScore()
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = "creator",
            GameStarted = true,
            EmojisSent = true,
            RoundWord = "apple",
            RoundEnded = false
        };
        db.ActiveRooms.Add(room);
        
        var member = new RoomMember
        {
            RoomCode = "ROOM01",
            Username = user.Username,
            Role = "Player",
            GameScore = 50
        };
        db.RoomMembers.Add(member);
        await db.SaveChangesAsync();

        var (isCorrect, roomCode) = await _roomManager.CheckWord(user.ConnectionId, "banana");

        Assert.False(isCorrect);
        
        await using var db2 = await _dbFactory.CreateDbContextAsync();
        var updatedMember = await db2.RoomMembers.FirstAsync(
            m => m.RoomCode == "ROOM01" && m.Username == user.Username);
        Assert.False(updatedMember.GuessedRight);
        Assert.Equal(50, updatedMember.GameScore); 
    }

    [Fact]
    public async Task CheckWord_CaseInsensitive_ShouldMarkAsCorrect()
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = "creator",
            GameStarted = true,
            EmojisSent = true,
            RoundWord = "apple",
            RoundEnded = false
        };
        db.ActiveRooms.Add(room);
        
        var member = new RoomMember
        {
            RoomCode = "ROOM01",
            Username = user.Username,
            Role = "Player",
            GameScore = 0
        };
        db.RoomMembers.Add(member);
        await db.SaveChangesAsync();

        var (isCorrect, _) = await _roomManager.CheckWord(user.ConnectionId, "APPLE");

        Assert.True(isCorrect);
    }

    [Fact]
    public async Task CheckWord_WhenNotGameStarted_ShouldThrowForbiddenException()
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = "creator",
            GameStarted = false
        };
        db.ActiveRooms.Add(room);
        
        var member = new RoomMember
        {
            RoomCode = "ROOM01",
            Username = user.Username,
            Role = "Player",
            GameScore = 0
        };
        db.RoomMembers.Add(member);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _roomManager.CheckWord(user.ConnectionId, "apple"));
    }

    [Fact]
    public async Task CheckWord_WhenCommander_ShouldThrowForbiddenException()
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = "creator",
            GameStarted = true,
            EmojisSent = true,
            RoundWord = "apple"
        };
        db.ActiveRooms.Add(room);
        
        var member = new RoomMember
        {
            RoomCode = "ROOM01",
            Username = user.Username,
            Role = "Commander", 
            GameScore = 0
        };
        db.RoomMembers.Add(member);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _roomManager.CheckWord(user.ConnectionId, "apple"));
    }

    [Fact]
    public async Task CheckWord_WhenEmojisNotSent_ShouldThrowForbiddenException()
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = "creator",
            GameStarted = true,
            EmojisSent = false, 
            RoundWord = "apple"
        };
        db.ActiveRooms.Add(room);
        
        var member = new RoomMember
        {
            RoomCode = "ROOM01",
            Username = user.Username,
            Role = "Player",
            GameScore = 0
        };
        db.RoomMembers.Add(member);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _roomManager.CheckWord(user.ConnectionId, "apple"));
    }

    [Fact]
    public async Task CheckWord_WhenRoundEnded_ShouldThrowForbiddenException()
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = "creator",
            GameStarted = true,
            EmojisSent = true,
            RoundWord = "apple",
            RoundEnded = true 
        };
        db.ActiveRooms.Add(room);
        
        var member = new RoomMember
        {
            RoomCode = "ROOM01",
            Username = user.Username,
            Role = "Player",
            GameScore = 0
        };
        db.RoomMembers.Add(member);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _roomManager.CheckWord(user.ConnectionId, "apple"));
    }

    #endregion

    #region LeftRoom Tests

    [Fact]
    public async Task LeftRoom_AsRegularPlayer_ShouldRemovePlayerFromRoom()
    {
        var creator = await CreateTestUserAsync("creator", "conn-creator");
        var player = await CreateTestUserAsync("player", "conn-player");
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = creator.Username,
            GameStarted = false
        };
        db.ActiveRooms.Add(room);
        
        db.RoomMembers.AddRange(
            new RoomMember { RoomCode = "ROOM01", Username = creator.Username, Role = "Player", GameScore = 0 },
            new RoomMember { RoomCode = "ROOM01", Username = player.Username, Role = "Player", GameScore = 0 }
        );
        await db.SaveChangesAsync();

        var (username, roomCode, isGameEnded) = await _roomManager.LeftRoom(player.ConnectionId);

        Assert.Equal(player.Username, username);
        Assert.Equal("ROOM01", roomCode);
        Assert.False(isGameEnded);
        
        await using var db2 = await _dbFactory.CreateDbContextAsync();
        var member = await db2.RoomMembers.FirstOrDefaultAsync(
            m => m.RoomCode == "ROOM01" && m.Username == player.Username);
        Assert.Null(member);
    }

    [Fact]
    public async Task LeftRoom_AsCreatorBeforeGameStart_ShouldEndGame()
    {
        var creator = await CreateTestUserAsync("creator", "conn-creator");
        var player = await CreateTestUserAsync("player", "conn-player");
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = creator.Username,
            GameStarted = false
        };
        db.ActiveRooms.Add(room);
        
        db.RoomMembers.AddRange(
            new RoomMember { RoomCode = "ROOM01", Username = creator.Username, Role = "Player", GameScore = 0 },
            new RoomMember { RoomCode = "ROOM01", Username = player.Username, Role = "Player", GameScore = 0 }
        );
        await db.SaveChangesAsync();

        var (username, roomCode, isGameEnded) = await _roomManager.LeftRoom(creator.ConnectionId);

        Assert.Equal(creator.Username, username);
        Assert.True(isGameEnded);
        
        await using var db2 = await _dbFactory.CreateDbContextAsync();
        var activeRoom = await db2.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == "ROOM01");
        Assert.Null(activeRoom);
    }

    [Fact]
    public async Task LeftRoom_WhenPlayerCountDropsBelowThreeDuringGame_ShouldEndGame()
    {
        var creator = await CreateTestUserAsync("creator", "conn-creator");
        var player2 = await CreateTestUserAsync("player2", "conn-2");
        var player3 = await CreateTestUserAsync("player3", "conn-3");
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = creator.Username,
            GameStarted = true
        };
        db.ActiveRooms.Add(room);
        
        db.RoomMembers.AddRange(
            new RoomMember { RoomCode = "ROOM01", Username = creator.Username, Role = "Player", GameScore = 0 },
            new RoomMember { RoomCode = "ROOM01", Username = player2.Username, Role = "Player", GameScore = 0 },
            new RoomMember { RoomCode = "ROOM01", Username = player3.Username, Role = "Player", GameScore = 0 }
        );
        await db.SaveChangesAsync();

        var (_, _, isGameEnded) = await _roomManager.LeftRoom(player3.ConnectionId);

        Assert.True(isGameEnded);
        
        await using var db2 = await _dbFactory.CreateDbContextAsync();
        var activeRoom = await db2.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == "ROOM01");
        Assert.Null(activeRoom);
    }

    #endregion

    #region GetCommander Tests

    [Fact]
    public async Task GetCommander_ShouldSelectRandomCommanderAndIncrementRound()
    {
        var player1 = await CreateTestUserAsync("player1", "conn-1");
        var player2 = await CreateTestUserAsync("player2", "conn-2");
        var player3 = await CreateTestUserAsync("player3", "conn-3");
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = player1.Username,
            GameStarted = true,
            CurrentRound = 0
        };
        db.ActiveRooms.Add(room);
        
        db.RoomMembers.AddRange(
            new RoomMember { RoomCode = "ROOM01", Username = player1.Username, Role = "Player", GameScore = 0 },
            new RoomMember { RoomCode = "ROOM01", Username = player2.Username, Role = "Player", GameScore = 0 },
            new RoomMember { RoomCode = "ROOM01", Username = player3.Username, Role = "Player", GameScore = 0 }
        );
        await db.SaveChangesAsync();

        var commanderUsername = await _roomManager.GetCommander(player1.ConnectionId);

        Assert.NotNull(commanderUsername);
        Assert.Contains(commanderUsername, new[] { player1.Username, player2.Username, player3.Username });
        
        await using var db2 = await _dbFactory.CreateDbContextAsync();
        var commander = await db2.RoomMembers.FirstOrDefaultAsync(
            m => m.RoomCode == "ROOM01" && m.Username == commanderUsername);
        Assert.NotNull(commander);
        Assert.Equal("Commander", commander.Role);
        
        var updatedRoom = await db2.ActiveRooms.FirstAsync(r => r.RoomCode == "ROOM01");
        Assert.Equal(1, updatedRoom.CurrentRound);
        Assert.False(updatedRoom.EmojisSent);
    }

    [Fact]
    public async Task GetCommander_WhenGameNotStarted_ShouldThrowForbiddenException()
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = user.Username,
            GameStarted = false
        };
        db.ActiveRooms.Add(room);
        
        var member = new RoomMember
        {
            RoomCode = "ROOM01",
            Username = user.Username,
            Role = "Player",
            GameScore = 0
        };
        db.RoomMembers.Add(member);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _roomManager.GetCommander(user.ConnectionId));
    }

    #endregion

    #region GetWord Tests

    [Fact]
    public async Task GetWord_AsCommander_ShouldReturnWordAndSetRoundWord()
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();
        var testWord = "testword";
        
        _mockWordService.Setup(x => x.GetWord("ROOM01")).Returns(testWord);
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = user.Username,
            GameStarted = true
        };
        db.ActiveRooms.Add(room);
        
        var member = new RoomMember
        {
            RoomCode = "ROOM01",
            Username = user.Username,
            Role = "Commander",
            GameScore = 0
        };
        db.RoomMembers.Add(member);
        await db.SaveChangesAsync();

        var word = await _roomManager.GetWord(user.ConnectionId);

        Assert.Equal(testWord, word);
        
        await using var db2 = await _dbFactory.CreateDbContextAsync();
        var updatedRoom = await db2.ActiveRooms.FirstAsync(r => r.RoomCode == "ROOM01");
        Assert.Equal(testWord, updatedRoom.RoundWord);
    }

    [Fact]
    public async Task GetWord_WhenNotCommander_ShouldThrowForbiddenException()
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = user.Username,
            GameStarted = true
        };
        db.ActiveRooms.Add(room);
        
        var member = new RoomMember
        {
            RoomCode = "ROOM01",
            Username = user.Username,
            Role = "Player", 
            GameScore = 0
        };
        db.RoomMembers.Add(member);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _roomManager.GetWord(user.ConnectionId));
    }

    #endregion

    #region SendEmojis Tests

    [Fact]
    public async Task SendEmojis_AsCommander_ShouldSetEmojisSentFlag()
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = user.Username,
            GameStarted = true,
            EmojisSent = false
        };
        db.ActiveRooms.Add(room);
        
        var member = new RoomMember
        {
            RoomCode = "ROOM01",
            Username = user.Username,
            Role = "Commander",
            GameScore = 0
        };
        db.RoomMembers.Add(member);
        await db.SaveChangesAsync();

        var roomCode = await _roomManager.SendEmojis(user.ConnectionId);

        Assert.Equal("ROOM01", roomCode);
        
        await using var db2 = await _dbFactory.CreateDbContextAsync();
        var updatedRoom = await db2.ActiveRooms.FirstAsync(r => r.RoomCode == "ROOM01");
        Assert.True(updatedRoom.EmojisSent);
        Assert.NotNull(updatedRoom.EmojisSentTime);
    }

    [Fact]
    public async Task SendEmojis_AsPlayer_ShouldThrowForbiddenException()
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = user.Username,
            GameStarted = true
        };
        db.ActiveRooms.Add(room);
        
        var member = new RoomMember
        {
            RoomCode = "ROOM01",
            Username = user.Username,
            Role = "Player", 
            GameScore = 0
        };
        db.RoomMembers.Add(member);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _roomManager.SendEmojis(user.ConnectionId));
    }

    #endregion

    #region GetResults Tests

    [Fact]
    public async Task GetResults_ShouldReturnSortedResultsAndResetRound()
    {
        var player1 = await CreateTestUserAsync("player1", "conn-1");
        var player2 = await CreateTestUserAsync("player2", "conn-2");
        var player3 = await CreateTestUserAsync("player3", "conn-3");
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = player1.Username,
            GameStarted = true,
            RoundEnded = true,
            CurrentRound = 1
        };
        db.ActiveRooms.Add(room);
        
        db.RoomMembers.AddRange(
            new RoomMember { RoomCode = "ROOM01", Username = player1.Username, Role = "Commander", GameScore = 200, GuessedRight = false },
            new RoomMember { RoomCode = "ROOM01", Username = player2.Username, Role = "Player", GameScore = 100, GuessedRight = true },
            new RoomMember { RoomCode = "ROOM01", Username = player3.Username, Role = "Player", GameScore = 300, GuessedRight = true }
        );
        await db.SaveChangesAsync();

        var (results, nextRound) = await _roomManager.GetResults(player1.ConnectionId);

        Assert.Equal(3, results.Count);
        Assert.Equal(player3.Username, results[0].username); 
        Assert.Equal(300, results[0].gameScore);
        Assert.True(nextRound); 
        
        await using var db2 = await _dbFactory.CreateDbContextAsync();
        var updatedRoom = await db2.ActiveRooms.FirstAsync(r => r.RoomCode == "ROOM01");
        Assert.False(updatedRoom.RoundEnded);
        Assert.Null(updatedRoom.RoundWord);
        
        var updatedMembers = await db2.RoomMembers.Where(m => m.RoomCode == "ROOM01").ToListAsync();
        Assert.All(updatedMembers, m =>
        {
            Assert.Equal("Player", m.Role);
            Assert.False(m.GuessedRight);
            Assert.Equal("", m.GuessedWord);
        });
    }

    [Fact]
    public async Task GetResults_OnLastRound_ShouldReturnNextRoundFalse()
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = user.Username,
            GameStarted = true,
            RoundEnded = true,
            CurrentRound = 10 
        };
        db.ActiveRooms.Add(room);
        
        var member = new RoomMember
        {
            RoomCode = "ROOM01",
            Username = user.Username,
            Role = "Player",
            GameScore = 100
        };
        db.RoomMembers.Add(member);
        await db.SaveChangesAsync();

        var (_, nextRound) = await _roomManager.GetResults(user.ConnectionId);

        Assert.False(nextRound);
    }

    [Fact]
    public async Task GetResults_WhenRoundNotEnded_ShouldThrowForbiddenException()
    {
        var user = await CreateTestUserAsync();
        var category = await CreateTestCategoryAsync();
        
        await using var db = await _dbFactory.CreateDbContextAsync();
        var room = new ActiveRoom
        {
            RoomCode = "ROOM01",
            RoomName = "Test Room",
            CategoryId = category.Id,
            Rounds = 10,
            RoundDuration = 30,
            RoomCreator = user.Username,
            GameStarted = true,
            RoundEnded = false 
        };
        db.ActiveRooms.Add(room);
        
        var member = new RoomMember
        {
            RoomCode = "ROOM01",
            Username = user.Username,
            Role = "Player",
            GameScore = 0
        };
        db.RoomMembers.Add(member);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _roomManager.GetResults(user.ConnectionId));
    }

    #endregion

    
    #region GlobalExceptionHandler Tests

public class GlobalExceptionHandlerTests
{
    private readonly Mock<ILogger<GlobalExceptionHandler>> _mockLogger;
    private readonly GlobalExceptionHandler _handler;
    private readonly DefaultHttpContext _httpContext;

    public GlobalExceptionHandlerTests()
    {
        _mockLogger = new Mock<ILogger<GlobalExceptionHandler>>();
        _handler = new GlobalExceptionHandler(_mockLogger.Object);
        _httpContext = new DefaultHttpContext();
        _httpContext.Response.Body = new MemoryStream();
    }

    [Fact]
    public async Task TryHandleAsync_WithAnyException_ShouldReturnTrue()
    {
        var exception = new Exception("Test exception");
        var result = await _handler.TryHandleAsync(_httpContext, exception, CancellationToken.None);
        Assert.True(result);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldSetStatusCodeTo500()
    {
        var exception = new Exception("Test exception");
        await _handler.TryHandleAsync(_httpContext, exception, CancellationToken.None);
        Assert.Equal((int)HttpStatusCode.InternalServerError, _httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldSetContentTypeToApplicationProblemJson()
    {
        var exception = new Exception("Test exception");
        await _handler.TryHandleAsync(_httpContext, exception, CancellationToken.None);
        Assert.StartsWith(
            "application/problem+json",
            _httpContext.Response.ContentType
        );

    }

    [Fact]
    public async Task TryHandleAsync_ShouldWriteProblemDetailsToResponse()
    {
        var exception = new Exception("Test exception");
        await _handler.TryHandleAsync(_httpContext, exception, CancellationToken.None);
        
        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        
        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(problemDetails);
        Assert.Equal((int)HttpStatusCode.InternalServerError, problemDetails.Status);
        Assert.Equal("An unexpected error occurred.", problemDetails.Title);
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc9110#name-500-internal-server-error", problemDetails.Type);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldLogError()
    {
        var exceptionMessage = "Test exception message";
        var exception = new Exception(exceptionMessage);
        await _handler.TryHandleAsync(_httpContext, exception, CancellationToken.None);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Unhandled exception")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_WithDifferentExceptionTypes_ShouldHandleAll()
    {
        var exceptions = new Exception[]
        {
            new InvalidOperationException("Invalid operation"),
            new ArgumentNullException("paramName"),
            new NullReferenceException("Null reference"),
            new DivideByZeroException("Division by zero")
        };

        foreach (var exception in exceptions)
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            var result = await _handler.TryHandleAsync(context, exception, CancellationToken.None);
            Assert.True(result);
            Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
        }
    }

    [Fact]
    public async Task TryHandleAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        var exception = new Exception("Test exception");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _handler.TryHandleAsync(_httpContext, exception, cts.Token);
        });
    }

    [Fact]
    public async Task TryHandleAsync_WithExceptionContainingInnerException_ShouldHandleGracefully()
    {
        var innerException = new InvalidOperationException("Inner exception");
        var outerException = new Exception("Outer exception", innerException);
        var result = await _handler.TryHandleAsync(_httpContext, outerException, CancellationToken.None);

        Assert.True(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, _httpContext.Response.StatusCode);
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                outerException,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }
}

#endregion

#region RoomHub Tests

public class RoomHubTests
{
    private readonly Mock<IRoomManager> _mockRoomManager;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<ISingleClientProxy> _mockCaller;
    private readonly Mock<IClientProxy> _mockOthersInGroup;
    private readonly Mock<IClientProxy> _mockGroup;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly RoomHub _hub;

    public RoomHubTests()
    {
        _mockRoomManager = new Mock<IRoomManager>();
        _mockClients = new Mock<IHubCallerClients>();
        _mockCaller = new Mock<ISingleClientProxy>();
        _mockOthersInGroup = new Mock<IClientProxy>();
        _mockGroup = new Mock<IClientProxy>();
        _mockContext = new Mock<HubCallerContext>();

        _mockClients.Setup(x => x.Caller).Returns(_mockCaller.Object);
        _mockClients.Setup(x => x.OthersInGroup(It.IsAny<string>())).Returns(_mockOthersInGroup.Object);
        _mockClients.Setup(x => x.Group(It.IsAny<string>())).Returns(_mockGroup.Object);
        _mockContext.Setup(x => x.ConnectionId).Returns("test-connection-id");

        _hub = new RoomHub(_mockRoomManager.Object)
        {
            Clients = _mockClients.Object,
            Context = _mockContext.Object
        };
    }

    [Fact]
    public async Task GetUserData_WithValidUsername_ShouldSendUserData()
    {
        var username = "testuser";
        _mockRoomManager.Setup(x => x.GetUserData(username, "test-connection-id")).Returns(Task.CompletedTask);
        await _hub.GetUserData(username);
        _mockRoomManager.Verify(x => x.GetUserData(username, "test-connection-id"), Times.Once);
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.userData, 
            It.Is<object[]>(args => args.Length == 1 && (string)args[0] == username), default), Times.Once);
    }

    [Fact]
    public async Task GetUserData_WhenForbidden_ShouldSendError()
    {
        var username = "testuser";
        _mockRoomManager.Setup(x => x.GetUserData(username, It.IsAny<string>())).ThrowsAsync(new ForbiddenException());
        await _hub.GetUserData(username);
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.Error, 
            It.Is<object[]>(args => args.Length == 1 && ((string)args[0]).Contains(RoomHubCommands.getUserData) && 
            ((string)args[0]).Contains(RoomHubErrors.forbidden)), default), Times.Once);
    }
    

    [Fact]
    public async Task CreateRoom_WhenForbidden_ShouldSendError()
    {
        _mockRoomManager.Setup(x => x.CreateRoom(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
            It.IsAny<int>(), It.IsAny<int>())).ThrowsAsync(new ForbiddenException());
        await _hub.CreateRoom("Test Room", "General", 10, 30);
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.Error, It.Is<object[]>(args => args.Length == 1 && 
            ((string)args[0]).Contains(RoomHubCommands.createRoom) && ((string)args[0]).Contains(RoomHubErrors.forbidden)), default), Times.Once);
    }

    [Fact]
    public async Task CreateRoom_WhenJoinedDifferentRoom_ShouldSendError()
    {
        _mockRoomManager.Setup(x => x.CreateRoom(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
            It.IsAny<int>(), It.IsAny<int>())).ThrowsAsync(new JoinedDifferentRoomException());
        await _hub.CreateRoom("Test Room", "General", 10, 30);
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.Error, It.Is<object[]>(args => args.Length == 1 && 
            ((string)args[0]).Contains(RoomHubErrors.joinedDifferentRoom)), default), Times.Once);
    }

    [Fact]
    public async Task CreateRoom_WithIncorrectRoomName_ShouldSendError()
    {
        _mockRoomManager.Setup(x => x.CreateRoom(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
            It.IsAny<int>(), It.IsAny<int>())).ThrowsAsync(new IncorrectRoomNameException());
        await _hub.CreateRoom("AB", "General", 10, 30);
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.Error, It.Is<object[]>(args => args.Length == 1 && 
            ((string)args[0]).Contains(RoomHubErrors.incorrectRoomName)), default), Times.Once);
    }

    [Fact]
    public async Task CreateRoom_WithIncorrectCategory_ShouldSendError()
    {
        _mockRoomManager.Setup(x => x.CreateRoom(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
            It.IsAny<int>(), It.IsAny<int>())).ThrowsAsync(new IncorrectRoomCategoryException());
        await _hub.CreateRoom("Test Room", "Invalid", 10, 30);
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.Error, It.Is<object[]>(args => args.Length == 1 && 
            ((string)args[0]).Contains(RoomHubErrors.incorrectRoomCategory)), default), Times.Once);
    }

    [Fact]
    public async Task CreateRoom_WithIncorrectRoundAmount_ShouldSendError()
    {
        _mockRoomManager.Setup(x => x.CreateRoom(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
            It.IsAny<int>(), It.IsAny<int>())).ThrowsAsync(new IncorrectRoundAmountException());
        await _hub.CreateRoom("Test Room", "General", 5, 30);
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.Error, It.Is<object[]>(args => args.Length == 1 && 
            ((string)args[0]).Contains(RoomHubErrors.incorrectRoundAmount)), default), Times.Once);
    }

    [Fact]
    public async Task CreateRoom_WithIncorrectRoundDuration_ShouldSendError()
    {
        _mockRoomManager.Setup(x => x.CreateRoom(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
            It.IsAny<int>(), It.IsAny<int>())).ThrowsAsync(new IncorrectRoundDurationException());
        await _hub.CreateRoom("Test Room", "General", 10, 10);
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.Error, It.Is<object[]>(args => args.Length == 1 && 
            ((string)args[0]).Contains(RoomHubErrors.incorrectRoundDuration)), default), Times.Once);
    }

    [Fact]
    public async Task JoinRoom_WithInvalidRoomCode_ShouldSendError()
    {
        _mockRoomManager.Setup(x => x.JoinRoom(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new IncorrectRoomCodeException());
        await _hub.JoinRoom("INVALID");
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.Error, It.Is<object[]>(args => args.Length == 1 && 
            ((string)args[0]).Contains(RoomHubErrors.incorrectRoomCode)), default), Times.Once);
    }

    [Fact]
    public async Task JoinRoom_WhenGameStarted_ShouldSendError()
    {
        _mockRoomManager.Setup(x => x.JoinRoom(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new RoomGameStartedException());
        await _hub.JoinRoom("ROOM01");
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.Error, It.Is<object[]>(args => args.Length == 1 && 
            ((string)args[0]).Contains(RoomHubErrors.roomGameStarted)), default), Times.Once);
    }


    [Fact]
    public async Task LeftRoom_WhenForbidden_ShouldSendError()
    {
        _mockRoomManager.Setup(x => x.LeftRoom(It.IsAny<string>())).ThrowsAsync(new ForbiddenException());
        await _hub.LeftRoom();
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.Error, It.Is<object[]>(args => args.Length == 1 && 
            ((string)args[0]).Contains(RoomHubErrors.forbidden)), default), Times.Once);
    }

    [Fact]
    public async Task StartGame_WithEnoughPlayers_ShouldNotifyGroup()
    {
        _mockRoomManager.Setup(x => x.StartGame("test-connection-id")).ReturnsAsync("ROOM01");
        await _hub.StartGame();
        _mockRoomManager.Verify(x => x.StartGame("test-connection-id"), Times.Once);
        _mockGroup.Verify(x => x.SendCoreAsync(RoomHubConstants.gameStarted, 
            It.Is<object[]>(args => args.Length == 0), default), Times.Once);
    }

    [Fact]
    public async Task StartGame_WhenNotEnoughPlayers_ShouldSendError()
    {
        _mockRoomManager.Setup(x => x.StartGame(It.IsAny<string>())).ThrowsAsync(new NotEnoughPlayersException());
        await _hub.StartGame();
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.Error, It.Is<object[]>(args => args.Length == 1 && 
            ((string)args[0]).Contains(RoomHubErrors.notEnoughPlayers)), default), Times.Once);
    }

    [Fact]
    public async Task StartGame_WhenAlreadyStarted_ShouldSendError()
    {
        _mockRoomManager.Setup(x => x.StartGame(It.IsAny<string>())).ThrowsAsync(new RoomGameStartedException());
        await _hub.StartGame();
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.Error, It.Is<object[]>(args => args.Length == 1 && 
            ((string)args[0]).Contains(RoomHubErrors.roomGameStarted)), default), Times.Once);
    }

    [Fact]
    public async Task GetCommander_ShouldReturnCommanderUsername()
    {
        _mockRoomManager.Setup(x => x.GetCommander("test-connection-id")).ReturnsAsync("commander1");
        await _hub.GetCommander();
        _mockRoomManager.Verify(x => x.GetCommander("test-connection-id"), Times.Once);
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.commanderSelected, 
            It.Is<object[]>(args => args.Length == 1 && (string)args[0] == "commander1"), default), Times.Once);
    }

    [Fact]
    public async Task GetCommander_WhenForbidden_ShouldSendError()
    {
        _mockRoomManager.Setup(x => x.GetCommander(It.IsAny<string>())).ThrowsAsync(new ForbiddenException());
        await _hub.GetCommander();
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.Error, It.Is<object[]>(args => args.Length == 1 && 
            ((string)args[0]).Contains(RoomHubErrors.forbidden)), default), Times.Once);
    }

    [Fact]
    public async Task GetWord_AsCommander_ShouldReturnWord()
    {
        _mockRoomManager.Setup(x => x.GetWord("test-connection-id")).ReturnsAsync("testword");
        await _hub.GetWord();
        _mockRoomManager.Verify(x => x.GetWord("test-connection-id"), Times.Once);
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.recivedWord, 
            It.Is<object[]>(args => args.Length == 1 && (string)args[0] == "testword"), default), Times.Once);
    }

    [Fact]
    public async Task GetWord_WhenForbidden_ShouldSendError()
    {
        _mockRoomManager.Setup(x => x.GetWord(It.IsAny<string>())).ThrowsAsync(new ForbiddenException());
        await _hub.GetWord();
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.Error, It.Is<object[]>(args => args.Length == 1 && 
            ((string)args[0]).Contains(RoomHubErrors.forbidden)), default), Times.Once);
    }

    [Fact]
    public async Task SendEmojis_AsCommander_ShouldNotifyOthers()
    {
        var emojis = new List<string> { "😀", "😃", "😄" };
        _mockRoomManager.Setup(x => x.SendEmojis("test-connection-id")).ReturnsAsync("ROOM01");
        await _hub.SendEmojis(emojis);
        _mockRoomManager.Verify(x => x.SendEmojis("test-connection-id"), Times.Once);
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.emojisRecieved, 
            It.Is<object[]>(args => args.Length == 0), default), Times.Once);
        _mockOthersInGroup.Verify(x => x.SendCoreAsync(RoomHubConstants.recieveEmojis, 
            It.Is<object[]>(args => args.Length == 1), default), Times.Once);
    }

    [Fact]
    public async Task SendEmojis_WhenForbidden_ShouldSendError()
    {
        _mockRoomManager.Setup(x => x.SendEmojis(It.IsAny<string>())).ThrowsAsync(new ForbiddenException());
        await _hub.SendEmojis(new List<string> { "😀" });
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.Error, It.Is<object[]>(args => args.Length == 1 && 
            ((string)args[0]).Contains(RoomHubErrors.forbidden)), default), Times.Once);
    }

    [Fact]
    public async Task CheckWord_WithCorrectWord_ShouldReturnTrue()
    {
        _mockRoomManager.Setup(x => x.CheckWord("test-connection-id", "apple")).ReturnsAsync((true, "ROOM01"));
        await _hub.CheckWord("apple");
        _mockRoomManager.Verify(x => x.CheckWord("test-connection-id", "apple"), Times.Once);
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.wordChecked, 
            It.Is<object[]>(args => args.Length == 1 && (bool)args[0] == true), default), Times.Once);
    }

    [Fact]
    public async Task CheckWord_WithIncorrectWord_ShouldReturnFalse()
    {
        _mockRoomManager.Setup(x => x.CheckWord("test-connection-id", "banana")).ReturnsAsync((false, "ROOM01"));
        await _hub.CheckWord("banana");
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.wordChecked, 
            It.Is<object[]>(args => args.Length == 1 && (bool)args[0] == false), default), Times.Once);
    }

    [Fact]
    public async Task CheckWord_WhenForbidden_ShouldSendError()
    {
        _mockRoomManager.Setup(x => x.CheckWord(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new ForbiddenException());
        await _hub.CheckWord("testword");
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.Error, It.Is<object[]>(args => args.Length == 1 && 
            ((string)args[0]).Contains(RoomHubErrors.forbidden)), default), Times.Once);
    }

    [Fact]
    public async Task GetResults_WithNextRound_ShouldSendBothMessages()
    {
        var results = new List<(string username, bool guessedRight, string? guessedWord, long gameScore)>
        {
            ("player1", true, "apple", 100L),
            ("player2", false, "banana", 50L)
        };
        _mockRoomManager.Setup(x => x.GetResults("test-connection-id")).ReturnsAsync((results, true));
        await _hub.GetResults();
        _mockRoomManager.Verify(x => x.GetResults("test-connection-id"), Times.Once);
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.roundEnded, 
            It.Is<object[]>(args => args.Length == 1), default), Times.Once);
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.roundStarted, 
            It.Is<object[]>(args => args.Length == 0), default), Times.Once);
    }

    [Fact]
    public async Task GetResults_WithoutNextRound_ShouldOnlySendRoundEnded()
    {
        var results = new List<(string username, bool guessedRight, string? guessedWord, long gameScore)>
        {
            ("player1", true, "apple", 100L)
        };
        _mockRoomManager.Setup(x => x.GetResults("test-connection-id")).ReturnsAsync((results, false));
        await _hub.GetResults();
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.roundEnded, 
            It.Is<object[]>(args => args.Length == 1), default), Times.Once);
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.roundStarted, 
            It.IsAny<object[]>(), default), Times.Never);
    }

    [Fact]
    public async Task GetResults_WhenForbidden_ShouldSendError()
    {
        _mockRoomManager.Setup(x => x.GetResults(It.IsAny<string>())).ThrowsAsync(new ForbiddenException());
        await _hub.GetResults();
        _mockCaller.Verify(x => x.SendCoreAsync(RoomHubConstants.Error, It.Is<object[]>(args => args.Length == 1 && 
            ((string)args[0]).Contains(RoomHubErrors.forbidden)), default), Times.Once);
    }
}

#endregion
    
 
    
}