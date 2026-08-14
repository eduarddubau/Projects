using Backend.DTOs.Workspace;
using FluentValidation;

namespace Backend.Validators.Workspace;

// The string converter refuses an unknown name; IsInEnum refuses {"role": 99}.
public class AddMemberRequestValidator : AbstractValidator<AddMemberRequest>
{
    public AddMemberRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Role).IsInEnum();
    }
}

public class ChangeMemberRoleRequestValidator : AbstractValidator<ChangeMemberRoleRequest>
{
    public ChangeMemberRoleRequestValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
    }
}
