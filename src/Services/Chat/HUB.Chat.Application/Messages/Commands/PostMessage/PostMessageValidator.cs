using FluentValidation;

namespace HUB.Chat.Application.Messages.Commands.PostMessage;

/// <summary>Validates <see cref="PostMessageCommand"/>.</summary>
public sealed class PostMessageValidator : AbstractValidator<PostMessageCommand>
{
    /// <summary>Largest a single attachment may be (bytes). Mirrors the Media service's upload cap.</summary>
    private const long MaxAttachmentBytes = 25L * 1024 * 1024;

    /// <summary>Most attachments one message may carry.</summary>
    private const int MaxAttachments = 10;

    /// <summary>Sets up validation rules.</summary>
    public PostMessageValidator()
    {
        RuleFor(x => x.ChannelId).NotEmpty();
        RuleFor(x => x.ActingUserId).NotEmpty();

        // Body is optional when the message carries attachments (an image-only message, CF-5).
        RuleFor(x => x.Body)
            .NotEmpty().When(x => x.Attachments is null || x.Attachments.Count == 0)
            .WithMessage("A message must have text or at least one attachment.");
        RuleFor(x => x.Body).MaximumLength(8000);

        // Each mention is a notification (and an email) — cap it so one message cannot spam a workspace.
        RuleFor(x => x.MentionedUserIds).Must(ids => ids.Count <= 50).WithMessage("A message can mention at most 50 people.");
        RuleFor(x => x.ClientMessageId).NotEqual(Guid.Empty).When(x => x.ClientMessageId is not null);

        When(x => x.Attachments is not null && x.Attachments.Count > 0, () =>
        {
            RuleFor(x => x.Attachments!).Must(a => a.Count <= MaxAttachments)
                .WithMessage($"A message can have at most {MaxAttachments} attachments.");
            RuleForEach(x => x.Attachments!).ChildRules(a =>
            {
                a.RuleFor(x => x.FileId).NotEmpty();
                a.RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
                a.RuleFor(x => x.Mime).NotEmpty().MaximumLength(255);
                a.RuleFor(x => x.Size).GreaterThan(0).LessThanOrEqualTo(MaxAttachmentBytes)
                    .WithMessage($"Each attachment must be between 1 byte and {MaxAttachmentBytes} bytes.");
            });
        });
    }
}
