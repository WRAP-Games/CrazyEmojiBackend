namespace Wrap.CrazyEmoji.Api.GameLogic;

public class Player
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public Points Points { get; set; } = new Points(0);
    public bool HasGuessed { get; set; } = false;
    public bool GuessedRight { get; set; } = false;
    public PlayerRole Role { get; set; }
}
