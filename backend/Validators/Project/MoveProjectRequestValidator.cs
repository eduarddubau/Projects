using Backend.DTOs.Project;
using FluentValidation;

namespace Backend.Validators.Project;

public class MoveProjectRequestValidator : AbstractValidator<MoveProjectRequest>
{
    public MoveProjectRequestValidator()
    {
        // An omitted body binds Guid.Empty, which would otherwise reach the guard as a real id.
        RuleFor(x => x.WorkspaceId).NotEmpty();
    }
}
