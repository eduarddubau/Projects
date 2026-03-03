namespace Backend.Models;

public abstract class BaseEntity : IAuditEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }    
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}