using MonoHome.Core.Saves;
using MonoHome.Core.Transfers;
using MonoHome.Core.Repository;
using MonoHome.Core.Sync;
using MonoHome.Core.Sync.GitHub;
using MonoHome.Verifier;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using PKHeX.Core;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "fixtures", "private"));
var emeraldPath = Path.Combine(root, "emerald.srm");
var heartGoldPath = Path.Combine(root, "heartgold.sav");
var emeraldHash = Hash(emeraldPath);
var heartGoldHash = Hash(heartGoldPath);
var desktopEmerald = @"C:\Users\liujinwen\Desktop\Pokemon Emerald.srm";
var desktopHeartGold = @"C:\Users\liujinwen\Desktop\口袋妖怪(精灵宝可梦) 心灵之金 官译修正版v1.5.0(中).sav";
AssertEqual(Hash(desktopEmerald), emeraldHash, "Emerald fixture matches user-provided save");
AssertEqual(Hash(desktopHeartGold), heartGoldHash, "HeartGold fixture matches user-provided save");
var emerald = SaveInspector.Inspect(emeraldPath);
var heartGold = SaveInspector.Inspect(heartGoldPath);

AssertEqual("Emerald", emerald.Game, "Emerald fixture game");
AssertEqual(3, emerald.Generation, "Emerald fixture generation");
AssertEqual("HeartGold", heartGold.Game, "HeartGold fixture game");
AssertEqual(4, heartGold.Generation, "HeartGold fixture generation");
var emeraldPokemon = BoxReader.Read(emeraldPath);
var heartGoldPokemon = BoxReader.Read(heartGoldPath);
var emeraldPages = BoxReader.ReadPages(File.ReadAllBytes(emeraldPath), "emerald.srm");
var heartGoldPages = BoxReader.ReadPages(File.ReadAllBytes(heartGoldPath), "heartgold.sav");
AssertEqual("随身携带", emeraldPages[0].Name, "first storage page is party");
AssertEqual(6, emeraldPages[0].Capacity, "party has six positions");
AssertEqual(30, emeraldPages[1].Capacity, "standard box has thirty positions");
AssertTrue(emeraldPages.Skip(1).Any(page => page.Slots.Any(slot => slot.Pokemon is null)), "empty box positions are retained");
AssertEqual("随身携带", heartGoldPages[0].Name, "HeartGold first storage page is party");
AssertEqual(30, heartGoldPages[1].Capacity, "HeartGold standard box has thirty positions");
AssertTrue(emeraldPokemon.Count > 0, "Emerald has readable Pokémon slots");
AssertTrue(heartGoldPokemon.Count > 0, "HeartGold has readable Pokémon slots");
AssertTrue(emeraldPokemon.All(slot => slot.Nickname is not null), "reader exposes Pokémon nicknames");
Console.WriteLine($"Emerald slots: {emeraldPokemon.Count}; HeartGold slots: {heartGoldPokemon.Count}.");
Console.WriteLine("PASS: real save fixtures identified.");
var savesRoot = Path.Combine(Path.GetTempPath(), "mono-home-verifier-saves", Guid.NewGuid().ToString("N"));
var registered = SaveRegistry.Register(File.ReadAllBytes(emeraldPath), "emerald.srm", savesRoot, "content://local/emerald.srm");
AssertEqual(emerald.Game, registered.Game, "save registry records game");
AssertEqual(emeraldHash, registered.Hash, "save registry records input hash");
AssertEqual(registered.Id, SaveRegistry.GetLatest(savesRoot)!.Id, "save registry restores import");
AssertEqual(registered.Hash, SaveRegistry.Get(savesRoot, registered.Id)!.Hash, "save registry resolves a specific import");
AssertEqual("content://local/emerald.srm", SaveRegistry.Get(savesRoot, registered.Id)!.SourceUri!, "save registry persists source URI");
var writableRegistered = SaveRegistry.Register(File.ReadAllBytes(heartGoldPath), "heartgold-writable.sav", savesRoot, "content://local/heartgold-writable.sav", sourceFlags: 3);
AssertEqual(3, SaveRegistry.Get(savesRoot, writableRegistered.Id)!.SourceFlags, "save registry persists URI flags");

var syncBase = new byte[] { 1, 2, 3 };
var syncLocal = new byte[] { 1, 2, 4 };
var syncRemote = new byte[] { 1, 2, 5 };
var syncProvider = new SyncFakeRemoteSaveProvider();
var syncService = new SaveSyncService(syncProvider);
var syncBaseHash = SaveSyncService.ComputeHash(syncBase);
syncProvider.Seed("emerald", "main", "commit-1", syncBase);
var syncBinding = new SaveRemoteBinding("emerald", "main", "commit-1", syncBaseHash);
AssertEqual(SyncStatus.Aligned, syncService.Compare("emerald", syncBase, syncBinding, await syncProvider.GetLatestAsync("emerald", "main", CancellationToken.None)).Status, "sync equal content is aligned");
AssertEqual(SyncStatus.LocalNewer, syncService.Compare("emerald", syncLocal, syncBinding, await syncProvider.GetLatestAsync("emerald", "main", CancellationToken.None)).Status, "sync local change is local newer");
syncProvider.Seed("emerald", "main", "commit-2", syncRemote, "commit-1");
AssertEqual(SyncStatus.RemoteNewer, syncService.Compare("emerald", syncBase, syncBinding, await syncProvider.GetLatestAsync("emerald", "main", CancellationToken.None)).Status, "sync remote-only change is remote newer");
AssertEqual(SyncStatus.Diverged, syncService.Compare("emerald", syncLocal, syncBinding, await syncProvider.GetLatestAsync("emerald", "main", CancellationToken.None)).Status, "sync local and remote changes diverge");

var uploadProvider = new SyncFakeRemoteSaveProvider();
var uploadService = new SaveSyncService(uploadProvider);
var repositoryBinding = new RepositoryBinding("fake", "tester", "mono-home-saves", "main", DateTimeOffset.UtcNow, 1);
var uploadSnapshot = new LocalSaveSnapshot("emerald", syncBase, syncBaseHash, DateTimeOffset.UtcNow);
var firstUpload = await uploadService.UploadAsync(uploadSnapshot, repositoryBinding, new SaveRemoteBinding("emerald", "main", null), CancellationToken.None);
AssertTrue(firstUpload.Succeeded && !firstUpload.NoOp, "sync first upload succeeds");
AssertEqual(1, uploadProvider.UploadCount, "sync first upload creates one commit");
var alignedBinding = new SaveRemoteBinding("emerald", "main", firstUpload.State.BaseCommitSha, firstUpload.State.LocalHash);
var repeatedUpload = await uploadService.UploadAsync(uploadSnapshot, repositoryBinding, alignedBinding, CancellationToken.None);
AssertTrue(repeatedUpload.Succeeded && repeatedUpload.NoOp, "sync repeated upload is no-op");
AssertEqual(1, uploadProvider.UploadCount, "sync repeated upload does not create a commit");
var changedUpload = await uploadService.UploadAsync(
    new LocalSaveSnapshot("emerald", syncLocal, SaveSyncService.ComputeHash(syncLocal), DateTimeOffset.UtcNow),
    repositoryBinding,
    alignedBinding,
    CancellationToken.None);
AssertTrue(changedUpload.Succeeded && !changedUpload.NoOp, "sync changed upload succeeds");
AssertEqual(2, uploadProvider.UploadCount, "sync changed upload creates a commit");
AssertTrue(changedUpload.State.BaseCommitSha is not null && changedUpload.State.BaseCommitSha == changedUpload.State.RemoteLatest!.CommitSha, "sync upload aligns commit sha");
AssertTrue(changedUpload.State.RemoteLatest?.ContentHash is not null && changedUpload.State.LocalHash == changedUpload.State.RemoteLatest.ContentHash, "sync upload aligns content hash");
var forkedLineage = await uploadProvider.CreateLineageAsync("emerald", firstUpload.State.BaseCommitSha!, CancellationToken.None);
var forkedLatest = await uploadProvider.GetLatestAsync("emerald", forkedLineage, CancellationToken.None);
AssertTrue(forkedLatest is not null && forkedLatest.CommitSha == firstUpload.State.BaseCommitSha && forkedLatest.ParentCommitSha == firstUpload.State.BaseCommitSha, "sync lineage starts from selected historical commit");
var divergentUpload = await uploadService.UploadAsync(
    new LocalSaveSnapshot("emerald", syncRemote, SaveSyncService.ComputeHash(syncRemote), DateTimeOffset.UtcNow),
    repositoryBinding,
    new SaveRemoteBinding("emerald", "main", firstUpload.State.BaseCommitSha, syncBaseHash),
    CancellationToken.None);
AssertEqual(SyncStatus.Diverged, divergentUpload.State.Status, "sync divergent upload requires an explicit new lineage");
var forkedUpload = await uploadService.ForkAndUploadAsync(
    new LocalSaveSnapshot("emerald", syncRemote, SaveSyncService.ComputeHash(syncRemote), DateTimeOffset.UtcNow),
    repositoryBinding,
    new SaveRemoteBinding("emerald", "main", firstUpload.State.BaseCommitSha, syncBaseHash),
    CancellationToken.None);
AssertTrue(forkedUpload.Succeeded && forkedUpload.State.LineageId != "main", "sync changed historical save uploads to a new lineage");
AssertTrue((await uploadProvider.ListVersionsAsync("emerald", CancellationToken.None)).Count >= 4, "sync history retains old and forked versions");
Console.WriteLine("PASS: V0 save sync state machine and fake remote.");

var pullProvider = new SyncFakeRemoteSaveProvider();
var pullService = new SaveSyncService(pullProvider);
var pullContent = File.ReadAllBytes(emeraldPath);
pullProvider.Seed("emerald", "main", "commit-pull", pullContent);
var pullVersion = (await pullProvider.GetLatestAsync("emerald", "main", CancellationToken.None))!;
var pullResult = await pullService.PullAsync(
    registered,
    pullVersion,
    repositoryBinding,
    new SaveRemoteBinding("emerald", "main", null),
    CancellationToken.None);
AssertTrue(pullResult.Succeeded && pullResult.RecoveryPointPath is not null && File.Exists(pullResult.RecoveryPointPath), "sync pull creates a recovery point after validation");
AssertEqual(emeraldHash, SaveRegistry.Get(savesRoot, registered.Id)!.Hash, "sync pull keeps a valid local snapshot");
var invalidPullRoot = Path.Combine(Path.GetTempPath(), "mono-home-verifier-invalid-pull", Guid.NewGuid().ToString("N"));
var invalidLocal = SaveRegistry.Register(pullContent, "invalid-pull.srm", invalidPullRoot);
var invalidProvider = new SyncFakeRemoteSaveProvider();
var invalidService = new SaveSyncService(invalidProvider);
invalidProvider.Seed("emerald", "main", "commit-invalid", new byte[] { 1, 2, 3 });
var invalidVersion = (await invalidProvider.GetLatestAsync("emerald", "main", CancellationToken.None))!;
AssertThrows<InvalidDataException>(() => invalidService.PullAsync(invalidLocal, invalidVersion, repositoryBinding, new SaveRemoteBinding("emerald", "main", null), CancellationToken.None).GetAwaiter().GetResult(), "sync pull rejects invalid remote save");
AssertEqual(invalidLocal.Hash, SaveRegistry.Get(invalidPullRoot, invalidLocal.Id)!.Hash, "sync invalid pull preserves the local snapshot");
var failedPullProvider = new SyncFakeRemoteSaveProvider
{
    DownloadFailure = new GitHubApiException(HttpStatusCode.ServiceUnavailable, "remote unavailable"),
};
failedPullProvider.Seed("emerald", "main", "commit-failed", pullContent);
var failedPullVersion = (await failedPullProvider.GetLatestAsync("emerald", "main", CancellationToken.None))!;
AssertThrows<GitHubApiException>(() => new SaveSyncService(failedPullProvider)
    .PullAsync(invalidLocal, failedPullVersion, repositoryBinding, new SaveRemoteBinding("emerald", "main", null), CancellationToken.None)
    .GetAwaiter().GetResult(), "sync network pull failure is surfaced");
AssertEqual(invalidLocal.Hash, SaveRegistry.Get(invalidPullRoot, invalidLocal.Id)!.Hash, "sync network pull failure preserves the local snapshot");
Console.WriteLine("PASS: V0 save sync pull validation and recovery.");

var githubHandler = new GitHubFakeHttpHandler();
using var githubHttp = new HttpClient(githubHandler) { BaseAddress = new Uri("https://api.github.test/") };
var githubApi = new GitHubApiClient(githubHttp, _ => Task.FromResult("test-token"));
var githubProbe = new GitHubRemoteSaveProvider(
    githubApi,
    new RepositoryBinding("github", "test", "repo", "main", DateTimeOffset.UtcNow, 1));
var githubBinding = await githubProbe.BindRepositoryAsync("test", "repo", CancellationToken.None);
AssertTrue(githubBinding.Provider == "github" && githubBinding.Owner == "test" && githubBinding.Repository == "repo", "GitHub binding validates private writable repository");
var githubProvider = new GitHubRemoteSaveProvider(githubApi, githubBinding);
var githubManifest = await githubProvider.GetManifestAsync("main", false, CancellationToken.None);
AssertEqual(1, githubManifest.Schema, "GitHub manifest schema");
AssertEqual("saves/emerald/emerald.srm", githubManifest.Saves["emerald"].Path, "GitHub manifest save path");
var githubLatest = await githubProvider.GetLatestAsync("emerald", "main", CancellationToken.None);
AssertTrue(githubLatest is not null && githubLatest.CommitSha == "commit-1" && githubLatest.BlobSha == "blob-1", "GitHub provider separates commit SHA and blob SHA");
AssertEqual(SaveSyncService.ComputeHash(new byte[] { 10, 20, 30 }), githubLatest!.ContentHash!, "GitHub provider computes content hash from bytes");
var customPathHandler = new GitHubFakeHttpHandler
{
    SavePath = "saves/emerald/custom.srm",
    ManifestSavePath = "saves/emerald/custom.srm",
};
using var customPathHttp = new HttpClient(customPathHandler) { BaseAddress = new Uri("https://api.github.test/") };
var customPathProvider = new GitHubRemoteSaveProvider(
    new GitHubApiClient(customPathHttp, _ => Task.FromResult("test-token")),
    githubBinding);
await customPathProvider.GetManifestAsync("main", false, CancellationToken.None);
AssertTrue(await customPathProvider.GetLatestAsync("emerald", "main", CancellationToken.None) is not null, "GitHub provider reads the manifest save path");
await customPathProvider.UploadAsync("emerald", "main", new byte[] { 40, 50, 60 }, "commit-1", "custom path", CancellationToken.None);
AssertTrue(customPathHandler.Requests.Any(request => request.Method == HttpMethod.Put && request.RequestUri!.AbsolutePath.EndsWith("/saves/emerald/custom.srm", StringComparison.Ordinal)), "GitHub provider uploads to the manifest save path");
var invalidManifestHandler = new GitHubFakeHttpHandler { ManifestSavePath = "../outside.sav" };
using var invalidManifestHttp = new HttpClient(invalidManifestHandler) { BaseAddress = new Uri("https://api.github.test/") };
var invalidManifestProvider = new GitHubRemoteSaveProvider(
    new GitHubApiClient(invalidManifestHttp, _ => Task.FromResult("test-token")),
    githubBinding);
await AssertThrowsAsync<InvalidDataException>(
    () => invalidManifestProvider.GetManifestAsync("main", false, CancellationToken.None),
    "GitHub manifest rejects unsafe save paths");
var githubHistory = await githubProvider.ListVersionsAsync("emerald", "save/emerald/test-lineage", CancellationToken.None);
AssertTrue(githubHistory.Count == 1 && githubHistory[0].ContentHash is null && githubHistory[0].LineageId == "save/emerald/test-lineage", "GitHub history keeps lineage and defers content hash");
var githubAllHistory = await githubProvider.ListAllVersionsAsync("emerald", "save/emerald/test-lineage", CancellationToken.None);
AssertTrue(githubAllHistory.Count == 2 && githubAllHistory.Select(version => version.LineageId).Distinct().Count() == 2, "GitHub history includes current and save-specific lineages");
var retryHandler = new GitHubFakeHttpHandler { TransientGetFailures = 1 };
using var retryHttp = new HttpClient(retryHandler) { BaseAddress = new Uri("https://api.github.test/") };
var retryProvider = new GitHubRemoteSaveProvider(new GitHubApiClient(retryHttp, _ => Task.FromResult("test-token")), githubBinding);
AssertTrue((await retryProvider.GetLatestAsync("emerald", "main", CancellationToken.None)) is not null, "GitHub GET retries transient service failure");
var invalidHandler = new GitHubFakeHttpHandler { ReturnUnprocessable = true };
using var invalidHttp = new HttpClient(invalidHandler) { BaseAddress = new Uri("https://api.github.test/") };
var invalidApi = new GitHubApiClient(invalidHttp, _ => Task.FromResult("test-token"));
GitHubApiException? invalidError = null;
try
{
    await invalidApi.GetRepositoryAsync("test", "repo", CancellationToken.None);
}
catch (GitHubApiException ex)
{
    invalidError = ex;
}
AssertTrue(invalidError is not null && invalidError.UserMessage.Contains("请求参数", StringComparison.Ordinal), "GitHub 422 error has user message");
foreach (var statusCode in new[]
{
    HttpStatusCode.Unauthorized,
    HttpStatusCode.Forbidden,
    HttpStatusCode.NotFound,
    HttpStatusCode.Conflict,
    (HttpStatusCode)429,
})
{
    var errorHandler = new GitHubFakeHttpHandler { ForcedStatusCode = statusCode, RetryAfter = TimeSpan.Zero };
    using var errorHttp = new HttpClient(errorHandler) { BaseAddress = new Uri("https://api.github.test/") };
    var errorApi = new GitHubApiClient(errorHttp, _ => Task.FromResult("test-token"));
    GitHubApiException? error = null;
    try
    {
        await errorApi.GetRepositoryAsync("test", "repo", CancellationToken.None);
    }
    catch (GitHubApiException ex)
    {
        error = ex;
    }
    AssertTrue(error is not null && error.StatusCode == statusCode && !string.IsNullOrWhiteSpace(error.UserMessage), $"GitHub {(int)statusCode} has user message");
}
var offlineHandler = new GitHubFakeHttpHandler { ThrowTransportFailure = true };
using var offlineHttp = new HttpClient(offlineHandler) { BaseAddress = new Uri("https://api.github.test/") };
GitHubApiException? offlineError = null;
try
{
    await new GitHubApiClient(offlineHttp, _ => Task.FromResult("test-token"))
        .GetRepositoryAsync("test", "repo", CancellationToken.None);
}
catch (GitHubApiException ex)
{
    offlineError = ex;
}
AssertTrue(offlineError is not null && offlineError.StatusCode is null && offlineError.UserMessage.Contains("无法连接", StringComparison.Ordinal), "GitHub transport failure has offline user message");
AssertEqual(3, offlineHandler.RequestLog.Count, "GitHub GET transport failure uses finite retries");
var timeoutHandler = new GitHubFakeHttpHandler { ResponseDelay = TimeSpan.FromSeconds(1) };
using var timeoutHttp = new HttpClient(timeoutHandler) { BaseAddress = new Uri("https://api.github.test/") };
await AssertThrowsAsync<OperationCanceledException>(
    () => new GitHubApiClient(timeoutHttp, _ => Task.FromResult("test-token"), TimeSpan.FromMilliseconds(10))
        .GetRepositoryAsync("test", "repo", CancellationToken.None),
    "GitHub request timeout cancels the request");
using var cancellationHttp = new HttpClient(new GitHubFakeHttpHandler { ResponseDelay = TimeSpan.FromSeconds(1) }) { BaseAddress = new Uri("https://api.github.test/") };
using var cancellation = new CancellationTokenSource();
var cancellationTask = new GitHubApiClient(cancellationHttp, _ => Task.FromResult("test-token"))
    .GetRepositoryAsync("test", "repo", cancellation.Token);
cancellation.CancelAfter(10);
await AssertThrowsAsync<OperationCanceledException>(() => cancellationTask, "GitHub caller cancellation is preserved");
var githubUploaded = await githubProvider.UploadAsync("emerald", "main", new byte[] { 40, 50, 60 }, "commit-1", "sync test", CancellationToken.None);
AssertEqual("commit-2", githubUploaded.CommitSha, "GitHub provider returns commit SHA after upload");
AssertEqual("blob-2", githubUploaded.BlobSha!, "GitHub provider returns blob SHA after upload");
var uploadedRequest = githubHandler.RequestLog.Last(request => request.Method == HttpMethod.Put && request.Path.EndsWith("/saves/emerald/emerald.srm", StringComparison.Ordinal));
using var uploadedPayload = JsonDocument.Parse(uploadedRequest.Body!);
AssertEqual("blob-1", uploadedPayload.RootElement.GetProperty("sha").GetString()!, "GitHub upload carries expected blob SHA");
AssertEqual("main", uploadedPayload.RootElement.GetProperty("branch").GetString()!, "GitHub upload carries expected branch");
var conflictHandler = new GitHubFakeHttpHandler { ForcedPutStatusCode = HttpStatusCode.Conflict };
using var conflictHttp = new HttpClient(conflictHandler) { BaseAddress = new Uri("https://api.github.test/") };
var conflictProvider = new GitHubRemoteSaveProvider(
    new GitHubApiClient(conflictHttp, _ => Task.FromResult("test-token")),
    githubBinding);
GitHubApiException? conflictError = null;
try
{
    await conflictProvider.UploadAsync("emerald", "main", new byte[] { 40, 50, 60 }, "commit-1", "conflict", CancellationToken.None);
}
catch (GitHubApiException ex)
{
    conflictError = ex;
}
AssertTrue(conflictError?.StatusCode == HttpStatusCode.Conflict, "GitHub write conflict is surfaced");
AssertEqual(1, conflictHandler.RequestLog.Count(request => request.Method == HttpMethod.Put), "GitHub write conflict is not retried");
AssertTrue(githubHandler.Requests.All(request => request.Headers.Authorization?.Scheme == "Bearer"), "GitHub requests carry bearer authorization");
var githubLineage = await githubProvider.CreateLineageAsync("emerald", "commit-1", CancellationToken.None);
AssertTrue(githubLineage.StartsWith("save/emerald/", StringComparison.Ordinal), "GitHub provider creates a save lineage ref");
githubHandler.ManifestMissing = true;
var initializedManifest = await githubProvider.GetManifestAsync("main", true, CancellationToken.None);
AssertEqual("mono-home", initializedManifest.App, "GitHub missing manifest is initialized");
var authClient = new GitHubAuthClient(new GitHubAuthOptions("public-client-id", new Uri("io.github.monohome:/oauth2redirect")));
var authRequest = authClient.CreateAuthorizationRequest();
AssertTrue(authRequest.AuthorizationUri.Query.Contains("code_challenge_method=S256", StringComparison.Ordinal), "GitHub auth uses PKCE S256");
AssertTrue(authRequest.AuthorizationUri.Query.Contains("state=", StringComparison.Ordinal) && authRequest.CodeVerifier.Length >= 43, "GitHub auth includes state and verifier");
AssertEqual("auth-code", GitHubAuthClient.ValidateCallback(authRequest.State, authRequest.State, "auth-code"), "GitHub auth accepts matching callback state");
AssertThrows<InvalidOperationException>(() => GitHubAuthClient.ValidateCallback(authRequest.State, "wrong-state", "auth-code"), "GitHub auth rejects mismatched callback state");
var deviceHandler = new GitHubDeviceFlowFakeHttpHandler();
using var deviceHttp = new HttpClient(deviceHandler) { BaseAddress = new Uri("https://github.test/") };
var deviceFlow = new GitHubDeviceFlowClient(deviceHttp, "public-client-id");
var deviceCode = await deviceFlow.RequestDeviceCodeAsync(CancellationToken.None);
AssertEqual("ABCD-EFGH", deviceCode.UserCode, "GitHub device flow returns user code");
var deviceToken = await deviceFlow.WaitForAccessTokenAsync(deviceCode, CancellationToken.None);
AssertEqual("ghu-test", deviceToken.AccessToken, "GitHub device flow returns user access token");
AssertEqual(2, deviceHandler.PollCount, "GitHub device flow handles pending response before success");
Console.WriteLine("PASS: V1 GitHub Contents API provider with fake HTTP.");

var selected = emeraldPokemon[1];
var repositoryRoot = Path.Combine(Path.GetTempPath(), "mono-home-verifier-repository", Guid.NewGuid().ToString("N"));
var stored = LocalRepository.Upload(BoxReader.ReadPokemon(emeraldPath, selected), repositoryRoot);
AssertEqual(stored.Id, LocalRepository.GetLatest(repositoryRoot)!.Id, "repository restores latest upload");
AssertTrue(File.Exists(stored.ManifestPath), "repository persists a metadata record");
var secondStored = LocalRepository.Upload(BoxReader.ReadPokemon(emeraldPath, emeraldPokemon[2]), repositoryRoot);
AssertEqual(2, LocalRepository.List(repositoryRoot).Count, "repository lists multiple uploaded Pokémon");
AssertEqual(secondStored.Id, LocalRepository.GetLatest(repositoryRoot)!.Id, "repository selects latest record after second upload");
var originalPokemonHash = Hash(stored.OriginalPath);
var storedPokemon = LocalRepository.LoadWorking(stored);
AssertEqual(selected.Species, storedPokemon.Species, "repository preserves selected Pokémon");
storedPokemon = LocalRepository.ApplyEdit(storedPokemon, new WorkingEdit("LOCAL", 25, 1, 0, 0, 0));
AssertEqual(25, storedPokemon.CurrentLevel, "working edit changes level");
AssertEqual(1, storedPokemon.HeldItem, "working edit changes held item");
var advancedEdit = LocalRepository.ApplyEdit(storedPokemon, new WorkingEdit(null, null, null, null, null, null, [1, 2, 3, 4], [1, 2, 3, 4, 5, 6], [1, 2, 3, 4, 5, 6]));
AssertEqual((ushort)1, advancedEdit.Move1, "working edit changes moves");
AssertEqual(6, advancedEdit.IV_SPD, "working edit changes IVs");
AssertEqual(6, advancedEdit.EV_SPD, "working edit changes EVs");
var repairOutcome = LocalRepository.RepairWithStrategy(advancedEdit);
AssertTrue(repairOutcome.Valid, $"automatic repair produces a legal {repairOutcome.Template} candidate");
Console.WriteLine($"PASS: automatic repair used {repairOutcome.Template}; {string.Join(", ", repairOutcome.Changes)}");
var attributeEdit = LocalRepository.ApplyEdit(advancedEdit, new WorkingEdit(
    null, null, null, null, null, null,
    Species: selected.Species,
    Nature: 1,
    AbilityIndex: 0,
    Gender: 0,
    Form: 0,
    Shiny: true,
    Egg: false));
AssertEqual(selected.Species, attributeEdit.Species, "working edit changes species context");
AssertEqual((Nature)1, attributeEdit.Nature, "working edit changes nature");
AssertEqual(1, attributeEdit.AbilityNumber, "working edit changes ability index");
AssertEqual((byte)0, attributeEdit.Gender, "working edit changes gender");
AssertEqual((byte)0, attributeEdit.Form, "working edit changes form");
AssertTrue(attributeEdit.IsShiny, "working edit changes shiny state");
AssertTrue(!attributeEdit.IsEgg, "working edit changes egg state");
LocalRepository.SaveWorking(stored, storedPokemon);
AssertEqual("LOCAL", LocalRepository.LoadWorking(stored).Nickname, "repository persists working-copy edits");
AssertEqual(originalPokemonHash, Hash(stored.OriginalPath), "repository original stays unchanged after edits");
stored = LocalRepository.DiscardEdits(stored);
AssertEqual(selected.Nickname, LocalRepository.LoadWorking(stored).Nickname, "discard restores working copy from immutable original");
storedPokemon = LocalRepository.ApplyEdit(LocalRepository.LoadWorking(stored), new WorkingEdit("LOCAL", 25, 1, 0, 0, 0));
LocalRepository.SaveWorking(stored, storedPokemon);
LocalRepository.SetLegality(stored, "valid");
AssertEqual("valid", LocalRepository.GetLatest(repositoryRoot)!.LegalityStatus, "repository persists legality state");
var parentHash = Hash(stored.WorkingPath);
var copy = LocalRepository.CreateLegalCopy(stored, storedPokemon, repositoryRoot);
AssertEqual(stored.Id, copy.ParentId!, "legal copy keeps parent ID");
AssertTrue(copy.Id != stored.Id, "legal copy has its own ID");
AssertEqual(parentHash, Hash(stored.WorkingPath), "legal copy does not rewrite parent");
AssertEqual(1L, copy.Revision, "new copy starts at revision 1");
var registeredTarget = SaveRegistry.Register(File.ReadAllBytes(heartGoldPath), "heartgold.sav", savesRoot, "content://local/heartgold.sav");
var preparationRoot = Path.Combine(Path.GetTempPath(), "mono-home-verifier-preparation", Guid.NewGuid().ToString("N"));
var preparation = TargetPreparationService.Prepare(copy, registeredTarget, heartGoldPath, preparationRoot);
AssertEqual(TargetPreparationState.Ready, preparation.State, "target preparation is ready for legal conversion");
AssertTrue(preparation.IsCurrentFor(copy, registeredTarget), "target preparation matches current repository revision");
copy = LocalRepository.SaveWorking(copy, LocalRepository.LoadWorking(copy));
AssertTrue(!preparation.IsCurrentFor(copy, registeredTarget), "target preparation becomes stale after repository revision changes");
var transferPath = Path.Combine(Path.GetTempPath(), "mono-home-heartgold-result.sav");
var transfer = EmeraldHgssTransfer.TransferStored(storedPokemon, heartGoldPath, transferPath);
AssertTrue(transfer.Succeeded, $"Emerald to HeartGold transfer: {transfer.Message}");
AssertEqual(selected.Species, transfer.Species, "selected Pokémon is transferred");
AssertTrue(transfer.Changes.Any(change => change.Field == "OriginalTrainer"), "transfer reports target trainer change");
AssertTrue(transfer.Changes.Any(change => change.Field == "Shiny"), "transfer reports shiny policy even when preserved");
AssertTrue(File.Exists(transferPath), "transfer output exists");
var convertedSave = SaveInspector.Inspect(transferPath);
AssertEqual("HeartGold", convertedSave.Game, "converted save game");
var targetSave = (SAV4HGSS)SaveUtil.GetSaveFile(transferPath)!;
var inserted = targetSave.GetBoxSlotAtIndex(20);
AssertEqual(targetSave.OT, inserted.OriginalTrainerName, "converted Pokémon uses target OT");
AssertEqual(targetSave.TID16, inserted.TID16, "converted Pokémon uses target TID");
AssertEqual(targetSave.SID16, inserted.SID16, "converted Pokémon uses target SID");
AssertEqual(emeraldHash, Hash(emeraldPath), "Emerald source stays unchanged");
AssertEqual(heartGoldHash, Hash(heartGoldPath), "HeartGold source stays unchanged");
Console.WriteLine($"PASS: transfer output {transferPath}; species {transfer.Species}; {transfer.Message}");
var chosenDestination = heartGoldPages.Skip(1)
    .SelectMany((page, pageIndex) => page.Slots.Where(slot => slot.Pokemon is null).Select(slot => pageIndex * page.Capacity + slot.Index))
    .Skip(1)
    .First();
var routedTransferPath = Path.Combine(Path.GetTempPath(), "mono-home-heartgold-routed.sav");
var routedTransfer = EmeraldHgssTransfer.TransferStored(storedPokemon, heartGoldPath, routedTransferPath, TransferMode.Conversion, chosenDestination);
AssertTrue(routedTransfer.Succeeded, $"routed transfer: {routedTransfer.Message}");
var routedSave = (SAV4HGSS)SaveUtil.GetSaveFile(routedTransferPath)!;
AssertEqual(selected.Species, routedSave.GetBoxSlotAtIndex(chosenDestination).Species, "routed transfer uses selected destination slot");
Console.WriteLine($"PASS: routed transfer wrote selected slot {chosenDestination}.");

var special = emeraldPokemon.FirstOrDefault(slot => slot.IsShiny || slot.HeldItem != 0 || slot.StatusCondition != 0 || slot.PokerusStrain != 0 || slot.IsEgg);
AssertTrue(special is not null, "Emerald fixture has a special-state Pokémon");
Console.WriteLine($"Special slot: #{special!.Species} nickname={special.Nickname} shiny={special.IsShiny} item={special.HeldItem} status={special.StatusCondition} pokerus={special.PokerusStrain}/{special.PokerusDays} egg={special.IsEgg}.");
var specialStored = LocalRepository.Upload(BoxReader.ReadPokemon(emeraldPath, special!), Path.Combine(Path.GetTempPath(), "mono-home-verifier-special"));
var specialTransfer = EmeraldHgssTransfer.TransferStored(LocalRepository.LoadWorking(specialStored), heartGoldPath, Path.Combine(Path.GetTempPath(), "mono-home-heartgold-special.sav"));
AssertTrue(specialTransfer.Succeeded, $"special-state transfer: {specialTransfer.Message}");
AssertTrue(specialTransfer.Changes.Any(change => change.Field == "HeldItem"), "transfer reports held-item policy");
AssertTrue(specialTransfer.Changes.Any(change => change.Field == "Form") && specialTransfer.Changes.Any(change => change.Field == "Ribbons"), "transfer reports form and ribbon policies");
Console.WriteLine($"PASS: special-state transfer #{special.Species}; {string.Join(", ", specialTransfer.Changes.Select(change => change.Field))}.");
var fidelityProbePath = Path.Combine(Path.GetTempPath(), "mono-home-heartgold-fidelity.sav");
var fidelityProbe = EmeraldHgssTransfer.TransferStored(BoxReader.ReadPokemon(emeraldPath, special!), heartGoldPath, fidelityProbePath, TransferMode.Fidelity);
AssertTrue(fidelityProbe.Succeeded, $"fidelity transfer: {fidelityProbe.Message}");
var fidelitySave = (SAV4HGSS)SaveUtil.GetSaveFile(fidelityProbePath)!;
var fidelityInserted = fidelitySave.GetBoxSlotAtIndex(20);
AssertEqual(BoxReader.ReadPokemon(emeraldPath, special!).OriginalTrainerName, fidelityInserted.OriginalTrainerName, "fidelity keeps original trainer");
AssertEqual(BoxReader.ReadPokemon(emeraldPath, special!).TID16, fidelityInserted.TID16, "fidelity keeps original TID");
AssertTrue(new LegalityAnalysis(fidelityInserted).Valid, "fidelity output is legal");
Console.WriteLine($"PASS: fidelity transfer preserves source trainer and IDs; {fidelityProbe.Message}");

var conversionFailures = new List<string>();
foreach (var (slot, index) in emeraldPokemon.Select((slot, index) => (slot, index)))
{
    var output = Path.Combine(Path.GetTempPath(), $"mono-home-heartgold-all-{index}.sav");
    var result = EmeraldHgssTransfer.TransferStored(
        BoxReader.ReadPokemon(emeraldPath, slot),
        heartGoldPath,
        output);
    if (!result.Succeeded)
        conversionFailures.Add($"#{slot.Species} {slot.Nickname}: {result.Message}");
    else
    {
        var persisted = (SAV4HGSS)SaveUtil.GetSaveFile(output)!;
        var insertedSlot = persisted.GetBoxSlotAtIndex(20);
        if (insertedSlot.Species != slot.Species || !new LegalityAnalysis(insertedSlot).Valid)
            conversionFailures.Add($"#{slot.Species} {slot.Nickname}: persisted output did not validate.");
    }
}
AssertTrue(conversionFailures.Count == 0, $"all {emeraldPokemon.Count} supplied Emerald Pokémon convert legally ({string.Join(" | ", conversionFailures)})");
Console.WriteLine($"PASS: all {emeraldPokemon.Count} supplied Emerald Pokémon convert legally to HeartGold.");
var fidelityAccepted = 0;
foreach (var (slot, index) in emeraldPokemon.Select((slot, index) => (slot, index)))
{
    var output = Path.Combine(Path.GetTempPath(), $"mono-home-heartgold-fidelity-all-{index}.sav");
    var result = EmeraldHgssTransfer.TransferStored(BoxReader.ReadPokemon(emeraldPath, slot), heartGoldPath, output, TransferMode.Fidelity);
    if (!result.Succeeded)
        continue;
    fidelityAccepted++;
    var persisted = (SAV4HGSS)SaveUtil.GetSaveFile(output)!;
    AssertTrue(new LegalityAnalysis(persisted.GetBoxSlotAtIndex(20)).Valid, $"fidelity output #{index} is legal");
}
AssertTrue(fidelityAccepted > 0, "fidelity route accepts at least one supplied Pokémon");
Console.WriteLine($"PASS: fidelity route legally accepted {fidelityAccepted}/{emeraldPokemon.Count} supplied Pokémon; rejected sources remain blocked.");
var batchPath = Path.Combine(Path.GetTempPath(), "mono-home-heartgold-batch.sav");
var batchEntities = new[] { BoxReader.ReadPokemon(emeraldPath, emeraldPokemon[0]), BoxReader.ReadPokemon(emeraldPath, special!) };
Console.WriteLine($"Batch entities: {string.Join(",", batchEntities.Select(entity => entity.Species))}");
var batch = EmeraldHgssTransfer.TransferStoredMany(batchEntities, heartGoldPath, batchPath, TransferMode.Conversion);
AssertTrue(batch.Succeeded, $"batch transfer: {batch.Message}");
var batchSave = (SAV4HGSS)SaveUtil.GetSaveFile(batchPath)!;
Console.WriteLine($"Batch slots: {string.Join(", ", batch.Reports.Select(report => $"{report.Slot}=#{batchSave.GetBoxSlotAtIndex(report.Slot).Species}"))}");
AssertTrue(batch.Reports.All(report => new LegalityAnalysis(batchSave.GetBoxSlotAtIndex(report.Slot)).Valid), "batch output slots are legal");
Console.WriteLine($"PASS: batch transfer wrote {batch.Reports.Count} legal Pokémon.");
var transferLogRoot = Path.Combine(Path.GetTempPath(), "mono-home-verifier-transfers", Guid.NewGuid().ToString("N"));
var transferLog = TransferJournal.Append(transferLogRoot, stored.Id, "Emerald", "HeartGold", transfer);
AssertEqual(transferLog.Id, TransferJournal.List(transferLogRoot).Single().Id, "transfer journal persists output");
AssertEqual("conversion", transferLog.Mode!, "transfer journal persists conversion mode");
var exportedLog = TransferJournal.MarkExported(transferLogRoot, transferLog.Id, "heartgold-transfer.sav");
AssertEqual("succeeded", exportedLog.Status, "transfer journal records completed export");
var removable = LocalRepository.Upload(BoxReader.ReadPokemon(emeraldPath, emeraldPokemon[0]), Path.Combine(Path.GetTempPath(), "mono-home-verifier-removal"));
LocalRepository.Remove(removable);
AssertTrue(!File.Exists(removable.ManifestPath), "transferred repository record is removed");
Console.WriteLine("PASS: successful transfer removal deletes the central warehouse record.");

static void AssertEqual<T>(T expected, T actual, string label) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
}

static void AssertTrue(bool value, string label)
{
    if (!value)
        throw new InvalidOperationException($"{label}: expected true.");
}

static void AssertThrows<T>(Action action, string label) where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        return;
    }
    throw new InvalidOperationException($"{label}: expected {typeof(T).Name}.");
}

static async Task AssertThrowsAsync<T>(Func<Task> action, string label) where T : Exception
{
    try
    {
        await action();
    }
    catch (T)
    {
        return;
    }
    throw new InvalidOperationException($"{label}: expected {typeof(T).Name}.");
}

static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
