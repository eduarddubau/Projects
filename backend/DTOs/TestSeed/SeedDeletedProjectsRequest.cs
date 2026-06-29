namespace Backend.DTOs.TestSeed;

public record SeedDeletedProjectsRequest
{
    public required List<SeedDeletedProjectItem> Projects { get; init; }
}

public record SeedDeletedProjectItem
{
    public required string Name { get; init; }
    public required int DeletedDaysAgo { get; init; }
}

public record SeededProjectDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
}
