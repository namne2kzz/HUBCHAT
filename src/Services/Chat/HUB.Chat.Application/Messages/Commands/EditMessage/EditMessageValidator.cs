using FluentValidation;

namespace HUB.Chat.Application.Messages.Commands.EditMessage;

/// <summary>Validates <see cref="EditMessageCommand"/>.</summary>
public sealed class EditMessageValidator : AbstractValidator<EditMessageCommand>
{
    /// <summary>Sets up validation rules.</summary>
    public EditMessageValidator()
    {
        RuleFor(x => x.MessageId).NotEmpty();
        RuleFor(x => x.ActingUserId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(8000);
    }
}
