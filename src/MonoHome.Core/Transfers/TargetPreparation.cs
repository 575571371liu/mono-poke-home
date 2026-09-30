using MonoHome.Core.Repository;
using MonoHome.Core.Saves;
using MonoHome.Core.Storage;

namespace MonoHome.Core.Transfers;

public enum TargetPreparationState { Pending, Ready, Blocked, Stale }

public sealed record TargetPreparation(
    string RepositoryId,
    long RepositoryRevision,
    string TargetSaveId,
    string TargetHash,
    TargetPreparationState State,
    string Message,
    TransferMode Mode,
    IReadOnlyList<TransferChange> Changes,
    string? PreparedSavePath,
    DateTimeOffset CreatedAt)
{
    public bool IsCurrentFor(StoredPokemon stored, RegisteredSave target) =>
        RepositoryId == stored.Id && RepositoryRevision == stored.Revision &&
        TargetSaveId == target.Id && TargetHash == target.Hash && State == TargetPreparationState.Ready;
}

public static class TargetPreparationService
{
    public static TargetPreparation Prepare(
        StoredPokemon stored,
        RegisteredSave target,
        string targetSavePath,
        string cacheRoot,
        int destinationSlot = -1,
        TransferMode mode = TransferMode.Conversion)
    {
        Directory.CreateDirectory(cacheRoot);
        // The mode is part of the cache key: a Conversion and a Fidelity preparation of the
        // same entity and slot produce different saves and must not share a path.
        var output = Path.Combine(cacheRoot, $"{stored.Id}-{target.Id}-{stored.Revision}-{destinationSlot}-{mode}.sav");
        try
        {
            var report = EmeraldHgssTransfer.TransferStored(LocalRepository.LoadWorking(stored), targetSavePath, output, mode, destinationSlot);
            return new(
                stored.Id,
                stored.Revision,
                target.Id,
                target.Hash,
                report.Succeeded ? TargetPreparationState.Ready : TargetPreparationState.Blocked,
                report.Message,
                mode,
                report.Changes,
                report.Succeeded ? output : null,
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            AtomicFile.TryDelete(output);
            return new(stored.Id, stored.Revision, target.Id, target.Hash, TargetPreparationState.Blocked, ex.Message,
                mode, [], null, DateTimeOffset.UtcNow);
        }
    }
}
