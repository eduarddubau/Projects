using Backend.DTOs.Project;
using FluentValidation;

namespace Backend.Validators.Project;

public abstract class ProjectRequestValidatorBase<T> : AbstractValidator<T>
    where T : IProjectRequest
{
    protected ProjectRequestValidatorBase()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Backend.Models.Project.NameMaxLength);

        RuleFor(x => x.Description)
            .MaximumLength(Backend.Models.Project.DescriptionMaxLength)
            .When(x => x.Description is not null);
    }
}
