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
            await JoinRoom(roomCode);
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

    public async Task JoinRoom(string roomCode)
    {
        try
        {
            var (username, roomName, category, rounds, roundDuration, roomCreator, players) = await _roomManager.JoinRoom(Context.ConnectionId, roomCode);
            await Clients.Caller.SendAsync(RoomHubConstants.joinedRoom, new { roomName, category, rounds, roundDuration, roomCreator, players });
            await Clients.OthersInGroup(roomCode).SendAsync(RoomHubConstants.playerJoined, username);
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.joinRoom} {RoomHubErrors.forbidden}");
        }
        catch (JoinedDifferentRoomException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.joinRoom} {RoomHubErrors.joinedDifferentRoom}");
        }
        catch (IncorrectRoomCodeException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.joinRoom} {RoomHubErrors.incorrectRoomCode}");
        }
        catch (RoomGameStartedException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.joinRoom} {RoomHubErrors.roomGameStarted}");
        }
    }

    public async Task LeftRoom()
    {
        try
        {
            var (username, roomCode, isGameEnded) = await _roomManager.LeftRoom(Context.ConnectionId);
            await Clients.OthersInGroup(roomCode).SendAsync(RoomHubConstants.playerLeft, username);
            if (isGameEnded)
            {
                await Clients.Group(roomCode).SendAsync(RoomHubConstants.gameEnded);
            }
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.leftRoom} {RoomHubErrors.forbidden}");
        }
    }

    public async Task StartGame()
    {
        try
        {
            var roomCode = await _roomManager.StartGame(Context.ConnectionId);
            await Clients.Group(roomCode).SendAsync(RoomHubConstants.gameStarted);
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.startGame} {RoomHubErrors.forbidden}");
        }
        catch (RoomGameStartedException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.startGame} {RoomHubErrors.roomGameStarted}");
        }
        catch (NotEnoughPlayersException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.startGame} {RoomHubErrors.notEnoughPlayers}");
        }
    }

    public async Task GetCommander()
    {
        try
        {
            var commanderUsername = await _roomManager.GetCommander(Context.ConnectionId);
            await Clients.Caller.SendAsync(RoomHubConstants.commanderSelected, commanderUsername);
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.getCommander} {RoomHubErrors.forbidden}");
        }
    }

    public async Task GetWord()
    {
        try
        {
            var word = await _roomManager.GetWord(Context.ConnectionId);
            await Clients.Caller.SendAsync(RoomHubConstants.recivedWord, word);
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.getWord} {RoomHubErrors.forbidden}");
        }
    }

    public async Task SendEmojis(List<string> emojis)
    {
        try
        {
            var roomCode = await _roomManager.SendEmojis(Context.ConnectionId);
            await Clients.Caller.SendAsync(RoomHubConstants.emojisRecieved);
            await Clients.OthersInGroup(roomCode).SendAsync(RoomHubConstants.recieveEmojis, emojis);
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.sendEmojis} {RoomHubErrors.forbidden}");
        }
    }

    public async Task CheckWord(string word)
    {
        try
        {
            var (isCorrect, roomCode) = await _roomManager.CheckWord(Context.ConnectionId, word);
            await Clients.Caller.SendAsync(RoomHubConstants.wordChecked, isCorrect);
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.checkWord} {RoomHubErrors.forbidden}");
        }
    }
    
    public async Task GetResults()
    {
        try
        {
            var (results, nextRound) = await _roomManager.GetResults(Context.ConnectionId);
            await Clients.Caller.SendAsync(RoomHubConstants.roundEnded, results);
            Task.Delay(1000).Wait();
            if (nextRound)
            {
                await Clients.Caller.SendAsync(RoomHubConstants.roundStarted);
            }
        }
        catch (ForbiddenException)
        {
            await Clients.Caller.SendAsync(RoomHubConstants.Error,
                $"{RoomHubCommands.checkWord} {RoomHubErrors.forbidden}");
        }
    }
}