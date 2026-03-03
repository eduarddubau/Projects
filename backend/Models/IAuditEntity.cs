namespace Backend.Models;

public interface IAuditEntity
{
    Guid Id { get; set; }
    DateTime CreatedAt { get; set; }
    string? CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    string? UpdatedBy { get; set; }
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}