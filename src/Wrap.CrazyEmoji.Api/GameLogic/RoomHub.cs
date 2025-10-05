using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Wrap.CrazyEmoji.Api.GameLogic;

public class RoomHub : Hub
{
    private static ConcurrentDictionary<string, List<string>> Rooms = new();

    public async Task SetUsername(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        Context.Items["Username"] = username;
        await Clients.Caller.SendAsync("UsernameSet", username);
    }
    public async Task CreateRoom(string roomCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomCode);

        Rooms.TryAdd(roomCode, new List<string>());
        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        Rooms[roomCode].Add(Context.ConnectionId);

        await Clients.Caller.SendAsync("RoomCreated", roomCode);
        await JoinRoom(roomCode);
    }

    public async Task JoinRoom(string roomCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomCode);

        var username = Context.Items["Username"] as string ?? throw new InvalidOperationException("Username not set");

        if (Rooms.ContainsKey(roomCode))
        {
            Rooms[roomCode].Add(Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

            Context.Items["RoomCode"] = roomCode;

            await Clients.Caller.SendAsync("JoinedRoom", roomCode);
            await Clients.Group(roomCode).SendAsync("PlayerJoined", username);
        }
        else
        {
            await Clients.Caller.SendAsync("Error", "Room not found");
        }
    }

    public async Task SendMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var username = Context.Items["Username"] as string ?? Context.ConnectionId;
        var roomCode = Context.Items["RoomCode"] as string;

        await Clients.Group(roomCode!).SendAsync("ReceiveMessage", username, message);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var kv in Rooms)
        {
            if (kv.Value.Remove(Context.ConnectionId))
            {
                await Clients.Group(kv.Key).SendAsync("PlayerLeft", Context.ConnectionId);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }
}
