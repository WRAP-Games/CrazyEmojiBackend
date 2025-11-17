namespace Wrap.CrazyEmoji.Api.GameLogic.Exceptions;

public class RoomNotFoundException : Exception
{
    public string RoomCode { get; }

    public RoomNotFoundException(string roomCode)
        : base($"Room '{roomCode}' was not found.")
    {
        RoomCode = roomCode;
    }
}