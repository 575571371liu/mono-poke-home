using System.Net;
using System.Text;

namespace MonoHome.Verifier;

public sealed class GitHubFakeHttpHandler : HttpMessageHandler
{
    static readonly byte[] InitialContent = [10, 20, 30];

    public List<HttpRequestMessage> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var path = request.RequestUri!.AbsolutePath;
        if (request.Method == HttpMethod.Get && path == "/repos/test/repo")
            return Json("{\"private\":true,\"default_branch\":\"main\",\"permissions\":{\"push\":true},\"name\":\"repo\"}");

        if (request.Method == HttpMethod.Get && path == "/repos/test/repo/contents/saves/emerald/emerald.srm")
        {
            var reference = Query(request.RequestUri, "ref");
            var content = reference == "commit-2" ? new byte[] { 40, 50, 60 } : InitialContent;
            var blob = reference == "commit-2" ? "blob-2" : "blob-1";
            return Json($"{{\"path\":\"saves/emerald/emerald.srm\",\"sha\":\"{blob}\",\"encoding\":\"base64\",\"content\":\"{Convert.ToBase64String(content)}\"}}");
        }

        if (request.Method == HttpMethod.Get && path == "/repos/test/repo/commits")
            return Json("[{\"sha\":\"commit-1\",\"commit\":{\"message\":\"seed\",\"committer\":{\"date\":\"2026-09-29T00:00:00Z\"}},\"parents\":[]}]");

        if (request.Method == HttpMethod.Put && path == "/repos/test/repo/contents/saves/emerald/emerald.srm")
            return Json("{\"content\":{\"sha\":\"blob-2\"},\"commit\":{\"sha\":\"commit-2\"}}");

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
