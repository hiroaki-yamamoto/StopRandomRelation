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
  /// Minimum allowed value for <see cref="UnprotectedOffMapWorldPawnThreshold"/>.
  /// </summary>
  public const int MinWorldPawnThreshold = 1;

  /// <summary>
  /// Default value for <see cref="UnprotectedOffMapWorldPawnThreshold"/>.
  /// </summary>
  public const int DefaultWorldPawnThreshold = 100;

  /// <summary>
  /// Maximum allowed value for <see cref="UnprotectedOffMapWorldPawnThreshold"/>.
  /// </summary>
  public const int MaxWorldPawnThreshold = 1_000_000;

  /// <summary>
  /// Default interval in game ticks for periodic off-map sweeps (<c>0</c> = immediate).
  /// </summary>
  public const int DefaultOffMapSweepIntervalTicks = 2500;

  /// <summary>
  /// Maximum allowed interval in game ticks for periodic off-map sweeps.
  /// </summary>
  public const int MaxOffMapSweepIntervalTicks = 1_000_000;

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
    DeleteUnprotectedOffMapWorldPawns = false,
    UnprotectedOffMapWorldPawnThreshold = DefaultWorldPawnThreshold,
    OffMapSweepIntervalTicks = DefaultOffMapSweepIntervalTicks,
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
  /// Gets a value indicating whether unprotected off-map world pawns are deleted
  /// once their count reaches <see cref="UnprotectedOffMapWorldPawnThreshold"/>.
  /// </summary>
  public bool DeleteUnprotectedOffMapWorldPawns { get; init; }

  /// <summary>
  /// Gets the minimum number of unprotected off-map world pawns required to
  /// trigger deletion when <see cref="DeleteUnprotectedOffMapWorldPawns"/> is enabled.
  /// </summary>
  public int UnprotectedOffMapWorldPawnThreshold { get; init; }

  /// <summary>
  /// Gets the periodic sweep interval in game ticks (<c>0</c> triggers immediate
  /// evaluation when unprotected off-map world pawns reach the threshold).
  /// </summary>
  public int OffMapSweepIntervalTicks { get; init; }

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

  /// <summary>
  /// Clamps <paramref name="value"/> to the valid range for
  /// <see cref="UnprotectedOffMapWorldPawnThreshold"/>.
  /// </summary>
  /// <param name="value">Raw threshold value.</param>
  /// <returns>Value clamped between <see cref="MinWorldPawnThreshold"/> and <see cref="MaxWorldPawnThreshold"/>.</returns>
  public static int ClampWorldPawnThreshold(int value)
  {
    return Math.Clamp(value, MinWorldPawnThreshold, MaxWorldPawnThreshold);
  }

  /// <summary>
  /// Clamps <paramref name="value"/> to the valid range for
  /// <see cref="OffMapSweepIntervalTicks"/>.
  /// </summary>
  /// <param name="value">Raw sweep interval in ticks.</param>
  /// <returns>Value clamped between <c>0</c> and <see cref="MaxOffMapSweepIntervalTicks"/>.</returns>
  public static int ClampSweepIntervalTicks(int value)
  {
    return Math.Clamp(value, 0, MaxOffMapSweepIntervalTicks);
  }
}
