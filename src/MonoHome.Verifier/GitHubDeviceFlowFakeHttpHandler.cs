using System.Net;
using System.Text;

namespace MonoHome.Verifier;

public sealed class GitHubDeviceFlowFakeHttpHandler : HttpMessageHandler
{
    public int PollCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri!.AbsolutePath == "/login/device/code")
            return Task.FromResult(Json("{\"device_code\":\"device-1\",\"user_code\":\"ABCD-EFGH\",\"verification_uri\":\"https://github.com/login/device\",\"expires_in\":60,\"interval\":0}"));

        if (request.RequestUri.AbsolutePath == "/login/oauth/access_token")
        {
            PollCount++;
            return Task.FromResult(PollCount == 1
                ? Json("{\"error\":\"authorization_pending\"}")
                : Json("{\"access_token\":\"ghu-test\",\"token_type\":\"bearer\",\"expires_in\":28800,\"refresh_token\":\"ghr-test\"}"));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json"),
    };
}
