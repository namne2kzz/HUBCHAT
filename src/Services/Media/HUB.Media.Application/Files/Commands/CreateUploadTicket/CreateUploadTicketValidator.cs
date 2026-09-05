using FluentValidation;

namespace HUB.Media.Application.Files.Commands.CreateUploadTicket;

/// <summary>Validates <see cref="CreateUploadTicketCommand"/> (size + content-type allow-list).</summary>
public sealed class CreateUploadTicketValidator : AbstractValidator<CreateUploadTicketCommand>
{
    /// <summary>Max upload size (100 MB).</summary>
    public const long MaxSizeBytes = 100L * 1024 * 1024;

    private static readonly string[] AllowedPrefixes = ["image/", "video/", "audio/", "text/", "application/"];

    /// <summary>Sets up validation rules.</summary>
    public CreateUploadTicketValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.ActingUserId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(260);
        RuleFor(x => x.SizeBytes).GreaterThan(0).LessThanOrEqualTo(MaxSizeBytes);
        RuleFor(x => x.ContentType).NotEmpty()
            .Must(ct => AllowedPrefixes.Any(p => ct.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Content type is not allowed.");
    }
}
