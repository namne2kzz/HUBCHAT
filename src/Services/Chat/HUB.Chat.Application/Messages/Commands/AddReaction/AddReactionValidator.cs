using FluentValidation;

namespace HUB.Chat.Application.Messages.Commands.AddReaction;

/// <summary>Validates <see cref="AddReactionCommand"/>.</summary>
public sealed class AddReactionValidator : AbstractValidator<AddReactionCommand>
{
    /// <summary>Sets up validation rules.</summary>
    public AddReactionValidator()
    {
        RuleFor(x => x.MessageId).NotEmpty();
        RuleFor(x => x.ActingUserId).NotEmpty();
        // The column is 64 chars; without this an over-long value reached the database and surfaced as a 500.
        RuleFor(x => x.Emoji).NotEmpty().MaximumLength(64);
    }
}
