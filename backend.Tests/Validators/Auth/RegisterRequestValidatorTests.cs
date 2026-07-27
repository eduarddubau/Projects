using Backend.DTOs.Auth;
using Backend.Validators.Auth;

namespace Backend.Tests.Validators.Auth;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    private static RegisterRequest ValidRequest() => new()
    {
        FirstName = "Ada",
        LastName = "Lovelace",
        Email = "ada@example.com",
        Password = "Str0ng!Pass"
    };

    [Fact]
    public void Validate_WithValidRequest_HasNoErrors()
    {
        var result = _validator.Validate(ValidRequest());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_WithInvalidEmail_HasError(string email)
    {
        var request = ValidRequest() with { Email = email };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequest.Email));
    }

    [Theory]
    [InlineData("short1!")]          // too short
    [InlineData("nouppercase1!")]    // missing uppercase
    [InlineData("NOLOWERCASE1!")]    // missing lowercase
    [InlineData("NoNumbersHere!")]   // missing digit
    [InlineData("NoSpecialChar1")]   // missing special character
    public void Validate_WithWeakPassword_HasError(string password)
    {
        var request = ValidRequest() with { Password = password };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequest.Password));
    }

    [Fact]
    public void Validate_WithEmptyFirstName_HasError()
    {
        var request = ValidRequest() with { FirstName = "" };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequest.FirstName));
    }

    [Fact]
    public void Validate_WithNicknameTooLong_HasError()
    {
        var request = ValidRequest() with { Nickname = new string('a', 31) };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequest.Nickname));
    }

    [Fact]
    public void Validate_WithoutNickname_HasNoErrors()
    {
        var result = _validator.Validate(ValidRequest() with { Nickname = null });

        Assert.True(result.IsValid);
    }
}
