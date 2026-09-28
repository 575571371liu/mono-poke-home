# Android Prototype Warehouse Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Android APK follow the approved upload and download warehouse interactions.

**Architecture:** Keep PKHeX.Core and native Android Views. Expose real save storage pages in core, render one full-width native page at a time, and keep warehouse download target selection after repository selection.

**Tech Stack:** .NET 10 for Android, C#, Android Views/XML, PKHeX.Core, MonoHome.Verifier.

**Spec:** `docs/superpowers/specs/2026-09-27-universal-save-hub-design.md`

## Global Constraints

- Source pages are party first, followed by the actual save boxes; one page is visible at a time.
- Header arrows must wrap and show real occupancy and capacity.
- Standard boxes contain all 30 positions; empty positions are not selectable.
- Upload is source save → local warehouse only. Target selection belongs exclusively to warehouse download.
- Clicking a registered source-save card switches the board below to that save's party and boxes; the upload selection is cleared.
- Unsupported routes must be blocked in Chinese. Source snapshots, repository originals, and verified write-back remain unchanged.

## Review Focus

- Empty slots cannot be uploaded.
- Party ↔ last box cycling works in both directions.
- Changing source save clears upload selection.
- No write occurs before a writable registered target is chosen.
- Read-only, stale, and unsupported targets cannot start write-back.

### Task 1: Add real storage-page topology

**Files:**
- Modify: `src/MonoHome.Core/Saves/BoxReader.cs`
- Modify: `src/MonoHome.Verifier/Program.cs`

**Interfaces:** Produce `StorageSlot`, `StoragePage`, and `BoxReader.ReadPages(ReadOnlyMemory<byte>, string)`.

- [ ] Add verifier assertions that the first page is `随身携带` with six positions, a standard page has 30 positions, and empty positions are retained.
- [ ] Run the verifier; it must fail because `ReadPages` does not exist.
- [ ] Implement `StorageSlot(int Index, PokemonSlot? Pokemon)`, `StoragePage(string Id, string Name, int Capacity, IReadOnlyList<StorageSlot> Slots)`, and `ReadPages`. Read six party positions and every `SaveFile.BoxCount × SaveFile.BoxSlotCount` position; use null for species zero.
- [ ] Re-run verifier and require all checks to pass.

### Task 2: Replace source spinner with the cyclic source board

**Files:**
- Modify: `src/MonoHome.Android/Resources/layout/activity_main.xml`
- Modify: `src/MonoHome.Android/MainActivity.cs`

**Interfaces:** Consume `StoragePage`, `StorageSlot`, and `LocalRepository.Upload`; produce a full-width source board and selected-source upload action.

- [ ] Remove the source spinner, icon preview, and single-item upload control. Add full-width `source_box_header`, `source_box_grid`, and hidden `source_upload_action`.
- [ ] Keep `sourcePages`, `sourcePageIndex`, and `selectedSourceSlots`. Render the active page in six columns (five at narrow width). Wire arrows with `(sourcePageIndex + delta + sourcePages.Count) % sourcePages.Count`.
- [ ] Clicking an occupied slot opens Chinese detail with `加入本次上传` or `移出本次上传`; empty slots do nothing.
- [ ] Upload every selected snapshot entity to local warehouse, show the existing two-second visual, refresh warehouse, then clear selection. Do not display a target.
- [ ] Build the Android project with zero warnings/errors.

### Task 3: Make warehouse download target-first

**Files:**
- Modify: `src/MonoHome.Android/Resources/layout/activity_main.xml`
- Modify: `src/MonoHome.Android/MainActivity.cs`

**Interfaces:** Consume `selectedWarehouseIds`, `TargetPreparationService`, and `TargetSaveWriter`; produce a target dialog launched by `下载至目标存档`.

- [ ] Keep warehouse action hidden until cards are selected; its only primary label is `下载至目标存档`.
- [ ] On click, list all registered saves with Chinese name, generation, and writable/stale status. Only a persistent read/write target whose hash matches its snapshot can begin preparation.
- [ ] Keep existing preparation and verified writer for supported routes. For every unsupported route, show an actionable Chinese block reason and keep repository entities unchanged.
- [ ] Run verifier and Android build with zero failures/warnings.

### Task 4: Build and verify release APK

**Files:**
- Create: `..\\..\\outputs\\MonoHome\\MONO-HOME-release.apk`

- [ ] Clean and rebuild release APK, copying the signed artifact to outputs.
- [ ] Install it on Android. Verify: 30 slots; wrapped header arrows; slot details and upload selection; upload to warehouse; warehouse selection opens `下载至目标存档` target list before any write.
- [ ] Commit source changes with `feat: align Android warehouse with transfer prototype`. Do not commit APKs or private saves.
