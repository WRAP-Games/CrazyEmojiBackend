namespace Wrap.CrazyEmoji.Api.GameLogic.Exceptions;

public class CommanderNotFoundException : Exception
{
    public string RoomCode { get; }
    public CommanderNotFoundException(string roomCode)
        : base($"No commander found in room '{roomCode}'.")
    {
        RoomCode = roomCode;
    }
}
