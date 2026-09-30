namespace MonoHome.Verifier;

/// <summary>
/// Resolves the files and directories the verification suite depends on.
///
/// The suite is the only regression gate for this repository, so it has to run on any
/// machine: save fixtures are discovered relative to the repository instead of from a
/// hard-coded developer path, and the scratch area can be redirected with
/// <c>MONO_HOME_SCRATCH</c> when the ambient temp directory is not writable.
///
/// Static property initialisers run in declaration order, so anything used by an
/// initialiser above it must be declared before it.
/// </summary>
static class TestEnvironment
{
    const string ScratchVariable = "MONO_HOME_SCRATCH";
    const string EmeraldVariable = "MONO_HOME_EMERALD";
    const string HeartGoldVariable = "MONO_HOME_HEARTGOLD";

    /// <summary>Walks up from the output directory until the repository root is found.</summary>
    static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>
    /// Writable directory for throwaway verification output. Prefers
    /// <c>MONO_HOME_SCRATCH</c>, then the ambient temp directory, then the repository.
    /// </summary>
    public static string ScratchRoot { get; } = ResolveScratchRoot();

    /// <summary>
    /// Per-run directory inside <see cref="ScratchRoot"/>, so repeated runs never
    /// observe each other's leftovers.
    /// </summary>
    public static string RunRoot { get; } = CreateRunRoot();

    public static string EmeraldSave { get; } = ResolveSave(EmeraldVariable, "emerald.srm", "Emerald");

    public static string HeartGoldSave { get; } = ResolveSave(HeartGoldVariable, "heartgold.sav", "HeartGold");

    /// <summary>Creates a uniquely named writable directory under <see cref="RunRoot"/>.</summary>
    public static string NewScratchDirectory(string name)
    {
        var path = Path.Combine(RunRoot, name);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Builds a file path under <see cref="RunRoot"/> without creating it.</summary>
    public static string ScratchFile(string fileName) => Path.Combine(RunRoot, fileName);

    static string CreateRunRoot()
    {
        var path = Path.Combine(ScratchRoot, $"run-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    static string ResolveScratchRoot()
    {
        var configured = Environment.GetEnvironmentVariable(ScratchVariable);
        if (!string.IsNullOrWhiteSpace(configured))
            return RequireWritable(configured, $"{ScratchVariable} is set to it");

        // Some Windows profiles deny child processes the right to create directories
        // under the user temp folder, so a repository-local fallback is required for
        // the suite to run at all.
        var candidates = new List<string> { Path.GetTempPath() };
        candidates.AddRange(RepositoryCandidates());

        foreach (var candidate in candidates)
        {
            if (TryPrepare(candidate, out var resolved, out _))
                return resolved;
        }

        throw new InvalidOperationException(
            $"No writable scratch directory found (tried {string.Join(", ", candidates)}). " +
            $"Set {ScratchVariable} to a writable directory and retry.");
    }

    static IEnumerable<string> RepositoryCandidates()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            yield return Path.Combine(directory.FullName, ".verify-tmp");
    }

    static string RequireWritable(string path, string reason)
    {
        if (TryPrepare(path, out var resolved, out var error))
            return resolved;
        throw new InvalidOperationException($"Verification scratch directory '{path}' is not writable ({reason}): {error}");
    }

    /// <summary>Creates and probes <paramref name="path"/> so later writes cannot fail on permissions.</summary>
    static bool TryPrepare(string path, out string resolved, out string? error)
    {
        resolved = path;
        try
        {
            Directory.CreateDirectory(path);
            resolved = Path.GetFullPath(path);
            var probe = Path.Combine(resolved, $".probe-{Guid.NewGuid():N}");
            using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            stream.WriteByte(0);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    static string ResolveSave(string variable, string fileName, string game)
    {
        var configured = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (File.Exists(configured))
                return Path.GetFullPath(configured);
            throw new InvalidOperationException($"{variable} points at '{configured}', which does not exist.");
        }

        var bundled = Path.Combine(RepositoryRoot, "fixtures", "private", fileName);
        if (File.Exists(bundled))
            return bundled;

        throw new InvalidOperationException(
            $"The {game} fixture was not found at '{bundled}'. " +
            $"Place the save there or set {variable} to its path.");
    }

    static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "fixtures", "private")))
                return directory.FullName;
        }
        return AppContext.BaseDirectory;
    }
}
