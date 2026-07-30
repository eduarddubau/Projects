using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Models;

public class Workspace : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsPersonal { get; set; }

    public virtual ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();
    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();

    [ForeignKey("CreatedBy")]
    public virtual User? Creator { get; set; }

    [ForeignKey("UpdatedBy")]
    public virtual User? Updater { get; set; }
}