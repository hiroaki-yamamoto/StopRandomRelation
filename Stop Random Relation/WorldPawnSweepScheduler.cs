namespace Stop_Random_Relation;

/// <summary>
/// Thread-safe coordinator deciding when off-map relation sweeps and threshold
/// world pawn pruning should run during <c>WorldPawnsTick</c>.
/// </summary>
/// <remarks>
/// Uses atomic <see cref="Interlocked"/> state transitions so concurrent callers
/// cannot trigger duplicate evaluations for the same game tick or lose dirty
/// notifications emitted by lifecycle hooks.
/// </remarks>
public sealed class WorldPawnSweepScheduler
{
  /// <summary>
  /// Fallback tick interval in immediate mode (<c>OffMapSweepIntervalTicks == 0</c>)
  /// for catching state changes not covered by direct hooks (e.g., expired quests).
  /// </summary>
  public const int ImmediateFallbackIntervalTicks = 60;

  private int _dirty = 1;
  private int _lastClaimedTick = int.MinValue;

  /// <summary>
  /// Gets the shared runtime scheduler instance used by mod hooks and settings.
  /// </summary>
  public static WorldPawnSweepScheduler Shared { get; } = new();

  /// <summary>
  /// Marks the world pawn pool as needing re-evaluation on the next eligible tick.
  /// </summary>
  public void MarkDirty()
  {
    Interlocked.Exchange(ref _dirty, 1);
  }

  /// <summary>
  /// Resets scheduler state (intended for testing or game session resets).
  /// </summary>
  public void Reset()
  {
    Interlocked.Exchange(ref _dirty, 1);
    Interlocked.Exchange(ref _lastClaimedTick, int.MinValue);
  }

  /// <summary>
  /// Evaluates in <c>O(1)</c> time whether an off-map sweep and pruning pass
  /// should execute at <paramref name="ticksGame"/>.
  /// </summary>
  /// <param name="ticksGame">Current game tick count.</param>
  /// <param name="totalWorldPawns">Total number of pawns in <c>WorldPawns</c>.</param>
  /// <param name="options">Current mod configuration snapshot.</param>
  /// <returns><c>true</c> if the caller should run the sweep; otherwise <c>false</c>.</returns>
  /// <example>
  /// <code>
  /// var scheduler = new WorldPawnSweepScheduler();
  /// var opts = RelationCleanupOptions.Default with { OffMapSweepIntervalTicks = 0 };
  /// bool run = scheduler.ShouldRunSweep(1, 10, opts);
  /// // run == true (initial dirty state)
  /// </code>
  /// </example>
  public bool ShouldRunSweep(
    int ticksGame,
    int totalWorldPawns,
    in RelationCleanupOptions options)
  {
    if (!options.ModEnabled || ticksGame <= 0)
    {
      return false;
    }

    int interval = RelationCleanupOptions.ClampSweepIntervalTicks(options.OffMapSweepIntervalTicks);
    if (interval > 0)
    {
      if (ticksGame % interval != 0 || !TryClaimTick(ticksGame))
      {
        return false;
      }

      Interlocked.Exchange(ref _dirty, 0);
      return true;
    }

    if (Interlocked.Exchange(ref _dirty, 0) != 0)
    {
      Interlocked.Exchange(ref _lastClaimedTick, ticksGame);
      return true;
    }

    if (!options.DeleteUnprotectedOffMapWorldPawns
      || ticksGame % ImmediateFallbackIntervalTicks != 0)
    {
      return false;
    }

    int threshold = RelationCleanupOptions.ClampWorldPawnThreshold(
      options.UnprotectedOffMapWorldPawnThreshold);
    return totalWorldPawns >= threshold && TryClaimTick(ticksGame);
  }

  private bool TryClaimTick(int ticksGame)
  {
    int prev = Volatile.Read(ref _lastClaimedTick);
    if (prev == ticksGame)
    {
      return false;
    }

    return Interlocked.CompareExchange(ref _lastClaimedTick, ticksGame, prev) == prev;
  }
}
