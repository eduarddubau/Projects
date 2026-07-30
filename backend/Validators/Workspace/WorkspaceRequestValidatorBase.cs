using Backend.DTOs.Workspace;
using FluentValidation;

namespace Backend.Validators.Workspace;

public abstract class WorkspaceRequestValidatorBase<T> : AbstractValidator<T> where T : IWorkspaceRequest
{
    protected WorkspaceRequestValidatorBase()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(60);

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description is not null);
    }
}
