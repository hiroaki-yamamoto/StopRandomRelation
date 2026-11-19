using Xunit;

namespace Stop_Random_Relation.Tests;

public class RelationCleanupOptionsTests
{
  [Fact]
  public void DefaultOptions_MatchSpecificationDefaults()
  {
    RelationCleanupOptions defaults = RelationCleanupOptions.Default;

    Assert.True(defaults.ModEnabled);
    Assert.True(defaults.CleanColonistRelationsOnPermanentDeath);
    Assert.True(defaults.CleanColonistToWorldRelationsOnExitOrDeath);
    Assert.True(defaults.CleanWorldToWorldRelationsOnExitOrDeath);
    Assert.True(defaults.CleanOffMapWorldPawnRelations);
    Assert.False(defaults.DeleteUnprotectedOffMapWorldPawns);
    Assert.Equal(100, defaults.UnprotectedOffMapWorldPawnThreshold);
    Assert.Equal(2500, defaults.OffMapSweepIntervalTicks);
    Assert.True(defaults.ProtectLivingFactionLeaders);
    Assert.True(defaults.IncludeNonBloodRelations);
    Assert.False(defaults.VerboseLogging);
  }

  [Theory]
  [InlineData(-10, RelationCleanupOptions.MinWorldPawnThreshold)]
  [InlineData(0, RelationCleanupOptions.MinWorldPawnThreshold)]
  [InlineData(1, 1)]
  [InlineData(100, 100)]
  [InlineData(2_000_000, RelationCleanupOptions.MaxWorldPawnThreshold)]
  public void ClampWorldPawnThreshold_ClampsToValidRange(int input, int expected)
  {
    Assert.Equal(expected, RelationCleanupOptions.ClampWorldPawnThreshold(input));
  }

  [Theory]
  [InlineData(-5, 0)]
  [InlineData(0, 0)]
  [InlineData(2500, 2500)]
  [InlineData(2_000_000, RelationCleanupOptions.MaxOffMapSweepIntervalTicks)]
  public void ClampSweepIntervalTicks_ClampsToValidRange(int input, int expected)
  {
    Assert.Equal(expected, RelationCleanupOptions.ClampSweepIntervalTicks(input));
  }
}
