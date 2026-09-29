using MonoHome.Core.Saves;
using MonoHome.Core.Transfers;
using MonoHome.Core.Repository;
using MonoHome.Core.Sync;
using MonoHome.Verifier;
using System.Security.Cryptography;
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
Console.WriteLine("PASS: V0 save sync state machine and fake remote.");

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

static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
