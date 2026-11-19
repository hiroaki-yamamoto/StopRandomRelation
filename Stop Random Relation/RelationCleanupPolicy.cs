namespace Stop_Random_Relation;

/// <summary>
/// Pure, thread-safe domain policy determining whether relations should be severed
/// and whether world pawns must be protected from garbage collection.
/// </summary>
/// <remarks>
/// All methods are O(1) time and O(1) space pure functions operating solely on
/// immutable value snapshots (<see cref="PawnRoleInfo"/> and <see cref="RelationCleanupOptions"/>).
/// </remarks>
public static class RelationCleanupPolicy
{
  /// <summary>
  /// Determines whether a relation category is eligible for cleanup under the
  /// provided <paramref name="options"/>.
  /// </summary>
  /// <param name="category">The classified relation category.</param>
  /// <param name="options">Current mod configuration snapshot.</param>
  /// <returns><c>true</c> if the relation category is managed by cleanup rules.</returns>
  public static bool IsTargetRelation(
    RelationCategory category,
    in RelationCleanupOptions options)
  {
    return category switch
    {
      RelationCategory.Blood => true,
      RelationCategory.MarriageOrRomance => options.IncludeNonBloodRelations,
      _ => false,
    };
  }

  /// <summary>
  /// Evaluates whether a direct relation between <paramref name="subject"/> and
  /// <paramref name="other"/> must be severed for the given lifecycle <paramref name="trigger"/>.
  /// </summary>
  /// <param name="subject">The pawn undergoing the lifecycle event or sweep.</param>
  /// <param name="other">The related pawn on the opposite end of the relation.</param>
  /// <param name="category">Classification of the relation definition.</param>
  /// <param name="trigger">Lifecycle event causing evaluation.</param>
  /// <param name="options">Current mod configuration snapshot.</param>
  /// <returns><c>true</c> if the relation should be severed; otherwise <c>false</c>.</returns>
  /// <example>
  /// <code>
  /// var colonist = new PawnRoleInfo(true, true, false, false, true, false);
  /// var raider = new PawnRoleInfo(false, false, false, false, false, false);
  /// bool sever = RelationCleanupPolicy.ShouldSeverRelation(
  ///   raider, colonist, RelationCategory.Blood, RelationTrigger.MapExit,
  ///   RelationCleanupOptions.Default);
  /// // sever == true
  /// </code>
  /// </example>
  public static bool ShouldSeverRelation(
    in PawnRoleInfo subject,
    in PawnRoleInfo other,
    RelationCategory category,
    RelationTrigger trigger,
    in RelationCleanupOptions options)
  {
    if (!options.ModEnabled || !IsTargetRelation(category, options))
    {
      return false;
    }

    return trigger switch
    {
      RelationTrigger.MapExit =>
        !(subject.IsColonist || subject.IsPlayerFaction)
        && ShouldSeverNonPlayerPawnRelation(other, options),
      RelationTrigger.Death =>
        ShouldSeverOnMortality(subject, other, requireUnresurrectable: true, options),
      RelationTrigger.PermanentDeath =>
        ShouldSeverOnMortality(subject, other, requireUnresurrectable: false, options),
      RelationTrigger.OffMapSweep =>
        ShouldSeverOnOffMapSweep(subject, other, options),
      _ => false,
    };
  }

  /// <summary>
  /// Determines whether a world pawn must be strictly protected from
  /// <c>WorldPawnGC</c> discarding.
  /// </summary>
  /// <param name="pawn">Role snapshot of the evaluated world pawn.</param>
  /// <param name="options">Current mod configuration snapshot.</param>
  /// <returns><c>true</c> if GC must retain the pawn; otherwise <c>false</c>.</returns>
  public static bool ShouldProtectFromGC(
    in PawnRoleInfo pawn,
    in RelationCleanupOptions options)
  {
    return options.ModEnabled
      && options.ProtectLivingFactionLeaders
      && pawn.IsActiveFactionLeader;
  }

  /// <summary>
  /// Determines whether a world pawn is an unprotected, off-map deletion
  /// candidate eligible for threshold pruning.
  /// </summary>
  /// <param name="info">Projected world pawn pruning state.</param>
  /// <param name="options">Current mod configuration snapshot.</param>
  /// <returns>
  /// <c>true</c> if the pawn is off-map and not protected by mod rules,
  /// remaining relationships, or essential game state; otherwise <c>false</c>.
  /// </returns>
  /// <example>
  /// <code>
  /// var role = new PawnRoleInfo(false, false, false, false, false, false);
  /// var info = new WorldPawnPruneInfo(role, IsOnAnyMap: false, IsProtectedByGameState: false, HasAnyRelation: false);
  /// var opts = RelationCleanupOptions.Default with { DeleteUnprotectedOffMapWorldPawns = true };
  /// bool delete = RelationCleanupPolicy.ShouldDeleteWorldPawnOnThreshold(info, opts);
  /// // delete == true
  /// </code>
  /// </example>
  public static bool ShouldDeleteWorldPawnOnThreshold(
    in WorldPawnPruneInfo info,
    in RelationCleanupOptions options)
  {
    if (!options.ModEnabled || !options.DeleteUnprotectedOffMapWorldPawns)
    {
      return false;
    }

    if (info.IsOnAnyMap || info.Role.IsOnColonistMap)
    {
      return false;
    }

    if (info.Role.IsColonist || info.Role.IsPlayerFaction)
    {
      return false;
    }

    if (ShouldProtectFromGC(info.Role, options))
    {
      return false;
    }

    if (info.IsProtectedByGameState || info.HasAnyRelation)
    {
      return false;
    }

    return true;
  }

  private static bool ShouldSeverNonPlayerPawnRelation(
    in PawnRoleInfo other,
    in RelationCleanupOptions options)
  {
    if (other.IsColonist)
    {
      return options.CleanColonistToWorldRelationsOnExitOrDeath;
    }

    return !other.IsPlayerFaction && options.CleanWorldToWorldRelationsOnExitOrDeath;
  }

  private static bool ShouldSeverOnMortality(
    in PawnRoleInfo subject,
    in PawnRoleInfo other,
    bool requireUnresurrectable,
    in RelationCleanupOptions options)
  {
    if (subject.IsColonist)
    {
      if (other.IsColonist)
      {
        return options.CleanColonistRelationsOnPermanentDeath
          && (!requireUnresurrectable || !subject.IsResurrectable);
      }

      return !other.IsPlayerFaction && options.CleanColonistToWorldRelationsOnExitOrDeath;
    }

    if (subject.IsPlayerFaction)
    {
      return false;
    }

    return ShouldSeverNonPlayerPawnRelation(other, options);
  }

  private static bool ShouldSeverOnOffMapSweep(
    in PawnRoleInfo subject,
    in PawnRoleInfo other,
    in RelationCleanupOptions options)
  {
    if (!options.CleanOffMapWorldPawnRelations)
    {
      return false;
    }

    if (subject.IsColonist)
    {
      // Clean up pre-existing dead, unresurrectable colonists in WorldPawns.
      if (!subject.IsDead || subject.IsResurrectable)
      {
        return false;
      }

      return ShouldSeverOnMortality(subject, other, requireUnresurrectable: true, options);
    }

    if (subject.IsPlayerFaction || subject.IsOnColonistMap)
    {
      return false;
    }

    if (other.IsColonist)
    {
      return subject.IsDead && options.CleanColonistToWorldRelationsOnExitOrDeath;
    }

    if (other.IsPlayerFaction)
    {
      return false;
    }

    if (other.IsOnColonistMap)
    {
      return subject.IsDead && options.CleanWorldToWorldRelationsOnExitOrDeath;
    }

    return true;
  }
}
