namespace Backend.DTOs;

/// <summary>Server policy a client has to know to describe itself accurately.</summary>
public record ClientConfigDto
{
    public int TrashWindowDays { get; init; }
}
