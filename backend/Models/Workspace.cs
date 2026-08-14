using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Models;

public class Workspace : BaseEntity
{
    /// <summary>Single source of truth for the column, the validator and any derived name.</summary>
    public const int NameMaxLength = 60;
    public const int DescriptionMaxLength = 500;

    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsPersonal { get; set; }

    public virtual ICollection<WorkspaceMember> Members { get; set; } = [];
    public virtual ICollection<Project> Projects { get; set; } = [];

    [ForeignKey("CreatedBy")]
    public virtual User? Creator { get; set; }

    [ForeignKey("UpdatedBy")]
    public virtual User? Updater { get; set; }
}
