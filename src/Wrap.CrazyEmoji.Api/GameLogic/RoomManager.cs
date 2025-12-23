using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wrap.CrazyEmoji.Api.Abstractions;
using Wrap.CrazyEmoji.Api.Data;
using Wrap.CrazyEmoji.Api.GameLogic.Exceptions;

namespace Wrap.CrazyEmoji.Api.GameLogic;

public class RoomManager(
    GameDbContext db,
    IServiceScopeFactory scopeFactory,
    IWordService wordService,
    IPasswordHasher<Data.Entities.User> hasher) : IRoomManager
{
    private static readonly Random RandomGenerator = Random.Shared;

    public async Task CreateUser(string connectionId, string username, string password)
    {
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

        var userExists = await db.Users.AnyAsync(u => u.Username == username);
        if (userExists)
        {
            throw new UsernameTakenException();
        }

        var user = new Data.Entities.User
        {
            Username = username,
            ConnectionId = connectionId
        };
        user.Password = hasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    public async Task LoginUser(string connectionId, string username, string password)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username)
            ?? throw new InvalidUsernameException();

        var result = hasher.VerifyHashedPassword(user, user.Password, password);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new InvalidPasswordException();
        }

        user.ConnectionId = connectionId;
        await db.SaveChangesAsync();
    }

    public async Task<(string username, string roomCode)> GetCurrentUserDataAsync(string connectionId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new InvalidConnectionIdException();
        }

        var roomMember = await db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);

        var roomCode = roomMember?.RoomCode ?? "-1";

        return (user.Username, roomCode);
    }

    public async Task GetUserData(string username, string connectionId)
    {
        var userSend = await db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (userSend == null)
        {
            throw new ForbiddenException();
        }

        var userGet = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (userGet == null)
        {
            throw new ForbiddenException();
        }

        var roomMemberGet = await db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == userGet.Username);
        if (roomMemberGet == null)
        {
            throw new ForbiddenException();
        }

        var roomMemberSend = await db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == userSend.Username);
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
        var user = await db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new ForbiddenException();
        }

        var roomMember = await db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);
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

        var categorySet = await db.Categories.FirstOrDefaultAsync(c => c.Name == category);
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

        var rooms = await db.ActiveRooms
            .Select(ar => ar.RoomCode)
            .ToListAsync();

        var roomCode = GenerateUniqueRoomCode(rooms);

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

        db.ActiveRooms.Add(activeRoom);
        await db.SaveChangesAsync();

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
        var user = await db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new ForbiddenException();
        }

        var member = await db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);
        if (member != null && member.RoomCode != roomCode)
        {
            throw new JoinedDifferentRoomException();
        }

        var activeRoom = await db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
        if (activeRoom == null)
        {
            throw new IncorrectRoomCodeException();
        }

        if (activeRoom.GameStarted)
        {
            throw new RoomGameStartedException();
        }

        if (member is null)
        {
            var roomMember = new Data.Entities.RoomMember
            {
                RoomCode = roomCode,
                Username = user.Username,
                Role = "Player",
                GameScore = 0
            };

            db.RoomMembers.Add(roomMember);
            await db.SaveChangesAsync();
        }

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == activeRoom.CategoryId);

        var players = await db.RoomMembers
            .Where(rm => rm.RoomCode == roomCode)
            .Select(rm => rm.Username)
            .ToListAsync();

        return (user.Username, activeRoom.RoomName, category.Name, activeRoom.Rounds, activeRoom.RoundDuration, activeRoom.RoomCreator, players);
    }

    public async Task<(string username, string roomCode, bool isGameEnded)> LeftRoom(string connectionId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new ForbiddenException();
        }

        var roomMember = await db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);
        if (roomMember == null)
        {
            throw new ForbiddenException();
        }

        bool isGameEnded = false;

        var activeRoom = await db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomMember.RoomCode)
            ?? throw new ForbiddenException();

        db.RoomMembers.Remove(roomMember);
        await db.SaveChangesAsync();

        var remainingPlayers = await db.RoomMembers
            .Where(rm => rm.RoomCode == roomMember.RoomCode)
            .ToListAsync();

        if (!activeRoom.GameStarted)
        {
            if (activeRoom.RoomCreator == user.Username) isGameEnded = true;
        }
        else
        {
            if (remainingPlayers.Count < 3) isGameEnded = true;
        }

        if (isGameEnded)
        {
            db.ActiveRooms.Remove(activeRoom);
            db.RoomMembers.RemoveRange(remainingPlayers);
            await db.SaveChangesAsync();
        }

        return (user.Username, roomMember.RoomCode, isGameEnded);
    }

    public async Task<string> StartGame(string connectionId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new ForbiddenException();
        }

        var roomMember = await db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);
        if (roomMember == null)
        {
            throw new ForbiddenException();
        }

        var activeRoom = await db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomMember.RoomCode);
        if (activeRoom.RoomCreator != user.Username)
        {
            throw new ForbiddenException();
        }

        if (activeRoom.GameStarted)
        {
            throw new RoomGameStartedException();
        }

        var playerCount = await db.RoomMembers
            .Where(rm => rm.RoomCode == roomMember.RoomCode)
            .CountAsync();

        if (playerCount < 3)
        {
            throw new NotEnoughPlayersException();
        }

        activeRoom.GameStarted = true;
        await db.SaveChangesAsync();

        return activeRoom.RoomCode;
    }

    public async Task<string> GetCommander(string connectionId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new ForbiddenException();
        }

        var roomMember = await db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);
        if (roomMember == null)
        {
            throw new ForbiddenException();
        }

        var activeRoom = await db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomMember.RoomCode)
            ?? throw new ForbiddenException();

        if (!activeRoom.GameStarted)
        {
            throw new ForbiddenException();
        }

        var members = await db.RoomMembers
            .Where(m => m.RoomCode == activeRoom.RoomCode)
            .ToListAsync();

        var existingCommander = members.FirstOrDefault(m => m.Role == "Commander");
        if (existingCommander is not null)
        {
            return existingCommander.Username;
        }

        var commander = members[RandomGenerator.Next(members.Count)];
        commander.Role = "Commander";
        activeRoom.RoundWord = null;
        activeRoom.EmojisSent = false;
        activeRoom.RoundEnded = false;
        activeRoom.CurrentRound++;

        await db.SaveChangesAsync();

        return commander.Username;
    }

    public async Task<string> GetWord(string connectionId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new ForbiddenException();
        }

        var roomMember = await db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);
        if (roomMember == null)
        {
            throw new ForbiddenException();
        }

        var activeRoom = await db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomMember.RoomCode);
        if (!activeRoom.GameStarted)
        {
            throw new ForbiddenException();
        }

        if (roomMember.Role != "Commander")
        {
            throw new ForbiddenException();
        }

        var word = await wordService.GetRandomWordAsync(activeRoom.CategoryId ?? 1);

        activeRoom.RoundWord = word;
        await db.SaveChangesAsync();

        return word;
    }

    public async Task<string> SendEmojis(string connectionId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new ForbiddenException();
        }

        var roomMember = await db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);
        if (roomMember == null)
        {
            throw new ForbiddenException();
        }

        var activeRoom = await db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomMember.RoomCode);
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
        await db.SaveChangesAsync();

        StartRoundTimer(activeRoom.RoomCode);

        return roomMember.RoomCode;
    }

    public async Task<(bool isCorrect, string roomCode)> CheckWord(string connectionId, string word)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
        if (user == null)
        {
            throw new ForbiddenException();
        }

        var roomMember = await db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username);
        if (roomMember == null)
        {
            throw new ForbiddenException();
        }

        var activeRoom = await db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomMember.RoomCode);
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

        await db.SaveChangesAsync();

        return (isCorrect, roomMember.RoomCode);
    }

    private void StartRoundTimer(string roomCode)
    {
        Task.Run(async () =>
        {
            while (true)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var scopedDb = scope.ServiceProvider.GetRequiredService<GameDbContext>();

                var room = await scopedDb.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
                if (room is null) break;

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
        await using var scope = scopeFactory.CreateAsyncScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var room = await scopedDb.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
        if (room == null) return;

        room.RoundEnded = true;

        room.EmojisSentTime = null;
        await scopedDb.SaveChangesAsync();
    }

    public async Task<(List<RoundResult> results, bool nextRound)> GetResults(string connectionId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.ConnectionId == connectionId) ??
            throw new ForbiddenException();

        var roomMember = await db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == user.Username)
            ?? throw new ForbiddenException();

        var activeRoom = await db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == roomMember.RoomCode)
            ?? throw new ForbiddenException();

        if (!activeRoom.GameStarted)
        {
            throw new ForbiddenException();
        }

        var members = await db.RoomMembers
            .Where(m => m.RoomCode == roomMember.RoomCode)
            .ToListAsync();

        if (!activeRoom.RoundEnded)
        {
            bool allPlayersGuessed = members
                .Where(m => m.Role != "Commander")
                .All(m => !string.IsNullOrEmpty(m.GuessedWord));

            if (!allPlayersGuessed)
                throw new ForbiddenException();

            activeRoom.RoundEnded = true;
            await db.SaveChangesAsync();
        }

        if (!activeRoom.RoundEnded)
        {
            bool allPlayersGuessed = members
                .Where(m => m.Role != "Commander")
                .All(m => !string.IsNullOrEmpty(m.GuessedWord));

            if (!allPlayersGuessed)
                throw new ForbiddenException();

            activeRoom.RoundEnded = true;
            await _db.SaveChangesAsync();
        }

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

        foreach (var m in members)
        {
            m.Role = "Player";
            m.GuessedWord = null;
            m.GuessedRight = false;
        }

        await db.SaveChangesAsync();

        return (results, nextRound);
    }
}