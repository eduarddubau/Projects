using Backend.DTOs.Project;
using FluentValidation;

namespace Backend.Validators.Project;

public class CreateProjectDtoValidator : AbstractValidator<CreateProjectDto>
{
    public CreateProjectDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description is not null);
    }
}