using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wrap.CrazyEmoji.Api.Data.Entities;

[Table("users")]
public class UserEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Column("room_code")]
    public string? RoomCode { get; set; }

    [ForeignKey("RoomCode")]
    public virtual GameEntity? Game { get; set; }

    public virtual ICollection<GameRoundEntity> CommandedRounds { get; set; } = new List<GameRoundEntity>();
}