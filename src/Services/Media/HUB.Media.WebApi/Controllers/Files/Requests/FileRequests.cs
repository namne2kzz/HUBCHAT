namespace HUB.Media.WebApi.Controllers.Files.Requests;

/// <summary>Body for requesting an upload ticket.</summary>
/// <param name="WorkspaceId">Owning workspace.</param>
/// <param name="ChannelId">Attached channel (optional).</param>
/// <param name="FileName">Original file name.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="SizeBytes">Declared size in bytes.</param>
public sealed record CreateUploadTicketRequest(Guid WorkspaceId, Guid? ChannelId, string FileName, string ContentType, long SizeBytes);
