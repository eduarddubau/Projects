using Backend.DTOs.Task;
using FluentValidation;

namespace Backend.Validators.Task;

public class MoveTaskRequestValidator : AbstractValidator<MoveTaskRequest>
{
    public MoveTaskRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();

        // Both null is legitimate — it means "the end of the column".
        RuleFor(x => x.NextTaskId)
            .NotEqual(x => x.PreviousTaskId)
            .When(x => x.PreviousTaskId is not null)
            .WithMessage("A task cannot be dropped between a card and itself.");
    }
}
