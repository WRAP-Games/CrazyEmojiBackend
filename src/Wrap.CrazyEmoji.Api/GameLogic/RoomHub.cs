using Microsoft.AspNetCore.SignalR;
using Wrap.CrazyEmoji.Api.Constants;

namespace Wrap.CrazyEmoji.Api.GameLogic;

public class RoomHub(RoomManager roomManager) : Hub
{
    private readonly RoomManager _roomManager = roomManager;

    public async Task SetUsername(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        Context.Items[RoomHubConstants.Username] = username;
        await Clients.Caller.SendAsync(RoomHubConstants.UsernameSet, username);
    }

    public async Task CreateRoom(string roomCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomCode);

        var created = await _roomManager.CreateRoomAsync(roomCode);
        if (!created)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error, "RoomAlreadyExists");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        Context.Items[RoomHubConstants.RoomCode] = roomCode;

        await Clients.Caller.SendAsync(RoomHubConstants.CreatedRoom, roomCode);
        await JoinRoom(roomCode);
    }

    public async Task JoinRoom(string roomCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomCode);

        var username = Context.Items[RoomHubConstants.Username] as string ?? Context.ConnectionId;
        var player = new Player
        {
            ConnectionId = Context.ConnectionId,
            Username = username,
            Role = PlayerRole.Player
        };

        var joined = await _roomManager.AddPlayerAsync(roomCode, player);
        if (!joined)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error, "Room not found");
            return;
        }

        Context.Items[RoomHubConstants.RoomCode] = roomCode;
        await Clients.Caller.SendAsync(RoomHubConstants.JoinedRoom, roomCode);
    }

    public async Task StartGame()
    {
        if (Context.Items[RoomHubConstants.RoomCode] is not string roomCode)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error, "You are not in a room.");
            return;
        }

        var gameStarted = await _roomManager.StartGameAsync(roomCode);
        if (gameStarted) await Clients.Group(roomCode).SendAsync(RoomHubConstants.GameStarted, roomCode);
    }

    public async Task GetAndSendEmojis(string emojis)
    {
        if (Context.Items[RoomHubConstants.RoomCode] is string roomCode)
            await _roomManager.SendEmojisAsync(roomCode, Context.ConnectionId, emojis);
    }

    public async Task CheckWord(string word)
    {
        if (Context.Items[RoomHubConstants.RoomCode] is string roomCode)
            await _roomManager.CheckWordAsync(roomCode, Context.ConnectionId, word);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _roomManager.RemovePlayerAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}