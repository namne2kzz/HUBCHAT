using FluentValidation;

namespace HUB.Chat.Application.Channels.Commands.OpenLinkedThread;

/// <summary>Validates <see cref="OpenLinkedThreadCommand"/>.</summary>
public sealed class OpenLinkedThreadValidator : AbstractValidator<OpenLinkedThreadCommand>
{
    /// <summary>Sets up validation rules.</summary>
    public OpenLinkedThreadValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.ExternalId).NotEmpty();
        RuleFor(x => x.ActingUserId).NotEmpty();
        RuleFor(x => x.ExternalKey).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Title).MaximumLength(100);
        RuleFor(x => x.Url).MaximumLength(500);
    }
}
