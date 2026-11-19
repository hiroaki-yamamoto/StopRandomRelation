# Stop Random Relation

> [!WARNING]
> **AI-Generated Code Notice**
> The source code for this mod was written using AI. While care has been taken to ensure proper behavior, unexpected bugs or conflicts with other mods may occur. Use at your own risk, and consider backing up your save files before installing or updating.

## Overview

You crash-landed on a remote, lawless RimWorld light-years away from civilization—yet somehow the pirate raiding your potato farm, the passing bulk goods trader, and the drop-pod raider who literally just fell through your roof all turn out to be your colonist's long-lost mother, ex-spouse's cousin, or estranged uncle. It is as if the entire galaxy once attended the same awkward family reunion and never bothered to leave.

While this surreal cosmic genealogy adds unintentional comedy to RimWorld's storytelling, it also prevents world pawns with lingering family ties from being garbage-collected (`WorldPawnGC`), bloating your save files and memory over time.

**Stop Random Relation** dynamically prunes unneeded pawn relationships so world pawns can be garbage-collected without losing "uninvited family relations" storytelling while pawns are active on your maps.

## Features

- **Colonist Relations**: Preserves blood and spouse relations between living or resurrectable colonists, and severs them once a deceased colonist's corpse is desiccated, butchered, cremated, or permanently destroyed.
- **Visitor & Raider Relations**: Preserves colonist-to-world-pawn relations while the relative is alive on a colonist map, and severs them immediately upon map departure or death (or when the colonist dies).
- **World Pawn Relations**: Preserves world-pawn-to-world-pawn relations while both are alive on a colonist map, and severs relations when either leaves/dies or when neither is on a colonist map.
- **Faction Leader Protection**: Protects living faction leaders from `WorldPawnGC` even when they have no remaining family ties.
- **Marriage & Romance Cleanup**: Optionally applies cleanup rules to spouse, fiance, lover, ex-spouse, and ex-lover relations in addition to blood relations (animal bonds and mechanitor overseer links are never removed).
- **Immediate Mod Settings**: All rules are configurable in Mod Settings and take effect immediately without restarting.

## Requirements

- **RimWorld**: `1.7`
- **Required Dependency**: **Harmony** (`brrainz.harmony`)

### Load Order

Load `Stop Random Relation` **after** `Harmony`.

## Installation

1. Subscribe to or install **Harmony** and enable it.
2. Place the `Stop Random Relation` folder into your RimWorld `Mods` directory (or subscribe via Steam Workshop).
3. In the in-game Mods menu, place `Stop Random Relation` below `Harmony`, enable it, and restart the game.

## Settings

You can configure all options under **Options > Mod Settings > Stop Random Relation** (changes apply immediately):

- **Enable Stop Random Relation**: Master switch to toggle all relation pruning and `WorldPawnGC` protections.
- **Colonist Relations**
  - **Clean relations when colonist corpse cannot be resurrected**: Severs relations between colonists once a deceased colonist's corpse is desiccated, butchered, cremated, or permanently destroyed.
- **Visitor & Raider Relations**
  - **Clean colonist-to-world-pawn relations on map departure or death**: Maintains relationships while world-pawn relatives are alive on a colonist map, and severs them once they exit the map or die (or when the colonist dies).
- **World Pawn Relations**
  - **Clean world-pawn-to-world-pawn relations on map departure or death**: Maintains relations between world pawns while both are alive on a colonist map, and severs them once either leaves the map or dies.
  - **Sever off-map world pawn relations**: Severs relations between non-colonist world pawns neither of whom is present on a colonist map.
- **Protection Rules**
  - **Protect living faction leaders from garbage collection**: Ensures living faction leaders are never discarded by `WorldPawnGC` even when they have no remaining family ties.
  - **Include marriage / spouse relations in cleanup**: Applies cleanup rules to spouse, fiance, lover, ex-spouse, and ex-lover relations in addition to blood relations.
- **Debugging**
  - **Verbose logging**: Outputs relationship severing and GC protection details to the RimWorld developer log.

## Contribution

Contributions are welcome. However, please contribute **code that fixes the issue (via Pull Request)** rather than submitting bug reports alone.

## License & Credits

- **Author**: Hiroaki Yamamoto and Gemini
- **License**: [MIT License](LICENSE.md)
