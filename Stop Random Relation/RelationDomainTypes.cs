namespace Stop_Random_Relation;

/// <summary>
/// Represents the lifecycle event that triggered relationship evaluation.
/// </summary>
public enum RelationTrigger
{
  /// <summary>
  /// A pawn exited a map and returned to the world pool.
  /// </summary>
  MapExit = 0,

  /// <summary>
  /// A pawn died (<c>Pawn.Kill</c>).
  /// </summary>
  Death = 1,

  /// <summary>
  /// A deceased pawn's corpse was destroyed or desiccated beyond revival.
  /// </summary>
  PermanentDeath = 2,

  /// <summary>
  /// Periodic or pre-GC sweep of world pawns outside colonist maps.
  /// </summary>
  OffMapSweep = 3,
}

/// <summary>
/// Broad classification of a <c>PawnRelationDef</c> for cleanup filtering.
/// </summary>
public enum RelationCategory
{
  /// <summary>
  /// Unrelated or non-targeted relation (e.g., animal Bond or Mechanitor Overseer).
  /// </summary>
  None = 0,

  /// <summary>
  /// Biological family relation (<c>familyByBloodRelation</c> or <c>ParentBirth</c>).
  /// </summary>
  Blood = 1,

  /// <summary>
  /// Marriage or romantic affinity relation (Spouse, Fiance, Lover, ExSpouse, ExLover).
  /// </summary>
  MarriageOrRomance = 2,
}

/// <summary>
/// Pure-value projection of a pawn's domain state used by cleanup policies.
/// </summary>
/// <param name="IsColonist">
/// <c>true</c> if the pawn is a free colonist belonging to the player faction.
/// </param>
/// <param name="IsPlayerFaction">
/// <c>true</c> if the pawn belongs to the player faction (including colony animals/mechs).
/// </param>
/// <param name="IsDead">
/// <c>true</c> if the pawn is currently dead.
/// </param>
/// <param name="IsResurrectable">
/// <c>true</c> if the pawn is dead but still possesses a non-destroyed,
/// non-desiccated corpse (or active resurrection capability).
/// </param>
/// <param name="IsOnColonistMap">
/// <c>true</c> if the pawn is spawned or held on an active colonist map.
/// </param>
/// <param name="IsActiveFactionLeader">
/// <c>true</c> if the pawn is alive and designated as a faction leader.
/// </param>
public readonly record struct PawnRoleInfo(
  bool IsColonist,
  bool IsPlayerFaction,
  bool IsDead,
  bool IsResurrectable,
  bool IsOnColonistMap,
  bool IsActiveFactionLeader);

/// <summary>
/// Represents a direct relation entry captured during snapshot iteration.
/// </summary>
/// <typeparam name="TPawn">The pawn representation type.</typeparam>
/// <typeparam name="TRelationDef">The relation definition type.</typeparam>
/// <param name="Def">The relation definition.</param>
/// <param name="Other">The related pawn.</param>
public readonly record struct DirectRelationEntry<TPawn, TRelationDef>(
  TRelationDef Def,
  TPawn Other);

/// <summary>
/// Zero-allocation adapter contract allowing the relation cleanup engine to
/// inspect and mutate either live RimWorld pawns or test doubles without reflection.
/// </summary>
/// <typeparam name="TPawn">The pawn representation type.</typeparam>
/// <typeparam name="TRelationDef">The relation definition type.</typeparam>
public interface IRelationAccessor<TPawn, TRelationDef>
  where TPawn : class
  where TRelationDef : class
{
  /// <summary>
  /// Returns the synchronization lock object for the specified pawn's relation tracker,
  /// or <c>null</c> if the pawn has no relation tracker.
  /// </summary>
  /// <param name="pawn">The pawn whose relation lock object is requested.</param>
  /// <returns>An object suitable for <c>lock</c>, or <c>null</c>.</returns>
  object? GetSyncRoot(TPawn? pawn);

  /// <summary>
  /// Evaluates the domain role snapshot of the specified pawn.
  /// </summary>
  /// <param name="pawn">The pawn to evaluate.</param>
  /// <returns>A <see cref="PawnRoleInfo"/> value describing the pawn.</returns>
  PawnRoleInfo GetRoleInfo(TPawn? pawn);

  /// <summary>
  /// Classifies a relation definition into a <see cref="RelationCategory"/>.
  /// </summary>
  /// <param name="def">The relation definition to classify.</param>
  /// <returns>The relation category.</returns>
  RelationCategory GetRelationCategory(TRelationDef? def);

  /// <summary>
  /// Returns <c>true</c> if <paramref name="pawn"/> currently has at least one
  /// direct relation, allowing <c>O(1)</c> fast-skipping before snapshotting.
  /// </summary>
  /// <param name="pawn">The pawn to inspect.</param>
  /// <returns><c>true</c> if one or more direct relations exist; otherwise <c>false</c>.</returns>
  bool HasAnyDirectRelations(TPawn pawn);

  /// <summary>
  /// Copies the pawn's direct relations into <paramref name="buffer"/> under lock
  /// to allow safe iteration without collection-modification exceptions.
  /// </summary>
  /// <param name="pawn">The pawn whose direct relations are snapshotted.</param>
  /// <param name="buffer">Caller-owned list populated with snapshots.</param>
  void SnapshotDirectRelations(
    TPawn pawn,
    List<DirectRelationEntry<TPawn, TRelationDef>> buffer);

  /// <summary>
  /// Removes the specified direct relation from <paramref name="pawn"/>.
  /// </summary>
  /// <param name="pawn">The pawn whose relation list is mutated.</param>
  /// <param name="def">The relation definition to remove.</param>
  /// <param name="other">The other pawn in the direct relation.</param>
  /// <returns><c>true</c> if a matching relation existed and was removed; otherwise <c>false</c>.</returns>
  bool RemoveDirectRelation(TPawn pawn, TRelationDef def, TPawn other);

  /// <summary>
  /// Returns a human-readable label for diagnostic logging.
  /// </summary>
  /// <param name="pawn">The pawn to describe.</param>
  /// <returns>A short display string.</returns>
  string DescribePawn(TPawn? pawn);

  /// <summary>
  /// Returns a human-readable label for a relation definition.
  /// </summary>
  /// <param name="def">The relation definition to describe.</param>
  /// <returns>A short display string.</returns>
  string DescribeRelation(TRelationDef? def);
}
