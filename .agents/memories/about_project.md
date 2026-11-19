---
id: about_project
title: About Project
---

# About This Project
This library is a mod for *RimWorld* that aims to resolve the following issue.

# The Issue
When blood relations occur between colonists and world pawns, or between two
world pawns, those world pawns are excluded from garbage collection (GC) and
remain in the save data in order to preserve those relationships. However,
since some playstyles enjoy mechanics like "uninvited family relations"
(random raiders or visitors spawning as relatives), this mod resolves
the save-bloat issue while accommodating those playstyles.

# Resolution Approach

## Blood Relations Between Colonists
When blood relations are established between colonists through marriage,
childbirth, etc., those relationships are preserved.
However, if one of the related colonists dies and can no longer be resurrected
(due to being butchered, rotting away, etc.), the relationship is removed and
the deceased colonist becomes eligible for GC.

## Blood Relations Between a Colonist and a World Pawn
In the case of a blood relation between a colonist and a world pawn (hereafter
referred to as the "relative"), the relationship is maintained while the relative
is alive.
However, if either the colonist or the relative dies, or if the relative exits
the map where the colonist is located, the relationship is removed.
If the colonist dies, both become eligible for GC; if the colonist is still
alive, the relative becomes eligible for GC.

## Blood Relations Between World Pawns
When blood relations exist between world pawns, they are handled as follows:

### When the World Pawns Are Present on a Colonist Map
The blood relation is maintained until one of the world pawns exits the
colonist map or dies. Once this condition is no longer met, the relationship is
removed, and the world pawn that exited or died becomes eligible for GC.

### When the World Pawns Are Not Present on a Colonist Map
The blood relation is removed, and the world pawns become eligible for GC.

## When a World Pawn Has No Blood Relations, but Is a Faction Leader
As long as a faction leader is alive, that faction leader will not be eligible for GC.

# Mod Settings Menu
All items described in the resolution approach can be configured via the mod
settings menu, accessible from the Options screen. Changes made to these settings
take effect immediately without requiring a game restart.

The UI of the mod setting should respect Rimworld's vanilla UI.
