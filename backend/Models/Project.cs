using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Models;

public class Project : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }

    [ForeignKey("CreatedBy")]
    public virtual User? Creator { get; set; }

    [ForeignKey("UpdatedBy")]
    public virtual User? Updater { get; set; }
}