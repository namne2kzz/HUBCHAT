using FluentValidation;

namespace HUB.Chat.Application.Messages.Commands.PostMessage;

/// <summary>Validates <see cref="PostMessageCommand"/>.</summary>
public sealed class PostMessageValidator : AbstractValidator<PostMessageCommand>
{
    /// <summary>Sets up validation rules.</summary>
    public PostMessageValidator()
    {
        RuleFor(x => x.ChannelId).NotEmpty();
        RuleFor(x => x.ActingUserId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(8000);
        // Each mention is a notification (and an email) — cap it so one message cannot spam a workspace.
        RuleFor(x => x.MentionedUserIds).Must(ids => ids.Count <= 50).WithMessage("A message can mention at most 50 people.");
        RuleFor(x => x.ClientMessageId).NotEqual(Guid.Empty).When(x => x.ClientMessageId is not null);
    }
}
