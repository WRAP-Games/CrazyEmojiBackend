using Microsoft.AspNetCore.SignalR;
using Wrap.CrazyEmoji.Api.Constants;
using Wrap.CrazyEmoji.Api.GameLogic.Exceptions;

namespace Wrap.CrazyEmoji.Api.GameLogic;

public class RoomHub(RoomManager roomManager) : Hub
{
    private readonly RoomManager _roomManager = roomManager;

    private async Task CreateUser(string username, string password)
    {
        try
        {
            await _roomManager.CreateUser(Context.ConnectionId, username, password);
            await Clients.Caller.SendAsync(RoomHubConstants.createdUser, username);
        }
        catch (InvalidUsernameException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.createUser} {RoomHubErrors.incorrectUsername}");
        }
        catch (InvalidPasswordException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.createUser} {RoomHubErrors.incorrectPassword}");
        }
        catch (UsernameTakenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.createUser} {RoomHubErrors.usernameTaken}");
        }

    }

    private async Task LoginUser(string username, string password)
    {
        try
        {
            await _roomManager.LoginUser(Context.ConnectionId, username, password);
            await Clients.Caller.SendAsync(RoomHubConstants.userLoggedIn);
        }
        catch (InvalidUsernameException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.loginUser} {RoomHubErrors.incorrectUsernamePassword}");
        }
    }

    private async Task GetCurrentUserData()
    {
        try
        {
            var (username, roomCode) = await _roomManager.GetCurrentUserDataAsync(Context.ConnectionId);
            await Clients.Caller.SendAsync(RoomHubConstants.currentUserData, new { username, roomCode });
        }
        catch (InvalidConnectionIdException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.getCurrentUserData} {RoomHubErrors.incorrectConnectionId}");
        }
    }

    public async Task GetUserData(string username)
    {
        try
        {
            await _roomManager.GetUserData(username, Context.ConnectionId);
            await Clients.Caller.SendAsync(RoomHubConstants.userData, username);
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.getUserData} {RoomHubErrors.forbidden}");
        }
    }

    public async Task CreateRoom(string roomName, string category, int rounds, int roundDuration)
    {
        try
        {
            var roomCode = await _roomManager.CreateRoom(Context.ConnectionId, roomName, category, rounds, roundDuration);
            await Clients.Caller.SendAsync(RoomHubConstants.createdRoom, roomCode);
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.createRoom} {RoomHubErrors.forbidden}");
        }
        catch (JoinedDifferentRoomException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.createRoom} {RoomHubErrors.joinedDifferentRoom}");
        }
        catch (IncorrectRoomNameException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.createRoom} {RoomHubErrors.incorrectRoomName}");
        }
        catch (IncorrectRoomCategoryException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.createRoom} {RoomHubErrors.incorrectRoomCategory}");
        }
        catch (IncorrectRoundAmountException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.createRoom} {RoomHubErrors.incorrectRoundAmount}");
        }
        catch (IncorrectRoundDurationException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.createRoom} {RoomHubErrors.incorrectRoundDuration}");
        }
    }

}