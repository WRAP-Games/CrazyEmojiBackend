using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Wrap.CrazyEmoji.Api.Data.Entities;

[Table("Friendship")]
[Index(nameof(UserAUsername), nameof(UserBUsername), IsUnique = true)]
public class Friendship
{
    public long Id { get; set; }
    public string UserAUsername { get; set; } = null!;
    public string UserBUsername { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public User? UserANavigation { get; set; }
    public User? UserBNavigation { get; set; }
}
