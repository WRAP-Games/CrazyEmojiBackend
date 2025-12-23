using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Wrap.CrazyEmoji.Api.Abstractions;
using Wrap.CrazyEmoji.Api.Data;
using Wrap.CrazyEmoji.Api.GameLogic.Exceptions;

namespace Wrap.CrazyEmoji.Api.GameLogic;

public class RoomManager : IRoomManager
{
    private readonly IHubContext<RoomHub> _hubContext;
    private readonly IDbWordService _wordService;
    private readonly ILogger<RoomManager> _logger;
    private readonly ConcurrentDictionary<string, string> _currentWords = new();
    private readonly ConcurrentDictionary<string, bool> _emojisSent = new();
    private readonly ConcurrentDictionary<string, int> _roomRounds = new();

    private readonly IDbContextFactory<GameDbContext> _dbFactory;
    private static readonly Random RandomGenerator = Random.Shared;

    public RoomManager(
        IHubContext<RoomHub> hubContext,
        ILogger<RoomManager> logger,
        IDbContextFactory<GameDbContext> dbFactory,
        IDbWordService wordService)
    {
        _hubContext = hubContext;
        _logger = logger;
        _dbFactory = dbFactory;
        _wordService = wordService;
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

        var rooms = await _db.ActiveRooms
            .Select(ar => ar.RoomCode)
            .ToListAsync();

        var roomCode = GenerateUniqueRoomCode(rooms);

        await _wordService.LoadWordsForRoomAsync(roomCode, categorySet.Id, rounds);

        var activeRoom = new Data.Entities.ActiveRoom
        {
            RoomCode = roomCode,
            RoomName = roomName,
            CategoryId = categorySet.Id,
            Rounds = rounds,
            RoundDuration = roundDuration,
            RoomCreator = user.Username,
            GameStarted = false
        };

        _db.ActiveRooms.Add(activeRoom);
        await _db.SaveChangesAsync();

        return roomCode;
    }

    private string GenerateUniqueRoomCode(List<string> existingRoomCodes)
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
        while (existingRoomCodes.Contains(roomCode) && attempts < maxAttempts);

        if (attempts >= maxAttempts)
            throw new InvalidOperationException("Unable to generate unique room code after maximum attempts.");

        return roomCode;
    }

    public async Task<(string username, string roomName, string category, int rounds, int roundDuration, string roomCreator, List<string> players)> JoinRoom(string connectionId, string roomCode)
    {
        await using var _db = await _dbFactory.CreateDbContextAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new ForbiddenException();
        }

        var member = await _db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);
        if (member != null)
        {
            throw new JoinedDifferentRoomException();
        }

        var activeRoom = await _db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
        if (activeRoom == null)
        {
            throw new IncorrectRoomCodeException();
        }

        if (activeRoom.GameStarted)
        {
            throw new RoomGameStartedException();
        }

        var roomMember = new Data.Entities.RoomMember
        {
            RoomCode = roomCode,
            Username = user.Username,
            Role = "Player",
            GameScore = 0
        };

        _db.RoomMembers.Add(roomMember);
        await _db.SaveChangesAsync();

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == activeRoom.CategoryId);

        var players = await _db.RoomMembers
            .Where(rm => rm.RoomCode == roomCode)
            .Select(rm => rm.Username)
            .ToListAsync();

        return (user.Username, activeRoom.RoomName, category.Name, activeRoom.Rounds, activeRoom.RoundDuration, activeRoom.RoomCreator, players);
    }

    public async Task<(string username, string roomCode, bool isGameEnded)> LeftRoom(string connectionId)
    {
        await using var _db = await _dbFactory.CreateDbContextAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new ForbiddenException();
        }

        var roomMember = await _db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);
        if (roomMember == null)
        {
            throw new ForbiddenException();
        }

        bool isGameEnded = false;

        var activeRoom = await _db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomMember.RoomCode);
        if (activeRoom.RoomCreator == user.Username && !activeRoom.GameStarted)
        {
            isGameEnded = true;
        }

        var playerCount = await _db.RoomMembers
            .Where(rm => rm.RoomCode == roomMember.RoomCode)
            .CountAsync();

        if (playerCount < 3 && activeRoom.GameStarted)
        {
            isGameEnded = true;
        }

        _db.RoomMembers.Remove(roomMember);
        await _db.SaveChangesAsync();

        if (isGameEnded)
        {
            _db.ActiveRooms.Remove(activeRoom);
            var remainingMembers = _db.RoomMembers.Where(rm => rm.RoomCode == roomMember.RoomCode);
            _db.RoomMembers.RemoveRange(remainingMembers);
            await _db.SaveChangesAsync();
        }

        return (user.Username, roomMember.RoomCode, isGameEnded);
    }

    public async Task<string> StartGame(string connectionId)
    {
        await using var _db = await _dbFactory.CreateDbContextAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new ForbiddenException();
        }

        var roomMember = await _db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);
        if (roomMember == null)
        {
            throw new ForbiddenException();
        }

        var activeRoom = await _db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomMember.RoomCode);
        if (activeRoom.RoomCreator != user.Username)
        {
            throw new ForbiddenException();
        }

        if (activeRoom.GameStarted)
        {
            throw new RoomGameStartedException();
        }

        var playerCount = await _db.RoomMembers
            .Where(rm => rm.RoomCode == roomMember.RoomCode)
            .CountAsync();

        if (playerCount < 3)
        {
            throw new NotEnoughPlayersException();
        }

        activeRoom.GameStarted = true;
        await _db.SaveChangesAsync();

        return activeRoom.RoomCode;
    }

    public async Task<string> GetCommander(string connectionId)
    {
        await using var _db = await _dbFactory.CreateDbContextAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new ForbiddenException();
        }

        var roomMember = await _db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);
        if (roomMember == null)
        {
            throw new ForbiddenException();
        }

        var activeRoom = await _db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomMember.RoomCode);
        if (!activeRoom.GameStarted)
        {
            throw new ForbiddenException();
        }

        var players = await _db.RoomMembers
            .Where(rm => rm.RoomCode == roomMember.RoomCode)
            .Select(rm => rm.Username)
            .ToListAsync();

        int commanderIndex = RandomGenerator.Next(players.Count);
        string commanderUsername = players[commanderIndex];
        var commanderMember = await _db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == commanderUsername && rm.RoomCode == roomMember.RoomCode);
        commanderMember.Role = "Commander";

        activeRoom.EmojisSent = false;

        activeRoom.CurrentRound++;

        await _db.SaveChangesAsync();

        return commanderUsername;
    }

    public async Task<string> GetWord(string connectionId)
    {
        await using var _db = await _dbFactory.CreateDbContextAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new ForbiddenException();
        }

        var roomMember = await _db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);
        if (roomMember == null)
        {
            throw new ForbiddenException();
        }

        var activeRoom = await _db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomMember.RoomCode);
        if (!activeRoom.GameStarted)
        {
            throw new ForbiddenException();
        }

        if (roomMember.Role != "Commander")
        {
            throw new ForbiddenException();
        }

        var word = _wordService.GetWord(roomMember.RoomCode);

        activeRoom.RoundWord = word;
        await _db.SaveChangesAsync();

        return word;
    }

    public async Task<string> SendEmojis(string connectionId)
    {
        await using var _db = await _dbFactory.CreateDbContextAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new ForbiddenException();
        }

        var roomMember = await _db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);
        if (roomMember == null)
        {
            throw new ForbiddenException();
        }

        var activeRoom = await _db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomMember.RoomCode);
        if (!activeRoom.GameStarted)
        {
            throw new ForbiddenException();
        }

        if (roomMember.Role == "Player")
        {
            throw new ForbiddenException();
        }

        activeRoom.EmojisSent = true;
        activeRoom.EmojisSentTime = DateTime.Now;
        await _db.SaveChangesAsync();

        StartRoundTimer(activeRoom.RoomCode);

        return roomMember.RoomCode;
    }

    public async Task<(bool isCorrect, string roomCode)> CheckWord(string connectionId, string word)
    {
        await using var _db = await _dbFactory.CreateDbContextAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new ForbiddenException();
        }

        var roomMember = await _db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);
        if (roomMember == null)
        {
            throw new ForbiddenException();
        }

        var activeRoom = await _db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomMember.RoomCode);
        if (!activeRoom.GameStarted)
        {
            throw new ForbiddenException();
        }

        if (roomMember.Role != "Player")
        {
            throw new ForbiddenException();
        }

        if (!activeRoom.EmojisSent)
        {
            throw new ForbiddenException();
        }

        if (activeRoom.RoundEnded)
        {
            throw new ForbiddenException();
        }

        var isCorrect = string.Equals(activeRoom.RoundWord, word, StringComparison.OrdinalIgnoreCase);

        roomMember.GuessedWord = word;
        roomMember.GuessedRight = isCorrect;

        if (isCorrect)
        {
            roomMember.GameScore += 100;
        }

        await _db.SaveChangesAsync();

        return (isCorrect, roomMember.RoomCode);
    }

    public void StartRoundTimer(string roomCode)
    {
        Task.Run(async () =>
        {
            await using var _db = await _dbFactory.CreateDbContextAsync();

            while (true)
            {
                var room = await _db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
                if (room == null) break;

                if (room.EmojisSentTime != null)
                {
                    var elapsed = DateTime.Now - room.EmojisSentTime.Value;
                    if (elapsed.TotalSeconds >= room.RoundDuration)
                    {
                        await EndRoundAsync(roomCode);
                        break;
                    }
                }

                await Task.Delay(500);
            }
        });
    }

    private async Task EndRoundAsync(string roomCode)
    {
        await using var _db = await _dbFactory.CreateDbContextAsync();

        var room = await _db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
        if (room == null) return;

        room.RoundEnded = true;

        room.EmojisSentTime = null;
        await _db.SaveChangesAsync();
    }

    public async Task<(List<RoundResult> results, bool nextRound)> GetResults(string connectionId)
    {
        await using var _db = await _dbFactory.CreateDbContextAsync();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new ForbiddenException();
        }

        var roomMember = await _db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);
        if (roomMember == null)
        {
            throw new ForbiddenException();
        }

        var activeRoom = await _db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomMember.RoomCode);
        if (!activeRoom.GameStarted)
        {
            throw new ForbiddenException();
        }

        if (!activeRoom.RoundEnded)
        {
            throw new ForbiddenException();
        }

        var members = await _db.RoomMembers
            .Where(m => m.RoomCode == roomMember.RoomCode)
            .ToListAsync();

        var results = members.Select(m => new RoundResult
            {
                username = m.Username,
                guessedRight = m.GuessedRight,
                guessedWord = m.GuessedWord,
                gameScore = m.GameScore
            })
            .OrderByDescending(r => r.gameScore)
            .ToList();

        bool nextRound = activeRoom.CurrentRound < activeRoom.Rounds;

        activeRoom.RoundEnded = false;
        activeRoom.RoundWord = null;

        foreach (var m in members)
        {
            m.Role = "Player";
            m.GuessedRight = false;
            m.GuessedWord = "";
        }

        await _db.SaveChangesAsync();

        return (results, nextRound);
    }
}