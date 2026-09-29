using System.Net;
using System.Text;

namespace MonoHome.Verifier;

public sealed class GitHubFakeHttpHandler : HttpMessageHandler
{
    static readonly byte[] InitialContent = [10, 20, 30];

    public List<HttpRequestMessage> Requests { get; } = [];
    public List<(HttpMethod Method, string Path, string? Body)> RequestLog { get; } = [];
    public bool ManifestMissing { get; set; }
    public int TransientGetFailures { get; set; }
    public bool ReturnUnprocessable { get; set; }
    public HttpStatusCode? ForcedStatusCode { get; set; }
    public HttpStatusCode? ForcedPutStatusCode { get; set; }
    public TimeSpan? RetryAfter { get; set; }
    public TimeSpan ResponseDelay { get; set; }
    public string SavePath { get; set; } = "saves/emerald/emerald.srm";
    public string ManifestSavePath { get; set; } = "saves/emerald/emerald.srm";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var path = request.RequestUri!.AbsolutePath;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        RequestLog.Add((request.Method, path, body));
        if (ResponseDelay > TimeSpan.Zero)
            await Task.Delay(ResponseDelay, cancellationToken);
        if (ForcedStatusCode is { } forcedStatusCode)
        {
            var response = new HttpResponseMessage(forcedStatusCode)
            {
                Content = new StringContent("{\"message\":\"forced failure\"}", Encoding.UTF8, "application/json"),
            };
            if (RetryAfter is { } retryAfter)
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter);
            return response;
        }
        if (ReturnUnprocessable)
            return new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent("{\"message\":\"invalid branch\"}", Encoding.UTF8, "application/json"),
            };
        if (request.Method == HttpMethod.Get && TransientGetFailures > 0)
        {
            TransientGetFailures--;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }
        if (request.Method == HttpMethod.Get && path == "/repos/test/repo")
            return Json("{\"private\":true,\"default_branch\":\"main\",\"permissions\":{\"push\":true},\"name\":\"repo\"}");

        if (request.Method == HttpMethod.Get && path == $"/repos/test/repo/contents/{SavePath}")
        {
            var reference = Query(request.RequestUri, "ref");
            var content = reference == "commit-2" ? new byte[] { 40, 50, 60 } : InitialContent;
            var blob = reference == "commit-2" ? "blob-2" : "blob-1";
            return Json($"{{\"path\":\"{SavePath}\",\"sha\":\"{blob}\",\"encoding\":\"base64\",\"content\":\"{Convert.ToBase64String(content)}\"}}");
        }

        if (request.Method == HttpMethod.Get && path == "/repos/test/repo/contents/.mono-home/manifest.json")
        {
            if (ManifestMissing)
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            var manifest = $"{{\"schema\":1,\"app\":\"mono-home\",\"saves\":{{\"emerald\":{{\"displayName\":\"绿宝石\",\"path\":\"{ManifestSavePath}\",\"format\":\"srm\"}}}}}}";
            return Json($"{{\"path\":\".mono-home/manifest.json\",\"sha\":\"manifest-1\",\"encoding\":\"base64\",\"content\":\"{Convert.ToBase64String(Encoding.UTF8.GetBytes(manifest))}\"}}");
        }

        if (request.Method == HttpMethod.Get && path == "/repos/test/repo/commits")
            return Json("[{\"sha\":\"commit-1\",\"commit\":{\"message\":\"seed\",\"committer\":{\"date\":\"2026-09-29T00:00:00Z\"}},\"parents\":[]}]");

        if (request.Method == HttpMethod.Get && path == "/repos/test/repo/branches")
            return Json("[{\"name\":\"main\",\"commit\":{\"sha\":\"commit-1\"}},{\"name\":\"save/emerald/test-lineage\",\"commit\":{\"sha\":\"commit-1\"}},{\"name\":\"save/heartgold/other-lineage\",\"commit\":{\"sha\":\"heartgold-1\"}}]");

        if (request.Method == HttpMethod.Put && ForcedPutStatusCode is { } forcedPutStatusCode)
            return new HttpResponseMessage(forcedPutStatusCode)
            {
                Content = new StringContent("{\"message\":\"write conflict\"}", Encoding.UTF8, "application/json"),
            };

        if (request.Method == HttpMethod.Put && path == $"/repos/test/repo/contents/{SavePath}")
            return Json("{\"content\":{\"sha\":\"blob-2\"},\"commit\":{\"sha\":\"commit-2\"}}");

        if (request.Method == HttpMethod.Put && path == "/repos/test/repo/contents/.mono-home/manifest.json")
            return Json("{\"content\":{\"sha\":\"manifest-1\"},\"commit\":{\"sha\":\"manifest-commit\"}}");

        if (request.Method == HttpMethod.Post && path == "/repos/test/repo/git/refs")
            return Json("{\"ref\":\"refs/heads/save/emerald/test-lineage\",\"object\":{\"sha\":\"commit-1\"}}");

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"message\":\"not found\"}", Encoding.UTF8, "application/json"),
        };
    }

    static HttpResponseMessage Json(string value)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(value, Encoding.UTF8, "application/json"),
        };

    static string? Query(Uri uri, string name) => uri.Query
        .TrimStart('?')
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(pair => pair.Split('=', 2))
        .Where(parts => parts.Length == 2 && parts[0] == name)
        .Select(parts => Uri.UnescapeDataString(parts[1]))
        .FirstOrDefault();
}
