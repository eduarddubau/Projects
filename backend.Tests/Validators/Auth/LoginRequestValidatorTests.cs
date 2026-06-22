using Backend.DTOs.Auth;
using Backend.Validators.Auth;

namespace Backend.Tests.Validators.Auth;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_HasNoErrors()
    {
        var result = _validator.Validate(new LoginRequest("ada@example.com", "Str0ng!Pass"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_WithInvalidEmail_HasError(string email)
    {
        var result = _validator.Validate(new LoginRequest(email, "Str0ng!Pass"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginRequest.Email));
    }

    [Fact]
    public void Validate_WithEmptyPassword_HasError()
    {
        var result = _validator.Validate(new LoginRequest("ada@example.com", ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginRequest.Password));
    }
}
