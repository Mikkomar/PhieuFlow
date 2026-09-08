using System.Net;
using System.Text;

namespace PhieuFlow.Tests.Unit;

/// <summary>
/// Shared hand-rolled test doubles for the <c>PhieuFlow.ServiceAuth</c> token classes —
/// no mocking library, matching house style.
/// </summary>
internal sealed record RecordedRequest(
    HttpMethod Method, Uri? Uri, string? Authorization, string? ContentType, string? Body);

/// <summary>
/// A queued <see cref="HttpMessageHandler"/>: each queued responder answers one request;
/// once a single responder remains it answers every further request. Records what it saw.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responders = new();

    public List<RecordedRequest> Requests { get; } = [];

    public int SendCount => Requests.Count;

    public StubHttpMessageHandler Respond(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => Respond((request, _) => Task.FromResult(responder(request)));

    public StubHttpMessageHandler Respond(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responders.Enqueue(responder);
        return this;
    }

    /// <summary>Answers with a Keycloak-shaped token payload.</summary>
    public StubHttpMessageHandler RespondWithToken(string accessToken, int expiresInSeconds)
        => Respond(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"access_token":"{{accessToken}}","expires_in":{{expiresInSeconds}}}""",
                Encoding.UTF8,
                "application/json"),
        });

    public StubHttpMessageHandler RespondWithStatus(HttpStatusCode status)
        => Respond(_ => new HttpResponseMessage(status));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.Authorization?.ToString(),
            request.Content?.Headers.ContentType?.ToString(),
            body));

        if (_responders.Count == 0)
        {
            throw new InvalidOperationException(
                $"StubHttpMessageHandler received an unexpected {request.Method} {request.RequestUri}.");
        }

        var responder = _responders.Count == 1 ? _responders.Peek() : _responders.Dequeue();
        return await responder(request, cancellationToken);
    }
}

/// <summary>Hands every named client the same stub handler, which it does not own.</summary>
internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

/// <summary>A <see cref="TimeProvider"/> whose clock only moves when the test moves it.</summary>
internal sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}
