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
    Assert.True(defaults.ProtectLivingFactionLeaders);
    Assert.True(defaults.IncludeNonBloodRelations);
    Assert.False(defaults.VerboseLogging);
  }
}
