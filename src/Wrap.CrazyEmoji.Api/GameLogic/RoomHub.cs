using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Wrap.CrazyEmoji.Api.GameLogic;

public class RoomHub : Hub
{
    private static ConcurrentDictionary<string, List<string>> Rooms = new();

    public async Task CreateRoom(string roomCode)
    {
        Rooms.TryAdd(roomCode, new List<string>());
        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        Rooms[roomCode].Add(Context.ConnectionId);

        await Clients.Caller.SendAsync("RoomCreated", roomCode);
    }

    public async Task JoinRoom(string roomCode)
    {
        if (Rooms.ContainsKey(roomCode))
        {
            Rooms[roomCode].Add(Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
            await Clients.Caller.SendAsync("JoinedRoom", roomCode);
            await Clients.Group(roomCode).SendAsync("PlayerJoined", Context.ConnectionId);
        }
        else
        {
            await Clients.Caller.SendAsync("Error", "Room not found");
        }
    }

    public async Task SendMessage(string roomCode, string message)
    {
        await Clients.Group(roomCode).SendAsync("ReceiveMessage", Context.ConnectionId, message);
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
