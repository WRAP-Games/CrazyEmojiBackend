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

        return Ok(await BuildFriendDtosAsync(db, user.Username, friendships, friendNames));
    }

    [HttpGet("requests")]
    public async Task<ActionResult<List<FriendRequestDto>>> GetIncomingRequests([FromQuery] string username, [FromQuery] string password)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await AuthenticateAsync(db, username, password);
        if (user is null) return Unauthorized();

        var requests = await db.FriendRequests
            .Where(r => r.ToUsername == user.Username)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

        var senders = await db.Users
            .Where(u => requests.Select(r => r.FromUsername).Contains(u.Username))
            .ToDictionaryAsync(u => u.Username);

        var result = requests.Select(r =>
        {
            var sender = senders.GetValueOrDefault(r.FromUsername);
            return new FriendRequestDto(
                r.FromUsername,
                sender is not null && !string.IsNullOrWhiteSpace(sender.ConnectionId),
                r.CreatedAtUtc
            );
        }).ToList();

        return Ok(result);
    }

    [HttpGet("requests/outgoing")]
    public async Task<ActionResult<List<FriendRequestDto>>> GetOutgoingRequests([FromQuery] string username, [FromQuery] string password)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await AuthenticateAsync(db, username, password);
        if (user is null) return Unauthorized();

        var requests = await db.FriendRequests
            .Where(r => r.FromUsername == user.Username)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

        var targets = await db.Users
            .Where(u => requests.Select(r => r.ToUsername).Contains(u.Username))
            .ToDictionaryAsync(u => u.Username);

        var result = requests.Select(r =>
        {
            var target = targets.GetValueOrDefault(r.ToUsername);
            return new FriendRequestDto(
                r.ToUsername,
                target is not null && !string.IsNullOrWhiteSpace(target.ConnectionId),
                r.CreatedAtUtc
            );
        }).ToList();

        return Ok(result);
    }

    [HttpPost("request")]
    public async Task<IActionResult> SendFriendRequest([FromBody] FriendActionRequest request)
    {
        try
        {
            using var db = dbFactory.CreateDbContext();

            var user = await AuthenticateAsync(db, request.Username, request.Password);
            if (user is null) return Unauthorized();

            var friendUsername = request.FriendUsername.Trim();
            if (string.IsNullOrWhiteSpace(friendUsername)) return BadRequest("Friend username is required.");
            if (string.Equals(friendUsername, user.Username, StringComparison.OrdinalIgnoreCase))
                return BadRequest("You cannot send a friend request to yourself.");

            var target = await db.Users.FirstOrDefaultAsync(u => u.Username == friendUsername);
            if (target is null) return NotFound("User not found.");

            var pair = NormalizePair(user.Username, target.Username);
            var alreadyFriends = await db.Friendships.AnyAsync(f => f.UserAUsername == pair.a && f.UserBUsername == pair.b);
            if (alreadyFriends) return Conflict("You are already friends.");

            var outgoingExists = await db.FriendRequests.AnyAsync(r => r.FromUsername == user.Username && r.ToUsername == target.Username);
            if (outgoingExists) return Conflict("Friend request already sent.");

            var inverseRequest = await db.FriendRequests.FirstOrDefaultAsync(r => r.FromUsername == target.Username && r.ToUsername == user.Username);
            if (inverseRequest is not null)
            {
                db.FriendRequests.Remove(inverseRequest);

                db.Friendships.Add(new Friendship
                {
                    UserAUsername = pair.a,
                    UserBUsername = pair.b,
                    CreatedAtUtc = DateTime.UtcNow
                });

                db.SaveChanges();
                return Ok(new { message = "Friend request accepted automatically because they had already sent you one.", becameFriends = true });
            }

            db.FriendRequests.Add(new FriendRequest
            {
                FromUsername = user.Username,
                ToUsername = target.Username,
                CreatedAtUtc = DateTime.UtcNow
            });

            db.SaveChanges();
            return Ok(new { message = "Friend request sent.", becameFriends = false });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return StatusCode(500, ex.ToString());
        }
    }

    [HttpPost("requests/{fromUsername}/accept")]
    public async Task<ActionResult<FriendDto>> AcceptFriendRequest(string fromUsername, [FromBody] FriendActionRequest request)
    {
        try
        {
            using var db = dbFactory.CreateDbContext();

            var user = await AuthenticateAsync(db, request.Username, request.Password);
            if (user is null) return Unauthorized();

            var pending = await db.FriendRequests
                .FirstOrDefaultAsync(r => r.FromUsername == fromUsername && r.ToUsername == user.Username);

            if (pending is null)
                return NotFound("Friend request not found.");

            var fromUser = await db.Users.FirstOrDefaultAsync(u => u.Username == fromUsername);
            if (fromUser is null)
                return NotFound("User not found.");

            var pair = NormalizePair(user.Username, fromUser.Username);

            var existingFriendship = await db.Friendships
                .FirstOrDefaultAsync(f => f.UserAUsername == pair.a && f.UserBUsername == pair.b);

            if (existingFriendship is null)
            {
                existingFriendship = new Friendship
                {
                    UserAUsername = pair.a,
                    UserBUsername = pair.b,
                    CreatedAtUtc = DateTime.UtcNow
                };

                db.Friendships.Add(existingFriendship);
                db.SaveChanges();
            }

            db.FriendRequests.Remove(pending);
            db.SaveChanges();

            var member = await db.RoomMembers.FirstOrDefaultAsync(rm => rm.Username == fromUser.Username);
            var room = member is null
                ? null
                : await db.ActiveRooms.FirstOrDefaultAsync(r => r.RoomCode == member.RoomCode);

            return Ok(new FriendDto(
                fromUser.Username,
                !string.IsNullOrWhiteSpace(fromUser.ConnectionId),
                member?.RoomCode,
                room?.RoomName,
                existingFriendship.CreatedAtUtc));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return StatusCode(500, ex.ToString());
        }
    }

    [HttpPost("requests/{fromUsername}/decline")]
    public async Task<IActionResult> DeclineFriendRequest(string fromUsername, [FromBody] FriendActionRequest request)
    {
        try
        {
            using var db = dbFactory.CreateDbContext();

            var user = await AuthenticateAsync(db, request.Username, request.Password);
            if (user is null) return Unauthorized();

            var pending = await db.FriendRequests
                .FirstOrDefaultAsync(r => r.FromUsername == fromUsername && r.ToUsername == user.Username);

            if (pending is null)
                return NotFound("Friend request not found.");

            db.FriendRequests.Remove(pending);
            db.SaveChanges();

            return NoContent();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return StatusCode(500, ex.ToString());
        }
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

    private static async Task<List<FriendDto>> BuildFriendDtosAsync(
        GameDbContext db,
        string currentUsername,
        List<Friendship> friendships,
        List<string> friendNames)
    {
        var users = await db.Users
            .Where(u => friendNames.Contains(u.Username))
            .ToDictionaryAsync(u => u.Username);

        var roomMembers = await db.RoomMembers
            .Where(rm => friendNames.Contains(rm.Username))
            .ToListAsync();

        var roomCodes = roomMembers
            .Select(rm => rm.RoomCode)
            .Distinct()
            .ToList();

        var rooms = await db.ActiveRooms
            .Where(r => roomCodes.Contains(r.RoomCode))
            .ToDictionaryAsync(r => r.RoomCode);

        var result = new List<FriendDto>();

        foreach (var friendship in friendships)
        {
            var friendUsername = friendship.UserAUsername == currentUsername
                ? friendship.UserBUsername
                : friendship.UserAUsername;

            if (!users.TryGetValue(friendUsername, out var friendUser))
                continue;

            var roomMember = roomMembers.FirstOrDefault(rm => rm.Username == friendUsername);
            rooms.TryGetValue(roomMember?.RoomCode ?? string.Empty, out var room);

            result.Add(new FriendDto(
                friendUsername,
                !string.IsNullOrWhiteSpace(friendUser.ConnectionId),
                roomMember?.RoomCode,
                room?.RoomName,
                friendship.CreatedAtUtc));
        }

        return result;
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