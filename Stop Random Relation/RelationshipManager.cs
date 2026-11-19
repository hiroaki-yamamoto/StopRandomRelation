using RimWorld;
using Verse;

namespace Stop_Random_Relation;

/// <summary>
/// Core domain service coordinating relationship queries, thread-safe relation
/// severing, and <c>WorldPawnGC</c> protection checks for RimWorld pawns.
/// </summary>
public static class RelationshipManager
{
  private readonly struct RimWorldRelationAccessor : IRelationAccessor<Pawn, PawnRelationDef>
  {
    public object? GetSyncRoot(Pawn? pawn) => pawn?.relations;

    public PawnRoleInfo GetRoleInfo(Pawn? pawn) => PawnStateEvaluator.GetRoleInfo(pawn);

    public RelationCategory GetRelationCategory(PawnRelationDef? def) =>
      PawnStateEvaluator.GetRelationCategory(def);

    public bool HasAnyDirectRelations(Pawn pawn)
    {
      Pawn_RelationsTracker? relations = pawn.relations;
      if (relations == null)
      {
        return false;
      }

      lock (relations)
      {
        return relations.DirectRelations != null && relations.DirectRelations.Count > 0;
      }
    }

    public void SnapshotDirectRelations(
      Pawn pawn,
      List<DirectRelationEntry<Pawn, PawnRelationDef>> buffer)
    {
      Pawn_RelationsTracker? relations = pawn.relations;
      if (relations == null)
      {
        return;
      }

      lock (relations)
      {
        List<DirectPawnRelation>? directRelations = relations.DirectRelations;
        if (directRelations == null || directRelations.Count == 0)
        {
          return;
        }

        for (int i = 0; i < directRelations.Count; i++)
        {
          DirectPawnRelation rel = directRelations[i];
          if (rel != null && rel.def != null && rel.otherPawn != null)
          {
            buffer.Add(new DirectRelationEntry<Pawn, PawnRelationDef>(rel.def, rel.otherPawn));
          }
        }
      }
    }

    public bool RemoveDirectRelation(Pawn pawn, PawnRelationDef def, Pawn other)
    {
      if (pawn?.relations == null || def == null || other == null)
      {
        return false;
      }

      return pawn.relations.TryRemoveDirectRelation(def, other);
    }

    public string DescribePawn(Pawn? pawn)
    {
      return pawn == null ? "<null>" : $"{pawn.LabelShort} ({pawn.ThingID})";
    }

    public string DescribeRelation(PawnRelationDef? def)
    {
      return def == null ? "<null>" : def.defName;
    }
  }

  private static readonly Action<string> VerboseLogger =
    msg => Log.Message($"[Stop Random Relation] {msg}");

  /// <summary>
  /// Severs all direct relations on <paramref name="pawn"/> matching
  /// <paramref name="predicate"/>, clearing reciprocal references atomically.
  /// </summary>
  /// <param name="pawn">The target pawn whose relations are evaluated.</param>
  /// <param name="predicate">Filter specifying which relation definitions qualify for removal.</param>
  /// <returns>The number of relationships severed.</returns>
  /// <example>
  /// <code>
  /// int severed = RelationshipManager.SeverRelations(
  ///   leavingRaider,
  ///   relDef => relDef.familyByBloodRelation);
  /// </code>
  /// </example>
  public static int SeverRelations(Pawn? pawn, Func<PawnRelationDef, bool> predicate)
  {
    if (pawn == null || predicate == null)
    {
      return 0;
    }

    return RelationCleanupEngine.SeverRelations<Pawn, PawnRelationDef, RimWorldRelationAccessor>(
      default,
      pawn,
      predicate);
  }

  /// <summary>
  /// Determines whether a world pawn should be strictly protected from
  /// <c>WorldPawnGC</c>.
  /// </summary>
  /// <param name="pawn">The world pawn evaluated by GC.</param>
  /// <param name="protectFactionLeaders">Whether living faction leaders are protected.</param>
  /// <returns><c>true</c> if GC must retain this pawn; otherwise <c>false</c>.</returns>
  public static bool ShouldProtectFromGC(Pawn? pawn, bool protectFactionLeaders)
  {
    return protectFactionLeaders && PawnStateEvaluator.IsActiveFactionLeader(pawn);
  }

  /// <summary>
  /// Determines whether a world pawn should be strictly protected from
  /// <c>WorldPawnGC</c> under the provided <paramref name="options"/>.
  /// </summary>
  /// <param name="pawn">The world pawn evaluated by GC.</param>
  /// <param name="options">Current mod configuration snapshot.</param>
  /// <returns><c>true</c> if GC must retain this pawn; otherwise <c>false</c>.</returns>
  public static bool ShouldProtectFromGC(Pawn? pawn, in RelationCleanupOptions options)
  {
    PawnRoleInfo role = PawnStateEvaluator.GetRoleInfo(pawn);
    return RelationCleanupPolicy.ShouldProtectFromGC(role, options);
  }

  /// <summary>
  /// Handles relationship cleanup when a pawn exits a map via <c>Pawn.ExitMap</c>.
  /// </summary>
  /// <param name="pawn">The pawn that exited the map.</param>
  /// <param name="options">Current mod configuration snapshot.</param>
  /// <returns>The number of relationships severed.</returns>
  public static int HandlePawnExitMap(Pawn? pawn, in RelationCleanupOptions options)
  {
    return RelationCleanupEngine.CleanForTrigger<Pawn, PawnRelationDef, RimWorldRelationAccessor>(
      default,
      pawn,
      RelationTrigger.MapExit,
      options,
      options.VerboseLogging ? VerboseLogger : null);
  }

  /// <summary>
  /// Handles relationship cleanup when a pawn dies via <c>Pawn.Kill</c>.
  /// </summary>
  /// <param name="pawn">The pawn that died.</param>
  /// <param name="options">Current mod configuration snapshot.</param>
  /// <returns>The number of relationships severed.</returns>
  public static int HandlePawnKilled(Pawn? pawn, in RelationCleanupOptions options)
  {
    return RelationCleanupEngine.CleanForTrigger<Pawn, PawnRelationDef, RimWorldRelationAccessor>(
      default,
      pawn,
      RelationTrigger.Death,
      options,
      options.VerboseLogging ? VerboseLogger : null);
  }

  /// <summary>
  /// Handles relationship cleanup when a deceased pawn's corpse becomes
  /// permanently unresurrectable (destroyed, cremated, butchered, or desiccated).
  /// </summary>
  /// <param name="pawn">The deceased pawn whose corpse is no longer resurrectable.</param>
  /// <param name="options">Current mod configuration snapshot.</param>
  /// <returns>The number of relationships severed.</returns>
  public static int HandlePermanentDeath(Pawn? pawn, in RelationCleanupOptions options)
  {
    return RelationCleanupEngine.CleanForTrigger<Pawn, PawnRelationDef, RimWorldRelationAccessor>(
      default,
      pawn,
      RelationTrigger.PermanentDeath,
      options,
      options.VerboseLogging ? VerboseLogger : null);
  }

  /// <summary>
  /// Scans current world pawns and severs relations between non-colonist
  /// world pawns neither of whom is present on a colonist map.
  /// </summary>
  /// <param name="options">Current mod configuration snapshot.</param>
  /// <returns>Total number of relationships severed across the sweep.</returns>
  public static int CleanAllOffMapWorldPawnRelations(in RelationCleanupOptions options)
  {
    if (!options.ModEnabled || !options.CleanOffMapWorldPawnRelations)
    {
      return 0;
    }

    var worldPawns = Find.WorldPawns?.AllPawnsAliveOrDead;
    if (worldPawns == null || worldPawns.Count == 0)
    {
      return 0;
    }

    // Copy only world pawns that have direct relations so empty pawns are skipped in O(1).
    List<Pawn> candidates = new();
    for (int i = 0; i < worldPawns.Count; i++)
    {
      Pawn? pawn = worldPawns[i];
      if (pawn != null && default(RimWorldRelationAccessor).HasAnyDirectRelations(pawn))
      {
        candidates.Add(pawn);
      }
    }

    if (candidates.Count == 0)
    {
      return 0;
    }

    return RelationCleanupEngine.CleanOffMapWorldPawns<Pawn, PawnRelationDef, RimWorldRelationAccessor>(
      default,
      candidates,
      options,
      options.VerboseLogging ? VerboseLogger : null);
  }
}
