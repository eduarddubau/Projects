using Backend.DTOs.User;
using Backend.Models;

namespace Backend.Mappings;

public static class UserMappingExtensions
{
    /// <summary>
    /// Kept deliberately trivial so it stays equivalent to the inline form the IQueryable
    /// projections below must use — EF can't translate a method call into SQL, so the rule
    /// necessarily exists twice. There is no blank-name fallback because FirstName and
    /// LastName are required, validated NotEmpty, and NOT NULL in the database.
    /// </summary>
    public static string? GetDisplayName(this User? user) =>
        user is null ? null : user.FirstName + " " + user.LastName;

    public static UserResponseDto MapToDto(this User user) => new()
    {
        Id = user.Id,
        Email = user.Email ?? string.Empty,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Nickname = user.Nickname,
        IsDeleted = user.IsDeleted,
        DeletedAt = user.DeletedAt,
        CreatedAt = user.CreatedAt,
        CreatedBy = user.CreatedBy,
        CreatedByDisplayName = user.Creator.GetDisplayName() ?? string.Empty,
        UpdatedAt = user.UpdatedAt,
        UpdatedBy = user.UpdatedBy,
        UpdatedByDisplayName = user.Updater.GetDisplayName() ?? string.Empty
    };

    public static IQueryable<UserResponseDto> MapToDto(this IQueryable<User> query)
    {
        return query.Select(u => new UserResponseDto
        {
            Id = u.Id,
            Email = u.Email ?? string.Empty,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Nickname = u.Nickname,
            IsDeleted = u.IsDeleted,
            DeletedAt = u.DeletedAt,
            CreatedAt = u.CreatedAt,
            CreatedBy = u.CreatedBy,
            CreatedByDisplayName = u.Creator == null
                ? string.Empty
                : u.Creator.FirstName + " " + u.Creator.LastName,
            UpdatedAt = u.UpdatedAt,
            UpdatedBy = u.UpdatedBy,
            UpdatedByDisplayName = u.Updater == null
                ? string.Empty
                : u.Updater.FirstName + " " + u.Updater.LastName
        });
    }

    public static User ToEntity<T>(this T dto) where T : class, IUserMapSource
    {
        // UserName is deliberately not the email. Both columns carry live-scoped unique
        // indexes, so a coupled pair means every email change is two writes and forgetting
        // one silently reserves the old address forever. Deriving it from the id keeps a
        // single identifier and an invariant you can check in one query.
        var id = Guid.CreateVersion7();

        return new User
        {
            Id = id,
            UserName = id.ToString("N"),
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Nickname = dto.Nickname
        };
    }

}
