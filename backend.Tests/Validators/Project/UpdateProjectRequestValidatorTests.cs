using Backend.DTOs.Project;
using Backend.Validators.Project;

namespace Backend.Tests.Validators.Project;

public class UpdateProjectRequestValidatorTests
{
    private readonly UpdateProjectRequestValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_HasNoErrors()
    {
        var result = _validator.Validate(new UpdateProjectRequest("My Project", "A short description"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithNullDescription_HasNoErrors()
    {
        var result = _validator.Validate(new UpdateProjectRequest("My Project", null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithEmptyName_HasError()
    {
        var result = _validator.Validate(new UpdateProjectRequest("", null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateProjectRequest.Name));
    }

    [Fact]
    public void Validate_WithNameTooLong_HasError()
    {
        var result = _validator.Validate(new UpdateProjectRequest(new string('a', 101), null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateProjectRequest.Name));
    }

    [Fact]
    public void Validate_WithDescriptionTooLong_HasError()
    {
        var result = _validator.Validate(new UpdateProjectRequest("My Project", new string('a', 501)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateProjectRequest.Description));
    }
}
