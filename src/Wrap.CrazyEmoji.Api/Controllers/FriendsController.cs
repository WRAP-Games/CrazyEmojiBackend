using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wrap.CrazyEmoji.Api.Contracts.Friends;
using Wrap.CrazyEmoji.Api.Data;
using Wrap.CrazyEmoji.Api.Data.Entities;

namespace Wrap.CrazyEmoji.Api.Controllers;

[ApiController]
[Route("api/friends")]
public class FriendsController(IDbContextFactory<GameDbContext> dbFactory) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FriendDto>>> GetFriends([FromQuery] string username, [FromQuery] string password)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await AuthenticateAsync(db, username, password);
        if (user is null) return Unauthorized();

        var friendships = await db.Friendships
            .Where(f => f.UserAUsername == user.Username || f.UserBUsername == user.Username)
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToListAsync();

        var friendNames = friendships
            .Select(f => f.UserAUsername == user.Username ? f.UserBUsername : f.UserAUsername)
            .Distinct()
            .ToList();

        var friendUsers = await db.Users
            .Where(u => friendNames.Contains(u.Username))
            .ToDictionaryAsync(u => u.Username);

        var roomMembers = await db.RoomMembers
            .Where(rm => friendNames.Contains(rm.Username))
            .ToListAsync();

        var roomCodes = roomMembers.Select(rm => rm.RoomCode).Distinct().ToList();
        var rooms = await db.ActiveRooms
            .Where(r => roomCodes.Contains(r.RoomCode))
            .ToDictionaryAsync(r => r.RoomCode);

        var result = friendships.Select(f =>
        {
            var friendName = f.UserAUsername == user.Username ? f.UserBUsername : f.UserAUsername;
            var friendUser = friendUsers.GetValueOrDefault(friendName);
            var member = roomMembers.FirstOrDefault(rm => rm.Username == friendName);
            var room = member is null ? null : rooms.GetValueOrDefault(member.RoomCode);
            return new FriendDto(
                friendName,
                friendUser is not null && !string.IsNullOrWhiteSpace(friendUser.ConnectionId),
                member?.RoomCode,
                room?.RoomName,
                f.CreatedAtUtc);
        }).ToList();

        return Ok(result);
    }

    [HttpPost("add")]
    public async Task<ActionResult<FriendDto>> AddFriend([FromBody] FriendActionRequest request)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await AuthenticateAsync(db, request.Username, request.Password);
        if (user is null) return Unauthorized();

        var friendUsername = request.FriendUsername.Trim();
        if (string.Equals(friendUsername, user.Username, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("You cannot add yourself as a friend.");
        }

        var friend = await db.Users.FirstOrDefaultAsync(u => u.Username == friendUsername);
        if (friend is null) return NotFound("User not found.");

        var pair = NormalizePair(user.Username, friend.Username);
        var existing = await db.Friendships.FirstOrDefaultAsync(f => f.UserAUsername == pair.a && f.UserBUsername == pair.b);
        if (existing is not null) return Conflict("You are already friends.");

        var entity = new Friendship
        {
            UserAUsername = pair.a,
            UserBUsername = pair.b,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Friendships.Add(entity);
        await db.SaveChangesAsync();

        var member = await db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == friend.Username);
        var room = member is null ? null : await db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == member.RoomCode);

        return Ok(new FriendDto(friend.Username, !string.IsNullOrWhiteSpace(friend.ConnectionId), member?.RoomCode, room?.RoomName, entity.CreatedAtUtc));
    }

    [HttpDelete("{friendUsername}")]
    public async Task<IActionResult> RemoveFriend(string friendUsername, [FromQuery] string username, [FromQuery] string password)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await AuthenticateAsync(db, username, password);
        if (user is null) return Unauthorized();

        var pair = NormalizePair(user.Username, friendUsername);
        var entity = await db.Friendships.FirstOrDefaultAsync(f => f.UserAUsername == pair.a && f.UserBUsername == pair.b);
        if (entity is null) return NotFound();

        db.Friendships.Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("profile/{friendUsername}")]
    public async Task<ActionResult<FriendProfileDto>> GetProfile(string friendUsername, [FromQuery] string username, [FromQuery] string password)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await AuthenticateAsync(db, username, password);
        if (user is null) return Unauthorized();

        var profileUser = await db.Users.FirstOrDefaultAsync(u => u.Username == friendUsername);
        if (profileUser is null) return NotFound();

        var pair = NormalizePair(user.Username, profileUser.Username);
        var isFriend = await db.Friendships.AnyAsync(f => f.UserAUsername == pair.a && f.UserBUsername == pair.b);
        var friendsCount = await db.Friendships.CountAsync(f => f.UserAUsername == profileUser.Username || f.UserBUsername == profileUser.Username);
        var member = await db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == profileUser.Username);
        var room = member is null ? null : await db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == member.RoomCode);

        return Ok(new FriendProfileDto(
            profileUser.Username,
            !string.IsNullOrWhiteSpace(profileUser.ConnectionId),
            isFriend,
            friendsCount,
            member?.RoomCode,
            room?.RoomName));
    }

    private static async Task<User?> AuthenticateAsync(GameDbContext db, string username, string password)
    {
        return await db.Users.FirstOrDefaultAsync(u => u.Username == username && u.Password == password);
    }

    private static (string a, string b) NormalizePair(string left, string right)
    {
        return string.CompareOrdinal(left, right) < 0 ? (left, right) : (right, left);
    }
}
