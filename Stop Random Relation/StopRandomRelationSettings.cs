using Verse;

namespace Stop_Random_Relation;

/// <summary>
/// Persists user-configurable mod settings via RimWorld's <c>Scribe_Values</c>
/// system and provides lock-free atomic snapshot reads for runtime hooks.
/// </summary>
public class StopRandomRelationSettings : ModSettings
{
  private sealed record SnapshotHolder(RelationCleanupOptions Value);

  /// <summary>Master enable/disable switch.</summary>
  public bool modEnabled = true;

  /// <summary>Clean colonist relations when corpse cannot be resurrected.</summary>
  public bool cleanColonistRelationsOnPermanentDeath = true;

  /// <summary>Clean colonist-to-world-pawn relations on exit or death.</summary>
  public bool cleanColonistToWorldRelationsOnExitOrDeath = true;

  /// <summary>Clean world-pawn-to-world-pawn relations on exit or death.</summary>
  public bool cleanWorldToWorldRelationsOnExitOrDeath = true;

  /// <summary>Clean relations between off-map world pawns.</summary>
  public bool cleanOffMapWorldPawnRelations = true;

  /// <summary>Delete unprotected off-map world pawns when their count reaches threshold.</summary>
  public bool deleteUnprotectedOffMapWorldPawns = false;

  /// <summary>Threshold of unprotected off-map world pawns required to trigger deletion.</summary>
  public int unprotectedOffMapWorldPawnThreshold = RelationCleanupOptions.DefaultWorldPawnThreshold;

  /// <summary>Periodic off-map sweep interval in ticks (0 = immediate on threshold).</summary>
  public int offMapSweepIntervalTicks = RelationCleanupOptions.DefaultOffMapSweepIntervalTicks;

  /// <summary>Protect living faction leaders from WorldPawnGC.</summary>
  public bool protectLivingFactionLeaders = true;

  /// <summary>Include non-blood marriage/romance relations in cleanup.</summary>
  public bool includeNonBloodRelations = true;

  /// <summary>Emit verbose diagnostic messages to the RimWorld log.</summary>
  public bool verboseLogging = false;

  private readonly object _writeLock = new();
  private volatile SnapshotHolder _snapshot = new(RelationCleanupOptions.Default);

  /// <summary>
  /// Returns an immutable, thread-safe snapshot of the current settings.
  /// </summary>
  /// <returns>The current <see cref="RelationCleanupOptions"/> snapshot.</returns>
  public RelationCleanupOptions GetOptionsSnapshot()
  {
    return _snapshot.Value;
  }

  /// <summary>
  /// Atomically publishes the current field values to the lock-free snapshot.
  /// Must be called after modifying settings fields in UI or loading data.
  /// </summary>
  public void RefreshSnapshot()
  {
    bool changed = false;
    lock (_writeLock)
    {
      int clampedThreshold = RelationCleanupOptions.ClampWorldPawnThreshold(
        unprotectedOffMapWorldPawnThreshold);
      int clampedInterval = RelationCleanupOptions.ClampSweepIntervalTicks(
        offMapSweepIntervalTicks);

      var updated = new RelationCleanupOptions
      {
        ModEnabled = modEnabled,
        CleanColonistRelationsOnPermanentDeath = cleanColonistRelationsOnPermanentDeath,
        CleanColonistToWorldRelationsOnExitOrDeath = cleanColonistToWorldRelationsOnExitOrDeath,
        CleanWorldToWorldRelationsOnExitOrDeath = cleanWorldToWorldRelationsOnExitOrDeath,
        CleanOffMapWorldPawnRelations = cleanOffMapWorldPawnRelations,
        DeleteUnprotectedOffMapWorldPawns = deleteUnprotectedOffMapWorldPawns,
        UnprotectedOffMapWorldPawnThreshold = clampedThreshold,
        OffMapSweepIntervalTicks = clampedInterval,
        ProtectLivingFactionLeaders = protectLivingFactionLeaders,
        IncludeNonBloodRelations = includeNonBloodRelations,
        VerboseLogging = verboseLogging,
      };

      if (_snapshot.Value != updated)
      {
        _snapshot = new SnapshotHolder(updated);
        changed = true;
      }
    }

    if (changed)
    {
      WorldPawnSweepScheduler.Shared.MarkDirty();
    }
  }

  /// <summary>
  /// Restores all configurable options to their specification defaults and
  /// immediately updates the live snapshot.
  /// </summary>
  public void ResetToDefaults()
  {
    lock (_writeLock)
    {
      RelationCleanupOptions defaults = RelationCleanupOptions.Default;
      modEnabled = defaults.ModEnabled;
      cleanColonistRelationsOnPermanentDeath = defaults.CleanColonistRelationsOnPermanentDeath;
      cleanColonistToWorldRelationsOnExitOrDeath = defaults.CleanColonistToWorldRelationsOnExitOrDeath;
      cleanWorldToWorldRelationsOnExitOrDeath = defaults.CleanWorldToWorldRelationsOnExitOrDeath;
      cleanOffMapWorldPawnRelations = defaults.CleanOffMapWorldPawnRelations;
      deleteUnprotectedOffMapWorldPawns = defaults.DeleteUnprotectedOffMapWorldPawns;
      unprotectedOffMapWorldPawnThreshold = defaults.UnprotectedOffMapWorldPawnThreshold;
      offMapSweepIntervalTicks = defaults.OffMapSweepIntervalTicks;
      protectLivingFactionLeaders = defaults.ProtectLivingFactionLeaders;
      includeNonBloodRelations = defaults.IncludeNonBloodRelations;
      verboseLogging = defaults.VerboseLogging;
      _snapshot = new SnapshotHolder(defaults);
    }

    WorldPawnSweepScheduler.Shared.MarkDirty();
  }

  /// <summary>
  /// Serializes and deserializes mod settings using RimWorld's Scribe system.
  /// </summary>
  public override void ExposeData()
  {
    base.ExposeData();
    Scribe_Values.Look(ref modEnabled, "modEnabled", true);
    Scribe_Values.Look(
      ref cleanColonistRelationsOnPermanentDeath,
      "cleanColonistRelationsOnPermanentDeath",
      true);
    Scribe_Values.Look(
      ref cleanColonistToWorldRelationsOnExitOrDeath,
      "cleanColonistToWorldRelationsOnExitOrDeath",
      true);
    Scribe_Values.Look(
      ref cleanWorldToWorldRelationsOnExitOrDeath,
      "cleanWorldToWorldRelationsOnExitOrDeath",
      true);
    Scribe_Values.Look(
      ref cleanOffMapWorldPawnRelations,
      "cleanOffMapWorldPawnRelations",
      true);
    Scribe_Values.Look(
      ref deleteUnprotectedOffMapWorldPawns,
      "deleteUnprotectedOffMapWorldPawns",
      false);
    Scribe_Values.Look(
      ref unprotectedOffMapWorldPawnThreshold,
      "unprotectedOffMapWorldPawnThreshold",
      RelationCleanupOptions.DefaultWorldPawnThreshold);
    Scribe_Values.Look(
      ref offMapSweepIntervalTicks,
      "offMapSweepIntervalTicks",
      RelationCleanupOptions.DefaultOffMapSweepIntervalTicks);
    Scribe_Values.Look(
      ref protectLivingFactionLeaders,
      "protectLivingFactionLeaders",
      true);
    Scribe_Values.Look(
      ref includeNonBloodRelations,
      "includeNonBloodRelations",
      true);
    Scribe_Values.Look(ref verboseLogging, "verboseLogging", false);
    RefreshSnapshot();
  }
}
