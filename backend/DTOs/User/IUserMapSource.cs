namespace Backend.DTOs.User;

public interface IUserMapSource
{
    string Email { get; }
    string FirstName { get; }
    string LastName { get; }
}