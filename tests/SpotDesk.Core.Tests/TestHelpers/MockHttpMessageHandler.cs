using System.Net;

namespace SpotDesk.Core.Tests.TestHelpers;

/// <summary>
/// In-memory <see cref="HttpMessageHandler"/> that lets tests queue pre-baked responses
/// and inspect the requests that were sent.
/// </summary>
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    private readonly List<HttpRequestMessage>   _requests  = new();

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    /// <summary>Enqueue a response that will be returned for the next call.</summary>
    public void Enqueue(HttpStatusCode status, string json)
        => _responses.Enqueue(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

    /// <summary>Enqueue a 200 OK with the given JSON body.</summary>
    public void EnqueueOk(string json) => Enqueue(HttpStatusCode.OK, json);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken  cancellationToken)
    {
        _requests.Add(request);

        if (_responses.Count == 0)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{\"error\":\"no_more_responses\"}")
            });

        return Task.FromResult(_responses.Dequeue());
    }
}
