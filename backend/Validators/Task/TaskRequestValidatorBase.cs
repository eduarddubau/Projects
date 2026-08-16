using Backend.DTOs.Task;
using FluentValidation;

namespace Backend.Validators.Task;

public abstract class TaskRequestValidatorBase<T> : AbstractValidator<T>
    where T : ITaskRequest
{
    protected TaskRequestValidatorBase()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(Backend.Models.TaskItem.TitleMaxLength);

        RuleFor(x => x.Description)
            .MaximumLength(Backend.Models.TaskItem.DescriptionMaxLength)
            .When(x => x.Description is not null);

        RuleFor(x => x.Status).IsInEnum();

        // A 400: unlike the assignee rule, this needs no lookup to decide.
        RuleFor(x => x.DueDate)
            .GreaterThanOrEqualTo(x => x.StartDate!.Value)
            .When(x => x.StartDate is not null && x.DueDate is not null)
            .WithMessage("The due date cannot be before the start date.");
    }
}
