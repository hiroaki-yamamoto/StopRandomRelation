using Xunit;

namespace Stop_Random_Relation.Tests;

public class WorldPawnSweepSchedulerTests
{
  [Fact]
  public void ShouldRunSweep_PeriodicMode_RunsOnlyOnConfiguredInterval()
  {
    var scheduler = new WorldPawnSweepScheduler();
    RelationCleanupOptions options = RelationCleanupOptions.Default with
    {
      OffMapSweepIntervalTicks = 2500,
    };

    Assert.False(scheduler.ShouldRunSweep(0, 200, options));
    Assert.False(scheduler.ShouldRunSweep(2499, 200, options));
    Assert.True(scheduler.ShouldRunSweep(2500, 200, options));
    // Same tick cannot be claimed twice.
    Assert.False(scheduler.ShouldRunSweep(2500, 200, options));
    Assert.True(scheduler.ShouldRunSweep(5000, 200, options));
  }

  [Fact]
  public void ShouldRunSweep_ImmediateMode_RunsOnDirtyAndFallbackWhenAtOrAboveThreshold()
  {
    var scheduler = new WorldPawnSweepScheduler();
    RelationCleanupOptions options = RelationCleanupOptions.Default with
    {
      OffMapSweepIntervalTicks = 0,
      DeleteUnprotectedOffMapWorldPawns = true,
      UnprotectedOffMapWorldPawnThreshold = 100,
    };

    // Initial dirty state fires immediately on first positive tick.
    Assert.True(scheduler.ShouldRunSweep(1, 5, options));
    Assert.False(scheduler.ShouldRunSweep(2, 5, options));

    // Marking dirty schedules the next tick immediately even below total threshold.
    scheduler.MarkDirty();
    Assert.True(scheduler.ShouldRunSweep(3, 5, options));

    // Non-dirty fallback runs on fallback interval only when totalWorldPawns >= threshold.
    int fallbackTick = WorldPawnSweepScheduler.ImmediateFallbackIntervalTicks;
    Assert.False(scheduler.ShouldRunSweep(fallbackTick, 99, options));
    Assert.True(scheduler.ShouldRunSweep(fallbackTick, 100, options));
    Assert.False(scheduler.ShouldRunSweep(fallbackTick, 100, options));
  }

  [Fact]
  public void ShouldRunSweep_ReturnsFalseWhenModDisabled()
  {
    var scheduler = new WorldPawnSweepScheduler();
    RelationCleanupOptions disabled = RelationCleanupOptions.Default with
    {
      ModEnabled = false,
      OffMapSweepIntervalTicks = 0,
    };

    Assert.False(scheduler.ShouldRunSweep(1, 500, disabled));
  }

  [Fact]
  public async Task ShouldRunSweep_ConcurrentSameTickCalls_ClaimsAtMostOnce()
  {
    var scheduler = new WorldPawnSweepScheduler();
    RelationCleanupOptions options = RelationCleanupOptions.Default with
    {
      OffMapSweepIntervalTicks = 100,
    };

    int claimedCount = 0;
    Task[] tasks = Enumerable.Range(0, 16)
      .Select(_ => Task.Run(() =>
      {
        if (scheduler.ShouldRunSweep(100, 200, options))
        {
          Interlocked.Increment(ref claimedCount);
        }
      }))
      .ToArray();

    await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));
    Assert.Equal(1, claimedCount);
  }
}
