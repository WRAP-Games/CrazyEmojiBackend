using System.ComponentModel.DataAnnotations.Schema;
namespace Wrap.CrazyEmoji.Api.Data.Entities;

[Table("FriendRequest")]
public partial class FriendRequest
{
    public long Id { get; set; }

    public string FromUsername { get; set; } = null!;

    public string ToUsername { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public virtual User FromUserNavigation { get; set; } = null!;

    public virtual User ToUserNavigation { get; set; } = null!;
}