---
id: spec
title: Detailed Specifications
---

# Detailed Specifications: Stop Random Relation

## 1. Overview and Purpose

### 1.1 Background
In vanilla *RimWorld*, when pawns form blood relationships (either randomly generated during raids/visits or established through gameplay events), those world pawns are permanently protected from the engine's world pawn garbage collection (`WorldPawnGC`). Over extended playthroughs, this mechanism leads to significant "save bloat" and cumulative performance degradation as thousands of deceased or irrelevant world pawns remain serialized in the save file.

While players often enjoy the dynamic storytelling produced by "uninvited family relations" (e.g., a raider or guest turning out to be a colonist's sibling or parent), keeping these world pawns indefinitely once their narrative role is concluded is unnecessary.

### 1.2 Objective
*Stop Random Relation* dynamically prunes relationships and disposes of world pawns when they are no longer relevant to active gameplay. This mod preserves relationships while pawns are actively present in the player's colony or story, and cleanly severs relationships to allow garbage collection once those pawns leave, die without a possibility of resurrection, or exist entirely outside the colony map.

---

## 2. Terminology and Domain Definitions

| Term | Definition | RimWorld Code Equivalent |
| :--- | :--- | :--- |
| **Colonist** | A living or resurrectable pawn belonging to the player's primary faction. | `pawn.Faction != null && pawn.Faction.IsPlayer && pawn.IsFreeColonist` |
| **World Pawn** | A pawn maintained in the world pawn registry (`WorldPawns`) who is not currently spawned on any active map. | `Find.WorldPawns.Contains(pawn)` |
| **Colonist Map** | Any active `Map` where player colonists are currently present (e.g., home colony map, player caravan camp, player quest encounter map). | `map.IsPlayerHome \|\| map.mapPawns.FreeColonistsCount > 0` |
| **Blood Relation** | A direct biological relationship between two pawns. | `DirectPawnRelation` where `def.isBloodRelation == true` (e.g., Parent, Child, Sibling, Half-Sibling, Grandparent, Grandchild, Uncle/Aunt, Nephew/Niece, Cousin). |
| **Spouse Relation** | A formal partner relationship created by marriage. By default, treated in tandem with blood relations or configurable via settings. | `DirectPawnRelation` where `def == PawnRelationDefOf.Spouse` |
| **Resurrectable Pawn** | A deceased pawn whose corpse exists in a state capable of being revived (e.g., not desiccated, not butchered, not cremated, not destroyed). | `pawn.Corpse != null && !pawn.Corpse.Destroyed && ResurrectionUtility.CanResurrect(pawn)` |
| **Unresurrectable Pawn** | A deceased pawn whose corpse is permanently destroyed, cremated, butchered, rotted to desiccation without revival items, or missing. | `pawn.Dead && (pawn.Corpse == null \|\| pawn.Corpse.Destroyed \|\| !ResurrectionUtility.CanResurrect(pawn))` |
| **Faction Leader** | A pawn currently designated as the living head of an existing faction. | `pawn.Faction != null && pawn.Faction.leader == pawn` |
| **Garbage Collection (GC)** | The process by which the engine discards unneeded world pawns from memory and save data. | `Find.WorldPawns.gc.PawnCanBeDiscarded(pawn)` |

---

## 3. Functional Requirements

### 3.1 Blood Relations Between Colonists
1. **Preservation During Life**:
   - Blood relationships and marriage ties between active colonists must be fully preserved while both colonists are alive.
2. **Preservation During Resurrectable Death**:
   - If one of the related colonists dies, the relationship must remain intact as long as the deceased colonist's corpse can potentially be resurrected (`ResurrectionUtility.CanResurrect(pawn)`).
3. **Severing Upon Permanent Death**:
   - When a colonist's corpse is destroyed, cremated, consumed, butchered, or rots to a state where resurrection is impossible, all relationships between that deceased colonist and living colonists must be severed.
4. **GC Eligibility**:
   - Once all ties to living colonists are severed, the deceased colonist becomes eligible for `WorldPawnGC` discarding when moved to the world pool.

### 3.2 Blood Relations Between a Colonist and a World Pawn
1. **Preservation While Present on Map**:
   - When a world pawn who is a relative of a colonist spawns onto a colonist map (e.g., visitor, raid, trader, wanderer, prisoner), the blood relationship is maintained while the relative remains alive and spawned on that map.
2. **Severing Conditions**:
   - The relationship between the colonist and the relative must be immediately severed when **any** of the following events occur:
     1. **Map Exit**: The relative exits the colonist map (`pawn.ExitMap()`) and returns to the world.
     2. **Relative Death**: The relative dies while on or off the colonist map.
     3. **Colonist Death**: The colonist dies (and/or is rendered unresurrectable according to configuration).
3. **GC Eligibility**:
   - When the relative exits or dies, the severed relative immediately becomes eligible for GC discarding by `WorldPawnGC`.
   - If the colonist dies, both the colonist (once unresurrectable) and the relative become eligible for GC.

### 3.3 Blood Relations Between World Pawns
1. **World Pawns Present on a Colonist Map**:
   - If two world pawns sharing a blood relation are both spawned on a colonist map, their relationship is maintained while both pawns remain on that map and alive.
   - As soon as either world pawn exits the colonist map or dies, the relationship between them is severed.
   - The pawn that exited or died immediately becomes eligible for GC.
2. **World Pawns Not Present on a Colonist Map**:
   - Any blood relationship between two world pawns neither of whom is on a colonist map must be severed.
   - Both world pawns immediately become eligible for GC, provided they have no remaining ties to active colonists and are not faction leaders.

### 3.4 Faction Leader Protection
1. **Preservation Rule**:
   - A living world pawn who is the leader of an active faction (`pawn.Faction != null && pawn.Faction.leader == pawn && !pawn.Dead`) must **never** be discarded by GC, even if all their blood relations have been completely removed.
2. **Deceased Leaders**:
   - If a faction leader dies and the faction replaces or discards them, they are subject to standard unresurrectable GC rules.

### 3.5 Mod Settings Menu
1. **Integration**:
   - Accessible via the game's standard Options -> Mod Settings screen (`Mod.DoSettingsWindowContents`).
2. **Vanilla UI Aesthetic**:
   - Must strictly adhere to RimWorld's standard UI conventions using `Listing_Standard`, standard label formatting, tooltips, and default spacing.
3. **Immediate Application**:
   - All setting changes must take effect immediately upon alteration in the menu without requiring a game restart or reloading of the save file.
4. **Configurable Options**:
   - **Master Enable/Disable**: Master toggle to enable or disable mod interception.
   - **Clean Colonist-Colonist Relations on Permanent Death**: Toggle whether unresurrectable colonist corpses have relations severed (Default: `true`).
   - **Clean Colonist-World Pawn Relations on Exit/Death**: Toggle whether relations with world pawns are severed when they exit the map or die (Default: `true`).
   - **Clean World Pawn-World Pawn Relations on Exit/Death**: Toggle whether world-to-world pawn relations on colonist maps are severed upon departure/death (Default: `true`).
   - **Clean Off-Map World Pawn Relations**: Toggle whether world pawns outside colonist maps have their relationships severed during GC scans (Default: `true`).
   - **Protect Living Faction Leaders**: Toggle strict protection preventing living faction leaders from being discarded (Default: `true`).
   - **Include Non-Blood Relations (Spouses/In-laws)**: Option to extend cleanup logic to spouses and affinity relations (Default: `true`).
   - **Diagnostic Logging**: Toggle verbose debug logging to the RimWorld developer console (Default: `false`).

---

## 4. Architecture and Design

```
+-------------------------------------------------------------+
|                  Stop Random Relation Mod                   |
+-------------------------------------------------------------+
       |                                              |
       v                                              v
+-----------------------------+        +------------------------------+
| StopRandomRelationSettings  |        |      Harmony Patches         |
| - Configuration values      |        | - Pawn.ExitMap               |
| - Scribe_Values persistence |        | - Pawn.Kill                  |
| - Immediate in-memory sync  |        | - Corpse.Destroy / Rot Mode  |
+-----------------------------+        | - WorldPawnGC.PawnCanBeDisc. |
                                       +------------------------------+
                                                      |
                                                      v
                                       +------------------------------+
                                       |     RelationshipManager      |
                                       | - EvaluateRelations()        |
                                       | - SeverBloodRelations()      |
                                       | - CanPawnBeDiscarded()       |
                                       +------------------------------+
                                                      |
                                                      v
                                       +------------------------------+
                                       |      PawnStateEvaluator      |
                                       | - IsOnColonistMap()          |
                                       | - IsResurrectable()          |
                                       | - IsFactionLeader()          |
                                       +------------------------------+
```

### 4.1 Component Responsibilities (SOLID Principles)

1. **`StopRandomRelationMod` (Entry Point & UI Controller)**
   - Single Responsibility: Manages mod metadata, exposes the settings window via RimWorld's `Mod` lifecycle, and initializes Harmony patches on game launch.
2. **`StopRandomRelationSettings` (Configuration Model)**
   - Single Responsibility: Holds configuration fields, provides serialization via `ExposeData()` using `Scribe_Values`, and ensures thread-safe read access.
3. **`PawnStateEvaluator` (Domain Predicates)**
   - Single Responsibility: Pure domain queries regarding pawn state.
   - Methods:
     - `bool IsOnColonistMap(Pawn pawn)`
     - `bool IsColonist(Pawn pawn)`
     - `bool IsResurrectable(Pawn pawn)`
     - `bool IsActiveFactionLeader(Pawn pawn)`
4. **`RelationshipManager` (Core Domain Service)**
   - Single Responsibility: Performs relationship queries and mutations (severing relations, clearing reciprocal references, validating GC eligibility).
   - Methods:
     - `void CleanColonistToWorldPawnRelations(Pawn pawn, Map map)`
     - `void CleanWorldPawnToWorldPawnRelations(Pawn pawn, Map map)`
     - `void CleanPermanentDeathRelations(Pawn deadPawn)`
     - `void SeverDirectRelations(Pawn pawnA, Pawn pawnB, Func<PawnRelationDef, bool> filter)`
     - `bool ShouldPreventDiscard(Pawn pawn)`

---

## 5. Harmony Patch Points and Triggers

| Target Class & Method | Patch Type | Purpose | Reference Source |
| :--- | :--- | :--- | :--- |
| `Verse.Pawn.ExitMap(bool, Rot4)` | `Postfix` | Triggered when any pawn leaves a map. Severs relations with colonists or other world pawns who remain or exited. | `Verse.Pawn.ExitMap` |
| `Verse.Pawn.Kill(DamageInfo?, Hediff)` | `Postfix` | Triggered when any pawn dies. Evaluates whether the deceased was a relative or colonist, severing invalid relations. | `Verse.Pawn.Kill` |
| `Verse.Corpse.Destroy(DestroyMode)` | `Prefix` | Triggered when a corpse is butchered, cremated, or destroyed. Severs relations for the now unresurrectable colonist. | `Verse.Corpse.Destroy` |
| `Verse.Corpse.RotStageChanged()` | `Postfix` | Triggered when a corpse rot mode changes (e.g. to desiccated). If revival is no longer possible, triggers relation severing. | `Verse.Corpse.RotStageChanged` |
| `RimWorld.Planet.WorldPawnGC.PawnCanBeDiscarded(Pawn)` | `Postfix` | Intercepts GC evaluation. Ensures living faction leaders return `false`, and world pawns without colonist relations return `true`. | `RimWorld.Planet.WorldPawnGC.PawnCanBeDiscarded` |
| `RimWorld.Planet.WorldPawns.WorldPawnsTick()` | `Postfix` (Periodic) | Optional maintenance pass to clean stale world-pawn-to-world-pawn relations for pawns generated off-map. | `RimWorld.Planet.WorldPawns` |

---

## 6. Algorithmic Complexity and Performance

### 6.1 Time Complexity
- **Map Exit / Death Hooks**:
  - Let $R$ be the number of direct relationships associated with the triggering pawn.
  - In vanilla RimWorld, $R$ is typically between $0$ and $15$.
  - Iterating through `pawn.relations.DirectRelations` is $O(R)$.
  - Severing a direct relationship (`RemoveDirectRelation`) involves list removal from both pawns: $O(R)$.
  - Total time complexity per event: $\mathcal{O}(R) \approx \mathcal{O}(1)$ relative to world population.
- **WorldPawnGC Interception**:
  - `PawnCanBeDiscarded` runs during the GC cycle (every 15,000 game ticks by default).
  - Checking faction leader status: $\mathcal{O}(1)$.
  - Checking if any relation belongs to an active colonist: $\mathcal{O}(R)$.
  - Total time complexity: $\mathcal{O}(R \cdot N)$ where $N$ is the count of world pawns evaluated in the batch, adding negligible overhead over vanilla GC.

### 6.2 Space Complexity
- Auxiliary Space: $\mathcal{O}(1)$.
- All operations mutate existing `DirectPawnRelation` lists in-place.
- No secondary global tables or caches are persisted in memory or save data, completely eliminating leak risks.

---

## 7. Thread Safety and Concurrency

1. **Unity Single-Thread Context**:
   - RimWorld core simulation, including pawn ticking, map exit, death, and UI rendering, executes on the main Unity thread.
2. **Mod Compatibility (RimThreaded / Multithreading Mods)**:
   - To guarantee thread safety in multithreaded environments:
     - All relationship modifications must operate on cloned or snapshot lists (e.g., `pawn.relations.DirectRelations.ToList()`) to avoid `InvalidOperationException` (collection modified during iteration).
     - Settings reads are non-blocking atomic field reads.
     - Critical mutation sections in `RelationshipManager` should utilize localized object locks if invoked outside the main thread:
       ```csharp
       lock (pawn.relations) { /* atomic relation severing */ }
       ```

---

## 8. Mod Settings Specification and Layout

### 8.1 Settings Window Wireframe (Vanilla `Listing_Standard`)

```
+-----------------------------------------------------------------------+
| Mod Settings: Stop Random Relation                                    |
+-----------------------------------------------------------------------+
| [X] Enable Stop Random Relation                                       |
|     Master switch to toggle all relation pruning and GC enhancements.  |
|                                                                       |
| Colonist Relations:                                                   |
| [X] Clean relations when colonist corpse cannot be resurrected        |
|     Severs blood relations once a colonist's corpse is desiccated,    |
|     butchered, cremated, or permanently destroyed.                    |
|                                                                       |
| Visitor & Raider Relations:                                           |
| [X] Clean colonist-to-world-pawn relations on map departure or death  |
|     Maintains relationships while relatives are on your map, but      |
|     severs them once they leave or die.                               |
|                                                                       |
| World Pawn Relations:                                                 |
| [X] Clean world-pawn-to-world-pawn relations on map departure/death   |
|     Severs relations between world pawns once either leaves the map.  |
| [X] Sever off-map world pawn relations                                |
|     Immediately severs relations between world pawns never on a map.  |
|                                                                       |
| Protection Rules:                                                     |
| [X] Protect living faction leaders from garbage collection            |
|     Ensures faction leaders are never discarded even without family.  |
| [X] Include marriage / spouse relations in cleanup                    |
|     Applies cleanup rules to non-blood spouse relationships as well.  |
|                                                                       |
| Debugging:                                                            |
| [ ] Verbose logging                                                   |
|     Outputs relationship severing details to the developer console.   |
+-----------------------------------------------------------------------+
```

---

## 9. Implementation Reference & Doctest Example

The following C# code sample illustrates the interface and core logic for relation severing, designed for testability and clean separation of concerns.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Stop_Random_Relation
{
    /// <summary>
    /// Evaluates and manages relationship pruning between pawns to prevent save bloat.
    /// </summary>
    public static class RelationshipManager
    {
        /// <summary>
        /// Severs all blood relations between the specified pawn and external pawns based on mod settings.
        /// </summary>
        /// <param name="pawn">The target pawn whose relations are being evaluated.</param>
        /// <param name="predicate">A filter specifying which relations qualify for removal.</param>
        /// <returns>The number of relationships successfully severed.</returns>
        /// <example>
        /// <code>
        /// int severed = RelationshipManager.SeverRelations(leavingRaider, relDef => relDef.isBloodRelation);
        /// Log.Message($"Severed {severed} relations from departing raider.");
        /// </code>
        /// </example>
        public static int SeverRelations(Pawn pawn, Func<PawnRelationDef, bool> predicate)
        {
            if (pawn?.relations == null) return 0;

            // Snapshot direct relations to prevent collection modification exceptions
            List<DirectPawnRelation> relationsToEvaluate = pawn.relations.DirectRelations.ToList();
            int severedCount = 0;

            foreach (DirectPawnRelation relation in relationsToEvaluate)
            {
                if (predicate(relation.def))
                {
                    Pawn otherPawn = relation.otherPawn;
                    pawn.relations.RemoveDirectRelation(relation);

                    // Ensure reciprocal relation cleanup if still present
                    if (otherPawn?.relations != null && otherPawn.relations.DirectRelationExists(relation.def, pawn))
                    {
                        otherPawn.relations.RemoveDirectRelation(relation.def, pawn);
                    }

                    severedCount++;
                }
            }

            return severedCount;
        }

        /// <summary>
        /// Determines if a world pawn should be protected from WorldPawnGC.
        /// </summary>
        /// <param name="pawn">The world pawn being evaluated by GC.</param>
        /// <param name="protectFactionLeaders">Whether active faction leaders are protected.</param>
        /// <returns>True if GC should skip this pawn; false if eligible for discard.</returns>
        public static bool ShouldProtectFromGC(Pawn pawn, bool protectFactionLeaders)
        {
            if (pawn == null) return false;

            // Faction leader protection
            if (protectFactionLeaders && pawn.Faction != null && pawn.Faction.leader == pawn && !pawn.Dead)
            {
                return true;
            }

            return false;
        }
    }
}
```

---

## 10. Edge Cases and Mitigations

| Edge Case | Potential Issue | Mitigation Strategy |
| :--- | :--- | :--- |
| **Caravans & World Travel** | Colonists forming a caravan are moved to `WorldPawns`. Could be falsely classified as world pawns. | `Pawn.IsColonist` or `pawn.Faction.IsPlayer` must be checked; colonists in caravans are player faction pawns and are never subjected to world pawn severing. |
| **Temporary Encounter Maps** | Quest maps, ambushes, or ancient complexes where colonists are present. | Colonist map definition evaluates `map.mapPawns.FreeColonistsCount > 0`, ensuring temporary quest maps are recognized as colonist maps. |
| **Resurrection Mech Serum / Anomaly** | A colonist dies, but the player has a serum or anomaly revival method available. | Severing only occurs once the corpse is *unresurrectable* (`ResurrectionUtility.CanResurrect` returns false or corpse is destroyed). Fresh or refrigerated corpses retain relationships. |
| **Prisoners & Recruits** | A relative raider is captured rather than allowed to exit. | Captives remain on the colonist map (`pawn.Spawned` on map). Their relationship remains intact while imprisoned. If later released and they exit, relations are then severed. |
| **Circular Relationships** | Triadic relations (e.g. Pawn A sibling of B, B sibling of C). | Relations are pruned individually via `RemoveDirectRelation` on both endpoints without breaking family trees of remaining colonists. |
| **Gene / Biotech Children** | Embryos, vat-grown babies, or xenotype heritage. | Handled via standard RimWorld `DirectRelation` entries (`Parent`, `Child`); biotech genetic parent tags are safely maintained until unresurrectable death. |

---

## 11. Verification and Quality Assurance Plan

### 11.1 Automated Unit Tests
- Create test harnesses mocking `Pawn`, `Pawn_RelationsTracker`, and `Faction`.
- Verify:
  1. `SeverRelations` removes both directed and reciprocal relations.
  2. Living faction leaders consistently return `true` for `ShouldProtectFromGC`.
  3. Colonist corpses marked desiccated or destroyed trigger relationship pruning.
  4. Non-blood relations are respected according to setting toggles.

### 11.2 Manual In-Game QA Matrix

| Test ID | Action / Scenario | Expected Result | Pass/Fail Criteria |
| :--- | :--- | :--- | :--- |
| **TC-01** | Spawn raider who is a brother of Colonist A. Defeat raid; raider flees and exits map edge. | Raider exits map -> relationship severed -> raider passes to world pawns -> discarded on next GC tick. | Colonist A social tab no longer lists raider brother; save file XML contains no bloated pawn entry. |
| **TC-02** | Colonist B dies. Corpse is placed in freezer. | Relationship is preserved while corpse is fresh. | Social tab shows Colonist B as deceased relative. |
| **TC-03** | Colonist B corpse is cremated in electric crematorium. | Relationship is severed upon corpse destruction. | Colonist B removed from living colonist relations; eligible for GC. |
| **TC-04** | Two world pawn visitors arrive who are married/siblings. Both leave map alive. | Once both exit map, relationship between them is severed. | Both pawns become eligible for GC. |
| **TC-05** | Faction leader of friendly Outlander Union visits and leaves. | Relations (if any) severed, but leader pawn is **never** discarded by GC. | Faction leader remains in world pawns and accessible in Factions dialog. |
| **TC-06** | Change settings in Options -> Mod Settings during an active game. | Settings take effect immediately without reloading or restarting. | Next map exit adheres to new toggles. |
