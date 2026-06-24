namespace Backend.DTOs;

public abstract record AuditResponseDto
{
    public Guid Id { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? UpdatedBy { get; init; }
    public string? CreatedByDisplayName { get; init; }
    public string? UpdatedByDisplayName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAt { get; init; }
}
