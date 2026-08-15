using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Models;

public class Project : BaseEntity
{
    /// <summary>Single source of truth for the column and the validator.</summary>
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    public required string Name { get; set; }
    public string? Description { get; set; }

    public Guid WorkspaceId { get; set; }

    // Nullable nav, required FK: "not loaded" and "no workspace" are different states.
    public virtual Workspace? Workspace { get; set; }

    [ForeignKey("CreatedBy")]
    public virtual User? Creator { get; set; }

    [ForeignKey("UpdatedBy")]
    public virtual User? Updater { get; set; }
}
