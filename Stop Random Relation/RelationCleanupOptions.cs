namespace Stop_Random_Relation;

/// <summary>
/// Immutable, thread-safe snapshot of all Stop Random Relation settings.
/// </summary>
/// <remarks>
/// Reading an immutable value snapshot avoids torn reads and lock contention
/// when evaluation occurs across simulation or worker threads.
/// </remarks>
public readonly record struct RelationCleanupOptions
{
  /// <summary>
  /// Gets the default configuration matching the specification defaults.
  /// </summary>
  public static RelationCleanupOptions Default => new()
  {
    ModEnabled = true,
    CleanColonistRelationsOnPermanentDeath = true,
    CleanColonistToWorldRelationsOnExitOrDeath = true,
    CleanWorldToWorldRelationsOnExitOrDeath = true,
    CleanOffMapWorldPawnRelations = true,
    ProtectLivingFactionLeaders = true,
    IncludeNonBloodRelations = true,
    VerboseLogging = false,
  };

  /// <summary>
  /// Gets a value indicating whether the mod's pruning and GC hooks are active.
  /// </summary>
  public bool ModEnabled { get; init; }

  /// <summary>
  /// Gets a value indicating whether relations of deceased colonists are severed
  /// once their corpses can no longer be resurrected.
  /// </summary>
  public bool CleanColonistRelationsOnPermanentDeath { get; init; }

  /// <summary>
  /// Gets a value indicating whether colonist-to-world-pawn relations are
  /// severed when the relative exits a colonist map or either pawn dies.
  /// </summary>
  public bool CleanColonistToWorldRelationsOnExitOrDeath { get; init; }

  /// <summary>
  /// Gets a value indicating whether world-pawn-to-world-pawn relations on
  /// colonist maps are severed when either pawn exits or dies.
  /// </summary>
  public bool CleanWorldToWorldRelationsOnExitOrDeath { get; init; }

  /// <summary>
  /// Gets a value indicating whether relations between off-map world pawns are
  /// severed during GC preparation and periodic maintenance sweeps.
  /// </summary>
  public bool CleanOffMapWorldPawnRelations { get; init; }

  /// <summary>
  /// Gets a value indicating whether living faction leaders are strictly
  /// protected from <c>WorldPawnGC</c> even with zero relations.
  /// </summary>
  public bool ProtectLivingFactionLeaders { get; init; }

  /// <summary>
  /// Gets a value indicating whether marriage and romance relations (spouse,
  /// fiance, lover, ex-spouse, ex-lover) are cleaned alongside blood relations.
  /// </summary>
  public bool IncludeNonBloodRelations { get; init; }

  /// <summary>
  /// Gets a value indicating whether diagnostic logs are emitted to the
  /// RimWorld console when relations are severed or leaders are protected.
  /// </summary>
  public bool VerboseLogging { get; init; }
}
