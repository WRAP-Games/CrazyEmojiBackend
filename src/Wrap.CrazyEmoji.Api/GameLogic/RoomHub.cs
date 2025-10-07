using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Wrap.CrazyEmoji.Api.Abstractions;

namespace Wrap.CrazyEmoji.Api.GameLogic;

public class RoomHub(IWordService wordService) : Hub
{
    private static readonly ConcurrentDictionary<string, List<Player>> Rooms = new();
    private static readonly ConcurrentDictionary<string, string> CurrentWords = new();

    private const string PlayerLeft = "PlayerLeft";
    private const string JoinedRoom = "JoinedRoom";
    private const string UsernameSet = "UsernameSet";
    private const string CreatedRoom = "CreatedRoom";
    private const string ReceiveWord = "ReceiveWord";
    private const string CorrectGuess = "CorrectGuess";
    private const string ReceiveEmojis = "ReceiveEmojis";
    private const string IncorrectGuess = "IncorrectGuess";
    private const string CommanderSelected = "CommanderSelected";
    private const string CommanderAnnounced = "CommanderAnnounced";
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

    public async Task SelectCommander()
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

    public async Task SendWordToCommander()
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
            await Clients.Caller.SendAsync(Error, "Word is not send to the commander.");
            return;
        }

        var caller = players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
        if (caller == null || caller.Role != PlayerRole.Commander)
        {
            await Clients.Caller.SendAsync(Error, "Only the commander can send emojis.");
            return;
        }

        await Clients.GroupExcept(roomCode, Context.ConnectionId).SendAsync(ReceiveEmojis, emojis);
    }

    public async Task GetWordAndSendPoints(string word)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(word);

        if (Context.Items[RoomCode] is not string roomCode
            || !Rooms.TryGetValue(roomCode, out var players))
        {
            await Clients.Caller.SendAsync(Error, "Room not found.");
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

        if (string.Equals(word, currentWord, StringComparison.OrdinalIgnoreCase))
        {
            guesser.Points += 100;
            await Clients.Caller.SendAsync(CorrectGuess, $"Congratulations! You guessed the word correctly. Points = {guesser.Points}", 100);
        }
        else
        {
            await Clients.Caller.SendAsync(IncorrectGuess, $"Your guess was incorrect. Try again! Points = {guesser.Points}", 0);
        }
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
