namespace Wrap.CrazyEmoji.Api.Contracts.Friends;

public record FriendActionRequest(string Username, string Password, string FriendUsername);

public record FriendDto(
    string Username,
    bool IsOnline,
    string? CurrentRoomCode,
    string? CurrentRoomName,
    DateTime FriendsSinceUtc);

public record FriendProfileDto(
    string Username,
    bool IsOnline,
    bool IsFriend,
    int FriendsCount,
    string? CurrentRoomCode,
    string? CurrentRoomName);
