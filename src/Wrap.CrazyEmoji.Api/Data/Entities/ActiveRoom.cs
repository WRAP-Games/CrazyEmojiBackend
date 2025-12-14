using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Wrap.CrazyEmoji.Api.Data.Entities;

public partial class ActiveRoom
{
    [Key]
    [StringLength(6)]
    public string RoomCode { get; set; } = null!;

    [StringLength(32)]
    public string RoomName { get; set; } = null!;

    public string Category { get; set; } = null!;

    public int Rounds { get; set; }

    public int RoundDuration { get; set; }

    [StringLength(32)]
    public string RoomCreator { get; set; } = null!;

    [ForeignKey("RoomCreator")]
    [InverseProperty("ActiveRooms")]
    public virtual User RoomCreatorNavigation { get; set; } = null!;

    [InverseProperty("RoomCodeNavigation")]
    public virtual ICollection<RoomMember> RoomMembers { get; set; } = new List<RoomMember>();
}
