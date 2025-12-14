using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wrap.CrazyEmoji.Api.Data.Entities;

public class Word
{
    [Key]
    public long Id { get; set; }

    [Required]
    public string Text { get; set; } = null!;

    public long CategoryId { get; set; }

    [ForeignKey("CategoryId")]
    [InverseProperty("Words")]
    public virtual Category Category { get; set; } = null!;
}