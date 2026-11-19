using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace Stop_Random_Relation;

/// <summary>
/// Harmony patch definitions hooking pawn map exit, death, corpse
/// destruction/desiccation, and <c>WorldPawnGC</c> lifecycle events.
/// </summary>
internal static class HarmonyPatches
{
  /// <summary>
  /// Triggers relation cleanup when a non-player pawn exits a map.
  /// </summary>
  [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap), [typeof(bool), typeof(Rot4)])]
  internal static class Pawn_ExitMap_Patch
  {
    /// <summary>
    /// Postfix executed after <see cref="Pawn.ExitMap(bool, Rot4)"/> completes.
    /// </summary>
    /// <param name="__instance">The pawn that exited the map.</param>
    [HarmonyPostfix]
    public static void Postfix(Pawn __instance)
    {
      if (Current.ProgramState != ProgramState.Playing || __instance == null)
      {
        return;
      }

      RelationshipManager.HandlePawnExitMap(
        __instance,
        StopRandomRelationMod.GetOptions());
      WorldPawnSweepScheduler.Shared.MarkDirty();
    }
  }

  /// <summary>
  /// Triggers relation cleanup when a pawn dies.
  /// </summary>
  [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill), [typeof(DamageInfo?), typeof(Hediff)])]
  internal static class Pawn_Kill_Patch
  {
    /// <summary>
    /// Postfix executed after <see cref="Pawn.Kill(DamageInfo?, Hediff)"/> completes.
    /// </summary>
    /// <param name="__instance">The pawn that was killed.</param>
    [HarmonyPostfix]
    public static void Postfix(Pawn __instance)
    {
      if (Current.ProgramState != ProgramState.Playing || __instance == null || !__instance.Dead)
      {
        return;
      }

      RelationshipManager.HandlePawnKilled(
        __instance,
        StopRandomRelationMod.GetOptions());
      WorldPawnSweepScheduler.Shared.MarkDirty();
    }
  }

  private static void TryHandleCorpsePermanentDeath(
    Corpse? corpse,
    bool requireUnresurrectableCheck)
  {
    if (Current.ProgramState != ProgramState.Playing || corpse == null)
    {
      return;
    }

    Pawn? innerPawn = corpse.InnerPawn;
    if (innerPawn == null || !innerPawn.Dead || innerPawn.HasDeathRefusalOrResurrecting)
    {
      return;
    }

    if (requireUnresurrectableCheck && PawnStateEvaluator.IsResurrectable(innerPawn))
    {
      return;
    }

    RelationshipManager.HandlePermanentDeath(
      innerPawn,
      StopRandomRelationMod.GetOptions());
    WorldPawnSweepScheduler.Shared.MarkDirty();
  }

  /// <summary>
  /// Triggers permanent-death relation cleanup right before a corpse is
  /// destroyed (cremated, butchered, consumed, or vanished).
  /// </summary>
  [HarmonyPatch(typeof(Corpse), nameof(Corpse.Destroy), [typeof(DestroyMode)])]
  internal static class Corpse_Destroy_Patch
  {
    /// <summary>
    /// Prefix executed before <see cref="Corpse.Destroy(DestroyMode)"/> clears
    /// the inner pawn reference.
    /// </summary>
    /// <param name="__instance">The corpse being destroyed.</param>
    [HarmonyPrefix]
    public static void Prefix(Corpse __instance)
    {
      TryHandleCorpsePermanentDeath(__instance, requireUnresurrectableCheck: false);
    }
  }

  /// <summary>
  /// Triggers permanent-death relation cleanup when a corpse rots to a
  /// non-resurrectable stage (such as desiccation).
  /// </summary>
  [HarmonyPatch(typeof(Corpse), nameof(Corpse.RotStageChanged))]
  internal static class Corpse_RotStageChanged_Patch
  {
    /// <summary>
    /// Postfix executed after <see cref="Corpse.RotStageChanged"/> updates the
    /// corpse's rot stage.
    /// </summary>
    /// <param name="__instance">The corpse whose rot stage changed.</param>
    [HarmonyPostfix]
    public static void Postfix(Corpse __instance)
    {
      TryHandleCorpsePermanentDeath(__instance, requireUnresurrectableCheck: true);
    }
  }

  /// <summary>
  /// Ensures living faction leaders are never discarded by <c>WorldPawnGC</c>,
  /// even when all blood and marriage relations have been pruned.
  /// </summary>
  [HarmonyPatch(typeof(WorldPawnGC), "GetCriticalPawnReason", [typeof(Pawn)])]
  internal static class WorldPawnGC_GetCriticalPawnReason_Patch
  {
    /// <summary>
    /// Postfix enforcing critical retention reason for living faction leaders.
    /// </summary>
    /// <param name="pawn">The world pawn being evaluated by GC.</param>
    /// <param name="__result">The retention reason returned by RimWorld.</param>
    [HarmonyPostfix]
    public static void Postfix(Pawn pawn, ref string? __result)
    {
      if (pawn == null)
      {
        return;
      }

      RelationCleanupOptions options = StopRandomRelationMod.GetOptions();
      if (RelationshipManager.ShouldProtectFromGC(pawn, options) && string.IsNullOrEmpty(__result))
      {
        __result = "FactionLeader";
        if (options.VerboseLogging)
        {
          Log.Message(
            $"[Stop Random Relation] Protected living faction leader {pawn.LabelShort} ({pawn.ThingID}) from WorldPawnGC.");
        }
      }
    }
  }

  /// <summary>
  /// Sweeps off-map world pawn relations immediately before <c>WorldPawnGC</c>
  /// accumulates kept pawns, ensuring newly orphaned world pawns are discarded
  /// in the current GC pass.
  /// </summary>
  [HarmonyPatch(typeof(WorldPawnGC), "AccumulatePawnGCData", [typeof(Dictionary<Pawn, string>)])]
  internal static class WorldPawnGC_AccumulatePawnGCData_Patch
  {
    /// <summary>
    /// Prefix executed at the beginning of GC data accumulation.
    /// </summary>
    [HarmonyPrefix]
    public static void Prefix()
    {
      if (Current.ProgramState != ProgramState.Playing)
      {
        return;
      }

      RelationshipManager.CleanAllOffMapWorldPawnRelations(
        StopRandomRelationMod.GetOptions());
    }
  }

  /// <summary>
  /// Marks the world pawn pool dirty whenever a pawn enters <see cref="WorldPawns"/>
  /// so immediate-mode threshold pruning evaluates promptly on the next tick.
  /// </summary>
  [HarmonyPatch(typeof(WorldPawns), nameof(WorldPawns.PassToWorld), [typeof(Pawn), typeof(PawnDiscardDecideMode)])]
  internal static class WorldPawns_PassToWorld_Patch
  {
    /// <summary>
    /// Postfix executed after <see cref="WorldPawns.PassToWorld(Pawn, PawnDiscardDecideMode)"/> completes.
    /// </summary>
    [HarmonyPostfix]
    public static void Postfix()
    {
      if (Current.ProgramState != ProgramState.Playing)
      {
        return;
      }

      WorldPawnSweepScheduler.Shared.MarkDirty();
    }
  }

  /// <summary>
  /// Periodically (or immediately when sweep interval is 0) cleans stale off-map
  /// world pawn relations and prunes excess unprotected off-map world pawns
  /// during world ticking.
  /// </summary>
  [HarmonyPatch(typeof(WorldPawns), nameof(WorldPawns.WorldPawnsTick))]
  internal static class WorldPawns_WorldPawnsTick_Patch
  {
    /// <summary>
    /// Postfix executed on <see cref="WorldPawns.WorldPawnsTick"/>.
    /// </summary>
    /// <param name="__instance">The active <see cref="WorldPawns"/> manager.</param>
    [HarmonyPostfix]
    public static void Postfix(WorldPawns __instance)
    {
      if (Current.ProgramState != ProgramState.Playing)
      {
        return;
      }

      TickManager? tickManager = Find.TickManager;
      if (tickManager == null)
      {
        return;
      }

      RelationCleanupOptions options = StopRandomRelationMod.GetOptions();
      int totalWorldPawns = __instance?.AllPawnsAliveOrDead?.Count ?? 0;
      if (!WorldPawnSweepScheduler.Shared.ShouldRunSweep(
        tickManager.TicksGame,
        totalWorldPawns,
        options))
      {
        return;
      }

      RelationshipManager.CleanAllOffMapWorldPawnRelations(options);
      RelationshipManager.PruneExcessUnprotectedOffMapWorldPawns(options);
    }
  }
}
