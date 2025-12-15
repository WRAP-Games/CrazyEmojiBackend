using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Wrap.CrazyEmoji.Api.Data.Entities;

[Table("User")]
[Index("ConnectionId", Name = "User_connectionId_key", IsUnique = true)]
public partial class User
{
    public long Id { get; set; }

    [Key]
    [StringLength(32)]
    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    [Column("connectionId")]
    public string ConnectionId { get; set; } = null!;

    [InverseProperty("RoomCreatorNavigation")]
    public virtual ICollection<ActiveRoom> ActiveRooms { get; set; } = new List<ActiveRoom>();

    [InverseProperty("UsernameNavigation")]
    public virtual ICollection<RoomMember> RoomMembers { get; set; } = new List<RoomMember>();
}
