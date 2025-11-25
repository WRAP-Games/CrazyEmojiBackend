using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wrap.CrazyEmoji.Api.Data.Entities;

[Table("game_rounds")]
public class GameRoundEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("room_code")]
    public string RoomCode { get; set; } = string.Empty;

    [Required]
    [Column("commander_id")]
    public Guid CommanderId { get; set; }

    [Required]
    [Column("word")]
    public string Word { get; set; } = string.Empty;

    [Column("emoji")]
    public string? Emoji { get; set; }

    [Column("round_number")]
    public int RoundNumber { get; set; }

    [ForeignKey("RoomCode")]
    public virtual GameEntity? Game { get; set; }

    [ForeignKey("CommanderId")]
    public virtual UserEntity? Commander { get; set; }
}