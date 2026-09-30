using System.Net;
using System.Text;

namespace MonoHome.Verifier;

/// <summary>
/// Stands in for GitHub's OAuth endpoints.
///
/// GitHub answers these endpoints with <c>application/x-www-form-urlencoded</c> unless
/// the request opts in with <c>Accept: application/json</c>. The handler reproduces that
/// contract so a missing header fails loudly instead of silently passing.
/// </summary>
public sealed class GitHubDeviceFlowFakeHttpHandler : HttpMessageHandler
{
    public int PollCount { get; private set; }

    public int RefreshCount { get; private set; }

    /// <summary>True when every request carried <c>Accept: application/json</c>.</summary>
    public bool AlwaysRequestedJson { get; private set; } = true;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!AcceptsJson(request))
        {
            AlwaysRequestedJson = false;
            return Form("error=incorrect_client_credentials&error_description=Accept+header+missing");
        }

        if (request.RequestUri!.AbsolutePath == "/login/device/code")
            return Json("{\"device_code\":\"device-1\",\"user_code\":\"ABCD-EFGH\",\"verification_uri\":\"https://github.com/login/device\",\"expires_in\":60,\"interval\":0}");

        if (request.RequestUri.AbsolutePath == "/login/oauth/access_token")
        {
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (form.Contains("grant_type=refresh_token", StringComparison.Ordinal))
            {
                RefreshCount++;
                return Json("{\"access_token\":\"ghu-refreshed\",\"token_type\":\"bearer\",\"expires_in\":28800,\"refresh_token\":\"ghr-next\"}");
            }
            PollCount++;
            return PollCount == 1
                ? Json("{\"error\":\"authorization_pending\"}")
                : Json("{\"access_token\":\"ghu-test\",\"token_type\":\"bearer\",\"expires_in\":28800,\"refresh_token\":\"ghr-test\"}");
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    static bool AcceptsJson(HttpRequestMessage request) =>
        request.Headers.Accept.Any(header => header.MediaType == "application/json");

    static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json"),
    };

    static HttpResponseMessage Form(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/x-www-form-urlencoded"),
    };
}
