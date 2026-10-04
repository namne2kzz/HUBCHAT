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
        RuleFor(x => x.ClientMessageId).NotEqual(Guid.Empty).When(x => x.ClientMessageId is not null);
    }
}
