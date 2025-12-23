using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Wrap.CrazyEmoji.Api.Abstractions;
using Wrap.CrazyEmoji.Api.Data;
using Wrap.CrazyEmoji.Api.Data.Entities;
using Wrap.CrazyEmoji.Api.GameLogic;
using Wrap.CrazyEmoji.Api.GameLogic.Exceptions;
using Xunit;

namespace Wrap.CrazyEmoji.UnitTests.Api.GameLogic;

public sealed class RoomManagerTests
{
    private static GameDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GameDbContext(options);
    }

    private static RoomManager CreateSut(GameDbContext db)
    {
        var scopeFactory = Mock.Of<IServiceScopeFactory>();
        var wordService = Mock.Of<IWordService>();
        var hasher = new PasswordHasher<User>();

        return new RoomManager(db, scopeFactory, wordService, hasher);
    }

    private sealed class TestAsyncScopeFactory : IServiceScopeFactory
    {
        private readonly GameDbContext _db;

        public TestAsyncScopeFactory(GameDbContext db) => _db = db;

        public IServiceScope CreateScope() => new TestAsyncScope(_db);

        private sealed class TestAsyncScope(GameDbContext db) : IServiceScope, IAsyncDisposable
        {
            public IServiceProvider ServiceProvider { get; } = new TestServiceProvider(db);

            public void Dispose() { }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private sealed class TestServiceProvider(GameDbContext db) : IServiceProvider
        {
            public object? GetService(Type serviceType)
            {
                if (serviceType == typeof(GameDbContext)) return db;
                return null;
            }
        }
    }

    private static RoomManager CreateSut(GameDbContext db, IServiceScopeFactory scopeFactory, IWordService wordService)
    {
        var hasher = new PasswordHasher<User>();
        return new RoomManager(db, scopeFactory, wordService, hasher);
    }

    [Fact]
    public async Task CreateUser_InvalidUsernameLength_ThrowsInvalidUsernameException()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);

        await Assert.ThrowsAsync<InvalidUsernameException>(() =>
            sut.CreateUser("c1", "ab", "Password1!"));
    }

    [Fact]
    public async Task CreateUser_InvalidUsernameCharacters_ThrowsInvalidUsernameException()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);

        await Assert.ThrowsAsync<InvalidUsernameException>(() =>
            sut.CreateUser("c1", "bad name", "Password1!"));
    }

    [Fact]
    public async Task CreateUser_InvalidPasswordLength_ThrowsInvalidPasswordException()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);

        await Assert.ThrowsAsync<InvalidPasswordException>(() =>
            sut.CreateUser("c1", "valid_user", "short"));
    }

    [Fact]
    public async Task CreateUser_InvalidPasswordCharacters_ThrowsInvalidPasswordException()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);

        await Assert.ThrowsAsync<InvalidPasswordException>(() =>
            sut.CreateUser("c1", "valid_user", "Password1#"));
    }

    [Fact]
    public async Task CreateUser_WhenUsernameAlreadyExists_ThrowsUsernameTakenException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "valid_user", Password = "x", ConnectionId = "cOld" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<UsernameTakenException>(() =>
            sut.CreateUser("c1", "valid_user", "Password1!"));
    }

    [Fact]
    public async Task CreateUser_ValidInput_PersistsUserWithHashedPassword()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);

        await sut.CreateUser("c1", "valid_user", "Password1!");

        var created = await db.Users.SingleAsync(u => u.Username == "valid_user", TestContext.Current.CancellationToken);
        created.ConnectionId.ShouldBe("c1");
        created.Password.ShouldNotBeNullOrWhiteSpace();
        created.Password.ShouldNotBe("Password1!");
    }

    [Fact]
    public async Task LoginUser_WhenUsernameDoesNotExist_ThrowsInvalidUsernameException()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);

        await Assert.ThrowsAsync<InvalidUsernameException>(() =>
            sut.LoginUser("c1", "missing", "Password1!"));
    }

    [Fact]
    public async Task LoginUser_WhenPasswordIsWrong_ThrowsInvalidPasswordException()
    {
        await using var db = CreateDb();
        var hasher = new PasswordHasher<User>();
        var user = new User { Username = "valid_user", ConnectionId = "cOld" };
        user.Password = hasher.HashPassword(user, "Password1!");
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new RoomManager(db, Mock.Of<IServiceScopeFactory>(), Mock.Of<IWordService>(), hasher);

        await Assert.ThrowsAsync<InvalidPasswordException>(() =>
            sut.LoginUser("c1", "valid_user", "WrongPassword1!"));
    }

    [Fact]
    public async Task LoginUser_WhenPasswordIsCorrect_UpdatesConnectionId()
    {
        await using var db = CreateDb();
        var hasher = new PasswordHasher<User>();
        var user = new User { Username = "valid_user", ConnectionId = "cOld" };
        user.Password = hasher.HashPassword(user, "Password1!");
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new RoomManager(db, Mock.Of<IServiceScopeFactory>(), Mock.Of<IWordService>(), hasher);

        await sut.LoginUser("cNew", "valid_user", "Password1!");

        var updated = await db.Users.SingleAsync(u => u.Username == "valid_user", TestContext.Current.CancellationToken);
        updated.ConnectionId.ShouldBe("cNew");
    }

    [Fact]
    public async Task GetCurrentUserDataAsync_WhenConnectionIdDoesNotExist_ThrowsInvalidConnectionIdException()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);

        await Assert.ThrowsAsync<InvalidConnectionIdException>(() =>
            sut.GetCurrentUserDataAsync("missing-connection"));
    }

    [Fact]
    public async Task GetCurrentUserDataAsync_WhenUserIsNotInRoom_ReturnsRoomCodeMinus1()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        var (username, roomCode) = await sut.GetCurrentUserDataAsync("c1");

        username.ShouldBe("u1");
        roomCode.ShouldBe("-1");
    }

    [Fact]
    public async Task CreateRoom_WhenConnectionIdIsUnknown_ThrowsForbiddenException()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.CreateRoom("missing", "Room1", "Anything", 10, 15));
    }

    [Fact]
    public async Task CreateRoom_WhenAlreadyJoinedRoom_ThrowsJoinedDifferentRoomException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.RoomMembers.Add(new RoomMember { Username = "u1", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        db.Categories.Add(new Category { Id = 1, Name = "Cat1" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<JoinedDifferentRoomException>(() =>
            sut.CreateRoom("c1", "Room1", "Cat1", 10, 15));
    }

    [Fact]
    public async Task CreateRoom_WhenRoomNameHasInvalidChars_ThrowsIncorrectRoomNameException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.Categories.Add(new Category { Id = 1, Name = "Cat1" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<IncorrectRoomNameException>(() =>
            sut.CreateRoom("c1", "Bad#Name", "Cat1", 10, 15));
    }

    [Fact]
    public async Task CreateRoom_WhenCategoryDoesNotExist_ThrowsIncorrectRoomCategoryException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<IncorrectRoomCategoryException>(() =>
            sut.CreateRoom("c1", "Room1", "MissingCategory", 10, 15));
    }

    [Fact]
    public async Task CreateRoom_WhenRoundsTooLow_ThrowsIncorrectRoundAmountException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.Categories.Add(new Category { Id = 1, Name = "Cat1" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<IncorrectRoundAmountException>(() =>
            sut.CreateRoom("c1", "Room1", "Cat1", 9, 15));
    }

    [Fact]
    public async Task CreateRoom_WhenRoundsTooHigh_ThrowsIncorrectRoundAmountException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.Categories.Add(new Category { Id = 1, Name = "Cat1" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<IncorrectRoundAmountException>(() =>
            sut.CreateRoom("c1", "Room1", "Cat1", 31, 15));
    }

    [Fact]
    public async Task CreateRoom_WhenRoundDurationTooLow_ThrowsIncorrectRoundDurationException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.Categories.Add(new Category { Id = 1, Name = "Cat1" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<IncorrectRoundDurationException>(() =>
            sut.CreateRoom("c1", "Room1", "Cat1", 10, 14));
    }

    [Fact]
    public async Task CreateRoom_WhenRoundDurationTooHigh_ThrowsIncorrectRoundDurationException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.Categories.Add(new Category { Id = 1, Name = "Cat1" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<IncorrectRoundDurationException>(() =>
            sut.CreateRoom("c1", "Room1", "Cat1", 10, 46));
    }

    [Fact]
    public async Task JoinRoom_WhenRoomDoesNotExist_ThrowsIncorrectRoomCodeException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<IncorrectRoomCodeException>(() =>
            sut.JoinRoom("c1", "NOPE00"));
    }

    [Fact]
    public async Task JoinRoom_WhenAlreadyMemberInDifferentRoom_ThrowsJoinedDifferentRoomException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.RoomMembers.Add(new RoomMember { Username = "u1", RoomCode = "OLD000", Role = "Player", GameScore = 0 });
        db.Categories.Add(new Category { Id = 1, Name = "Cat1" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "NEW111", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "u1", GameStarted = false, CurrentRound = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<JoinedDifferentRoomException>(() =>
            sut.JoinRoom("c1", "NEW111"));
    }

    [Fact]
    public async Task JoinRoom_WhenGameAlreadyStarted_ThrowsRoomGameStartedException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.Categories.Add(new Category { Id = 1, Name = "Cat1" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "u1", GameStarted = true, CurrentRound = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<RoomGameStartedException>(() =>
            sut.JoinRoom("c1", "ABC123"));
    }

    [Fact]
    public async Task JoinRoom_WhenValid_AddsRoomMemberAndReturnsPlayers()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.Categories.Add(new Category { Id = 1, Name = "Cat1" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "u1", GameStarted = false, CurrentRound = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        var (username, roomName, category, rounds, roundDuration, roomCreator, players) = await sut.JoinRoom("c1", "ABC123");

        username.ShouldBe("u1");
        roomName.ShouldBe("Room");
        category.ShouldBe("Cat1");
        rounds.ShouldBe(10);
        roundDuration.ShouldBe(15);
        roomCreator.ShouldBe("u1");
        players.ShouldContain("u1");

        var member = await db.RoomMembers.SingleAsync(rm => rm.Username == "u1" && rm.RoomCode == "ABC123", TestContext.Current.CancellationToken);
        member.Role.ShouldBe("Player");
    }

    [Fact]
    public async Task StartGame_WhenNotRoomCreator_ThrowsForbiddenException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "creator", ConnectionId = "cCreator", Password = "x" });
        db.Users.Add(new User { Username = "player", ConnectionId = "cPlayer", Password = "x" });
        db.Categories.Add(new Category { Id = 1, Name = "Cat1" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "creator", GameStarted = false, CurrentRound = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "creator", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "player", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "p2", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.StartGame("cPlayer"));
    }

    [Fact]
    public async Task StartGame_WhenNotEnoughPlayers_ThrowsNotEnoughPlayersException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "creator", ConnectionId = "cCreator", Password = "x" });
        db.Categories.Add(new Category { Id = 1, Name = "Cat1" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "creator", GameStarted = false, CurrentRound = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "creator", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "p2", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<NotEnoughPlayersException>(() =>
            sut.StartGame("cCreator"));
    }

    [Fact]
    public async Task StartGame_Valid_SetsGameStartedTrueAndReturnsRoomCode()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "creator", ConnectionId = "cCreator", Password = "x" });
        db.Users.Add(new User { Username = "p2", ConnectionId = "c2", Password = "x" });
        db.Users.Add(new User { Username = "p3", ConnectionId = "c3", Password = "x" });
        db.Categories.Add(new Category { Id = 1, Name = "Cat1" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "creator", GameStarted = false, CurrentRound = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "creator", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "p2", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "p3", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        var code = await sut.StartGame("cCreator");

        code.ShouldBe("ABC123");
        (await db.ActiveRooms.SingleAsync(r => r.RoomCode == "ABC123", TestContext.Current.CancellationToken)).GameStarted.ShouldBeTrue();
    }

    [Fact]
    public async Task GetUserData_WhenSenderConnectionIdUnknown_ThrowsForbiddenException()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetUserData("someone", "missing"));
    }

    [Fact]
    public async Task GetUserData_WhenTargetUserDoesNotExist_ThrowsForbiddenException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "sender", ConnectionId = "c1", Password = "x" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetUserData("missingUser", "c1"));
    }

    [Fact]
    public async Task GetUserData_WhenUsersNotInSameRoom_ThrowsForbiddenException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "sender", ConnectionId = "c1", Password = "x" });
        db.Users.Add(new User { Username = "target", ConnectionId = "c2", Password = "x" });
        db.RoomMembers.Add(new RoomMember { Username = "sender", RoomCode = "R1", Role = "Player", GameScore = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "target", RoomCode = "R2", Role = "Player", GameScore = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetUserData("target", "c1"));
    }

    [Fact]
    public async Task LeftRoom_WhenNotInRoom_ThrowsForbiddenException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.LeftRoom("c1"));
    }

    [Fact]
    public async Task LeftRoom_WhenCreatorLeavesBeforeGameStarted_RemovesRoomAndReturnsIsGameEndedTrue()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "creator", ConnectionId = "cCreator", Password = "x" });
        db.Users.Add(new User { Username = "p2", ConnectionId = "c2", Password = "x" });
        db.Users.Add(new User { Username = "p3", ConnectionId = "c3", Password = "x" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "creator", GameStarted = false, CurrentRound = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "creator", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "p2", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "p3", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        var (username, roomCode, isGameEnded) = await sut.LeftRoom("cCreator");

        username.ShouldBe("creator");
        roomCode.ShouldBe("ABC123");
        isGameEnded.ShouldBeTrue();

        (await db.ActiveRooms.AnyAsync(r => r.RoomCode == "ABC123", TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await db.RoomMembers.AnyAsync(rm => rm.RoomCode == "ABC123", TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task LeftRoom_WhenGameStartedAndRemainingPlayersLessThan3_EndsGameAndRemovesRoom()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.Users.Add(new User { Username = "u2", ConnectionId = "c2", Password = "x" });
        db.Users.Add(new User { Username = "u3", ConnectionId = "c3", Password = "x" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "u1", GameStarted = true, CurrentRound = 1 });
        db.RoomMembers.Add(new RoomMember { Username = "u1", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "u2", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "u3", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        var (_, roomCode, ended) = await sut.LeftRoom("c3");

        roomCode.ShouldBe("ABC123");
        ended.ShouldBeTrue();
        (await db.ActiveRooms.AnyAsync(r => r.RoomCode == "ABC123", TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task LeftRoom_WhenGameStartedAndRemainingPlayersAtLeast3_DoesNotEndGame()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.Users.Add(new User { Username = "u2", ConnectionId = "c2", Password = "x" });
        db.Users.Add(new User { Username = "u3", ConnectionId = "c3", Password = "x" });
        db.Users.Add(new User { Username = "u4", ConnectionId = "c4", Password = "x" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "u1", GameStarted = true, CurrentRound = 1 });
        db.RoomMembers.Add(new RoomMember { Username = "u1", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "u2", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "u3", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "u4", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        var (_, _, ended) = await sut.LeftRoom("c4");

        ended.ShouldBeFalse();
        (await db.ActiveRooms.AnyAsync(r => r.RoomCode == "ABC123", TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task GetCommander_WhenExistingCommander_ReturnsSameCommander()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "u1", GameStarted = true, CurrentRound = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "u1", RoomCode = "ABC123", Role = "Commander", GameScore = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "u2", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new RoomManager(db, Mock.Of<IServiceScopeFactory>(), Mock.Of<IWordService>(), new PasswordHasher<User>());

        var commander = await sut.GetCommander("c1");

        commander.ShouldBe("u1");
    }

    [Fact]
    public async Task GetCommander_WhenNotGameStarted_ThrowsForbiddenException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "u1", GameStarted = false, CurrentRound = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "u1", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetCommander("c1"));
    }

    [Fact]
    public async Task GetCommander_WhenNoCommander_AssignsCommanderIncrementsRoundAndResetsRoomState()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "u1", GameStarted = true, CurrentRound = 0, RoundWord = "X", EmojisSent = true, RoundEnded = true });
        db.RoomMembers.Add(new RoomMember { Username = "u1", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "u2", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        var commander = await sut.GetCommander("c1");

        commander.ShouldNotBeNullOrWhiteSpace();
        var members = await db.RoomMembers.Where(m => m.RoomCode == "ABC123").ToListAsync(TestContext.Current.CancellationToken);
        members.Single(m => m.Role == "Commander").Username.ShouldBe(commander);

        var room = await db.ActiveRooms.SingleAsync(r => r.RoomCode == "ABC123", TestContext.Current.CancellationToken);
        room.RoundWord.ShouldBeNull();
        room.EmojisSent.ShouldBeFalse();
        room.RoundEnded.ShouldBeFalse();
        room.CurrentRound.ShouldBe(1);
    }

    [Fact]
    public async Task GetWord_WhenGameNotStarted_ThrowsForbiddenException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "u1", GameStarted = false, CurrentRound = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "u1", RoomCode = "ABC123", Role = "Commander", GameScore = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var wordService = Mock.Of<IWordService>();
        var sut = CreateSut(db, Mock.Of<IServiceScopeFactory>(), wordService);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetWord("c1"));
    }

    [Fact]
    public async Task SendEmojis_WhenGameNotStarted_ThrowsForbiddenException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "u1", GameStarted = false, CurrentRound = 0 });
        db.RoomMembers.Add(new RoomMember { Username = "u1", RoomCode = "ABC123", Role = "Commander", GameScore = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.SendEmojis("c1"));
    }

    [Fact]
    public async Task CheckWord_WhenCorrect_AddsScoreAndReturnsTrue()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "u1", GameStarted = true, CurrentRound = 0, RoundWord = "APPLE", EmojisSent = true, RoundEnded = false });
        db.RoomMembers.Add(new RoomMember { Username = "u1", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        var (isCorrect, roomCode) = await sut.CheckWord("c1", "apple");

        isCorrect.ShouldBeTrue();
        roomCode.ShouldBe("ABC123");
        var member = await db.RoomMembers.SingleAsync(rm => rm.Username == "u1", TestContext.Current.CancellationToken);
        member.GuessedWord.ShouldBe("apple");
        member.GuessedRight.ShouldBeTrue();
        member.GameScore.ShouldBe(100);
    }

    [Fact]
    public async Task CheckWord_WhenIncorrect_DoesNotAddScoreAndReturnsFalse()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "u1", GameStarted = true, CurrentRound = 0, RoundWord = "APPLE", EmojisSent = true, RoundEnded = false });
        db.RoomMembers.Add(new RoomMember { Username = "u1", RoomCode = "ABC123", Role = "Player", GameScore = 10 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        var (isCorrect, _) = await sut.CheckWord("c1", "banana");

        isCorrect.ShouldBeFalse();
        (await db.RoomMembers.SingleAsync(rm => rm.Username == "u1", TestContext.Current.CancellationToken)).GameScore.ShouldBe(10);
    }

    [Fact]
    public async Task CheckWord_WhenEmojisNotSent_ThrowsForbiddenException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "u1", GameStarted = true, CurrentRound = 0, RoundWord = "APPLE", EmojisSent = false, RoundEnded = false });
        db.RoomMembers.Add(new RoomMember { Username = "u1", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.CheckWord("c1", "apple"));
    }

    [Fact]
    public async Task CheckWord_WhenRoundEnded_ThrowsForbiddenException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "u1", GameStarted = true, CurrentRound = 0, RoundWord = "APPLE", EmojisSent = true, RoundEnded = true });
        db.RoomMembers.Add(new RoomMember { Username = "u1", RoomCode = "ABC123", Role = "Player", GameScore = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.CheckWord("c1", "apple"));
    }

    [Fact]
    public async Task CheckWord_WhenCallerIsCommander_ThrowsForbiddenException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "u1", GameStarted = true, CurrentRound = 0, RoundWord = "APPLE", EmojisSent = true, RoundEnded = false });
        db.RoomMembers.Add(new RoomMember { Username = "u1", RoomCode = "ABC123", Role = "Commander", GameScore = 0 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.CheckWord("c1", "apple"));
    }

    [Fact]
    public async Task GetResults_WhenNotRoundEndedAndAllPlayersGuessed_MarksRoundEndedAndReturnsResults()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 2, RoundDuration = 15, RoomCreator = "u1", GameStarted = true, CurrentRound = 1, RoundEnded = false });
        db.RoomMembers.Add(new RoomMember { Username = "u1", RoomCode = "ABC123", Role = "Player", GameScore = 50, GuessedWord = "x", GuessedRight = false });
        db.RoomMembers.Add(new RoomMember { Username = "u2", RoomCode = "ABC123", Role = "Player", GameScore = 150, GuessedWord = "y", GuessedRight = true });
        db.RoomMembers.Add(new RoomMember { Username = "cmd", RoomCode = "ABC123", Role = "Commander", GameScore = 0, GuessedWord = null, GuessedRight = false });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        var (results, nextRound) = await sut.GetResults("c1");

        results.Count.ShouldBe(3);
        nextRound.ShouldBeTrue();
        (await db.ActiveRooms.SingleAsync(r => r.RoomCode == "ABC123", TestContext.Current.CancellationToken)).RoundEnded.ShouldBeTrue();
    }

    [Fact]
    public async Task GetResults_WhenNotAllPlayersGuessed_ThrowsForbiddenException()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.ActiveRooms.Add(new ActiveRoom { RoomCode = "ABC123", RoomName = "Room", CategoryId = 1, Rounds = 10, RoundDuration = 15, RoomCreator = "u1", GameStarted = true, CurrentRound = 1, RoundEnded = false });
        db.RoomMembers.Add(new RoomMember { Username = "u1", RoomCode = "ABC123", Role = "Player", GameScore = 0, GuessedWord = null });
        db.RoomMembers.Add(new RoomMember { Username = "cmd", RoomCode = "ABC123", Role = "Commander", GameScore = 0, GuessedWord = null });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetResults("c1"));
    }

    [Fact]
    public async Task GetResults_WhenRoundNotEnded_ReturnsOrderedResultsAndDoesNotResetRoundState()
    {
        // Arrange
        await using var db = CreateDb();
        db.Users.Add(new User { Username = "u1", ConnectionId = "c1", Password = "x" });
        db.ActiveRooms.Add(new ActiveRoom
        {
            RoomCode = "ABC123",
            RoomName = "Room",
            CategoryId = 1,
            Rounds = 2,
            RoundDuration = 15,
            RoomCreator = "u1",
            GameStarted = true,
            CurrentRound = 1,
            RoundEnded = false
        });
        db.RoomMembers.Add(new RoomMember { Username = "u1", RoomCode = "ABC123", Role = "Player", GameScore = 50, GuessedWord = "x", GuessedRight = false });
        db.RoomMembers.Add(new RoomMember { Username = "u2", RoomCode = "ABC123", Role = "Player", GameScore = 150, GuessedWord = "y", GuessedRight = true });
        db.RoomMembers.Add(new RoomMember { Username = "cmd", RoomCode = "ABC123", Role = "Commander", GameScore = 0, GuessedWord = null, GuessedRight = false });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = CreateSut(db);

        // Act
        var (results, nextRound) = await sut.GetResults("c1");

        // Assert
        results.Count.ShouldBe(3);
        results[0].username.ShouldBe("u2");

        // CurrentRound(1) < Rounds(2) => next round is available
        nextRound.ShouldBeTrue();

        // With all non-commander players having guessed, RoomManager marks the round ended.
        (await db.ActiveRooms.SingleAsync(r => r.RoomCode == "ABC123", TestContext.Current.CancellationToken)).RoundEnded.ShouldBeTrue();
    }
}