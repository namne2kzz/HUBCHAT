using FluentValidation;

namespace HUB.Chat.Application.Channels.Commands.OpenGroupDm;

/// <summary>Validates <see cref="OpenGroupDmCommand"/>.</summary>
public sealed class OpenGroupDmValidator : AbstractValidator<OpenGroupDmCommand>
{
    /// <summary>A group DM has 3–8 people total, so 2–7 others besides the caller.</summary>
    public const int MinOthers = 2;

    /// <summary>Upper bound on the other participants (8 total − the caller).</summary>
    public const int MaxOthers = 7;

    /// <summary>Sets up validation rules.</summary>
    public OpenGroupDmValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.ActingUserId).NotEmpty();

        // 2–7 others → 3–8 total. One other is a plain DM; more than seven is not a "small group".
        RuleFor(x => x.MemberIds)
            .Must(ids => ids is { Count: >= MinOthers and <= MaxOthers })
            .WithMessage($"A group message needs {MinOthers + 1}–{MaxOthers + 1} people (you plus {MinOthers}–{MaxOthers} others).");

        RuleFor(x => x.MemberIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Group members must be distinct.")
            .When(x => x.MemberIds is not null);

        RuleFor(x => x.MemberIds)
            .Must((cmd, ids) => !ids.Contains(cmd.ActingUserId))
            .WithMessage("You are already in the group — don't list yourself in the members.")
            .When(x => x.MemberIds is not null);

        RuleForEach(x => x.MemberIds).NotEmpty();
    }
}
