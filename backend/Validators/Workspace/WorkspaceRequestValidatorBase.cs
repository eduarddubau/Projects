using Backend.DTOs.Workspace;
using FluentValidation;

namespace Backend.Validators.Workspace;

public abstract class WorkspaceRequestValidatorBase<T> : AbstractValidator<T>
    where T : IWorkspaceRequest
{
    protected WorkspaceRequestValidatorBase()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Backend.Models.Workspace.NameMaxLength);

        RuleFor(x => x.Description)
            .MaximumLength(Backend.Models.Workspace.DescriptionMaxLength)
            .When(x => x.Description is not null);
    }
}
