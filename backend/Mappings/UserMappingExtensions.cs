using Backend.DTOs.User;
using Backend.Models;

namespace Backend.Mappings;

public static class UserMappingExtensions
{
    public static string? GetDisplayName(this User? user)
    {
        if (user is null) return null;
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }

    public static UserResponseDto MapToDto(this User user) => new()
    {
        Id = user.Id,
        Email = user.Email ?? string.Empty,
        FirstName = user.FirstName,
        LastName = user.LastName,
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
            IsDeleted = u.IsDeleted,
            DeletedAt = u.DeletedAt,
            CreatedAt = u.CreatedAt,
            CreatedBy = u.CreatedBy,
            CreatedByDisplayName = u.Creator == null
                ? string.Empty
                : (u.Creator.FirstName + " " + u.Creator.LastName).Trim() != ""
                    ? (u.Creator.FirstName + " " + u.Creator.LastName).Trim()
                    : u.Creator.Email ?? string.Empty,
            UpdatedAt = u.UpdatedAt,
            UpdatedBy = u.UpdatedBy,
            UpdatedByDisplayName = u.Updater == null
                ? string.Empty
                : (u.Updater.FirstName + " " + u.Updater.LastName).Trim() != ""
                    ? (u.Updater.FirstName + " " + u.Updater.LastName).Trim()
                    : u.Updater.Email ?? string.Empty
        });
    }

    public static User ToEntity<T>(this T dto) where T : class, IUserMapSource
    {
        return new User
        {
            UserName = dto.Email,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName
        };
    }
}