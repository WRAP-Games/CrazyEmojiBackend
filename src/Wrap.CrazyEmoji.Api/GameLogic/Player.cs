namespace Wrap.CrazyEmoji.Api.GameLogic;

public class Player
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int Points { get; set; } = 0;
    public PlayerRole Role { get; set; }
}
