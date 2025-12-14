using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wrap.CrazyEmoji.Api.Data.Entities;

public partial class ActiveRoom
{
    [Key]
    [StringLength(6)]
    public string RoomCode { get; set; } = null!;

    [StringLength(32)]
    public string RoomName { get; set; } = null!;

    public int Rounds { get; set; }

    public int RoundDuration { get; set; }

    [StringLength(32)]
    public string RoomCreator { get; set; } = null!;

    public long? CategoryId { get; set; }  // nullable because of ON DELETE SET NULL

    [ForeignKey("RoomCreator")]
    [InverseProperty("ActiveRooms")]
    public virtual User RoomCreatorNavigation { get; set; } = null!;

    [ForeignKey("CategoryId")]
    [InverseProperty("ActiveRooms")]
    public virtual Category? CategoryNavigation { get; set; }

    [InverseProperty("RoomCodeNavigation")]
    public virtual ICollection<RoomMember> RoomMembers { get; set; } = new List<RoomMember>();
}