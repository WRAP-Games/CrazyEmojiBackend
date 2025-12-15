namespace Wrap.CrazyEmoji.Api.Abstractions;

public interface IRoomManager
{
    Task CreateUser(string connectionId, string username, string password);
    Task LoginUser(string connectionId, string username, string password);
    Task<(string username, string roomCode)> GetCurrentUserDataAsync(string connectionId);
    Task GetUserData(string username, string connectionId);

    Task<string> CreateRoom(string connectionId, string roomName, string category, int rounds, int roundDuration);
    Task<(string username, string roomName, string category, int rounds, int roundDuration, string roomCreator, List<string> players)>
        JoinRoom(string connectionId, string roomCode);

    Task<(string username, string roomCode, bool isGameEnded)> LeftRoom(string connectionId);
    Task<string> StartGame(string connectionId);
    Task<string> GetCommander(string connectionId);
    Task<string> GetWord(string connectionId);
    Task<string> SendEmojis(string connectionId);
    Task<(bool isCorrect, string roomCode)> CheckWord(string connectionId, string word);
    Task<(List<(string username, bool guessedRight, string? guessedWord, long gameScore)>, bool nextRound)>
        GetResults(string connectionId);
}