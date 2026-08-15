using Backend.DTOs.Workspace;
using FluentValidation;

namespace Backend.Validators.Workspace;

// The string converter refuses an unknown name; IsInEnum refuses {"role": 99}.
// An omitted role is allowed here; AddMemberAsync defaults it to Member.
public class AddMemberRequestValidator : AbstractValidator<AddMemberRequest>
{
    public AddMemberRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Role).IsInEnum();
    }
}

// Without NotNull, a body of {} demotes an owner to Member.
public class ChangeMemberRoleRequestValidator : AbstractValidator<ChangeMemberRoleRequest>
{
    public ChangeMemberRoleRequestValidator()
    {
        RuleFor(x => x.Role).NotNull().IsInEnum();
    }
}
