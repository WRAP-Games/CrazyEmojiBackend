using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Wrap.CrazyEmoji.Api.Data.Entities;

[PrimaryKey("RoomCode", "Username")]
public partial class RoomMember
{
    [Key]
    [StringLength(6)]
    public string RoomCode { get; set; } = null!;

    [Key]
    [StringLength(32)]
    public string Username { get; set; } = null!;

    [StringLength(9)]
    public string Role { get; set; } = null!;

    public long GameScore { get; set; }

    [Required]
    public bool GuessedRight { get; set; } = false;

    public string? GuessedWord { get; set; } = "";

    [ForeignKey("RoomCode")]
    [InverseProperty("RoomMembers")]
    public virtual ActiveRoom RoomCodeNavigation { get; set; } = null!;

    [ForeignKey("Username")]
    [InverseProperty("RoomMembers")]
    public virtual User UsernameNavigation { get; set; } = null!;
}
