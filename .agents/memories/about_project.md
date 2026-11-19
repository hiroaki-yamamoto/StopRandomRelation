---
id: about_project
title: About Project
---

# About This Project
This library is a mod for *RimWorld* (`1.7`) that aims to resolve the following issue.

# The Issue
When blood or marriage/romance relations occur between colonists and world pawns, or between two
world pawns, those world pawns are excluded from garbage collection (`WorldPawnGC`) and
remain in the save data in order to preserve those relationships. However,
since some playstyles enjoy mechanics like "uninvited family relations"
(random raiders or visitors spawning as relatives), this mod resolves
the save-bloat issue while accommodating those playstyles.

# Resolution Approach

## Targeted Relations
The mod targets biological blood relations (`familyByBloodRelation` and `ParentBirth`)
and, when enabled (default: on), marriage and romance relations (`Spouse`, `Fiance`,
`Lover`, `ExSpouse`, `ExLover`). Other relation types such as animal bonds (`Bond`)
and mechanitor overseer links are never removed. Player-faction non-colonist pawns
(such as colony animals and player-controlled mechanoids) are never subjected to
relation pruning.

## Relations Between Colonists
When blood or marriage/romance relations are established between colonists through
childbirth, marriage, etc., those relationships are preserved while both colonists
are alive or while a deceased colonist can still be resurrected (including death
refusal, active resurrection, or an intact, non-desiccated corpse).
However, if one of the related colonists dies and can no longer be resurrected
(at death without a resurrectable corpse, upon corpse destruction such as butchering
or cremation, upon corpse desiccation, or during an off-map sweep for pre-existing
unresurrectable dead colonists in `WorldPawns`), the relationship is removed and
the deceased colonist becomes eligible for GC once in the world pool.

## Relations Between a Colonist and a World Pawn
In the case of a targeted relation between a colonist and a world pawn (hereafter
referred to as the "relative"), the relationship is maintained while both are alive
and the relative has not exited a map.
However, the relationship is removed when any of the following occur:
- The relative exits a map (colonists and player-faction pawns leaving a map in a
  caravan are exempt).
- The relative dies.
- The colonist dies (colonist-to-world-pawn relations are severed immediately on
  death, even if the colonist's corpse is still resurrectable).
- An off-map sweep encounters a pre-existing dead, unresurrectable colonist in
  `WorldPawns`.

Once severed, the relative becomes eligible for GC (when off-map and not otherwise
protected). If the colonist died, the colonist becomes eligible for GC once
unresurrectable and moved to the world pool.

## Relations Between World Pawns
When targeted relations exist between non-player world pawns, they are handled as follows:

### When World Pawns Are on a Colonist Map
The relation is maintained while both world pawns remain alive and neither exits a
map. As soon as one of the world pawns exits a map or dies (including permanent
corpse destruction/desiccation), the relationship is removed, and the pawn that
exited or died becomes eligible for GC.

### When World Pawns Are Not on a Colonist Map
During off-map sweeps (before `WorldPawnGC` data accumulation and during
`WorldPawnsTick`), relations between world pawns neither of whom is on a colonist
map are removed, making those world pawns eligible for GC.

## When a World Pawn Has No Relations, but Is a Faction Leader
As long as a faction leader is alive, that faction leader is protected from
`WorldPawnGC` (and from optional threshold deletion) even when they have no
remaining family or marriage ties.

## Optional Threshold Deletion of Unprotected Off-Map World Pawns
When enabled (default: off), periodic or immediate off-map sweeps count world pawns
that are off all maps, are not player-faction pawns or protected living faction
leaders, have no remaining direct or incoming relations, and are not protected by
essential RimWorld world state (such as quests, caravans, colony captivity,
kidnapping, transport pods, or forceful retention). When the number of eligible
candidates reaches the configured threshold (default: `100`), all eligible
candidates are discarded via `WorldPawnGC` and an in-game message reports how
many pawns were deleted.

# Mod Settings Menu
All behaviors described in the resolution approach—including individual relation
cleanup toggles, off-map sweep interval, threshold deletion and threshold count,
faction leader protection, marriage/romance inclusion, verbose logging, and a reset
button—can be configured via the mod settings menu, accessible from the Options
screen. Changes made to these settings take effect immediately without requiring a
game restart.

The UI of the mod settings respects RimWorld's vanilla UI using a scrollable
`Listing_Standard` layout.
