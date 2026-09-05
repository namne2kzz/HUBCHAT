using FluentValidation;

namespace HUB.Chat.Application.Channels.Commands.CreateChannel;

/// <summary>Validates <see cref="CreateChannelCommand"/>.</summary>
public sealed class CreateChannelValidator : AbstractValidator<CreateChannelCommand>
{
    /// <summary>Sets up validation rules.</summary>
    public CreateChannelValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.ActingUserId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Topic).MaximumLength(500);
    }
}
