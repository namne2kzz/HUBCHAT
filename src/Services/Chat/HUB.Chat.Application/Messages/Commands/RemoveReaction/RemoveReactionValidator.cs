using FluentValidation;

namespace HUB.Chat.Application.Messages.Commands.RemoveReaction;

/// <summary>Validates <see cref="RemoveReactionCommand"/>.</summary>
public sealed class RemoveReactionValidator : AbstractValidator<RemoveReactionCommand>
{
    /// <summary>Sets up validation rules.</summary>
    public RemoveReactionValidator()
    {
        RuleFor(x => x.MessageId).NotEmpty();
        RuleFor(x => x.ActingUserId).NotEmpty();
        // The column is 64 chars; without this an over-long value reached the database and surfaced as a 500.
        RuleFor(x => x.Emoji).NotEmpty().MaximumLength(64);
    }
}
