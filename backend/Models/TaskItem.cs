using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Models;

/// <summary>A unit of work inside a project.</summary>
public class TaskItem : BaseEntity
{
    /// <summary>Single source of truth for the column and the validator.</summary>
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    public required string Title { get; set; }
    public string? Description { get; set; }

    public TaskItemStatus Status { get; set; }

    /// <summary>Sort order within (ProjectId, Status). Contiguous from 0 after a move; gaps are harmless.</summary>
    public int Position { get; set; }

    public Guid ProjectId { get; set; }

    // Nullable nav, required FK: "not loaded" and "no project" are different states.
    public Project? Project { get; set; }

    public Guid? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }

    /// <summary>Derived from the status transition, never sent by a client.</summary>
    public DateTime? CompletedAt { get; set; }

    [ForeignKey("CreatedBy")]
    public User? Creator { get; set; }

    [ForeignKey("UpdatedBy")]
    public User? Updater { get; set; }
}
