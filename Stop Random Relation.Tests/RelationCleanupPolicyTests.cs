using Xunit;

namespace Stop_Random_Relation.Tests;

public class RelationCleanupPolicyTests
{
  private static readonly PawnRoleInfo LivingColonist = new(
    IsColonist: true,
    IsPlayerFaction: true,
    IsDead: false,
    IsResurrectable: false,
    IsOnColonistMap: true,
    IsActiveFactionLeader: false);

  private static readonly PawnRoleInfo CaravanColonist = new(
    IsColonist: true,
    IsPlayerFaction: true,
    IsDead: false,
    IsResurrectable: false,
    IsOnColonistMap: false,
    IsActiveFactionLeader: false);

  private static readonly PawnRoleInfo DeadResurrectableColonist = new(
    IsColonist: true,
    IsPlayerFaction: true,
    IsDead: true,
    IsResurrectable: true,
    IsOnColonistMap: true,
    IsActiveFactionLeader: false);

  private static readonly PawnRoleInfo DeadUnresurrectableColonist = new(
    IsColonist: true,
    IsPlayerFaction: true,
    IsDead: true,
    IsResurrectable: false,
    IsOnColonistMap: true,
    IsActiveFactionLeader: false);

  private static readonly PawnRoleInfo PlayerAnimal = new(
    IsColonist: false,
    IsPlayerFaction: true,
    IsDead: false,
    IsResurrectable: false,
    IsOnColonistMap: false,
    IsActiveFactionLeader: false);

  private static readonly PawnRoleInfo OnMapWorldPawn = new(
    IsColonist: false,
    IsPlayerFaction: false,
    IsDead: false,
    IsResurrectable: false,
    IsOnColonistMap: true,
    IsActiveFactionLeader: false);

  private static readonly PawnRoleInfo OffMapWorldPawn = new(
    IsColonist: false,
    IsPlayerFaction: false,
    IsDead: false,
    IsResurrectable: false,
    IsOnColonistMap: false,
    IsActiveFactionLeader: false);

  private static readonly PawnRoleInfo LivingFactionLeader = new(
    IsColonist: false,
    IsPlayerFaction: false,
    IsDead: false,
    IsResurrectable: false,
    IsOnColonistMap: false,
    IsActiveFactionLeader: true);

  private static readonly PawnRoleInfo DeadFactionLeader = new(
    IsColonist: false,
    IsPlayerFaction: false,
    IsDead: true,
    IsResurrectable: false,
    IsOnColonistMap: false,
    IsActiveFactionLeader: false);

  [Theory]
  [InlineData(RelationCategory.Blood, true, true)]
  [InlineData(RelationCategory.Blood, false, true)]
  [InlineData(RelationCategory.MarriageOrRomance, true, true)]
  [InlineData(RelationCategory.MarriageOrRomance, false, false)]
  [InlineData(RelationCategory.None, true, false)]
  [InlineData(RelationCategory.None, false, false)]
  public void IsTargetRelation_RespectsIncludeNonBloodSetting(
    RelationCategory category,
    bool includeNonBlood,
    bool expected)
  {
    RelationCleanupOptions options = RelationCleanupOptions.Default with
    {
      IncludeNonBloodRelations = includeNonBlood,
    };

    Assert.Equal(expected, RelationCleanupPolicy.IsTargetRelation(category, options));
  }

  [Fact]
  public void ShouldSeverRelation_ReturnsFalse_WhenModDisabled()
  {
    RelationCleanupOptions options = RelationCleanupOptions.Default with
    {
      ModEnabled = false,
    };

    Assert.False(RelationCleanupPolicy.ShouldSeverRelation(
      OffMapWorldPawn,
      LivingColonist,
      RelationCategory.Blood,
      RelationTrigger.MapExit,
      options));
    Assert.False(RelationCleanupPolicy.ShouldSeverRelation(
      DeadUnresurrectableColonist,
      LivingColonist,
      RelationCategory.Blood,
      RelationTrigger.PermanentDeath,
      options));
    Assert.False(RelationCleanupPolicy.ShouldSeverRelation(
      OffMapWorldPawn,
      OffMapWorldPawn,
      RelationCategory.Blood,
      RelationTrigger.OffMapSweep,
      options));
  }

  [Fact]
  public void ColonistToColonist_PreservedWhileResurrectable_SeveredOnPermanentDeath()
  {
    RelationCleanupOptions options = RelationCleanupOptions.Default;

    Assert.False(RelationCleanupPolicy.ShouldSeverRelation(
      DeadResurrectableColonist,
      LivingColonist,
      RelationCategory.Blood,
      RelationTrigger.Death,
      options));

    Assert.True(RelationCleanupPolicy.ShouldSeverRelation(
      DeadUnresurrectableColonist,
      LivingColonist,
      RelationCategory.Blood,
      RelationTrigger.Death,
      options));

    Assert.True(RelationCleanupPolicy.ShouldSeverRelation(
      DeadUnresurrectableColonist,
      LivingColonist,
      RelationCategory.Blood,
      RelationTrigger.PermanentDeath,
      options));

    RelationCleanupOptions disabledPermDeath = options with
    {
      CleanColonistRelationsOnPermanentDeath = false,
    };
    Assert.False(RelationCleanupPolicy.ShouldSeverRelation(
      DeadUnresurrectableColonist,
      LivingColonist,
      RelationCategory.Blood,
      RelationTrigger.PermanentDeath,
      disabledPermDeath));
  }

  [Fact]
  public void ColonistToWorldPawn_SeveredOnExitOrDeath_PreservedForCaravanColonistExit()
  {
    RelationCleanupOptions options = RelationCleanupOptions.Default;

    // World pawn relative exits map -> severed.
    Assert.True(RelationCleanupPolicy.ShouldSeverRelation(
      OffMapWorldPawn,
      LivingColonist,
      RelationCategory.Blood,
      RelationTrigger.MapExit,
      options));

    // Caravan colonist exits map -> preserved.
    Assert.False(RelationCleanupPolicy.ShouldSeverRelation(
      CaravanColonist,
      OffMapWorldPawn,
      RelationCategory.Blood,
      RelationTrigger.MapExit,
      options));

    // World pawn relative dies -> severed.
    Assert.True(RelationCleanupPolicy.ShouldSeverRelation(
      OnMapWorldPawn with { IsDead = true },
      LivingColonist,
      RelationCategory.Blood,
      RelationTrigger.Death,
      options));

    // Colonist dies -> relation to world pawn is severed even if colonist corpse is fresh.
    Assert.True(RelationCleanupPolicy.ShouldSeverRelation(
      DeadResurrectableColonist,
      OffMapWorldPawn,
      RelationCategory.Blood,
      RelationTrigger.Death,
      options));

    // Toggle off -> preserved.
    RelationCleanupOptions disabled = options with
    {
      CleanColonistToWorldRelationsOnExitOrDeath = false,
    };
    Assert.False(RelationCleanupPolicy.ShouldSeverRelation(
      OffMapWorldPawn,
      LivingColonist,
      RelationCategory.Blood,
      RelationTrigger.MapExit,
      disabled));
  }

  [Fact]
  public void PlayerAnimalInCaravan_NeverSeveredOnMapExit()
  {
    RelationCleanupOptions options = RelationCleanupOptions.Default;

    Assert.False(RelationCleanupPolicy.ShouldSeverRelation(
      PlayerAnimal,
      PlayerAnimal,
      RelationCategory.Blood,
      RelationTrigger.MapExit,
      options));
  }

  [Fact]
  public void WorldToWorldPawn_SeveredOnExitOrDeath_AndOffMapSweep()
  {
    RelationCleanupOptions options = RelationCleanupOptions.Default;

    Assert.True(RelationCleanupPolicy.ShouldSeverRelation(
      OffMapWorldPawn,
      OnMapWorldPawn,
      RelationCategory.Blood,
      RelationTrigger.MapExit,
      options));

    Assert.True(RelationCleanupPolicy.ShouldSeverRelation(
      OnMapWorldPawn with { IsDead = true },
      OnMapWorldPawn,
      RelationCategory.Blood,
      RelationTrigger.Death,
      options));

    // Off-map sweep severs two off-map world pawns.
    Assert.True(RelationCleanupPolicy.ShouldSeverRelation(
      OffMapWorldPawn,
      OffMapWorldPawn,
      RelationCategory.Blood,
      RelationTrigger.OffMapSweep,
      options));

    // Off-map sweep preserves if either pawn is on a colonist map or is a colonist.
    Assert.False(RelationCleanupPolicy.ShouldSeverRelation(
      OffMapWorldPawn,
      OnMapWorldPawn,
      RelationCategory.Blood,
      RelationTrigger.OffMapSweep,
      options));
    Assert.False(RelationCleanupPolicy.ShouldSeverRelation(
      OffMapWorldPawn,
      LivingColonist,
      RelationCategory.Blood,
      RelationTrigger.OffMapSweep,
      options));

    RelationCleanupOptions offMapDisabled = options with
    {
      CleanOffMapWorldPawnRelations = false,
    };
    Assert.False(RelationCleanupPolicy.ShouldSeverRelation(
      OffMapWorldPawn,
      OffMapWorldPawn,
      RelationCategory.Blood,
      RelationTrigger.OffMapSweep,
      offMapDisabled));

    // Dead unresurrectable colonist in WorldPawns is cleaned on off-map sweep,
    // while resurrectable dead colonist is preserved.
    Assert.True(RelationCleanupPolicy.ShouldSeverRelation(
      DeadUnresurrectableColonist,
      LivingColonist,
      RelationCategory.Blood,
      RelationTrigger.OffMapSweep,
      options));
    Assert.False(RelationCleanupPolicy.ShouldSeverRelation(
      DeadResurrectableColonist,
      LivingColonist,
      RelationCategory.Blood,
      RelationTrigger.OffMapSweep,
      options));
  }

  [Fact]
  public void ShouldProtectFromGC_ProtectsLivingFactionLeadersOnlyWhenEnabled()
  {
    RelationCleanupOptions options = RelationCleanupOptions.Default;

    Assert.True(RelationCleanupPolicy.ShouldProtectFromGC(LivingFactionLeader, options));
    Assert.False(RelationCleanupPolicy.ShouldProtectFromGC(DeadFactionLeader, options));
    Assert.False(RelationCleanupPolicy.ShouldProtectFromGC(OffMapWorldPawn, options));

    RelationCleanupOptions disabledProtection = options with
    {
      ProtectLivingFactionLeaders = false,
    };
    Assert.False(RelationCleanupPolicy.ShouldProtectFromGC(
      LivingFactionLeader,
      disabledProtection));

    RelationCleanupOptions disabledMod = options with
    {
      ModEnabled = false,
    };
    Assert.False(RelationCleanupPolicy.ShouldProtectFromGC(
      LivingFactionLeader,
      disabledMod));
  }

  [Fact]
  public void ShouldDeleteWorldPawnOnThreshold_RespectsFeatureAndModToggles()
  {
    var eligible = new WorldPawnPruneInfo(
      OffMapWorldPawn,
      IsOnAnyMap: false,
      IsProtectedByGameState: false,
      HasAnyRelation: false);

    Assert.False(RelationCleanupPolicy.ShouldDeleteWorldPawnOnThreshold(
      eligible,
      RelationCleanupOptions.Default));

    RelationCleanupOptions enabled = RelationCleanupOptions.Default with
    {
      DeleteUnprotectedOffMapWorldPawns = true,
    };
    Assert.True(RelationCleanupPolicy.ShouldDeleteWorldPawnOnThreshold(eligible, enabled));

    Assert.False(RelationCleanupPolicy.ShouldDeleteWorldPawnOnThreshold(
      eligible,
      enabled with { ModEnabled = false }));
  }

  [Fact]
  public void ShouldDeleteWorldPawnOnThreshold_ProtectsMapColonistPlayerLeaderGameStateAndRelations()
  {
    RelationCleanupOptions enabled = RelationCleanupOptions.Default with
    {
      DeleteUnprotectedOffMapWorldPawns = true,
    };

    var baseInfo = new WorldPawnPruneInfo(
      OffMapWorldPawn,
      IsOnAnyMap: false,
      IsProtectedByGameState: false,
      HasAnyRelation: false);

    Assert.False(RelationCleanupPolicy.ShouldDeleteWorldPawnOnThreshold(
      baseInfo with { IsOnAnyMap = true },
      enabled));
    Assert.False(RelationCleanupPolicy.ShouldDeleteWorldPawnOnThreshold(
      baseInfo with { Role = OnMapWorldPawn },
      enabled));
    Assert.False(RelationCleanupPolicy.ShouldDeleteWorldPawnOnThreshold(
      baseInfo with { Role = CaravanColonist },
      enabled));
    Assert.False(RelationCleanupPolicy.ShouldDeleteWorldPawnOnThreshold(
      baseInfo with { Role = PlayerAnimal },
      enabled));
    Assert.False(RelationCleanupPolicy.ShouldDeleteWorldPawnOnThreshold(
      baseInfo with { Role = LivingFactionLeader },
      enabled));
    Assert.True(RelationCleanupPolicy.ShouldDeleteWorldPawnOnThreshold(
      baseInfo with { Role = LivingFactionLeader },
      enabled with { ProtectLivingFactionLeaders = false }));
    Assert.True(RelationCleanupPolicy.ShouldDeleteWorldPawnOnThreshold(
      baseInfo with { Role = DeadFactionLeader },
      enabled));
    Assert.False(RelationCleanupPolicy.ShouldDeleteWorldPawnOnThreshold(
      baseInfo with { IsProtectedByGameState = true },
      enabled));
    Assert.False(RelationCleanupPolicy.ShouldDeleteWorldPawnOnThreshold(
      baseInfo with { HasAnyRelation = true },
      enabled));
  }
}
