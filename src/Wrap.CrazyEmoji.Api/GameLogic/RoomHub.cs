using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Wrap.CrazyEmoji.Api.Abstractions;

namespace Wrap.CrazyEmoji.Api.GameLogic;

public class RoomHub(IWordService wordService) : Hub
{
    private static readonly ConcurrentDictionary<string, List<Player>> Rooms = new();
    private static readonly ConcurrentDictionary<string, string> CurrentWords = new();
    private static readonly ConcurrentDictionary<string, bool> EmojisSent = new();

    private const string UsernameSet = "UsernameSet";
    private const string CreatedRoom = "CreatedRoom";
    private const string JoinedRoom = "JoinedRoom";
    private const string GameStarted = "GameStarted";
    private const string ReceiveEmojis = "ReceiveEmojis";
    private const string ReceiveWord = "ReceiveWord";

    private const string CommanderSelected = "CommanderSelected";
    private const string CommanderAnnounced = "CommanderAnnounced";
    private const string CorrectGuess = "CorrectGuess";
    private const string IncorrectGuess = "IncorrectGuess";
    private const string PlayerLeft = "PlayerLeft";
    private const string Error = "Error";

    private const string Username = "Username";
    private const string RoomCode = "RoomCode";

    public async Task SetUsername(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        Context.Items[Username] = username;
        await Clients.Caller.SendAsync(UsernameSet, username);
    }
    public async Task CreateRoom(string roomCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomCode);

        if (!Rooms.TryAdd(roomCode, []))
        {
            await Clients.Caller.SendAsync(Error, "RoomAlreadyExists");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        await Clients.Caller.SendAsync(CreatedRoom, roomCode);
        await JoinRoom(roomCode);
    }

    public async Task JoinRoom(string roomCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomCode);

        if (Rooms.TryGetValue(roomCode, out _))
        {
            Rooms[roomCode].Add(new()
            {
                ConnectionId = Context.ConnectionId,
                Username = Context.Items[Username] as string ?? Context.ConnectionId,
                Role = PlayerRole.Player
            });
            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

            Context.Items[RoomCode] = roomCode;

            await Clients.Caller.SendAsync(JoinedRoom, roomCode);
            return;
        }

        await Clients.Caller.SendAsync(Error, "Room not found");
    }

    public async Task StartGame()
    {
        if (Context.Items[RoomCode] is not string roomCode
                || !Rooms.TryGetValue(roomCode, out var players)
                || players.Count == 0)
        {
            await Clients.Caller.SendAsync(Error, "No players in room to start the game.");
            return;
        }

        if (players.Count < 3)
        {
            await Clients.Caller.SendAsync(Error, "Not enough players to start the game. Minimum 3 players required.");
            return;
        }

        foreach (var player in players)
        {
            player.Points = new Points(0);
            player.GuessedRight = false;
            player.HasGuessed = false;
            player.Role = PlayerRole.Player;
        }

        Context.Items["RoundNumber"] = 0;
        
         _ = Task.Run(async () =>
        {
            int maxRounds = 10;
            while ((Context.Items["RoundNumber"] is int currentRound) && currentRound < maxRounds)
            {
                if (!Rooms.TryGetValue(roomCode, out players)
                    || players.Count == 0)
                {
                    await Clients.Caller.SendAsync(Error, "No players in room to continue the game.");
                    break;
                }
    
                if (players.Count < 3)
                {
                    await Clients.Caller.SendAsync(Error, "Not enough players to continue the game. Minimum 3 players required.");
                    break;
                }
    
                await StartRound(roomCode);
                Context.Items["RoundNumber"] = currentRound + 1;            }
        });
    
        await Clients.Caller.SendAsync(GameStarted, roomCode);
    }

    private async Task StartRound(string roomCode)
    {
        EmojisSent[roomCode] = false;
        await SelectCommander();
        await SendWordToCommander();

        while (EmojisSent.TryGetValue(roomCode, out var sent) && !sent)
        {
            await Task.Delay(100);
        }

        if (Rooms.TryGetValue(roomCode, out var players))
        {
            while (players.Where(p => p.Role != PlayerRole.Commander).Any(p => !p.HasGuessed))
            {
                await Task.Delay(100);
            }
        }
        await SendPoints();

        players!.ForEach(p =>
        {
            p.HasGuessed = false;
            p.GuessedRight = false;
            p.Role = PlayerRole.Player;
        });
    }

    private async Task SelectCommander()
    {
        if (Context.Items[RoomCode] is not string roomCode
            || !Rooms.TryGetValue(roomCode, out var players)
            || players.Count == 0)
        {
            await Clients.Caller.SendAsync(Error, "No players in room to select a commander.");
            return;
        }

        if (players.Count < 3)
        {
            await Clients.Caller.SendAsync(Error, "Not enough players to select a commander. Minimum 3 players required.");
            return;
        }

        var random = new Random();
        int commanderIndex = random.Next(players.Count);
        string commanderConnectionId = players[commanderIndex].ConnectionId;
        players[commanderIndex].Role = PlayerRole.Commander;

        await Clients.Client(commanderConnectionId).SendAsync(CommanderSelected, "You have been selected as the commander.");
        await Clients.Group(roomCode).SendAsync(CommanderAnnounced, $"A commander has been selected. Commander is {players[commanderIndex].Username}");
    }

    private async Task SendWordToCommander()
    {
        if (Context.Items[RoomCode] is not string roomCode
            || !Rooms.TryGetValue(roomCode, out var players))
        {
            await Clients.Caller.SendAsync(Error, "Room not found.");
            return;
        }

        var commander = players.FirstOrDefault(p => p.Role == PlayerRole.Commander);
        if (commander is null)
        {
            await Clients.Caller.SendAsync(Error, "Commander not found in the room.");
            return;
        }

        var word = await wordService.GetRandomWordAsync();
        CurrentWords[roomCode] = word;
        await Clients.Client(commander.ConnectionId).SendAsync(ReceiveWord, word);
    }

    public async Task GetAndSendEmojis(string emojis)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emojis);

        if (Context.Items[RoomCode] is not string roomCode
            || !Rooms.TryGetValue(roomCode, out var players))
        {
            await Clients.Caller.SendAsync(Error, "Room not found.");
            return;
        }

        if (!CurrentWords.TryGetValue(roomCode, out _))
        {
            await Clients.Caller.SendAsync(Error, "Word has not been sent to the commander.");
            return;
        }

        var caller = players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
        if (caller == null || caller.Role != PlayerRole.Commander)
        {
            await Clients.Caller.SendAsync(Error, "Only the commander can send emojis.");
            return;
        }

        EmojisSent[roomCode] = true;

        await Clients.GroupExcept(roomCode, Context.ConnectionId).SendAsync(ReceiveEmojis, emojis);
    }

    public async Task CheckWord(string word)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(word);

        if (Context.Items[RoomCode] is not string roomCode
            || !Rooms.TryGetValue(roomCode, out var players))
        {
            await Clients.Caller.SendAsync(Error, "Room not found.");
            return;
        }

        if (!EmojisSent.TryGetValue(roomCode, out var sent) || !sent)
        {
            await Clients.Caller.SendAsync(Error, "Wait for the commander to send emojis before guessing.");
            return;
        }

        if (!CurrentWords.TryGetValue(roomCode, out var currentWord))
        {
            await Clients.Caller.SendAsync(Error, "No word has been set for this room.");
            return;
        }

        var guesser = players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
        if (guesser == null)
        {
            await Clients.Caller.SendAsync(Error, "Player not found in the room.");
            return;
        }

        if (guesser.Role == PlayerRole.Commander)
        {
            await Clients.Caller.SendAsync(Error, "Commanders cannot submit guesses.");
            return;
        }

        if (guesser.HasGuessed)
        {
            await Clients.Caller.SendAsync(Error, "You can only guess once per round.");
            return;
        }

        guesser.HasGuessed = true;

        if (string.Equals(word, currentWord, StringComparison.OrdinalIgnoreCase))
        {
            guesser.GuessedRight = true;
            await Clients.Caller.SendAsync(CorrectGuess, "Congratulations! You guessed the word correctly.");
        }
        else
        {
            guesser.GuessedRight = false;
            await Clients.Caller.SendAsync(IncorrectGuess, "Your guess was incorrect.");
        }
    }

    private async Task SendPoints()
    {
        if (Context.Items[RoomCode] is not string roomCode
            || !Rooms.TryGetValue(roomCode, out var players))
        {
            await Clients.Caller.SendAsync(Error, "Room not found.");
            return;
        }

        var nonCommanderPlayers = players.Where(p => p.Role != PlayerRole.Commander).ToList();
        foreach (var player in nonCommanderPlayers)
        {
            if (player.GuessedRight)
            {
                player.Points += new Points(100);
                await Clients.Client(player.ConnectionId).SendAsync(CorrectGuess, $"You earned 100 points! Total: {player.Points}", 100);
            }
            else
            {
                await Clients.Client(player.ConnectionId).SendAsync(IncorrectGuess, $"No points this round. Total: {player.Points}", 0);
            }
        }

        if (nonCommanderPlayers.All(p => p.GuessedRight))
        {
            await Clients.Group(roomCode).SendAsync("AllGuessedRight", "All players guessed right!");
        }
        else if (nonCommanderPlayers.All(p => !p.GuessedRight))
        {
            await Clients.Group(roomCode).SendAsync("AllGuessedWrong", "All players guessed wrong!");
        }
        else
        {
            var commander = players.FirstOrDefault(p => p.Role == PlayerRole.Commander);
            if (commander != null)
            {
                commander.Points += new Points(100);
                await Clients.Client(commander.ConnectionId).SendAsync("CommanderBonus", "Commander gets 100 points for mixed guesses!", 100);
            }
        }

        await Clients.Group(roomCode).SendAsync("RoundEnded", "The round has ended!");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var kv in Rooms)
        {
            if (kv.Value.RemoveAll(p => p.ConnectionId == Context.ConnectionId) > 0)
            {
                await Clients.Group(kv.Key).SendAsync(PlayerLeft, Context.ConnectionId);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }
}
