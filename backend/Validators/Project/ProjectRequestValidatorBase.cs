using Backend.DTOs.Project;
using FluentValidation;

namespace Backend.Validators.Project;

public abstract class ProjectRequestValidatorBase<T> : AbstractValidator<T> where T : IProjectRequest
{
    protected ProjectRequestValidatorBase()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description is not null);
    }
}
