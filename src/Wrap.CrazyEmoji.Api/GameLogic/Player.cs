namespace Wrap.CrazyEmoji.Api.GameLogic;

public class Player
{
    private string _username = string.Empty;
    private string _connectionId = string.Empty;
    private Points _points = new(0);

    public string Username
    {
        get => _username;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Username cannot be empty.");

            _username = value;
        }
    }

    public string ConnectionId
    {
        get => _connectionId;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Connection ID cannot be empty.");

            _connectionId = value;
        }
    }

    public Points Points { get; set; } = new Points(0);
    public PlayerRole Role { get; set; } = PlayerRole.Player;
    public bool HasGuessed { get; set; } = false;
    public bool GuessedRight { get; set; } = false;

    public Player() { }

    public Player(string username, string connectionId)
    {
        Username = username;
        ConnectionId = connectionId;
    }
}
