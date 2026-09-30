using System.Text.Json;

namespace MonoHome.Core.Storage;

/// <summary>
/// Crash-safe helpers for the small JSON/byte records this app keeps on disk.
///
/// The same two patterns were previously copy-pasted into every store, which meant every
/// hardening fix had to be applied in three places and any one of them could drift.
/// </summary>
public static class AtomicFile
{
    /// <summary>
    /// Writes <paramref name="data"/> to <paramref name="path"/> via a temporary file, so a
    /// reader never observes a half-written payload and a failure leaves the previous
    /// contents intact.
    /// </summary>
    public static void Write(string path, byte[] data)
    {
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(data, 0, data.Length);
                // Flush to the device before the rename, otherwise a power loss can leave the
                // new name pointing at a file whose contents were never persisted.
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    /// <summary>Atomically writes a JSON-serialized <paramref name="value"/>.</summary>
    public static void WriteJson<T>(string path, T value, JsonSerializerOptions options) =>
        Write(path, JsonSerializer.SerializeToUtf8Bytes(value, options));

    /// <summary>
    /// Reads a JSON record, returning <c>null</c> when it is missing, unreadable, or
    /// corrupt. Callers treat <c>null</c> as "no usable record", which is also the correct
    /// outcome when a record is being deleted concurrently.
    /// </summary>
    public static T? TryReadJson<T>(string path, JsonSerializerOptions options) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), options);
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    /// <summary>Deletes a file, ignoring the failure modes of best-effort cleanup.</summary>
    public static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
