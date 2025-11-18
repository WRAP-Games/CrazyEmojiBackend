using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wrap.CrazyEmoji.Api.Data.Entities;

[Table("game")]
public class GameEntity
{
    [Key]
    [Column("room_code")]
    public string RoomCode { get; set; } = string.Empty;

    [Required]
    [Column("room_name")]
    public string RoomName { get; set; } = string.Empty;

    [Required]
    [Column("host_id")]
    public Guid HostId { get; set; }

    [Column("max_round")]
    public int MaxRound { get; set; }

    [Column("current_round")]
    public int CurrentRound { get; set; } = 0;

    [ForeignKey("HostId")]
    public virtual UserEntity? Host { get; set; }

    public virtual ICollection<UserEntity> Users { get; set; } = new List<UserEntity>();

    public virtual ICollection<GameRoundEntity> Rounds { get; set; } = new List<GameRoundEntity>();
}