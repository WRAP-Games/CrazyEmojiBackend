using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Wrap.CrazyEmoji.Api.Abstractions;
using Wrap.CrazyEmoji.Api.Data;
using Wrap.CrazyEmoji.Api.GameLogic.Exceptions;

namespace Wrap.CrazyEmoji.Api.GameLogic;

public class RoomManager
{
    private readonly IHubContext<RoomHub> _hubContext;
    private readonly IWordService _wordService;
    private readonly ILogger<RoomManager> _logger;

    private readonly ConcurrentDictionary<string, List<Player>> _rooms = new();
    private readonly ConcurrentDictionary<string, string> _currentWords = new();
    private readonly ConcurrentDictionary<string, bool> _emojisSent = new();
    private readonly ConcurrentDictionary<string, int> _roomRounds = new();

    private readonly GameCache<Player> _playerCache = new();

    private readonly IDbContextFactory<GameDbContext> _dbFactory;
    private static readonly Random RandomGenerator = Random.Shared;

    public RoomManager(
        IHubContext<RoomHub> hubContext,
        IWordService wordService,
        ILogger<RoomManager> logger,
        IDbContextFactory<GameDbContext> dbFactory)
    {
        _hubContext = hubContext;
        _wordService = wordService;
        _logger = logger;
        _dbFactory = dbFactory;
    }

    public async Task CreateUser(string connectionId, string username, string password)
    {
        await using var _db = await _dbFactory.CreateDbContextAsync();

        if (username.Length < 3 || username.Length > 32)
        {
            throw new InvalidUsernameException();
        }

        if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
        {
            throw new InvalidUsernameException();
        }

        if (password.Length < 8 || password.Length > 32)
        {
            throw new InvalidPasswordException();
        }

        if (!Regex.IsMatch(password, @"^[a-zA-Z0-9@$!%*?&_\-]+$"))
        {
            throw new InvalidPasswordException();
        }

        var userExists = await _db.Users.AnyAsync(u => u.Username == username);
        if (userExists)
        {
            throw new UsernameTakenException();
        }

        var user = new Data.Entities.User
        {
            Username = username,
            Password = password,
            ConnectionId = connectionId
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
    }

    public async Task LoginUser(string connectionId, string username, string password)
    {
        await using var _db = await _dbFactory.CreateDbContextAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username && u.Password == password);
        if (user == null)
        {
            throw new InvalidUsernameException();
        }

        user.ConnectionId = connectionId;
        await _db.SaveChangesAsync();
    }

    public async Task<(string username, string roomCode)> GetCurrentUserDataAsync(string connectionId)
    {
        await using var _db = await _dbFactory.CreateDbContextAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new InvalidConnectionIdException();
        }

        var roomMember = await _db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);

        var roomCode = roomMember?.RoomCode ?? "-1";

        return (user.Username, roomCode);
    }

    public async Task GetUserData(string username, string connectionId)
    {
        await using var _db = await _dbFactory.CreateDbContextAsync();

        var userSend = await _db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (userSend == null)
        {
            throw new ForbiddenException();
        }

        var userGet = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (userGet == null)
        {
            throw new ForbiddenException();
        }

        var roomMemberGet = await _db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == userGet.Username);
        if (roomMemberGet == null)
        {
            throw new ForbiddenException();
        }

        var roomMemberSend = await _db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == userSend.Username);
        if (roomMemberSend == null)
        {
            throw new ForbiddenException();
        }

        if (roomMemberGet.RoomCode != roomMemberSend.RoomCode)
        {
            throw new ForbiddenException();
        }

    }

    public async Task<string> CreateRoom(string connectionId, string roomName, string category, int rounds, int roundDuration)
    {
        await using var _db = await _dbFactory.CreateDbContextAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new ForbiddenException();
        }

        var roomMember = await _db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);
        if (roomMember != null)
        {
            throw new JoinedDifferentRoomException();
        }

        if (roomName.Length < 3 || roomName.Length > 32)
        {
            throw new IncorrectRoomNameException();
        }

        if (!Regex.IsMatch(roomName, @"^[a-zA-Z0-9_ ]+$"))
        {
            throw new IncorrectRoomNameException();
        }

        var categorySet = await _db.Categories.FirstOrDefaultAsync(c => c.Name == category);
        if (categorySet == null)
        {
            throw new IncorrectRoomCategoryException();
        }

        if (rounds < 10 || rounds > 30)
        {
            throw new IncorrectRoundAmountException();
        }

        if (roundDuration < 15 || roundDuration > 45)
        {
            throw new IncorrectRoundDurationException();
        }

        var roomCode = GenerateUniqueRoomCode();

        return roomCode;
    }

    private string GenerateUniqueRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string roomCode;
        int attempts = 0;
        const int maxAttempts = 100;

        do
        {
            roomCode = new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[RandomGenerator.Next(s.Length)]).ToArray());
            attempts++;
        }
        while (_rooms.ContainsKey(roomCode) && attempts < maxAttempts);

        if (attempts >= maxAttempts)
            throw new InvalidOperationException("Unable to generate unique room code after maximum attempts.");

        return roomCode;
    }
}