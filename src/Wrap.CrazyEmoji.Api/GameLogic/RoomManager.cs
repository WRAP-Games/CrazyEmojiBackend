﻿using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Wrap.CrazyEmoji.Api.Abstractions;
using Wrap.CrazyEmoji.Api.Constants;

namespace Wrap.CrazyEmoji.Api.GameLogic;

public class RoomManager(IHubContext<RoomHub> hubContext, IWordService wordService)
{
    private readonly IHubContext<RoomHub> _hubContext = hubContext;
    private readonly IWordService _wordService = wordService;

    private readonly ConcurrentDictionary<string, List<Player>> _rooms = new();
    private readonly ConcurrentDictionary<string, string> _currentWords = new();
    private readonly ConcurrentDictionary<string, bool> _emojisSent = new();
    private readonly ConcurrentDictionary<string, int> _roomRounds = new();

    private static readonly Random RandomGenerator = Random.Shared;

    public Task<bool> CreateRoomAsync(string roomCode)
        => Task.FromResult(_rooms.TryAdd(roomCode, new List<Player>()));

    public async Task<bool> AddPlayerAsync(string roomCode, Player player)
    {
        if (!_rooms.TryGetValue(roomCode, out var players))
            return false;

        players.Add(player);
        await _hubContext.Groups.AddToGroupAsync(player.ConnectionId, roomCode);
        return true;
    }

    public async Task RemovePlayerAsync(string connectionId)
    {
        foreach (var room in _rooms)
        {
            if (room.Value.RemoveAll(p => p.ConnectionId == connectionId) > 0)
                await _hubContext.Clients.Group(room.Key)
                    .SendAsync(RoomHubConstants.PlayerLeft, connectionId);
        }
    }

    public async Task<bool> StartGameAsync(string roomCode)
    {
        if (!_rooms.TryGetValue(roomCode, out var players) || players.Count < 3)
        {
            await _hubContext.Clients.Group(roomCode)
                .SendAsync(RoomHubConstants.Error, "Not enough players to start.");
            return false;
        }

        foreach (var player in players)
        {
            player.Points = new Points(0);
            player.GuessedRight = false;
            player.HasGuessed = false;
            player.Role = PlayerRole.Player;
        }

        _roomRounds[roomCode] = 0;

        _ = Task.Run(async () =>
        {
            try
            {
                const int maxRounds = 10;
                while (_roomRounds.TryGetValue(roomCode, out int currentRound) && currentRound < maxRounds)
                {
                    await StartRoundAsync(roomCode);
                    _roomRounds[roomCode] = currentRound + 1;
                }

                await _hubContext.Clients.Group(roomCode)
                    .SendAsync("GameEnded", "The game has ended!");
            }
            catch (Exception ex)
            {
                await _hubContext.Clients.Group(roomCode)
                    .SendAsync(RoomHubConstants.Error, $"An error occurred: {ex.Message}");
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
            await _hubContext.Clients.Group(roomCode)
                .SendAsync(RoomHubConstants.Error, "Not enough players for commander selection.");
            return;
        }

        int commanderIndex = RandomGenerator.Next(players.Count);
        string commanderConnectionId = players[commanderIndex].ConnectionId;
        players[commanderIndex].Role = PlayerRole.Commander;

        await _hubContext.Clients.Client(commanderConnectionId)
            .SendAsync(RoomHubConstants.CommanderSelected, "You have been selected as the commander.");
        await _hubContext.Clients.Group(roomCode)
            .SendAsync(RoomHubConstants.CommanderAnnounced,
                $"Commander is {players[commanderIndex].Username}");
    }

    private async Task SendWordToCommanderAsync(string roomCode)
    {
        if (!_rooms.TryGetValue(roomCode, out var players))
        {
            await _hubContext.Clients.Group(roomCode)
                .SendAsync(RoomHubConstants.Error, "Room not found.");
            return;
        }

        var commander = players.FirstOrDefault(p => p.Role == PlayerRole.Commander);
        if (commander is null)
        {
            await _hubContext.Clients.Group(roomCode)
                .SendAsync(RoomHubConstants.Error, "Commander not found.");
            return;
        }

        var word = await _wordService.GetRandomWordAsync();
        _currentWords[roomCode] = word;
        await _hubContext.Clients.Client(commander.ConnectionId)
            .SendAsync(RoomHubConstants.ReceiveWord, word);
    }

    public async Task SendEmojisAsync(string roomCode, string connectionId, string emojis)
    {
        if (!_rooms.TryGetValue(roomCode, out var players))
        {
            await _hubContext.Clients.Client(connectionId)
                .SendAsync(RoomHubConstants.Error, "Room not found.");
            return;
        }

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
        {
            await _hubContext.Clients.Client(connectionId)
                .SendAsync(RoomHubConstants.Error, "Room not found.");
            return;
        }

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
            await _hubContext.Clients.Client(connectionId)
                .SendAsync(RoomHubConstants.CorrectGuess, "Correct!");
        }
        else
        {
            guesser.GuessedRight = false;
            await _hubContext.Clients.Client(connectionId)
                .SendAsync(RoomHubConstants.IncorrectGuess, "Wrong guess.");
        }
    }

    private async Task SendPointsAsync(string roomCode)
    {
        if (!_rooms.TryGetValue(roomCode, out var players))
            return;

        var nonCommanderPlayers = players.Where(p => p.Role != PlayerRole.Commander).ToList();

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