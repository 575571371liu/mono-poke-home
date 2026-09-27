using MonoHome.Core.Repository;
using MonoHome.Core.Saves;

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
    public static TargetPreparation Prepare(StoredPokemon stored, RegisteredSave target, string targetSavePath, string cacheRoot)
    {
        Directory.CreateDirectory(cacheRoot);
        var output = Path.Combine(cacheRoot, $"{stored.Id}-{target.Id}-{stored.Revision}.sav");
        try
        {
            var report = EmeraldHgssTransfer.TransferStored(LocalRepository.LoadWorking(stored), targetSavePath, output, TransferMode.Conversion);
            return new(
                stored.Id,
                stored.Revision,
                target.Id,
                target.Hash,
                report.Succeeded ? TargetPreparationState.Ready : TargetPreparationState.Blocked,
                report.Message,
                TransferMode.Conversion,
                report.Changes,
                report.Succeeded ? output : null,
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            try { File.Delete(output); } catch { }
            return new(stored.Id, stored.Revision, target.Id, target.Hash, TargetPreparationState.Blocked, ex.Message,
                TransferMode.Conversion, [], null, DateTimeOffset.UtcNow);
        }
    }
}
