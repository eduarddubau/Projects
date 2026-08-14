using Backend.DTOs.Workspace;
using FluentValidation;

namespace Backend.Validators.Workspace;

public class AcceptInviteRequestValidator : AbstractValidator<AcceptInviteRequest>
{
    public AcceptInviteRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty();
    }
}
