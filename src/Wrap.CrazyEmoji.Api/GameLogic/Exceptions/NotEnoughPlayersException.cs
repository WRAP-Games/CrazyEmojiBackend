namespace Wrap.CrazyEmoji.Api.GameLogic.Exceptions;

public class NotEnoughPlayersException : Exception
{
    public string RoomCode { get; }
    public int PlayerCount { get; }

    public NotEnoughPlayersException(string roomCode, int playerCount)
        : base($"Room '{roomCode}' has only {playerCount} players. At least 3 are required to start the game.")
    {
        RoomCode = roomCode;
        PlayerCount = playerCount;
    }
}