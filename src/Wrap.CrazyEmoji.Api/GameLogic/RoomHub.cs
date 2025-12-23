using Microsoft.AspNetCore.SignalR;
using Wrap.CrazyEmoji.Api.Abstractions;
using Wrap.CrazyEmoji.Api.Constants;
using Wrap.CrazyEmoji.Api.GameLogic.Exceptions;

namespace Wrap.CrazyEmoji.Api.GameLogic;

public class RoomHub(IRoomManager roomManager) : Hub
{
    private readonly IRoomManager _roomManager = roomManager;

    public async Task CreateUser(string username, string password)
    {
        try
        {
            await _roomManager.CreateUser(Context.ConnectionId, username, password);
            await Clients.Caller.SendAsync(RoomHubConstants.CreatedUser, username);
        }
        catch (InvalidUsernameException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.CreateUser} {RoomHubErrors.IncorrectUsername}");
        }
        catch (InvalidPasswordException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.CreateUser} {RoomHubErrors.IncorrectPassword}");
        }
        catch (UsernameTakenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.CreateUser} {RoomHubErrors.UsernameTaken}");
        }

    }

    public async Task LoginUser(string username, string password)
    {
        try
        {
            await _roomManager.LoginUser(Context.ConnectionId, username, password);
            await Clients.Caller.SendAsync(RoomHubConstants.UserLoggedIn);
        }
        catch (InvalidUsernameException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.LoginUser} {RoomHubErrors.IncorrectUsernamePassword}");
        }
    }

    public async Task GetCurrentUserData()
    {
        try
        {
            var (username, roomCode) = await _roomManager.GetCurrentUserDataAsync(Context.ConnectionId);
            await Clients.Caller.SendAsync(RoomHubConstants.CurrentUserData, new { username, roomCode });
        }
        catch (InvalidConnectionIdException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.GetCurrentUserData} {RoomHubErrors.IncorrectConnectionId}");
        }
    }

    public async Task GetUserData(string username)
    {
        try
        {
            await _roomManager.GetUserData(username, Context.ConnectionId);
            await Clients.Caller.SendAsync(RoomHubConstants.UserData, username);
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.GetUserData} {RoomHubErrors.Forbidden}");
        }
    }

    public async Task CreateRoom(string roomName, string category, int rounds, int roundDuration)
    {
        try
        {
            var roomCode = await _roomManager.CreateRoom(Context.ConnectionId, roomName, category, rounds, roundDuration);
            await Clients.Caller.SendAsync(RoomHubConstants.CreatedRoom, roomCode);
            await JoinRoom(roomCode);
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.CreateRoom} {RoomHubErrors.Forbidden}");
        }
        catch (JoinedDifferentRoomException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.CreateRoom} {RoomHubErrors.JoinedDifferentRoom}");
        }
        catch (IncorrectRoomNameException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.CreateRoom} {RoomHubErrors.IncorrectRoomName}");
        }
        catch (IncorrectRoomCategoryException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.CreateRoom} {RoomHubErrors.IncorrectRoomCategory}");
        }
        catch (IncorrectRoundAmountException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.CreateRoom} {RoomHubErrors.IncorrectRoundAmount}");
        }
        catch (IncorrectRoundDurationException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.CreateRoom} {RoomHubErrors.IncorrectRoundDuration}");
        }
    }

    public async Task JoinRoom(string roomCode)
    {
        try
        {
            var (username, roomName, category, rounds, roundDuration, roomCreator, players) = await _roomManager.JoinRoom(Context.ConnectionId, roomCode);
            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
            await Clients.Caller.SendAsync(RoomHubConstants.JoinedRoom, new { roomName, category, rounds, roundDuration, roomCreator, players });
            await Clients.OthersInGroup(roomCode).SendAsync(RoomHubConstants.PlayerJoined, username);
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.JoinRoom} {RoomHubErrors.Forbidden}");
        }
        catch (JoinedDifferentRoomException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.JoinRoom} {RoomHubErrors.JoinedDifferentRoom}");
        }
        catch (IncorrectRoomCodeException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.JoinRoom} {RoomHubErrors.IncorrectRoomCode}");
        }
        catch (RoomGameStartedException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.JoinRoom} {RoomHubErrors.RoomGameStarted}");
        }
    }

    public async Task LeftRoom()
    {
        try
        {
            var (username, roomCode, isGameEnded) = await _roomManager.LeftRoom(Context.ConnectionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);
            await Clients.Group(roomCode).SendAsync(RoomHubConstants.PlayerLeft, username);
            if (isGameEnded)
            {
                await Clients.Group(roomCode).SendAsync(RoomHubConstants.GameEnded);
            }
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.LeftRoom} {RoomHubErrors.Forbidden}");
        }
    }

    public async Task StartGame()
    {
        try
        {
            var roomCode = await _roomManager.StartGame(Context.ConnectionId);
            await Clients.Group(roomCode).SendAsync(RoomHubConstants.GameStarted);
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.StartGame} {RoomHubErrors.Forbidden}");
        }
        catch (RoomGameStartedException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.StartGame} {RoomHubErrors.RoomGameStarted}");
        }
        catch (NotEnoughPlayersException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.StartGame} {RoomHubErrors.NotEnoughPlayers}");
        }
    }

    public async Task GetCommander()
    {
        try
        {
            var commanderUsername = await _roomManager.GetCommander(Context.ConnectionId);
            await Clients.Caller.SendAsync(RoomHubConstants.CommanderSelected, commanderUsername);
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.GetCommander} {RoomHubErrors.Forbidden}");
        }
    }

    public async Task GetWord()
    {
        try
        {
            var word = await _roomManager.GetWord(Context.ConnectionId);
            await Clients.Caller.SendAsync(RoomHubConstants.RecivedWord, word);
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.GetWord} {RoomHubErrors.Forbidden}");
        }
    }

    public async Task SendEmojis(List<string> emojis)
    {
        try
        {
            var roomCode = await _roomManager.SendEmojis(Context.ConnectionId);
            await Clients.Caller.SendAsync(RoomHubConstants.EmojisRecieved);
            await Clients.OthersInGroup(roomCode).SendAsync(RoomHubConstants.RecieveEmojis, emojis);
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.SendEmojis} {RoomHubErrors.Forbidden}");
        }
    }

    public async Task CheckWord(string word)
    {
        try
        {
            var (isCorrect, roomCode) = await _roomManager.CheckWord(Context.ConnectionId, word);
            await Clients.Caller.SendAsync(RoomHubConstants.WordChecked, isCorrect);
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.CheckWord} {RoomHubErrors.Forbidden}");
        }
    }

    public async Task GetResults()
    {
        try
        {
            var (results, nextRound) = await _roomManager.GetResults(Context.ConnectionId);
            await Clients.Caller.SendAsync(RoomHubConstants.RoundEnded, results);
            Task.Delay(1000).Wait();
            if (nextRound)
            {
                await Clients.Caller.SendAsync(RoomHubConstants.RoundStarted);
            }
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.CheckWord} {RoomHubErrors.Forbidden}");
        }
    }
}