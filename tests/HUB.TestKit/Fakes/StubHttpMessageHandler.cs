using System.Net;

namespace HUB.TestKit.Fakes;

/// <summary>Test double that returns canned responses based on the request path, and records call counts.</summary>
/// <param name="responder">Maps a relative path (e.g. "users/{id}") to an HTTP response.</param>
public sealed class StubHttpMessageHandler(Func<string, HttpResponseMessage> responder) : HttpMessageHandler
{
    /// <summary>Number of requests that reached this handler.</summary>
    public int CallCount { get; private set; }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        var pathAndQuery = request.RequestUri!.PathAndQuery.TrimStart('/');
        return Task.FromResult(responder(pathAndQuery));
    }

    /// <summary>Builds a JSON 200 response.</summary>
    public static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
    };

    /// <summary>Builds a 404 response.</summary>
    public static HttpResponseMessage NotFound() => new(HttpStatusCode.NotFound);
}
