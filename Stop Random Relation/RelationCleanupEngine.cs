using System.Runtime.CompilerServices;

namespace Stop_Random_Relation;

/// <summary>
/// Thread-safe, zero-reflection relationship pruning engine parameterized by an
/// <see cref="IRelationAccessor{TPawn, TRelationDef}"/> struct adapter.
/// </summary>
/// <remarks>
/// <para>
/// Time complexity per event is <c>O(R)</c>, where <c>R</c> is the number of
/// direct relations on the evaluated pawn. Off-map sweep complexity is
/// <c>O(N + K * R)</c>, where <c>N</c> is total world pawns and <c>K</c> is
/// the number of world pawns that actually have direct relations.
/// </para>
/// <para>
/// Auxiliary space is amortized <c>O(1)</c> on repeated invocations via
/// thread-local buffer reuse (rent/return for re-entrancy safety). Concurrent
/// modifications to both endpoints use canonical lock ordering via
/// <see cref="RuntimeHelpers.GetHashCode(object)"/> to eliminate deadlocks.
/// </para>
/// </remarks>
public static class RelationCleanupEngine
{
  private static readonly object TieBreakerLock = new();

  private static class SnapshotBufferCache<TPawn, TRelationDef>
    where TPawn : class
    where TRelationDef : class
  {
    [ThreadStatic]
    private static List<DirectRelationEntry<TPawn, TRelationDef>>? _cached;

    public static List<DirectRelationEntry<TPawn, TRelationDef>> Rent()
    {
      List<DirectRelationEntry<TPawn, TRelationDef>>? buffer = _cached;
      if (buffer is null)
      {
        return [];
      }

      _cached = null;
      buffer.Clear();
      return buffer;
    }

    public static void Return(List<DirectRelationEntry<TPawn, TRelationDef>> buffer)
    {
      buffer.Clear();
      _cached = buffer;
    }
  }

  /// <summary>
  /// Evaluates all direct relations on <paramref name="subject"/> and severs
  /// both sides of every relation matching <paramref name="predicate"/>.
  /// </summary>
  /// <typeparam name="TPawn">The pawn type.</typeparam>
  /// <typeparam name="TRelationDef">The relation definition type.</typeparam>
  /// <typeparam name="TAccessor">Struct adapter type.</typeparam>
  /// <param name="accessor">Adapter instance.</param>
  /// <param name="subject">The pawn whose relations are evaluated.</param>
  /// <param name="predicate">Filter determining whether a relation def should be removed.</param>
  /// <param name="logAction">Optional callback invoked per severed relation when logging is enabled.</param>
  /// <returns>The number of direct relationships severed.</returns>
  public static int SeverRelations<TPawn, TRelationDef, TAccessor>(
    TAccessor accessor,
    TPawn? subject,
    Func<TRelationDef, bool> predicate,
    Action<string>? logAction = null)
    where TPawn : class
    where TRelationDef : class
    where TAccessor : struct, IRelationAccessor<TPawn, TRelationDef>
  {
    if (subject is null
      || predicate is null
      || accessor.GetSyncRoot(subject) is null
      || !accessor.HasAnyDirectRelations(subject))
    {
      return 0;
    }

    List<DirectRelationEntry<TPawn, TRelationDef>> snapshot =
      SnapshotBufferCache<TPawn, TRelationDef>.Rent();
    try
    {
      accessor.SnapshotDirectRelations(subject, snapshot);
      if (snapshot.Count == 0)
      {
        return 0;
      }

      int severed = 0;
      for (int i = 0; i < snapshot.Count; i++)
      {
        DirectRelationEntry<TPawn, TRelationDef> entry = snapshot[i];
        if (entry.Def is null || !predicate(entry.Def))
        {
          continue;
        }

        if (SeverPairAtomically(accessor, subject, entry.Def, entry.Other))
        {
          severed++;
          logAction?.Invoke(
            $"Severed relation {accessor.DescribeRelation(entry.Def)} between " +
            $"{accessor.DescribePawn(subject)} and {accessor.DescribePawn(entry.Other)}.");
        }
      }

      return severed;
    }
    finally
    {
      SnapshotBufferCache<TPawn, TRelationDef>.Return(snapshot);
    }
  }

  /// <summary>
  /// Evaluates all direct relations on <paramref name="subject"/> against
  /// <see cref="RelationCleanupPolicy"/> for the specified <paramref name="trigger"/>
  /// and severs invalid relations atomically on both pawns.
  /// </summary>
  /// <typeparam name="TPawn">The pawn type.</typeparam>
  /// <typeparam name="TRelationDef">The relation definition type.</typeparam>
  /// <typeparam name="TAccessor">Struct adapter type.</typeparam>
  /// <param name="accessor">Adapter instance.</param>
  /// <param name="subject">The pawn triggering evaluation.</param>
  /// <param name="trigger">The lifecycle trigger.</param>
  /// <param name="options">Current mod configuration snapshot.</param>
  /// <param name="logAction">Optional callback invoked per severed relation when verbose logging is enabled.</param>
  /// <returns>The number of relationships severed.</returns>
  public static int CleanForTrigger<TPawn, TRelationDef, TAccessor>(
    TAccessor accessor,
    TPawn? subject,
    RelationTrigger trigger,
    in RelationCleanupOptions options,
    Action<string>? logAction = null)
    where TPawn : class
    where TRelationDef : class
    where TAccessor : struct, IRelationAccessor<TPawn, TRelationDef>
  {
    if (!options.ModEnabled
      || subject is null
      || accessor.GetSyncRoot(subject) is null
      || !accessor.HasAnyDirectRelations(subject))
    {
      return 0;
    }

    List<DirectRelationEntry<TPawn, TRelationDef>> snapshot =
      SnapshotBufferCache<TPawn, TRelationDef>.Rent();
    try
    {
      PawnRoleInfo subjectRole = accessor.GetRoleInfo(subject);
      return CleanSubjectEntries(
        accessor,
        subject,
        subjectRole,
        trigger,
        options,
        snapshot,
        logAction);
    }
    finally
    {
      SnapshotBufferCache<TPawn, TRelationDef>.Return(snapshot);
    }
  }

  /// <summary>
  /// Sweeps a list of world pawns and severs relations between non-colonist,
  /// off-map world pawns (and pre-existing unresurrectable dead colonists).
  /// </summary>
  /// <typeparam name="TPawn">The pawn type.</typeparam>
  /// <typeparam name="TRelationDef">The relation definition type.</typeparam>
  /// <typeparam name="TAccessor">Struct adapter type.</typeparam>
  /// <param name="accessor">Adapter instance.</param>
  /// <param name="worldPawns">World pawns snapshot to evaluate.</param>
  /// <param name="options">Current mod configuration snapshot.</param>
  /// <param name="logAction">Optional callback invoked per severed relation.</param>
  /// <returns>Total number of relationships severed across the sweep.</returns>
  public static int CleanOffMapWorldPawns<TPawn, TRelationDef, TAccessor>(
    TAccessor accessor,
    IReadOnlyList<TPawn>? worldPawns,
    in RelationCleanupOptions options,
    Action<string>? logAction = null)
    where TPawn : class
    where TRelationDef : class
    where TAccessor : struct, IRelationAccessor<TPawn, TRelationDef>
  {
    if (!options.ModEnabled || !options.CleanOffMapWorldPawnRelations || worldPawns is null)
    {
      return 0;
    }

    int totalSevered = 0;
    List<DirectRelationEntry<TPawn, TRelationDef>> snapshot =
      SnapshotBufferCache<TPawn, TRelationDef>.Rent();
    try
    {
      for (int i = 0; i < worldPawns.Count; i++)
      {
        TPawn? pawn = worldPawns[i];
        if (pawn is null
          || accessor.GetSyncRoot(pawn) is null
          || !accessor.HasAnyDirectRelations(pawn))
        {
          continue;
        }

        PawnRoleInfo subjectRole = accessor.GetRoleInfo(pawn);
        bool isDeadUnresurrectableColonist =
          subjectRole.IsColonist && subjectRole.IsDead && !subjectRole.IsResurrectable;
        if (!isDeadUnresurrectableColonist
          && (subjectRole.IsColonist || subjectRole.IsPlayerFaction || subjectRole.IsOnColonistMap))
        {
          continue;
        }

        snapshot.Clear();
        totalSevered += CleanSubjectEntries(
          accessor,
          pawn,
          subjectRole,
          RelationTrigger.OffMapSweep,
          options,
          snapshot,
          logAction);
      }

      return totalSevered;
    }
    finally
    {
      SnapshotBufferCache<TPawn, TRelationDef>.Return(snapshot);
    }
  }

  private static int CleanSubjectEntries<TPawn, TRelationDef, TAccessor>(
    TAccessor accessor,
    TPawn subject,
    in PawnRoleInfo subjectRole,
    RelationTrigger trigger,
    in RelationCleanupOptions options,
    List<DirectRelationEntry<TPawn, TRelationDef>> snapshot,
    Action<string>? logAction)
    where TPawn : class
    where TRelationDef : class
    where TAccessor : struct, IRelationAccessor<TPawn, TRelationDef>
  {
    accessor.SnapshotDirectRelations(subject, snapshot);
    if (snapshot.Count == 0)
    {
      return 0;
    }

    int severed = 0;
    for (int i = 0; i < snapshot.Count; i++)
    {
      DirectRelationEntry<TPawn, TRelationDef> entry = snapshot[i];
      if (entry.Def is null)
      {
        continue;
      }

      RelationCategory category = accessor.GetRelationCategory(entry.Def);
      if (!RelationCleanupPolicy.IsTargetRelation(category, options))
      {
        continue;
      }

      PawnRoleInfo otherRole = accessor.GetRoleInfo(entry.Other);
      if (!RelationCleanupPolicy.ShouldSeverRelation(
        subjectRole, otherRole, category, trigger, options))
      {
        continue;
      }

      if (SeverPairAtomically(accessor, subject, entry.Def, entry.Other))
      {
        severed++;
        if (options.VerboseLogging && logAction is not null)
        {
          logAction(
            $"[{trigger}] Severed {accessor.DescribeRelation(entry.Def)}: " +
            $"{accessor.DescribePawn(subject)} <-> {accessor.DescribePawn(entry.Other)}.");
        }
      }
    }

    return severed;
  }

  /// <summary>
  /// Removes a direct relation between <paramref name="pawnA"/> and <paramref name="pawnB"/>
  /// atomically, acquiring locks in canonical object order to prevent deadlocks.
  /// </summary>
  private static bool SeverPairAtomically<TPawn, TRelationDef, TAccessor>(
    TAccessor accessor,
    TPawn pawnA,
    TRelationDef def,
    TPawn? pawnB)
    where TPawn : class
    where TRelationDef : class
    where TAccessor : struct, IRelationAccessor<TPawn, TRelationDef>
  {
    object? lockA = accessor.GetSyncRoot(pawnA);
    if (lockA is null)
    {
      return false;
    }

    object? lockB = pawnB is not null ? accessor.GetSyncRoot(pawnB) : null;
    if (lockB is null || ReferenceEquals(lockA, lockB))
    {
      lock (lockA)
      {
        bool removed = accessor.RemoveDirectRelation(pawnA, def, pawnB!);
        if (pawnB is not null && ReferenceEquals(lockA, lockB))
        {
          accessor.RemoveDirectRelation(pawnB, def, pawnA);
        }
        return removed;
      }
    }

    int hashA = RuntimeHelpers.GetHashCode(lockA);
    int hashB = RuntimeHelpers.GetHashCode(lockB);

    if (hashA < hashB)
    {
      lock (lockA)
      {
        lock (lockB)
        {
          return RemoveBothSides(accessor, pawnA, def, pawnB!);
        }
      }
    }

    if (hashA > hashB)
    {
      lock (lockB)
      {
        lock (lockA)
        {
          return RemoveBothSides(accessor, pawnA, def, pawnB!);
        }
      }
    }

    // Rare identity-hash collision: serialize acquisition with a static tie-breaker.
    lock (TieBreakerLock)
    {
      lock (lockA)
      {
        lock (lockB)
        {
          return RemoveBothSides(accessor, pawnA, def, pawnB!);
        }
      }
    }
  }

  private static bool RemoveBothSides<TPawn, TRelationDef, TAccessor>(
    TAccessor accessor,
    TPawn pawnA,
    TRelationDef def,
    TPawn pawnB)
    where TPawn : class
    where TRelationDef : class
    where TAccessor : struct, IRelationAccessor<TPawn, TRelationDef>
  {
    bool removedA = accessor.RemoveDirectRelation(pawnA, def, pawnB);
    bool removedB = accessor.RemoveDirectRelation(pawnB, def, pawnA);
    return removedA || removedB;
  }
}
