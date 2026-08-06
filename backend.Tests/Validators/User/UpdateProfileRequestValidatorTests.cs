using Backend.DTOs.User;
using Backend.Validators.User;

namespace Backend.Tests.Validators.User;

public class UpdateProfileRequestValidatorTests
{
    private readonly UpdateProfileRequestValidator _validator = new();

    private static UpdateProfileRequest ValidRequest() => new()
    {
        FirstName = "Ada",
        LastName = "Lovelace",
        Email = "ada@example.com"
    };

    [Fact]
    public void Validate_WithValidRequest_HasNoErrors()
    {
        var result = _validator.Validate(ValidRequest());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithEmptyFirstName_HasError()
    {
        var request = ValidRequest() with { FirstName = "" };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateProfileRequest.FirstName));
    }

    [Fact]
    public void Validate_WithFirstNameTooLong_HasError()
    {
        var request = ValidRequest() with { FirstName = new string('a', 51) };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateProfileRequest.FirstName));
    }

    [Fact]
    public void Validate_WithEmptyLastName_HasError()
    {
        var request = ValidRequest() with { LastName = "" };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateProfileRequest.LastName));
    }

    [Fact]
    public void Validate_WithLastNameTooLong_HasError()
    {
        var request = ValidRequest() with { LastName = new string('a', 51) };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateProfileRequest.LastName));
    }

    [Fact]
    public void Validate_WithNicknameTooLong_HasError()
    {
        var request = ValidRequest() with { Nickname = new string('a', 31) };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateProfileRequest.Nickname));
    }

    [Fact]
    public void Validate_WithoutNickname_HasNoErrors()
    {
        var result = _validator.Validate(ValidRequest() with { Nickname = null });

        Assert.True(result.IsValid);
    }
}
