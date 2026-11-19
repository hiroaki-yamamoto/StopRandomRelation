---
id: spec
title: Detailed Specifications
---

# Detailed Specifications: Stop Random Relation

## 1. Overview and Purpose

### 1.1 Background
In vanilla *RimWorld*, when pawns form blood or marriage/romance relationships (either randomly generated during raids/visits or established through gameplay events), those world pawns are permanently protected from the engine's world pawn garbage collection (`WorldPawnGC`). Over extended playthroughs, this mechanism leads to significant "save bloat" and cumulative performance degradation as thousands of deceased or irrelevant world pawns remain serialized in the save file.

While players often enjoy the dynamic storytelling produced by "uninvited family relations" (e.g., a raider or guest turning out to be a colonist's sibling or spouse), keeping these world pawns indefinitely once their narrative role is concluded is unnecessary.

### 1.2 Objective
*Stop Random Relation* dynamically prunes relationships and disposes of world pawns when they are no longer relevant to active gameplay. This mod preserves relationships while pawns are actively present in the player's colony or story, cleanly severs relationships once those pawns leave, die without a possibility of resurrection, or exist entirely outside colonist maps, protects living faction leaders from GC, and optionally deletes excess unprotected off-map world pawns once a configurable threshold is reached.

---

## 2. Terminology and Domain Definitions

| Term | Definition | RimWorld / Mod Code Equivalent |
| :--- | :--- | :--- |
| **Colonist** | A free colonist belonging to the player's faction. | `pawn != null && pawn.Faction != null && pawn.Faction.IsPlayer && pawn.IsFreeColonist` (`PawnStateEvaluator.IsColonist`) |
| **Player Faction Pawn** | Any pawn belonging to the player's faction (colonists, colony animals, and player-controlled mechanoids). | `pawn?.Faction != null && pawn.Faction.IsPlayer` (`PawnStateEvaluator.IsPlayerFaction`) |
| **World Pawn** | A pawn maintained in the world pawn registry (`WorldPawns`). | `Find.WorldPawns.Contains(pawn)` / `Find.WorldPawns.AllPawnsAliveOrDead` |
| **Colonist Map** | Any non-disposed active `Map` that is a player home map or has free colonists present. | `map != null && !map.Disposed && (map.IsPlayerHome \|\| (map.mapPawns != null && map.mapPawns.FreeColonistsCount > 0))` (`PawnStateEvaluator.IsColonistMap`) |
| **On Colonist Map** | Pawn is currently spawned or held (e.g., carried, in a bed/container) on a colonist map. | `PawnStateEvaluator.IsColonistMap(pawn.Map ?? pawn.MapHeld)` (`PawnStateEvaluator.IsOnColonistMap`) |
| **On Any Map** | Pawn (or its corpse/holder) is spawned or held on any map, or satisfies `IsOnColonistMap`. | `pawn.Spawned \|\| pawn.Map != null \|\| pawn.MapHeld != null \|\| (pawn.Dead && pawn.Corpse != null && (pawn.Corpse.Spawned \|\| pawn.Corpse.MapHeld != null)) \|\| IsOnColonistMap(pawn)` (`PawnStateEvaluator.IsOnAnyMap`) |
| **Blood Relation** | A direct biological family relationship between two pawns (`RelationCategory.Blood`). | `def.familyByBloodRelation \|\| def == PawnRelationDefOf.ParentBirth` (`PawnStateEvaluator.GetRelationCategory`) |
| **Marriage / Romance Relation** | Formal or romantic partner relationships (`RelationCategory.MarriageOrRomance`), included when `IncludeNonBloodRelations` is enabled. | `def` is `Spouse`, `Fiance`, `Lover`, `ExSpouse`, or `ExLover` (`PawnStateEvaluator.GetRelationCategory`) |
| **Non-Targeted Relation** | Any other relation (`RelationCategory.None`, e.g., animal `Bond` or mechanitor overseer links); never removed. | `GetRelationCategory(def) == RelationCategory.None` |
| **Resurrectable Pawn** | A deceased pawn with death refusal / active resurrection, or an existing, non-destroyed, non-discarded, non-desiccated corpse. | `pawn != null && pawn.Dead && (pawn.HasDeathRefusalOrResurrecting \|\| (corpse != null && !corpse.Destroyed && !corpse.Discarded && corpse.CurRotDrawMode != RotDrawMode.Dessicated && corpse.GetComp<CompRottable>()?.Stage != RotStage.Dessicated))` (`PawnStateEvaluator.IsResurrectable`) |
| **Unresurrectable Pawn** | A deceased pawn that does not satisfy `IsResurrectable`. | `pawn != null && pawn.Dead && !IsResurrectable(pawn)` (`PawnStateEvaluator.IsUnresurrectable`) |
| **Active Faction Leader** | A pawn currently designated as the living leader of a faction. | `pawn != null && !pawn.Dead && pawn.Faction != null && pawn.Faction.leader == pawn` (`PawnStateEvaluator.IsActiveFactionLeader`) |
| **Protected by World State** | A pawn that must be retained due to RimWorld world/quest/captivity/caravan/discard state. | `PawnStateEvaluator.IsProtectedByWorldState(pawn, worldPawns)` |
| **Garbage Collection (GC)** | RimWorld's `WorldPawnGC` retention evaluation and discarding process (`RemoveAndDiscardPawnViaGC`). | `WorldPawnGC.GetCriticalPawnReason`, `WorldPawnGC.AccumulatePawnGCData`, `WorldPawns.RemoveAndDiscardPawnViaGC` |

---

## 3. Functional Requirements

### 3.1 Master Switch & Targeted Relations
1. **Master Enable/Disable (`ModEnabled`)**:
   - When `false`, all relation pruning hooks, off-map sweeps, threshold pruning, sweep scheduling, and `WorldPawnGC` faction leader protections are bypassed.
2. **Targeted Relation Categories (`RelationCleanupPolicy.IsTargetRelation`)**:
   - `RelationCategory.Blood` (`familyByBloodRelation` or `ParentBirth`) is always targeted when cleanup triggers apply.
   - `RelationCategory.MarriageOrRomance` (`Spouse`, `Fiance`, `Lover`, `ExSpouse`, `ExLover`) is targeted when `IncludeNonBloodRelations` is `true` (default).
   - `RelationCategory.None` (such as animal `Bond` and mechanitor overseer links) is never targeted or removed.

### 3.2 Relations Between Colonists
1. **Preservation During Life & Resurrectable Death**:
   - Targeted relations between colonists are preserved while both colonists are alive and while a deceased colonist satisfies `PawnStateEvaluator.IsResurrectable`.
2. **Severing Upon Permanent Death**:
   - Governed by `CleanColonistRelationsOnPermanentDeath` (default: `true`).
   - Severed when:
     - A colonist dies (`Pawn.Kill` -> `RelationTrigger.Death`) and is already unresurrectable (`!subject.IsResurrectable`).
     - A colonist's corpse is destroyed (`Corpse.Destroy` Prefix -> `RelationTrigger.PermanentDeath`), provided `innerPawn.Dead && !innerPawn.HasDeathRefusalOrResurrecting`.
     - A colonist's corpse changes rot stage (`Corpse.RotStageChanged` Postfix -> `RelationTrigger.PermanentDeath`) and `!IsResurrectable(innerPawn)` (and `!innerPawn.HasDeathRefusalOrResurrecting`).
     - An off-map sweep (`RelationTrigger.OffMapSweep`) encounters a pre-existing dead, unresurrectable colonist in `WorldPawns`.
3. **Non-Colonist Player-Faction Pawns**:
   - Colony animals and player-controlled mechanoids (`IsPlayerFaction && !IsColonist`) never trigger relation pruning and are never severed from others.

### 3.3 Relations Between a Colonist and a World Pawn
1. **Preservation While Active**:
   - Relations between a colonist and a non-player world pawn are maintained while both remain alive and the world pawn has not exited a map.
   - Colonists and player-faction pawns exiting a map (e.g., forming a caravan) do **not** sever any relations on `Pawn.ExitMap`.
2. **Severing Conditions**:
   - Governed by `CleanColonistToWorldRelationsOnExitOrDeath` (default: `true`).
   - Severed immediately when **any** of the following occur:
     1. **Map Exit (`RelationTrigger.MapExit`)**: A non-player pawn exits a map (`Pawn.ExitMap`) and the related pawn is a colonist.
     2. **Relative Death (`RelationTrigger.Death` / `PermanentDeath`)**: A non-player pawn dies (`Pawn.Kill` or corpse destruction/desiccation) and the related pawn is a colonist.
     3. **Colonist Death (`RelationTrigger.Death` / `PermanentDeath`)**: A colonist dies (`Pawn.Kill` or permanent death) and the related pawn is a non-player pawn (`!other.IsPlayerFaction`) — severed immediately on colonist death even if the colonist's corpse is still resurrectable.
     4. **Off-Map Sweep (`RelationTrigger.OffMapSweep`)**: A pre-existing dead, unresurrectable colonist in `WorldPawns` has a relation to a non-player pawn.
3. **GC Eligibility**:
   - Once severed, the world pawn relative becomes eligible for `WorldPawnGC` discarding (if off-map and not otherwise protected).
   - A deceased colonist only becomes eligible for GC once unresurrectable and placed in `WorldPawns`.

### 3.4 Relations Between World Pawns
1. **World Pawns on a Map (Exit or Death)**:
   - Governed by `CleanWorldToWorldRelationsOnExitOrDeath` (default: `true`).
   - While two non-player world pawns remain alive on a colonist map, their relation is maintained.
   - When either non-player pawn exits a map (`RelationTrigger.MapExit`) or dies (`RelationTrigger.Death` / `PermanentDeath`), its relations with other non-player pawns (`!other.IsColonist && !other.IsPlayerFaction`) are severed immediately.
2. **Off-Map World Pawn Sweeps (`RelationTrigger.OffMapSweep`)**:
   - Governed by `CleanOffMapWorldPawnRelations` (default: `true`).
   - Triggered at the start of `WorldPawnGC.AccumulatePawnGCData` (Prefix) and on `WorldPawns.WorldPawnsTick` (Postfix) according to `WorldPawnSweepScheduler`.
   - Severs relations where neither `subject` nor `other` is a colonist, player-faction pawn, or currently on a colonist map (`!IsColonist && !IsPlayerFaction && !IsOnColonistMap`).
   - Also cleans pre-existing dead, unresurrectable colonists in `WorldPawns` (both colonist-to-colonist and colonist-to-world-pawn relations, subject to their respective toggles).

### 3.5 Off-Map Sweep Scheduling & Optional Threshold Pruning
1. **Sweep Scheduling (`WorldPawnSweepScheduler`)**:
   - Governed by `OffMapSweepIntervalTicks` (clamped `0..1,000,000`, default: `2500`).
   - **Periodic Mode (`interval > 0`)**: Runs during `WorldPawnsTick` when `ticksGame > 0` and `ticksGame % interval == 0` (at most once per tick via atomic claim).
   - **Immediate Mode (`interval == 0`)**: Runs on the next positive tick whenever marked dirty (`ExitMap`, `Kill`, `Corpse.Destroy`, `Corpse.RotStageChanged`, `PassToWorld`, or settings changes/resets), and also runs a fallback check every `60` ticks (`ImmediateFallbackIntervalTicks`) if `DeleteUnprotectedOffMapWorldPawns` is `true` and `totalWorldPawns >= threshold`.
2. **Threshold Deletion of Unprotected Off-Map World Pawns (`DeleteUnprotectedOffMapWorldPawns`)**:
   - Governed by `DeleteUnprotectedOffMapWorldPawns` (default: `false`) and `UnprotectedOffMapWorldPawnThreshold` (clamped `1..1,000,000`, default: `100`).
   - Runs during `WorldPawnsTick` after `CleanAllOffMapWorldPawnRelations` when the scheduler allows a sweep.
   - A world pawn is eligible for deletion (`RelationCleanupPolicy.ShouldDeleteWorldPawnOnThreshold`) **only if** all of the following hold:
     - `ModEnabled` and `DeleteUnprotectedOffMapWorldPawns` are `true`.
     - Pawn is not null, not `Discarded`, and not on any map (`!IsOnAnyMap && !Role.IsOnColonistMap`).
     - Pawn is not `IsColonist` and not `IsPlayerFaction`.
     - Pawn is not a protected living faction leader (`!(ProtectLivingFactionLeaders && IsActiveFactionLeader)`).
     - Pawn has no remaining direct or incoming relations (`!HasAnyProtectedRelation`).
     - Pawn is not protected by RimWorld world state (`!IsProtectedByWorldState`: must be in `WorldPawns`, not in `ForcefullyKeptPawns`, not `PawnSourced`, not `IsBeingDiscarded`, not `IsPrisonerOfColony`, not `IsSlaveOfColony`, not reserved by a quest, not kidnapped, not traveling in a transport pod world object, and `worldPawns.GetSituation(pawn)` must be `Free`, `Dead`, or `FactionLeader`).
   - If the total count of eligible candidates is `>= threshold`, all eligible candidates are removed and discarded via `worldPawns.RemoveAndDiscardPawnViaGC(pawn)`.
   - When `deletedCount > 0` and `Current.ProgramState == ProgramState.Playing`, displays a neutral in-game message (`SRR_WorldPawnsDeletedMessage`: `"Deleted {0} unprotected off-map world pawn(s)."`).

### 3.6 Faction Leader Protection
1. **Preservation Rule**:
   - Governed by `ProtectLivingFactionLeaders` (default: `true`).
   - When `ShouldProtectFromGC(pawn, options)` is `true` (`ModEnabled && ProtectLivingFactionLeaders && IsActiveFactionLeader`) and `WorldPawnGC.GetCriticalPawnReason` returns null/empty, the postfix sets `__result = "FactionLeader"` so `WorldPawnGC` retains the leader even with zero relations.
   - When `VerboseLogging` is enabled, logs the protection event to the developer console.
2. **Deceased Leaders**:
   - `IsActiveFactionLeader` checks `!pawn.Dead`. Deceased leaders are not protected by this rule.

### 3.7 Mod Settings Menu
1. **Integration**:
   - Accessible via **Options -> Mod Settings -> Stop Random Relation** (`StopRandomRelationMod.SettingsCategory` / `DoSettingsWindowContents`).
2. **Vanilla UI Aesthetic**:
   - Uses a scrollable `Widgets.BeginScrollView` container with `Listing_Standard`, vanilla section headers (`ColoredText.TipSectionTitleColor`), checkboxes with tooltips, numeric fields (`Widgets.TextFieldNumeric`) for integer settings, and a `"Reset to defaults"` button (`180x32`).
   - Localized in English (`Languages/English/Keyed/StopRandomRelation.xml`) and Japanese (`Languages/Japanese/Keyed/StopRandomRelation.xml`).
3. **Immediate Application**:
   - `RefreshSnapshot()` is called at the end of `DoSettingsWindowContents` (and during `ExposeData` / `ResetToDefaults`), atomically publishing a new `RelationCleanupOptions` snapshot and calling `WorldPawnSweepScheduler.Shared.MarkDirty()` if any value changed.
4. **Configurable Options** (11 fields in `StopRandomRelationSettings` / `RelationCleanupOptions`):
   - `modEnabled` (`ModEnabled`, bool, default: `true`)
   - `cleanColonistRelationsOnPermanentDeath` (`CleanColonistRelationsOnPermanentDeath`, bool, default: `true`)
   - `cleanColonistToWorldRelationsOnExitOrDeath` (`CleanColonistToWorldRelationsOnExitOrDeath`, bool, default: `true`)
   - `cleanWorldToWorldRelationsOnExitOrDeath` (`CleanWorldToWorldRelationsOnExitOrDeath`, bool, default: `true`)
   - `cleanOffMapWorldPawnRelations` (`CleanOffMapWorldPawnRelations`, bool, default: `true`)
   - `offMapSweepIntervalTicks` (`OffMapSweepIntervalTicks`, int, default: `2500`, range: `0..1,000,000`)
   - `deleteUnprotectedOffMapWorldPawns` (`DeleteUnprotectedOffMapWorldPawns`, bool, default: `false`)
   - `unprotectedOffMapWorldPawnThreshold` (`UnprotectedOffMapWorldPawnThreshold`, int, default: `100`, range: `1..1,000,000`)
   - `protectLivingFactionLeaders` (`ProtectLivingFactionLeaders`, bool, default: `true`)
   - `includeNonBloodRelations` (`IncludeNonBloodRelations`, bool, default: `true`)
   - `verboseLogging` (`VerboseLogging`, bool, default: `false`)

---

## 4. Architecture and Design

```
+-------------------------------------------------------------------------+
|                        StopRandomRelationMod (Mod)                      |
| - Harmony init ("hiroaki.stoprandomrelation")                           |
| - Scrollable Listing_Standard Settings UI & Reset button                |
+-------------------------------------------------------------------------+
       |                                              |
       v                                              v
+-----------------------------------+  +----------------------------------+
| StopRandomRelationSettings        |  | HarmonyPatches                   |
| - 11 Scribe_Values fields         |  | - Pawn.ExitMap (Postfix)         |
| - volatile SnapshotHolder         |  | - Pawn.Kill (Postfix)            |
| - Atomic GetOptionsSnapshot()     |  | - Corpse.Destroy (Prefix)        |
+-----------------------------------+  | - Corpse.RotStageChanged (Post)  |
       |                               | - WorldPawnGC.GetCritical...     |
       v                               | - WorldPawnGC.Accumulate...      |
+-----------------------------------+  | - WorldPawns.PassToWorld (Post)  |
| RelationCleanupOptions (struct)   |  | - WorldPawns.WorldPawnsTick      |
| WorldPawnSweepScheduler           |  +----------------------------------+
+-----------------------------------+                 |
                                                      v
                                       +----------------------------------+
                                       | RelationshipManager              |
                                       | - RimWorldRelationAccessor       |
                                       | - RimWorldWorldPawnPruneAccessor |
                                       | - HandlePawnExitMap / Killed /   |
                                       |   PermanentDeath                 |
                                       | - CleanAllOffMapWorldPawnRel...  |
                                       | - PruneExcessUnprotectedOffMap.. |
                                       | - ShouldProtectFromGC            |
                                       +----------------------------------+
                                          |             |             |
                      +-------------------+             |             +------------------+
                      v                                 v                                v
+-----------------------------------+  +----------------------------------+  +---------------------------+
| PawnStateEvaluator                |  | RelationCleanupEngine            |  | RelationCleanupPolicy     |
| - IsColonist / IsPlayerFaction    |  | - SeverRelations<...>            |  | - IsTargetRelation        |
| - IsColonistMap / IsOnColonistMap |  | - CleanForTrigger<...>           |  | - ShouldSeverRelation     |
| - IsResurrectable / Unresurrect.. |  | - CleanOffMapWorldPawns<...>     |  | - ShouldProtectFromGC     |
| - IsActiveFactionLeader           |  | - PruneUnprotectedOffMapWorld... |  | - ShouldDeleteWorldPawn.. |
| - GetRelationCategory / RoleInfo  |  | - ThreadLocalListCache &         |  +---------------------------+
| - IsOnAnyMap / HasAnyProtected... |  |   canonical lock ordering        |
| - IsProtectedByWorldState         |  +----------------------------------+
| - GetWorldPawnPruneInfo           |
+-----------------------------------+
```

### 4.1 Component Responsibilities
1. **`StopRandomRelationMod` (`StopRandomRelationMod.cs`)**:
   - `Mod` entry point (`Instance`), loads `StopRandomRelationSettings`, applies all Harmony patches via `Harmony("hiroaki.stoprandomrelation").PatchAll(...)`, exposes `GetOptions()`, and renders the scrollable settings window.
2. **`StopRandomRelationSettings` (`StopRandomRelationSettings.cs`)**:
   - `ModSettings` persistence via `Scribe_Values.Look`, maintains a `volatile SnapshotHolder` under `_writeLock` (`RefreshSnapshot()`, `ResetToDefaults()`, `GetOptionsSnapshot()`), and marks `WorldPawnSweepScheduler.Shared` dirty when snapshots change.
3. **`RelationCleanupOptions` (`RelationCleanupOptions.cs`)**:
   - Immutable `readonly record struct` holding configuration values, constants (`MinWorldPawnThreshold = 1`, `DefaultWorldPawnThreshold = 100`, `MaxWorldPawnThreshold = 1_000_000`, `DefaultOffMapSweepIntervalTicks = 2500`, `MaxOffMapSweepIntervalTicks = 1_000_000`), `Default`, and range-clamping helpers.
4. **`RelationDomainTypes` (`RelationDomainTypes.cs`)**:
   - Pure domain types: `RelationTrigger`, `RelationCategory`, `PawnRoleInfo`, `WorldPawnPruneInfo`, `DirectRelationEntry<TPawn, TRelationDef>`, `IRelationAccessor<TPawn, TRelationDef>`, and `IWorldPawnPruneAccessor<TPawn>`.
5. **`PawnStateEvaluator` (`PawnStateEvaluator.cs`)**:
   - Pure predicates inspecting RimWorld `Pawn`, `Map`, `Corpse`, `PawnRelationDef`, and `WorldPawns` state, projecting `PawnRoleInfo` and `WorldPawnPruneInfo`.
6. **`RelationCleanupPolicy` (`RelationCleanupPolicy.cs`)**:
   - Pure $\mathcal{O}(1)$ domain rules operating strictly on value snapshots (`PawnRoleInfo`, `WorldPawnPruneInfo`, `RelationCleanupOptions`).
7. **`RelationCleanupEngine` (`RelationCleanupEngine.cs`)**:
   - Generic, zero-reflection, thread-safe pruning engine using struct adapters (`IRelationAccessor`, `IWorldPawnPruneAccessor`), `ThreadLocalListCache<T>`, and deadlock-free canonical lock ordering.
8. **`RelationshipManager` (`RelationshipManager.cs`)**:
   - RimWorld-specific facade providing `RimWorldRelationAccessor` and `RimWorldWorldPawnPruneAccessor`, `WorldPawnMaintenanceLock`, event handlers (`HandlePawnExitMap`, `HandlePawnKilled`, `HandlePermanentDeath`), sweeps (`CleanAllOffMapWorldPawnRelations`, `PruneExcessUnprotectedOffMapWorldPawns`), `SeverRelations`, and `ShouldProtectFromGC`.
9. **`WorldPawnSweepScheduler` (`WorldPawnSweepScheduler.cs`)**:
   - Thread-safe coordinator using `Interlocked` / `Volatile` (`Shared`, `MarkDirty`, `Reset`, `ShouldRunSweep`) for periodic and immediate-mode off-map sweeps.
10. **`HarmonyPatches` (`HarmonyPatches.cs`)**:
    - Internal static container for all 8 Harmony patch classes.

---

## 5. Harmony Patch Points and Triggers

| Target Class & Method | Patch Type | Patch Class | Purpose |
| :--- | :--- | :--- | :--- |
| `Verse.Pawn.ExitMap(bool, Rot4)` | `Postfix` | `Pawn_ExitMap_Patch` | When playing and `__instance != null`, calls `RelationshipManager.HandlePawnExitMap` and `WorldPawnSweepScheduler.Shared.MarkDirty()`. |
| `Verse.Pawn.Kill(DamageInfo?, Hediff)` | `Postfix` | `Pawn_Kill_Patch` | When playing and `__instance != null && __instance.Dead`, calls `RelationshipManager.HandlePawnKilled` and `WorldPawnSweepScheduler.Shared.MarkDirty()`. |
| `Verse.Corpse.Destroy(DestroyMode)` | `Prefix` | `Corpse_Destroy_Patch` | When playing and inner pawn is dead without death refusal/resurrection, calls `RelationshipManager.HandlePermanentDeath` (without requiring prior desiccation check) and `MarkDirty()`. |
| `Verse.Corpse.RotStageChanged()` | `Postfix` | `Corpse_RotStageChanged_Patch` | When playing and inner pawn is dead without death refusal/resurrection and `!IsResurrectable(innerPawn)`, calls `RelationshipManager.HandlePermanentDeath` and `MarkDirty()`. |
| `RimWorld.Planet.WorldPawnGC.GetCriticalPawnReason(Pawn)` | `Postfix` | `WorldPawnGC_GetCriticalPawnReason_Patch` | If `pawn != null`, `ShouldProtectFromGC(pawn, options)` is `true`, and `string.IsNullOrEmpty(__result)`, sets `__result = "FactionLeader"` (with optional verbose log). |
| `RimWorld.Planet.WorldPawnGC.AccumulatePawnGCData(Dictionary<Pawn, string>)` | `Prefix` | `WorldPawnGC_AccumulatePawnGCData_Patch` | When playing, runs `RelationshipManager.CleanAllOffMapWorldPawnRelations` before RimWorld accumulates GC retention reasons. |
| `RimWorld.Planet.WorldPawns.PassToWorld(Pawn, PawnDiscardDecideMode)` | `Postfix` | `WorldPawns_PassToWorld_Patch` | When playing, calls `WorldPawnSweepScheduler.Shared.MarkDirty()`. |
| `RimWorld.Planet.WorldPawns.WorldPawnsTick()` | `Postfix` | `WorldPawns_WorldPawnsTick_Patch` | When playing and `WorldPawnSweepScheduler.Shared.ShouldRunSweep` returns `true`, runs `CleanAllOffMapWorldPawnRelations` and `PruneExcessUnprotectedOffMapWorldPawns`. |

---

## 6. Algorithmic Complexity and Performance

### 6.1 Time Complexity
- **Map Exit / Death / Permanent Death Hooks (`CleanForTrigger`)**:
  - $\mathcal{O}(1)$ fast-skip when `ModEnabled` is `false` or the pawn has no direct relations (`HasAnyDirectRelations`).
  - Otherwise $\mathcal{O}(R)$, where $R$ is the number of direct relations on the triggering pawn.
- **Off-Map Relation Sweep (`CleanAllOffMapWorldPawnRelations` / `CleanOffMapWorldPawns`)**:
  - Scans `WorldPawns.AllPawnsAliveOrDead` ($\mathcal{O}(N)$, where $N$ is total world pawns) using $\mathcal{O}(1)$ `HasAnyDirectRelations` checks to collect only the $K$ pawns that actually have direct relations.
  - Evaluates each candidate's $R$ relations: $\mathcal{O}(N + K \cdot R)$ total time.
- **Threshold Pruning (`PruneExcessUnprotectedOffMapWorldPawns` / `PruneUnprotectedOffMapWorldPawns`)**:
  - $\mathcal{O}(1)$ when disabled or when `worldPawns.Count < threshold`.
  - Otherwise $\mathcal{O}(N)$ worst-case, short-circuiting per pawn from cheapest $\mathcal{O}(1)$ checks (`Discarded`, `IsOnAnyMap`, `IsPlayerFaction`, `IsActiveFactionLeader`, `HasAnyProtectedRelation`, `WorldPawns` hash/dictionary lookups) before world-state queries, and early-exiting the scan loop if remaining pawns cannot reach `threshold` (`candidates.Count + (totalCount - i) < threshold`).
- **GC Critical Reason (`WorldPawnGC_GetCriticalPawnReason_Patch`)**:
  - $\mathcal{O}(1)$ time.

### 6.2 Space Complexity
- **Event Hooks (`CleanForTrigger`, `SeverRelations`)**: Amortized $\mathcal{O}(1)$ auxiliary space via `[ThreadStatic]` rent/return buffer reuse (`ThreadLocalListCache<T>`).
- **Off-Map Sweep**: $\mathcal{O}(K)$ temporary `candidates` list for world pawns with direct relations, plus amortized $\mathcal{O}(R)$ thread-local snapshot buffer.
- **Threshold Pruning**: Amortized $\mathcal{O}(C)$ thread-local candidate buffer ($C \le N$) via `ThreadLocalListCache<TPawn>`.
- No persistent secondary tables or caches are stored in save data.

---

## 7. Thread Safety and Concurrency

1. **Lock-Free Settings Snapshot (`StopRandomRelationSettings`)**:
   - `GetOptionsSnapshot()` reads a `volatile SnapshotHolder` wrapping the immutable `RelationCleanupOptions` record struct without locking. Writes serialize under `_writeLock`.
2. **Thread-Local Snapshot Buffers (`RelationCleanupEngine.ThreadLocalListCache<T>`)**:
   - Each call rents and returns a `[ThreadStatic]` buffer (cleared on rent/return) so concurrent threads or re-entrant calls never share mutable snapshot lists and avoid per-call allocations.
3. **Deadlock-Free Dual-Pawn Locking (`RelationCleanupEngine.SeverPairAtomically`)**:
   - Acquires `pawn.relations` sync roots in canonical order using `RuntimeHelpers.GetHashCode(lockA)` vs `RuntimeHelpers.GetHashCode(lockB)`, falling back to a static `TieBreakerLock` on rare identity-hash collisions, then removes both sides via `Pawn_RelationsTracker.TryRemoveDirectRelation`.
4. **World Pawn Maintenance Serialization (`RelationshipManager.WorldPawnMaintenanceLock`)**:
   - Serializes `CleanAllOffMapWorldPawnRelations` and `PruneExcessUnprotectedOffMapWorldPawns` against `WorldPawns.AllPawnsAliveOrDead`.
5. **Atomic Sweep Scheduling (`WorldPawnSweepScheduler`)**:
   - Uses `Interlocked.Exchange`, `Interlocked.CompareExchange`, and `Volatile.Read` on `_dirty` and `_lastClaimedTick` so concurrent calls for the same tick cannot duplicate a sweep or lose dirty notifications.

---

## 8. Mod Settings Specification and Layout

### 8.1 Settings Window Wireframe (`StopRandomRelationMod.DoSettingsWindowContents`)

Rendered inside `Widgets.BeginScrollView` using `Listing_Standard`:

```
+-----------------------------------------------------------------------+
| [X] Enable Stop Random Relation                                       |
| --------------------------------------------------------------------- |
| Colonist Relations                                                    |
| [X] Clean relations when colonist corpse cannot be resurrected        |
|                                                                       |
| Visitor & Raider Relations                                            |
| [X] Clean colonist-to-world-pawn relations on map departure or death  |
|                                                                       |
| World Pawn Relations                                                  |
| [X] Clean world-pawn-to-world-pawn relations on map departure/death   |
| [X] Sever off-map world pawn relations                                |
|     Off-map sweep interval (ticks, 0 = immediate)       [      2500 ] |
| [ ] Delete unprotected off-map world pawns at threshold               |
|     Unprotected off-map world pawn threshold            [       100 ] |
|                                                                       |
| Protection Rules                                                      |
| [X] Protect living faction leaders from garbage collection            |
| [X] Include marriage / spouse relations in cleanup                    |
|                                                                       |
| Debugging                                                             |
| [ ] Verbose logging                                                   |
| --------------------------------------------------------------------- |
| [ Reset to defaults ]                                                 |
+-----------------------------------------------------------------------+
```

---

## 9. Implementation Reference & Doctest Example

The core RimWorld facade in `RelationshipManager` delegates to the pure generic `RelationCleanupEngine` and `RelationCleanupPolicy`:

```csharp
using RimWorld;
using Verse;

namespace Stop_Random_Relation;

// Example matching RelationshipManager.SeverRelations XML doc-comment:
int severed = RelationshipManager.SeverRelations(
  leavingRaider,
  relDef => relDef.familyByBloodRelation);

// Pure policy evaluation (no RimWorld runtime required):
var colonist = new PawnRoleInfo(true, true, false, false, true, false);
var raider = new PawnRoleInfo(false, false, false, false, false, false);
bool sever = RelationCleanupPolicy.ShouldSeverRelation(
  raider,
  colonist,
  RelationCategory.Blood,
  RelationTrigger.MapExit,
  RelationCleanupOptions.Default); // true

// Pure threshold deletion evaluation:
var info = new WorldPawnPruneInfo(
  raider,
  IsOnAnyMap: false,
  IsProtectedByGameState: false,
  HasAnyRelation: false);
var opts = RelationCleanupOptions.Default with { DeleteUnprotectedOffMapWorldPawns = true };
bool delete = RelationCleanupPolicy.ShouldDeleteWorldPawnOnThreshold(info, opts); // true
```

---

## 10. Edge Cases and Mitigations

| Edge Case | Potential Issue | Mitigation Strategy |
| :--- | :--- | :--- |
| **Caravans & World Travel** | Colonists and colony animals/mechs forming a caravan exit the map and enter `WorldPawns`. | `RelationCleanupPolicy` checks `IsColonist \|\| IsPlayerFaction`; player-faction pawns never sever relations on `MapExit`, and `ShouldDeleteWorldPawnOnThreshold` / `CleanOffMapWorldPawns` skip them (except dead unresurrectable colonists). |
| **Temporary Encounter Maps** | Quest maps, ambushes, or ancient complexes where colonists are present. | `IsColonistMap` evaluates `map.IsPlayerHome \|\| map.mapPawns.FreeColonistsCount > 0`, and `IsOnColonistMap` checks `pawn.Map ?? pawn.MapHeld`. |
| **Resurrection / Death Refusal / Anomaly** | A colonist dies while resurrection serum, death refusal, or active resurrection is available. | `IsResurrectable` returns `true` for `HasDeathRefusalOrResurrecting` or non-destroyed, non-discarded, non-desiccated corpses; colonist-to-colonist relations wait for permanent death (`!IsResurrectable`). |
| **Prisoners & Colony Slaves** | A relative raider is captured or enslaved rather than exiting. | While on a colonist map, `IsOnColonistMap` preserves relations. If ever off-map, `IsProtectedByWorldState` checks `IsPrisonerOfColony` and `IsSlaveOfColony` so they are never deleted at threshold. |
| **Colony Animals & Mechanoids** | Animals bonded to colonists or mechanoids linked to mechanitors. | `IsPlayerFaction` protects them from relation severing and threshold pruning, and `GetRelationCategory` classifies `Bond` / overseer links as `RelationCategory.None`, which are never severed. |
| **Circular / Triadic Relationships** | Triadic ties (e.g., Colonist A <-> Colonist B <-> Raider C). | Relations are pruned per direct relation pair atomically on both endpoints (`SeverPairAtomically`), preserving remaining colonist ties. |
| **Gene / Biotech Children** | `ParentBirth` and standard parent/child ties. | `GetRelationCategory` explicitly includes `def == PawnRelationDefOf.ParentBirth` alongside `def.familyByBloodRelation` in `RelationCategory.Blood`. |
| **Essential Off-Map World Pawns** | Quest pawns, kidnapped pawns, pawns in transport pods, forcefully kept pawns, or pawns with remaining ties. | `ShouldDeleteWorldPawnOnThreshold` excludes any pawn with `HasAnyProtectedRelation`, `IsProtectedByWorldState`, `IsOnAnyMap`, or active faction leader protection. |

---

## 11. Verification and Automated Tests

### 11.1 Automated Unit Tests (`Stop Random Relation.Tests`)
Implemented with xUnit (`net10.0`) using zero-reflection struct adapter test doubles (`IRelationAccessor<TPawn, TRelationDef>`, `IWorldPawnPruneAccessor<TPawn>`):
- **`RelationCleanupPolicyTests.cs`**:
  - `IsTargetRelation` filtering for `Blood`, `MarriageOrRomance`, and `None`.
  - Master switch (`ModEnabled = false`) bypass.
  - Colonist-to-colonist preservation while resurrectable and severing on permanent death.
  - Colonist-to-world-pawn severing on exit/death (including immediate severing on colonist death) and preservation for caravan colonists and player animals.
  - World-to-world severing on exit/death and off-map sweeps (including pre-existing dead unresurrectable colonists).
  - Living vs. dead faction leader GC protection.
  - Threshold deletion (`ShouldDeleteWorldPawnOnThreshold`) protections for map presence, colonists, player faction, leaders, game state, and remaining relations.
- **`RelationCleanupEngineTests.cs`**:
  - `SeverRelations` directed + reciprocal cleanup and preservation of unmatched categories.
  - Triadic relation preservation on remaining colonists.
  - `IncludeNonBloodRelations` toggle behavior.
  - `CleanOffMapWorldPawns` off-map vs. on-map/colonist ties and dead unresurrectable colonist cleanup.
  - $O(1)$ fast-skip on empty relation lists without snapshot/role evaluation.
  - Concurrent opposing-pair lock ordering stress test (`CleanForTrigger_ConcurrentOpposingPairs_IsThreadSafeAndDeadlockFree`).
  - `PruneUnprotectedOffMapWorldPawns` threshold, early-exit, failed-discard notification suppression, and concurrent thread-safety tests.
- **`RelationCleanupOptionsTests.cs`**:
  - Default option verification and range clamping for `ClampWorldPawnThreshold` and `ClampSweepIntervalTicks`.
- **`WorldPawnSweepSchedulerTests.cs`**:
  - Periodic interval claiming, immediate-mode dirty + 60-tick fallback behavior, `ModEnabled = false`, and concurrent same-tick claim exclusivity.

### 11.2 Manual In-Game QA Matrix

| Test ID | Action / Scenario | Expected Result | Pass/Fail Criteria |
| :--- | :--- | :--- | :--- |
| **TC-01** | Spawn raider who is a brother of Colonist A. Defeat raid; raider exits map edge. | Raider exits map -> relationship severed -> raider passes to world pawns -> discarded on next GC tick (or threshold prune if enabled). | Colonist A social tab no longer lists raider brother; save file XML contains no bloated pawn entry. |
| **TC-02** | Colonist B dies. Corpse is placed in freezer. | Relationship to Colonist A is preserved while corpse is fresh. | Social tab shows Colonist B as deceased relative. |
| **TC-03** | Colonist B corpse is cremated in electric crematorium. | Relationship is severed upon corpse destruction (`Corpse.Destroy` prefix). | Colonist B removed from living colonist relations; eligible for GC. |
| **TC-04** | Two world pawn visitors arrive who are married/siblings. One or both leave map alive. | As soon as either exits map, relationship between them is severed. | Exiting pawn becomes eligible for GC. |
| **TC-05** | Faction leader of friendly faction visits and leaves. | Relations (if any) severed, but living leader pawn is **never** discarded by GC (`"FactionLeader"` critical reason). | Faction leader remains in world pawns and accessible in Factions dialog. |
| **TC-06** | Enable threshold deletion (`threshold = 100`) and accumulate unprotected off-map world pawns. | Eligible off-map pawns are deleted via `RemoveAndDiscardPawnViaGC` and notification message is shown. | Message displays deleted count; colonists, leaders, quest pawns, and related pawns remain untouched. |
| **TC-07** | Change settings in Options -> Mod Settings during an active game. | `RefreshSnapshot()` updates options immediately and marks scheduler dirty without reloading. | Next event or tick adheres to new settings. |
