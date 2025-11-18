using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Wrap.CrazyEmoji.Api.Abstractions;
using Wrap.CrazyEmoji.Api.Constants;
using Wrap.CrazyEmoji.Api.Data;
using Wrap.CrazyEmoji.Api.Data.Entities;
using Wrap.CrazyEmoji.Api.GameLogic.Exceptions;

namespace Wrap.CrazyEmoji.Api.GameLogic;

public class RoomManager
{
    private readonly IHubContext<RoomHub> _hubContext;
    private readonly IWordService _wordService;
    private readonly ILogger<RoomManager> _logger;
    private readonly GameDbContext _dbContext;

    private readonly ConcurrentDictionary<string, List<Player>> _rooms = new();
    private readonly ConcurrentDictionary<string, string> _currentWords = new();
    private readonly ConcurrentDictionary<string, bool> _emojisSent = new();
    private readonly ConcurrentDictionary<string, int> _roomRounds = new();

    private static readonly Random RandomGenerator = Random.Shared;

    public RoomManager(
        IHubContext<RoomHub> hubContext,
        IWordService wordService,
        ILogger<RoomManager> logger,
        GameDbContext dbContext)
    {
        _hubContext = hubContext;
        _wordService = wordService;
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task<string?> CreateRoomAsync(string roomName)
    {
        try
        {
            var roomCode = await GenerateUniqueRoomCodeAsync();

            var gameEntity = new GameEntity
            {
                RoomCode = roomCode,
                RoomName = roomName,
                MaxRound = 5,
                CurrentRound = 0,
                HostId = null
            };

            _dbContext.Games.Add(gameEntity);
            await _dbContext.SaveChangesAsync();

            _rooms.TryAdd(roomCode, new List<Player>());
            _roomRounds.TryAdd(roomCode, 0);

            _logger.LogInformation("Room {RoomCode} created with name '{RoomName}'",
                roomCode, roomName);

            return roomCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create room '{RoomName}'", roomName);
            return null;
        }
    }

    private async Task<string> GenerateUniqueRoomCodeAsync()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string roomCode;
        int attempts = 0;
        const int maxAttempts = 100;
        bool existsInMemory;
        bool existsInDb;

        do
        {
            roomCode = new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[RandomGenerator.Next(s.Length)]).ToArray());
            attempts++;

            existsInMemory = _rooms.ContainsKey(roomCode);
            existsInDb = await _dbContext.Games.AnyAsync(g => g.RoomCode == roomCode);
        }
        while ((existsInMemory || existsInDb) && attempts < maxAttempts);

        if (attempts >= maxAttempts)
            throw new InvalidOperationException("Unable to generate unique room code after maximum attempts.");

        return roomCode;
    }

    public async Task<bool> AddPlayerAsync(string roomCode, Player player)
    {
        try
        {
            var gameEntity = await _dbContext.Games.FirstOrDefaultAsync(g => g.RoomCode == roomCode);
            if (gameEntity == null)
                throw new RoomNotFoundException(roomCode);

            if (!_rooms.TryGetValue(roomCode, out var players))
            {
                var existingUsers = await _dbContext.Users
                    .Where(u => u.RoomCode == roomCode)
                    .ToListAsync();

                players = existingUsers.Select(u => new Player(u.Username, "")).ToList();
                _rooms.TryAdd(roomCode, players);
                _roomRounds.TryAdd(roomCode, gameEntity.CurrentRound);
            }

            var existingUser = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Username == player.Username && u.RoomCode == roomCode);

            if (existingUser == null)
            {
                var userEntity = new UserEntity
                {
                    Username = player.Username,
                    RoomCode = roomCode
                };
                _dbContext.Users.Add(userEntity);

                if (gameEntity.HostId == null)
                {
                    gameEntity.HostId = userEntity.Id;
                }

                await _dbContext.SaveChangesAsync();
            }

            var existingPlayerInMemory = players.FirstOrDefault(p => p.Username == player.Username);
            if (existingPlayerInMemory != null)
            {
                existingPlayerInMemory.ConnectionId = player.ConnectionId;
            }
            else
            {
                players.Add(player);
            }

            await _hubContext.Groups.AddToGroupAsync(player.ConnectionId, roomCode);

            _logger.LogInformation("Player {Player} joined room {RoomCode}", player.Username, roomCode);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add player {Player} to room {RoomCode}", player.Username, roomCode);
            return false;
        }
    }

    public async Task RemovePlayerAsync(string connectionId)
    {
        foreach (var room in _rooms)
        {
            if (room.Value.RemoveAll(p => p.ConnectionId == connectionId) > 0)
            {
                _logger.LogInformation(
                    "Player {ConnectionId} removed from room {RoomCode}",
                    connectionId,
                    room.Key);

                await _hubContext.Clients.Group(room.Key)
                    .SendAsync(RoomHubConstants.PlayerLeft, connectionId);
            }
        }
    }

    public async Task<bool> StartGameAsync(string roomCode)
    {
        if (!_rooms.TryGetValue(roomCode, out var players) || players.Count < 3)
        {
            throw new NotEnoughPlayersException(roomCode, players?.Count ?? 0);
        }

        foreach (var player in players)
        {
            player.Points = new Points(0);
            player.GuessedRight = false;
            player.HasGuessed = false;
            player.Role = PlayerRole.Player;
        }

        _roomRounds[roomCode] = 0;

        await Task.Run(async () =>
        {
            try
            {
                const int maxRounds = 10;
                while (_roomRounds.TryGetValue(roomCode, out int currentRound) && currentRound < maxRounds)
                {
                    _logger.LogInformation("Starting round {Round} in room {RoomCode}", currentRound + 1, roomCode);

                    await StartRoundAsync(roomCode);
                    _roomRounds[roomCode] = currentRound + 1;
                }

                await _hubContext.Clients.Group(roomCode)
                    .SendAsync("GameEnded", "The game has ended!");
            }
            catch (RoomNotFoundException ex)
            {
                _logger.LogWarning(ex, "Room {RoomCode} not found", roomCode);
                await _hubContext.Clients.Group(roomCode)
                    .SendAsync(RoomHubConstants.Error, ex.Message);
            }
            catch (NotEnoughPlayersException ex)
            {
                _logger.LogWarning(ex, "Not enough players in room {RoomCode}", roomCode);
                await _hubContext.Clients.Group(roomCode)
                    .SendAsync(RoomHubConstants.Error, ex.Message);
            }
            catch (CommanderNotFoundException ex)
            {
                _logger.LogWarning(ex, "Commander not found in room {RoomCode}", roomCode);
                await _hubContext.Clients.Group(roomCode)
                    .SendAsync(RoomHubConstants.Error, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in game loop for room {RoomCode}", roomCode);
                await _hubContext.Clients.Group(roomCode)
                    .SendAsync(RoomHubConstants.Error, "An unexpected error occurred.");
            }
        });

        return true;
    }

    private async Task StartRoundAsync(string roomCode)
    {
        _emojisSent[roomCode] = false;

        await SelectCommanderAsync(roomCode);
        await SendWordToCommanderAsync(roomCode);

        while (_emojisSent.TryGetValue(roomCode, out var sent) && !sent)
            await Task.Delay(100);

        if (_rooms.TryGetValue(roomCode, out var players))
        {
            while (players.Where(p => p.Role != PlayerRole.Commander).Any(p => !p.HasGuessed))
                await Task.Delay(100);
        }

        await SendPointsAsync(roomCode);

        var resetPlayers = _rooms[roomCode];
        resetPlayers.ForEach(p =>
        {
            p.HasGuessed = false;
            p.GuessedRight = false;
            p.Role = PlayerRole.Player;
        });
    }

    private async Task SelectCommanderAsync(string roomCode)
    {
        if (!_rooms.TryGetValue(roomCode, out var players) || players.Count < 3)
        {
            throw new NotEnoughPlayersException(roomCode, players?.Count ?? 0);
        }

        int commanderIndex = RandomGenerator.Next(players.Count);
        string commanderConnectionId = players[commanderIndex].ConnectionId;
        players[commanderIndex].Role = PlayerRole.Commander;

        await _hubContext.Clients.Client(commanderConnectionId)
            .SendAsync(RoomHubConstants.CommanderSelected, "You have been selected as the commander.");
        await _hubContext.Clients.GroupExcept(roomCode, commanderConnectionId)
            .SendAsync(RoomHubConstants.CommanderAnnounced,
                $"Commander is {players[commanderIndex].Username}");
    }

    private async Task SendWordToCommanderAsync(string roomCode)
    {
        if (!_rooms.TryGetValue(roomCode, out var players))
        {
            throw new RoomNotFoundException(roomCode);
        }

        var commander = players.FirstOrDefault(p => p.Role == PlayerRole.Commander);
        if (commander is null)
            throw new CommanderNotFoundException(roomCode);

        var word = await _wordService.GetRandomWordAsync();
        _currentWords[roomCode] = word;
        await _hubContext.Clients.Client(commander.ConnectionId)
            .SendAsync(RoomHubConstants.ReceiveWord, word);
    }

    public async Task SendEmojisAsync(string roomCode, string connectionId, string emojis)
    {
        if (!_rooms.TryGetValue(roomCode, out var players))
            throw new RoomNotFoundException(roomCode);

        var commander = players.FirstOrDefault(p => p.ConnectionId == connectionId);
        if (commander == null || commander.Role != PlayerRole.Commander)
        {
            await _hubContext.Clients.Client(connectionId)
                .SendAsync(RoomHubConstants.Error, "Only the commander can send emojis.");
            return;
        }

        _emojisSent[roomCode] = true;
        await _hubContext.Clients.GroupExcept(roomCode, connectionId)
            .SendAsync(RoomHubConstants.ReceiveEmojis, emojis);
    }

    public async Task CheckWordAsync(string roomCode, string connectionId, string word)
    {
        if (!_rooms.TryGetValue(roomCode, out var players))
            throw new RoomNotFoundException(roomCode);

        if (!_emojisSent.TryGetValue(roomCode, out var sent) || !sent)
        {
            await _hubContext.Clients.Client(connectionId)
                .SendAsync(RoomHubConstants.Error, "Wait for commander emojis first.");
            return;
        }

        if (!_currentWords.TryGetValue(roomCode, out var currentWord))
        {
            await _hubContext.Clients.Client(connectionId)
                .SendAsync(RoomHubConstants.Error, "No word set.");
            return;
        }

        var guesser = players.FirstOrDefault(p => p.ConnectionId == connectionId);

        if (guesser == null)
        {
            await _hubContext.Clients.Client(connectionId)
                .SendAsync(RoomHubConstants.Error, "Player not found.");
            return;
        }

        if (guesser.Role == PlayerRole.Commander)
        {
            await _hubContext.Clients.Client(connectionId)
                .SendAsync(RoomHubConstants.Error, "Commanders cannot guess.");
            return;
        }

        if (guesser.HasGuessed)
        {
            await _hubContext.Clients.Client(connectionId)
                .SendAsync(RoomHubConstants.Error, "You already guessed.");
            return;
        }

        guesser.HasGuessed = true;

        if (string.Equals(word, currentWord, StringComparison.OrdinalIgnoreCase))
        {
            guesser.GuessedRight = true;
        }
        else
        {
            guesser.GuessedRight = false;
        }
    }

    private async Task SendPointsAsync(string roomCode)
    {
        if (!_rooms.TryGetValue(roomCode, out var players))
            return;

        var nonCommanderPlayers = players.FindAll(p => p.Role != PlayerRole.Commander);

        foreach (var player in nonCommanderPlayers)
        {
            if (player.GuessedRight)
            {
                player.Points += new Points(100);
                await _hubContext.Clients.Client(player.ConnectionId)
                    .SendAsync(RoomHubConstants.CorrectGuess,
                        $"You earned 100 points! Total: {player.Points}", 100);
            }
            else
            {
                await _hubContext.Clients.Client(player.ConnectionId)
                    .SendAsync(RoomHubConstants.IncorrectGuess,
                        $"No points. Total: {player.Points}", 0);
            }
        }

        if (nonCommanderPlayers.All(p => p.GuessedRight))
        {
            await _hubContext.Clients.Group(roomCode)
                .SendAsync("AllGuessedRight", "All players guessed right!");
        }
        else if (nonCommanderPlayers.All(p => !p.GuessedRight))
        {
            await _hubContext.Clients.Group(roomCode)
                .SendAsync("AllGuessedWrong", "All players guessed wrong!");
        }
        else
        {
            var commander = players.FirstOrDefault(p => p.Role == PlayerRole.Commander);
            if (commander != null)
            {
                commander.Points += new Points(100);
                await _hubContext.Clients.Client(commander.ConnectionId)
                    .SendAsync("CommanderBonus", "Commander gets 100 points!", 100);
            }
        }

        await _hubContext.Clients.Group(roomCode)
            .SendAsync("RoundEnded", "The round has ended!");
    }
}