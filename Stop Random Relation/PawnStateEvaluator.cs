using RimWorld;
using Verse;

namespace Stop_Random_Relation;

/// <summary>
/// Pure domain predicates inspecting RimWorld <see cref="Pawn"/>,
/// <see cref="Map"/>, and <see cref="PawnRelationDef"/> state.
/// </summary>
/// <remarks>
/// All predicate evaluations run in <c>O(1)</c> time and <c>O(1)</c> space.
/// </remarks>
public static class PawnStateEvaluator
{
  /// <summary>
  /// Determines whether <paramref name="pawn"/> is a free colonist belonging
  /// to the player's faction.
  /// </summary>
  /// <param name="pawn">The pawn to evaluate.</param>
  /// <returns><c>true</c> if the pawn is a player colonist; otherwise <c>false</c>.</returns>
  public static bool IsColonist(Pawn? pawn)
  {
    return pawn != null
      && pawn.Faction != null
      && pawn.Faction.IsPlayer
      && pawn.IsFreeColonist;
  }

  /// <summary>
  /// Determines whether <paramref name="pawn"/> belongs to the player's faction
  /// (including colonists, colony animals, and player-controlled mechanoids).
  /// </summary>
  /// <param name="pawn">The pawn to evaluate.</param>
  /// <returns><c>true</c> if the pawn's faction is the player faction.</returns>
  public static bool IsPlayerFaction(Pawn? pawn)
  {
    return pawn?.Faction != null && pawn.Faction.IsPlayer;
  }

  /// <summary>
  /// Determines whether <paramref name="map"/> is an active colonist map
  /// (either a player home map or any active map containing free colonists).
  /// </summary>
  /// <param name="map">The map to evaluate.</param>
  /// <returns><c>true</c> if the map is a colonist map; otherwise <c>false</c>.</returns>
  public static bool IsColonistMap(Map? map)
  {
    if (map == null || map.Disposed)
    {
      return false;
    }

    return map.IsPlayerHome || (map.mapPawns != null && map.mapPawns.FreeColonistsCount > 0);
  }

  /// <summary>
  /// Determines whether <paramref name="pawn"/> is currently spawned or held
  /// (e.g., carried or in a bed/container) on an active colonist map.
  /// </summary>
  /// <param name="pawn">The pawn to evaluate.</param>
  /// <returns><c>true</c> if the pawn is present on a colonist map; otherwise <c>false</c>.</returns>
  public static bool IsOnColonistMap(Pawn? pawn)
  {
    if (pawn == null)
    {
      return false;
    }

    Map? map = pawn.Map ?? pawn.MapHeld;
    return IsColonistMap(map);
  }

  /// <summary>
  /// Determines whether a deceased <paramref name="pawn"/> still has a corpse
  /// capable of being resurrected (not destroyed, discarded, or desiccated).
  /// </summary>
  /// <param name="pawn">The deceased pawn to evaluate.</param>
  /// <returns>
  /// <c>true</c> if <paramref name="pawn"/> is dead and still resurrectable;
  /// <c>false</c> if alive or permanently unresurrectable.
  /// </returns>
  public static bool IsResurrectable(Pawn? pawn)
  {
    if (pawn == null || !pawn.Dead)
    {
      return false;
    }

    if (pawn.HasDeathRefusalOrResurrecting)
    {
      return true;
    }

    Corpse? corpse = pawn.Corpse;
    if (corpse == null || corpse.Destroyed || corpse.Discarded)
    {
      return false;
    }

    if (corpse.CurRotDrawMode == RotDrawMode.Dessicated)
    {
      return false;
    }

    CompRottable? rottable = corpse.GetComp<CompRottable>();
    if (rottable != null && rottable.Stage == RotStage.Dessicated)
    {
      return false;
    }

    return true;
  }

  /// <summary>
  /// Determines whether <paramref name="pawn"/> is dead and can no longer be
  /// resurrected.
  /// </summary>
  /// <param name="pawn">The pawn to evaluate.</param>
  /// <returns><c>true</c> if dead and unresurrectable; otherwise <c>false</c>.</returns>
  public static bool IsUnresurrectable(Pawn? pawn)
  {
    return pawn != null && pawn.Dead && !IsResurrectable(pawn);
  }

  /// <summary>
  /// Determines whether <paramref name="pawn"/> is the living leader of an
  /// active faction.
  /// </summary>
  /// <param name="pawn">The pawn to evaluate.</param>
  /// <returns><c>true</c> if the pawn is a living faction leader; otherwise <c>false</c>.</returns>
  public static bool IsActiveFactionLeader(Pawn? pawn)
  {
    return pawn != null
      && !pawn.Dead
      && pawn.Faction != null
      && pawn.Faction.leader == pawn;
  }

  /// <summary>
  /// Classifies a RimWorld <see cref="PawnRelationDef"/> into a
  /// <see cref="RelationCategory"/> for policy evaluation.
  /// </summary>
  /// <param name="def">The relation definition to classify.</param>
  /// <returns>
  /// <see cref="RelationCategory.Blood"/> for biological blood relations,
  /// <see cref="RelationCategory.MarriageOrRomance"/> for spouse/romance
  /// relations, or <see cref="RelationCategory.None"/> otherwise.
  /// </returns>
  public static RelationCategory GetRelationCategory(PawnRelationDef? def)
  {
    if (def == null)
    {
      return RelationCategory.None;
    }

    if (def.familyByBloodRelation || def == PawnRelationDefOf.ParentBirth)
    {
      return RelationCategory.Blood;
    }

    if (def == PawnRelationDefOf.Spouse
      || def == PawnRelationDefOf.Fiance
      || def == PawnRelationDefOf.Lover
      || def == PawnRelationDefOf.ExSpouse
      || def == PawnRelationDefOf.ExLover)
    {
      return RelationCategory.MarriageOrRomance;
    }

    return RelationCategory.None;
  }

  /// <summary>
  /// Projects a live RimWorld <see cref="Pawn"/> into an immutable
  /// <see cref="PawnRoleInfo"/> value for policy evaluation.
  /// </summary>
  /// <param name="pawn">The pawn to inspect.</param>
  /// <returns>An immutable <see cref="PawnRoleInfo"/> snapshot.</returns>
  public static PawnRoleInfo GetRoleInfo(Pawn? pawn)
  {
    if (pawn == null)
    {
      return default;
    }

    return new PawnRoleInfo(
      IsColonist: IsColonist(pawn),
      IsPlayerFaction: IsPlayerFaction(pawn),
      IsDead: pawn.Dead,
      IsResurrectable: IsResurrectable(pawn),
      IsOnColonistMap: IsOnColonistMap(pawn),
      IsActiveFactionLeader: IsActiveFactionLeader(pawn));
  }
}
