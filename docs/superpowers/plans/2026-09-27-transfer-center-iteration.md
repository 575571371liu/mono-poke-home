# Transfer Center Iteration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver an Android transfer-center experience where warehouse Pokémon are automatically made target-ready and transmitted directly into an authorized target save, with editing and Pokédex kept off the home flow.

**Architecture:** Keep `PKHeX.Core` and the existing `MonoHome.Core` transfer path. Introduce explicit repository cloning and target-preparation records in core, then make the Android layer request persistent read/write access and perform a verified write-back transaction. Replace the current editing/export controls on the home screen with a transfer dock and long-press secondary actions; use dedicated activities for editor and Pokédex.

**Tech Stack:** .NET 10 for Android, C#, Android Activity/XML views/SAF, PKHeX.Core, existing `MonoHome.Verifier` console regression runner.

**Spec:** `docs/superpowers/specs/2026-09-27-transfer-center-iteration-design.md`

## Global Constraints

- Keep .NET for Android and PKHeX.Core; do not migrate this iteration to Kotlin/Compose or NativeAOT.
- Show PKHeX Simplified Chinese species names in all user-facing warehouse and Pokédex labels.
- Preserve source saves and repository originals; editing always creates a separate legal repository entity.
- Do not expose export-copy, conversion-difference confirmation, or transfer history on the home screen.
- Require persistent, verified target write access; an inaccessible target shows “重新授权目标存档” and cannot transfer.
- Keep recovery copies, temporary output validation, source/target hashes, and transfer journal entries as background safety mechanisms.
- Release only the Emerald → HeartGold / SoulSilver routes already present in `docs/support-matrix.md`.

## Review Focus

- A target URI that grants read but not write permission must not enable transfer or alter its snapshot.
- A target save changed outside the app after preparation must invalidate preparation and block write-back.
- A failed batch must leave target bytes and all repository originals unchanged, while retaining the recovery point only if writing was attempted.
- Editing a PID-sensitive Gen 3 Pokémon must create a new legal repository record without changing the parent record bytes.
- A species absent from the selected target game must show an actionable Chinese reason rather than a false “已适配” state.

---

### Task 1: Make repository copies and target preparation explicit

**Files:**
- Modify: `src/MonoHome.Core/Repository/LocalRepository.cs`
- Create: `src/MonoHome.Core/Transfers/TargetPreparation.cs`
- Modify: `src/MonoHome.Verifier/Program.cs`

**Interfaces:**
- Consumes: `StoredPokemon`, `WorkingEdit`, `EmeraldHgssTransfer.TransferStored`, `PKM`.
- Produces: `StoredPokemon ParentId`, `StoredPokemon Revision`, `LocalRepository.CreateLegalCopy`, `TargetPreparation`, and `TargetPreparationService.Prepare`.

- [ ] **Step 1: Add failing verifier assertions for immutable-parent editing and preparation invalidation**

Add tests after the existing repository assertions:

```csharp
var parentHash = Hash(stored.WorkingPath);
var copy = LocalRepository.CreateLegalCopy(stored, attributeEdit, repositoryRoot);
AssertEqual(stored.Id, copy.ParentId!, "legal copy keeps parent ID");
AssertTrue(copy.Id != stored.Id, "legal copy has its own ID");
AssertEqual(parentHash, Hash(stored.WorkingPath), "legal copy does not rewrite parent");
AssertEqual(1L, copy.Revision, "new copy starts at revision 1");
```

Add a preparation test that prepares the copy against the fixture heartgold save, checks `State == TargetPreparationState.Ready`, then increments the copy revision and checks that `IsCurrentFor(copy)` is false.

- [ ] **Step 2: Run the verifier to confirm the new API is absent**

Run: `..\dotnet-sdk\dotnet.exe run --project src\MonoHome.Verifier\MonoHome.Verifier.csproj -c Release --no-restore`

Expected: compilation failure naming `CreateLegalCopy` and `TargetPreparationService`.

- [ ] **Step 3: Extend the repository manifest without invalidating existing records**

Change `StoredPokemon` to append optional fields:

```csharp
string? ParentId = null,
long Revision = 1
```

Implement:

```csharp
public static StoredPokemon CreateLegalCopy(StoredPokemon parent, PKM legalPokemon, string root)
{
    var copy = Upload(legalPokemon, root);
    var linked = copy with { ParentId = parent.Id, Revision = 1, LegalityStatus = "valid" };
    WriteRecord(linked);
    return linked;
}
```

Change `SaveWorking` to write an updated record with `Revision = stored.Revision + 1` and return that `StoredPokemon`; update all callers to retain the returned record. Keep `OriginalPath` immutable.

- [ ] **Step 4: Add a focused target-preparation model and service**

Create `TargetPreparation.cs` with these public contracts:

```csharp
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
    public static TargetPreparation Prepare(StoredPokemon stored, RegisteredSave target, string targetSavePath, string cacheRoot);
}
```

`Prepare` writes only under `cacheRoot`, calls `EmeraldHgssTransfer.TransferStored` with `TransferMode.Conversion`, and returns `Blocked` rather than throwing for a legal conversion rejection. Its `PreparedSavePath` contains the verified target-save bytes produced by the existing transfer core.

- [ ] **Step 5: Run the verifier and verify preparation artifacts are legal**

Run: `..\dotnet-sdk\dotnet.exe run --project src\MonoHome.Verifier\MonoHome.Verifier.csproj -c Release --no-restore`

Expected: all prior tests pass plus the parent-copy, revision, ready-preparation, and stale-preparation assertions. Confirm the prepared save has a legal inserted entity with `LegalityAnalysis.Valid == true`.

- [ ] **Step 6: Commit the focused core change**

```bash
git add src/MonoHome.Core/Repository/LocalRepository.cs src/MonoHome.Core/Transfers/TargetPreparation.cs src/MonoHome.Verifier/Program.cs
git commit -m "feat: add repository copies and target preparation"
```

### Task 2: Add persistent target write capability and verified write-back

**Files:**
- Modify: `src/MonoHome.Core/Saves/SaveRegistry.cs`
- Create: `src/MonoHome.Android/TargetSaveWriter.cs`
- Modify: `src/MonoHome.Android/MainActivity.cs`
- Modify: `src/MonoHome.Verifier/Program.cs`

**Interfaces:**
- Consumes: `RegisteredSave`, `TargetPreparation`, Android `ContentResolver` and `Uri`.
- Produces: `RegisteredSave SourceFlags`, `TargetSaveWriter.WriteAsync`, and `TargetWriteResult`.

- [ ] **Step 1: Add failing core tests for target metadata persistence**

Extend `RegisteredSave` with optional `SourceFlags` and assert a target registration round-trip:

```csharp
var writable = SaveRegistry.Register(File.ReadAllBytes(heartGoldPath), "heartgold.sav", savesRoot,
    "content://local/heartgold.sav", sourceFlags: 3);
AssertEqual(3, SaveRegistry.Get(savesRoot, writable.Id)!.SourceFlags, "save registry persists URI flags");
```

- [ ] **Step 2: Run verifier and confirm constructor/API failure**

Run: `..\dotnet-sdk\dotnet.exe run --project src\MonoHome.Verifier\MonoHome.Verifier.csproj -c Release --no-restore`

Expected: compilation failure naming `sourceFlags` or `SourceFlags`.

- [ ] **Step 3: Persist granted URI flags in save records**

Append `int SourceFlags = 0` to `RegisteredSave`, add `int sourceFlags = 0` to `SaveRegistry.Register`, and preserve it in `UpdateSnapshot`. Existing JSON records deserialize with `0`.

- [ ] **Step 4: Implement one Android write-back transaction boundary**

Create `TargetSaveWriter.cs`:

```csharp
public sealed record TargetWriteResult(bool Succeeded, byte[]? WrittenBytes, string? BackupPath, string Message);

public sealed class TargetSaveWriter
{
    public TargetSaveWriter(ContentResolver resolver, string savesRoot) { /* assign fields */ }
    public Task<TargetWriteResult> WriteAsync(RegisteredSave target, byte[] preparedBytes, CancellationToken cancellationToken = default);
}
```

`WriteAsync` must: open and hash the registered URI; require `target.SourceFlags` to include both read and write grants; create an app-private timestamped backup from the URI bytes; open output with mode `"wt"`; write prepared bytes; reopen input; compare bytes using `CryptographicOperations.FixedTimeEquals`; inspect using `SaveInspector.Inspect`; and return the verified written bytes. On any failure before output open, return a failed result without writing. On a failed post-write read/verify, retain backup and return its path.

- [ ] **Step 5: Request and retain read/write permission during target import**

In `MainActivity.PickSave`, add `ActivityFlags.GrantWriteUriPermission`. In `OnActivityResult`, calculate:

```csharp
var granted = data.Flags & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
resolver.TakePersistableUriPermission(uri, granted);
```

Pass `(int)granted` to target `RegisterSave`. Treat a target as transferable only if both persistent flags are present and its current URI bytes hash equals its registered snapshot hash. Read-only source saves remain allowed for upload.

- [ ] **Step 6: Replace export-only completion with writer result in a narrow integration test path**

Change `OverwriteRegisteredHeartGoldAsync` to call `TargetSaveWriter.WriteAsync`, then call `SaveRegistry.UpdateSnapshot` only from `result.WrittenBytes`. Do not expose it yet as the home primary button; Task 3 replaces that UI. Preserve the existing `CreateTargetBackup` only until this new writer creates the same recovery path, then remove duplicate backup creation.

- [ ] **Step 7: Run core regression and Android build**

Run:

```powershell
$dotnet=(Resolve-Path ..\dotnet-sdk\dotnet.exe).Path
& $dotnet run --project src\MonoHome.Verifier\MonoHome.Verifier.csproj -c Release --no-restore
& $dotnet build src\MonoHome.Android\MonoHome.Android.csproj -c Release --no-restore -v:minimal
```

Expected: verifier passes, Android build has 0 warnings and 0 errors.

- [ ] **Step 8: Commit target capability and writer changes**

```bash
git add src/MonoHome.Core/Saves/SaveRegistry.cs src/MonoHome.Android/TargetSaveWriter.cs src/MonoHome.Android/MainActivity.cs src/MonoHome.Verifier/Program.cs
git commit -m "feat: verify target save write-back"
```

### Task 3: Replace the home export/editor flow with target-ready transfer

**Files:**
- Modify: `src/MonoHome.Android/Resources/layout/activity_main.xml`
- Modify: `src/MonoHome.Android/MainActivity.cs`
- Modify: `src/MonoHome.Android/Resources/values/strings.xml`
- Test: Android 30 UI Automator dump and installed APK

**Interfaces:**
- Consumes: `TargetPreparationService.Prepare`, `TargetPreparation.IsCurrentFor`, `TargetSaveWriter.WriteAsync`.
- Produces: `PrepareSelectedAsync`, `TransferSelectedAsync`, target-aware warehouse card state, and `传送至 {目标}` home action.

- [ ] **Step 1: Define the failing UI checks before changing layout**

After launching the current app, capture an XML dump and assert it still contains the old labels:

```powershell
$adb='C:\Users\liujinwen\AppData\Local\Android\Sdk\platform-tools\adb.exe'
& $adb shell uiautomator dump /sdcard/before.xml
& $adb pull /sdcard/before.xml .\before.xml
Select-String -Path .\before.xml -Pattern '编辑工作副本|生成合法目标存档副本|导出最近备份'
```

Expected: all three labels exist before the layout rewrite.

- [ ] **Step 2: Reduce the home XML to transfer-center controls**

Remove the inline edit field block, `export_backup_button`, `save_nickname`, `discard_edits`, and the conversion-mode spinner. Keep source upload controls, warehouse grid, target selector, animation, and save cards. Add:

```xml
<TextView android:id="@+id/target_readiness" ... android:text="选择目标存档后自动适配" />
<Button android:id="@+id/transfer_button" ... android:text="传送至目标存档" />
<Button android:id="@+id/reauthorize_target" ... android:text="重新授权目标存档" android:visibility="gone" />
```

Use Chinese copy only for Pokémon names and controls. Do not display transfer history in this layout.

- [ ] **Step 3: Remove obsolete home bindings and route all transfer through preparation**

Remove edit-view fields, `SaveNickname`, `DiscardEdits`, `BeginExport`, `BeginBackupExport`, `pendingTransferId`, `pendingTransferIds`, and `transferMode` from `MainActivity`. Add an in-memory `Dictionary<string, TargetPreparation>` keyed by repository ID. On target change or card selection, call `PrepareSelectedAsync`; it loads the current `StoredPokemon`, calls `TargetPreparationService.Prepare` in `Task.Run`, and updates readiness text.

`TransferSelectedAsync` must reject a missing or stale readiness record; otherwise pass the prepared file bytes to `TargetSaveWriter`, update `heartGoldSave`/`heartGoldBytes` only after success, append a successful `TransferJournal` record, and show `“{中文种类名} 已传送至 {目标游戏} 存档”`. For multi-select, prepare all records first and write one verified batch file.

- [ ] **Step 4: Make card state target-aware and preserve long-press for secondary actions**

Extend `RepositoryMeta` to append one of `已适配`, `正在适配`, `无法传送` or `待选择目标`. Keep normal `card.Click` for selection. Add:

```csharp
card.LongClick += (_, _) => ShowWarehouseActions(record);
```

`ShowWarehouseActions` initially presents exactly `查看个体档案`, `查看宝可梦图鉴`, and `编辑并另存为合法副本`; Tasks 4 and 5 wire their activities.

- [ ] **Step 5: Add target-reauthorization behavior**

When target write flags are missing, URI read fails, or its hash differs from the snapshot, set readiness text to a Chinese reason, disable transfer, show `reauthorize_target`, and route its click to `PickSave(HeartGoldRequest)`. A successful re-import must invalidate all preparation records.

- [ ] **Step 6: Rebuild and run UI checks against the emulator**

Run a clean release build, install it, open the home screen, upload one fixture Pokémon, select HeartGold, and verify via UI Automator:

```powershell
Select-String -Path .\after.xml -Pattern '传送至.*心金|已适配|编辑工作副本|生成合法目标存档副本|导出最近备份'
```

Expected: the first two patterns are present; the final three patterns are absent. Verify the displayed warehouse species name is Chinese and no raw nickname is the card title.

- [ ] **Step 7: Commit the transfer-center home flow**

```bash
git add src/MonoHome.Android/Resources/layout/activity_main.xml src/MonoHome.Android/MainActivity.cs src/MonoHome.Android/Resources/values/strings.xml
git commit -m "feat: make home a direct transfer center"
```

### Task 4: Add individual details and copy-only editor activities

**Files:**
- Create: `src/MonoHome.Android/DetailActivity.cs`
- Create: `src/MonoHome.Android/EditCopyActivity.cs`
- Create: `src/MonoHome.Android/Resources/layout/activity_detail.xml`
- Create: `src/MonoHome.Android/Resources/layout/activity_edit_copy.xml`
- Modify: `src/MonoHome.Android/AndroidManifest.xml`
- Modify: `src/MonoHome.Android/MainActivity.cs`
- Modify: `src/MonoHome.Core/Repository/LocalRepository.cs`
- Modify: `src/MonoHome.Verifier/Program.cs`

**Interfaces:**
- Consumes: `StoredPokemon.Id`, `LocalRepository.LoadWorking`, `LocalRepository.ApplyEdit`, `LocalRepository.CreateLegalCopy`.
- Produces: read-only detail activity and an edit activity that returns a newly created repository ID.

- [ ] **Step 1: Add verifier coverage for complete copy lifecycle**

Add a test that applies a valid edit to a parent, calls `CreateLegalCopy`, reloads both records, and checks the parent original/working byte hashes stay unchanged while the child contains the edited value and `LegalityStatus == "valid"`.

- [ ] **Step 2: Run verifier to ensure the lifecycle assertion fails before UI wiring**

Run: `..\dotnet-sdk\dotnet.exe run --project src\MonoHome.Verifier\MonoHome.Verifier.csproj -c Release --no-restore`

Expected: failure until Task 1's copy behavior and validation result are fully used.

- [ ] **Step 3: Implement the read-only individual detail activity**

`DetailActivity` takes `repository_id` in `Intent.Extras`, reads from `FilesDir/warehouse`, and renders Chinese species name, original game, trainer, level, moves, shiny, held item, state, Pokerus, egg, and legality. It has no mutation control. If the record is missing, show a Chinese error and finish.

- [ ] **Step 4: Implement the dedicated copy editor**

`EditCopyActivity` receives the same ID, pre-fills the existing editor fields, builds `WorkingEdit`, calls `LocalRepository.ApplyEdit`, then runs the source-context legality gate used by `TargetPreparationService` before `CreateLegalCopy`. On success return:

```csharp
SetResult(Result.Ok, new Intent().PutExtra("repository_id", child.Id));
Finish();
```

The only destructive-looking action is named `生成新的合法副本并保存至仓库`; it must never call `SaveWorking` on the parent. Show validation errors inline in Chinese.

- [ ] **Step 5: Wire long-press actions and activity results**

In `ShowWarehouseActions`, start `DetailActivity`, `PokedexActivity` (Task 5), or `EditCopyActivity`. Handle the edit result in `OnActivityResult` by calling `RefreshWarehouse(returnedId)`, clearing old selection, and displaying the new Chinese species card. Do not navigate the normal card click into editing.

- [ ] **Step 6: Build and emulator-test the copy-only semantics**

Install a release build. Long-press a warehouse card, open editor, change level or nickname, save, return to home, and confirm card count increases by one. Use `adb pull` on app-private files or Verifier equivalent to compare the parent record's `original.pkm`/`working.pkm` hashes before and after.

- [ ] **Step 7: Commit detail and copy editor activities**

```bash
git add src/MonoHome.Android/DetailActivity.cs src/MonoHome.Android/EditCopyActivity.cs src/MonoHome.Android/Resources/layout/activity_detail.xml src/MonoHome.Android/Resources/layout/activity_edit_copy.xml src/MonoHome.Android/AndroidManifest.xml src/MonoHome.Android/MainActivity.cs src/MonoHome.Core/Repository/LocalRepository.cs src/MonoHome.Verifier/Program.cs
git commit -m "feat: add copy-only Pokémon editor"
```

### Task 5: Add offline Chinese Pokédex and target compatibility view

**Files:**
- Create: `src/MonoHome.Core/Pokedex/PokedexEntry.cs`
- Create: `src/MonoHome.Core/Pokedex/PokedexService.cs`
- Create: `src/MonoHome.Android/PokedexActivity.cs`
- Create: `src/MonoHome.Android/Resources/layout/activity_pokedex.xml`
- Modify: `src/MonoHome.Android/AndroidManifest.xml`
- Modify: `src/MonoHome.Android/MainActivity.cs`
- Modify: `src/MonoHome.Verifier/Program.cs`

**Interfaces:**
- Consumes: `PKHeX.Core.SpeciesName`, personal data tables, selected `RegisteredSave`, `TargetPreparation`.
- Produces: `PokedexEntry`, `PokedexService.Get`, `PokedexService.Search`, and target compatibility text.

- [ ] **Step 1: Write failing verifier tests for Chinese lookup and target availability**

Use known fixture species values:

```csharp
var dex = PokedexService.Get(260, 4);
AssertEqual("巨沼怪", dex.ChineseName, "Pokédex uses Chinese species name");
AssertTrue(dex.BaseStats.Count == 6, "Pokédex exposes six base stats");
AssertTrue(PokedexService.Search("巨沼").Any(entry => entry.Species == 260), "Pokédex searches Chinese names");
```

Add an assertion that the same entry reports it exists in HeartGold and that an unavailable/unsupported target route reports a non-empty Chinese reason.

- [ ] **Step 2: Run verifier to confirm missing Pokédex APIs**

Run: `..\dotnet-sdk\dotnet.exe run --project src\MonoHome.Verifier\MonoHome.Verifier.csproj -c Release --no-restore`

Expected: compilation failure naming `PokedexService`.

- [ ] **Step 3: Implement a small offline Pokédex service**

Define:

```csharp
public sealed record PokedexEntry(
    int Species, string ChineseName, IReadOnlyList<int> BaseStats,
    IReadOnlyList<string> Types, IReadOnlyList<string> Abilities,
    string GenderRatio, IReadOnlyList<int> EvolvesFrom, IReadOnlyList<int> EvolvesTo);

public static class PokedexService
{
    public static PokedexEntry Get(int species, int generation);
    public static IReadOnlyList<PokedexEntry> Search(string query, int generation = 4);
    public static string GetTargetCompatibility(int species, RegisteredSave? target);
}
```

Use PKHeX Chinese species data and personal tables; return Chinese field labels. Keep version-specific move-learning as a later enhancement only if the required PKHeX API can be verified from the local checkout; this task must not invent incomplete move sources.

- [ ] **Step 4: Implement the Pokedex activity**

Provide an EditText search field, a compact results list, and a detail panel showing Chinese name, National Dex number, icon, types, abilities, six base stats, forms/evolution where available, and current target compatibility. The activity accepts optional `species` and `target_save_id` extras and opens that entry directly when launched from a card. It does not show a transfer button for unreleased routes.

- [ ] **Step 5: Wire home and editor navigation to Pokédex**

Add a top “图鉴” action in the home header and connect the long-press item to `PokedexActivity` with the card species. In `EditCopyActivity`, provide contextual text links beside species and moves that launch the selected species entry; preserve unsaved editor text when returning.

- [ ] **Step 6: Run core and Android verification**

Run verifier, build a clean release APK, install it, open `图鉴`, search `巨沼`, and verify through UI Automator that `巨沼怪`, National Dex information, and HeartGold compatibility are visible. Also launch Pokédex from a long-pressed warehouse card.

- [ ] **Step 7: Commit the offline Pokédex**

```bash
git add src/MonoHome.Core/Pokedex src/MonoHome.Android/PokedexActivity.cs src/MonoHome.Android/Resources/layout/activity_pokedex.xml src/MonoHome.Android/AndroidManifest.xml src/MonoHome.Android/MainActivity.cs src/MonoHome.Verifier/Program.cs
git commit -m "feat: add offline Chinese Pokédex"
```

### Task 6: Release verification and delivery

**Files:**
- Modify: `docs/support-matrix.md`
- Modify: `..\..\outputs\MonoHome\MONO-HOME-test-report.md`
- Create: `..\..\outputs\MonoHome\MONO-HOME-release.apk`

**Interfaces:**
- Consumes: completed core, Android UI, private Emerald and HeartGold fixtures, Android 30 emulator.
- Produces: signed APK, evidence-backed test report, updated supported-route claims.

- [ ] **Step 1: Update support and user-facing boundaries**

Change `docs/support-matrix.md` to state that released Emerald → HeartGold / SoulSilver routes use direct target write-back only when persistent write access and read-back verification succeed. State that unsupported profiles remain data-only in Pokédex.

- [ ] **Step 2: Run the full core fixture suite**

Run:

```powershell
$dotnet=(Resolve-Path ..\dotnet-sdk\dotnet.exe).Path
& $dotnet run --project src\MonoHome.Verifier\MonoHome.Verifier.csproj -c Release --no-restore
```

Expected: user fixture hashes match; all 83 Emerald conversions pass; target preparation, direct-write preparation, copy-only edit, Chinese Pokédex, fidelity rejection, and batch output assertions pass.

- [ ] **Step 3: Do a clean release build and capture the final hash**

Run:

```powershell
$dotnet=(Resolve-Path ..\dotnet-sdk\dotnet.exe).Path
& $dotnet clean src\MonoHome.Android\MonoHome.Android.csproj -c Release -v:minimal
& $dotnet build src\MonoHome.Android\MonoHome.Android.csproj -c Release -t:Rebuild -v:minimal
Copy-Item src\MonoHome.Android\bin\Release\net10.0-android\io.github.monohome-Signed.apk ..\..\outputs\MonoHome\MONO-HOME-release.apk -Force
Get-FileHash ..\..\outputs\MonoHome\MONO-HOME-release.apk -Algorithm SHA256
```

Expected: 0 warnings, 0 errors, and a recorded SHA-256.

- [ ] **Step 4: Validate the direct transfer workflow on Android 30**

Install the APK, re-import the fixture target with read/write grants, upload one Emerald Pokémon, select the target, wait for “已适配”, invoke `传送至心金存档`, then reopen/inspect the target. Verify the target has the legal entity and that the source save hash and repository original hash are unchanged. Repeat with two selected Pokémon for a batch.

- [ ] **Step 5: Validate blocked-write and long-press secondary flows**

Re-import the target read-only or revoke its persisted grant; verify `重新授权目标存档` appears and no transfer file is produced. Re-grant it and verify successful transfer. Long-press a card, inspect individual details, create an edited legal copy, and navigate to the Chinese Pokédex.

- [ ] **Step 6: Write evidence into the delivery report**

Update `MONO-HOME-test-report.md` with exact APK SHA-256, fixture test command/output summary, emulator API level, direct-write success proof, write-block proof, original-hash proof, copy-only edit proof, Pokédex UI proof, and known storage limitation for Android-restricted simulator directories.

- [ ] **Step 7: Commit release documentation**

```bash
git add docs/support-matrix.md
git commit -m "docs: document direct transfer release boundaries"
```

Do not commit APKs, fixture saves, Android UI dumps, or user-private test reports into the source repository.
