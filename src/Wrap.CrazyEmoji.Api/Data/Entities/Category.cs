using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Wrap.CrazyEmoji.Api.Data.Entities;

public class Category
{
    [Key]
    public long Id { get; set; }

    [Required]
    public string Name { get; set; } = null!;

    [InverseProperty("Category")]
    public virtual ICollection<Word> Words { get; set; } = new List<Word>();

    [InverseProperty("CategoryNavigation")]
    public virtual ICollection<ActiveRoom> ActiveRooms { get; set; } = new List<ActiveRoom>();
}
