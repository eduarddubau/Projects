using Backend.Models;

public static class UserExtensions
{
    public static string GetDisplayName(this User? user)
    {
        if (user == null) return "System (Object was Null)";

        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        
        if (!string.IsNullOrWhiteSpace(fullName)) return fullName;

        return user.Email ?? "System (Email was Null)";
    }
}