using Xunit;

namespace Stop_Random_Relation.Tests;

public class RelationCleanupEngineTests
{
  private sealed class FakeRelationDef
  {
    public string Name { get; }
    public RelationCategory Category { get; }

    public FakeRelationDef(string name, RelationCategory category)
    {
      Name = name;
      Category = category;
    }
  }

  private sealed class FakePawn
  {
    public string Id { get; }
    public PawnRoleInfo Role { get; set; }
    public object SyncRoot { get; } = new();
    public List<(FakeRelationDef Def, FakePawn Other)> Relations { get; } = [];

    public FakePawn(string id, PawnRoleInfo role)
    {
      Id = id;
      Role = role;
    }

    public void AddMutualRelation(FakeRelationDef def, FakePawn other)
    {
      lock (SyncRoot)
      {
        Relations.Add((def, other));
      }
      lock (other.SyncRoot)
      {
        other.Relations.Add((def, this));
      }
    }

    public int RelationCount
    {
      get
      {
        lock (SyncRoot)
        {
          return Relations.Count;
        }
      }
    }
  }

  private readonly struct FakeRelationAccessor : IRelationAccessor<FakePawn, FakeRelationDef>
  {
    public object? GetSyncRoot(FakePawn? pawn) => pawn?.SyncRoot;

    public PawnRoleInfo GetRoleInfo(FakePawn? pawn) => pawn?.Role ?? default;

    public RelationCategory GetRelationCategory(FakeRelationDef? def) =>
      def?.Category ?? RelationCategory.None;

    public bool HasAnyDirectRelations(FakePawn pawn)
    {
      lock (pawn.SyncRoot)
      {
        return pawn.Relations.Count > 0;
      }
    }

    public void SnapshotDirectRelations(
      FakePawn pawn,
      List<DirectRelationEntry<FakePawn, FakeRelationDef>> buffer)
    {
      lock (pawn.SyncRoot)
      {
        foreach ((FakeRelationDef def, FakePawn other) in pawn.Relations)
        {
          buffer.Add(new DirectRelationEntry<FakePawn, FakeRelationDef>(def, other));
        }
      }
    }

    public bool RemoveDirectRelation(FakePawn pawn, FakeRelationDef def, FakePawn other)
    {
      for (int i = pawn.Relations.Count - 1; i >= 0; i--)
      {
        (FakeRelationDef d, FakePawn o) = pawn.Relations[i];
        if (ReferenceEquals(d, def) && ReferenceEquals(o, other))
        {
          pawn.Relations.RemoveAt(i);
          return true;
        }
      }

      return false;
    }

    public string DescribePawn(FakePawn? pawn) => pawn?.Id ?? "<null>";

    public string DescribeRelation(FakeRelationDef? def) => def?.Name ?? "<null>";
  }

  private static readonly FakeRelationDef BloodSibling = new("Sibling", RelationCategory.Blood);
  private static readonly FakeRelationDef MarriageSpouse = new("Spouse", RelationCategory.MarriageOrRomance);
  private static readonly FakeRelationDef AnimalBond = new("Bond", RelationCategory.None);

  private static readonly PawnRoleInfo ColonistRole = new(true, true, false, false, true, false);
  private static readonly PawnRoleInfo WorldOffMapRole = new(false, false, false, false, false, false);
  private static readonly PawnRoleInfo WorldOnMapRole = new(false, false, false, false, true, false);

  [Fact]
  public void SeverRelations_RemovesDirectedAndReciprocalRelations_PreservesUnmatched()
  {
    var raider = new FakePawn("Raider", WorldOffMapRole);
    var colonist = new FakePawn("Colonist", ColonistRole);
    var bondedAnimal = new FakePawn("Wolf", WorldOnMapRole);

    raider.AddMutualRelation(BloodSibling, colonist);
    raider.AddMutualRelation(AnimalBond, bondedAnimal);

    var logs = new List<string>();
    int severed = RelationCleanupEngine.SeverRelations<FakePawn, FakeRelationDef, FakeRelationAccessor>(
      default,
      raider,
      def => def.Category == RelationCategory.Blood,
      logs.Add);

    Assert.Equal(1, severed);
    Assert.Single(logs);
    Assert.Equal(0, colonist.RelationCount);
    Assert.Equal(1, raider.RelationCount);
    Assert.Equal(1, bondedAnimal.RelationCount);
  }

  [Fact]
  public void CleanForTrigger_TriadicRelations_PreservesRemainingColonistTree()
  {
    // Colonist A <-> Colonist B <-> Raider C (C exits map).
    var colonistA = new FakePawn("ColonistA", ColonistRole);
    var colonistB = new FakePawn("ColonistB", ColonistRole);
    var raiderC = new FakePawn("RaiderC", WorldOffMapRole);

    colonistA.AddMutualRelation(BloodSibling, colonistB);
    colonistB.AddMutualRelation(BloodSibling, raiderC);

    int severed = RelationCleanupEngine.CleanForTrigger<FakePawn, FakeRelationDef, FakeRelationAccessor>(
      default,
      raiderC,
      RelationTrigger.MapExit,
      RelationCleanupOptions.Default);

    Assert.Equal(1, severed);
    Assert.Equal(0, raiderC.RelationCount);
    Assert.Equal(1, colonistA.RelationCount);
    Assert.Equal(1, colonistB.RelationCount);
  }

  [Fact]
  public void CleanForTrigger_MarriageRelation_RespectsIncludeNonBloodToggle()
  {
    var raider = new FakePawn("Raider", WorldOffMapRole);
    var colonist = new FakePawn("Colonist", ColonistRole);
    raider.AddMutualRelation(MarriageSpouse, colonist);

    RelationCleanupOptions skipMarriage = RelationCleanupOptions.Default with
    {
      IncludeNonBloodRelations = false,
    };

    int severedWhenDisabled = RelationCleanupEngine.CleanForTrigger<FakePawn, FakeRelationDef, FakeRelationAccessor>(
      default,
      raider,
      RelationTrigger.MapExit,
      skipMarriage);

    Assert.Equal(0, severedWhenDisabled);
    Assert.Equal(1, raider.RelationCount);
    Assert.Equal(1, colonist.RelationCount);

    int severedWhenEnabled = RelationCleanupEngine.CleanForTrigger<FakePawn, FakeRelationDef, FakeRelationAccessor>(
      default,
      raider,
      RelationTrigger.MapExit,
      RelationCleanupOptions.Default);

    Assert.Equal(1, severedWhenEnabled);
    Assert.Equal(0, raider.RelationCount);
    Assert.Equal(0, colonist.RelationCount);
  }

  [Fact]
  public void CleanOffMapWorldPawns_SeversOffMapPairs_PreservesOnMapAndColonistTies()
  {
    var offMapA = new FakePawn("OffMapA", WorldOffMapRole);
    var offMapB = new FakePawn("OffMapB", WorldOffMapRole);
    var onMapVisitor = new FakePawn("Visitor", WorldOnMapRole);
    var colonist = new FakePawn("Colonist", ColonistRole);

    offMapA.AddMutualRelation(BloodSibling, offMapB);
    offMapA.AddMutualRelation(MarriageSpouse, colonist);
    onMapVisitor.AddMutualRelation(BloodSibling, offMapB);

    var logs = new List<string>();
    RelationCleanupOptions options = RelationCleanupOptions.Default with
    {
      VerboseLogging = true,
    };

    int severed = RelationCleanupEngine.CleanOffMapWorldPawns<FakePawn, FakeRelationDef, FakeRelationAccessor>(
      default,
      [offMapA, offMapB, onMapVisitor, colonist],
      options,
      logs.Add);

    // Only OffMapA <-> OffMapB should be severed.
    Assert.Equal(1, severed);
    Assert.Single(logs);
    Assert.Equal(1, offMapA.RelationCount); // Still tied to Colonist.
    Assert.Equal(1, colonist.RelationCount);
    Assert.Equal(1, onMapVisitor.RelationCount); // On colonist map -> kept.
    Assert.Equal(1, offMapB.RelationCount); // Still tied to on-map Visitor.
  }

  [Fact]
  public void CleanOffMapWorldPawns_CleansDeadUnresurrectableColonist_PreservesResurrectable()
  {
    var deadPermColonist = new FakePawn(
      "DeadPermColonist",
      new PawnRoleInfo(true, true, true, false, false, false));
    var deadResColonist = new FakePawn(
      "DeadResColonist",
      new PawnRoleInfo(true, true, true, true, true, false));
    var livingColonist = new FakePawn("LivingColonist", ColonistRole);

    deadPermColonist.AddMutualRelation(BloodSibling, livingColonist);
    deadResColonist.AddMutualRelation(BloodSibling, livingColonist);

    int severed = RelationCleanupEngine.CleanOffMapWorldPawns<FakePawn, FakeRelationDef, FakeRelationAccessor>(
      default,
      [deadPermColonist, deadResColonist, livingColonist],
      RelationCleanupOptions.Default);

    Assert.Equal(1, severed);
    Assert.Equal(0, deadPermColonist.RelationCount);
    Assert.Equal(1, deadResColonist.RelationCount);
    Assert.Equal(1, livingColonist.RelationCount);
  }

  private readonly struct CountingEmptySkipAccessor : IRelationAccessor<FakePawn, FakeRelationDef>
  {
    private readonly Action _onSnapshotOrRoleEvaluated;

    public CountingEmptySkipAccessor(Action onSnapshotOrRoleEvaluated)
    {
      _onSnapshotOrRoleEvaluated = onSnapshotOrRoleEvaluated;
    }

    public object? GetSyncRoot(FakePawn? pawn) => pawn?.SyncRoot;

    public PawnRoleInfo GetRoleInfo(FakePawn? pawn)
    {
      _onSnapshotOrRoleEvaluated();
      return pawn?.Role ?? default;
    }

    public RelationCategory GetRelationCategory(FakeRelationDef? def) =>
      def?.Category ?? RelationCategory.None;

    public bool HasAnyDirectRelations(FakePawn pawn)
    {
      lock (pawn.SyncRoot)
      {
        return pawn.Relations.Count > 0;
      }
    }

    public void SnapshotDirectRelations(
      FakePawn pawn,
      List<DirectRelationEntry<FakePawn, FakeRelationDef>> buffer)
    {
      _onSnapshotOrRoleEvaluated();
    }

    public bool RemoveDirectRelation(FakePawn pawn, FakeRelationDef def, FakePawn other) => false;
    public string DescribePawn(FakePawn? pawn) => pawn?.Id ?? "<null>";
    public string DescribeRelation(FakeRelationDef? def) => def?.Name ?? "<null>";
  }

  [Fact]
  public void CleanOffMapWorldPawns_SkipsEmptyPawnsInO1WithoutEvaluatingRoleOrSnapshot()
  {
    var emptyPawn1 = new FakePawn("Empty1", WorldOffMapRole);
    var emptyPawn2 = new FakePawn("Empty2", WorldOffMapRole);
    int expensiveEvaluations = 0;
    var accessor = new CountingEmptySkipAccessor(() => expensiveEvaluations++);

    int severed = RelationCleanupEngine.CleanOffMapWorldPawns<FakePawn, FakeRelationDef, CountingEmptySkipAccessor>(
      accessor,
      [emptyPawn1, emptyPawn2],
      RelationCleanupOptions.Default);

    Assert.Equal(0, severed);
    Assert.Equal(0, expensiveEvaluations);
  }

  [Fact]
  public async Task CleanForTrigger_ConcurrentOpposingPairs_IsThreadSafeAndDeadlockFree()
  {
    const int pairCount = 100;
    var pairs = new (FakePawn Left, FakePawn Right)[pairCount];
    for (int i = 0; i < pairCount; i++)
    {
      var left = new FakePawn($"Left_{i}", WorldOffMapRole);
      var right = new FakePawn($"Right_{i}", ColonistRole);
      left.AddMutualRelation(BloodSibling, right);
      left.AddMutualRelation(MarriageSpouse, right);
      pairs[i] = (left, right);
    }

    var options = RelationCleanupOptions.Default;

    // Hammer both sides concurrently in opposite lock orders to test deadlock freedom.
    Task[] tasks = Enumerable.Range(0, pairCount * 2).Select(idx => Task.Run(() =>
    {
      (FakePawn left, FakePawn right) = pairs[idx % pairCount];
      if (idx % 2 == 0)
      {
        RelationCleanupEngine.CleanForTrigger<FakePawn, FakeRelationDef, FakeRelationAccessor>(
          default, left, RelationTrigger.MapExit, options);
      }
      else
      {
        RelationCleanupEngine.CleanForTrigger<FakePawn, FakeRelationDef, FakeRelationAccessor>(
          default, right, RelationTrigger.PermanentDeath, options);
      }
    })).ToArray();

    await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));

    foreach ((FakePawn left, FakePawn right) in pairs)
    {
      Assert.Equal(0, left.RelationCount);
      Assert.Equal(0, right.RelationCount);
    }
  }

  private sealed class FakeWorldPawn
  {
    public string Id { get; }
    public WorldPawnPruneInfo Info { get; set; }
    public object SyncRoot { get; } = new();
    public bool Discarded { get; set; }
    public bool AllowDiscard { get; set; } = true;

    public FakeWorldPawn(string id, WorldPawnPruneInfo info)
    {
      Id = id;
      Info = info;
    }
  }

  private readonly struct FakeWorldPawnPruneAccessor : IWorldPawnPruneAccessor<FakeWorldPawn>
  {
    private readonly Action? _onInspect;

    public FakeWorldPawnPruneAccessor(Action? onInspect = null)
    {
      _onInspect = onInspect;
    }

    public WorldPawnPruneInfo GetWorldPawnPruneInfo(FakeWorldPawn? pawn)
    {
      _onInspect?.Invoke();
      return pawn?.Info ?? default;
    }

    public bool TryDiscardWorldPawn(FakeWorldPawn pawn)
    {
      if (!pawn.AllowDiscard)
      {
        return false;
      }

      lock (pawn.SyncRoot)
      {
        if (pawn.Discarded)
        {
          return false;
        }

        pawn.Discarded = true;
        return true;
      }
    }

    public string DescribePawn(FakeWorldPawn? pawn) => pawn?.Id ?? "<null>";
  }

  [Fact]
  public void PruneUnprotectedOffMapWorldPawns_SkipsInO1WhenDisabledOrBelowTotalCount()
  {
    int inspections = 0;
    var accessor = new FakeWorldPawnPruneAccessor(() => inspections++);
    var candidate = new FakeWorldPawn(
      "P1",
      new WorldPawnPruneInfo(WorldOffMapRole, false, false, false));

    int deletedDisabled = RelationCleanupEngine.PruneUnprotectedOffMapWorldPawns(
      accessor,
      [candidate],
      RelationCleanupOptions.Default with { UnprotectedOffMapWorldPawnThreshold = 1 });
    Assert.Equal(0, deletedDisabled);
    Assert.Equal(0, inspections);

    RelationCleanupOptions thresholdTwo = RelationCleanupOptions.Default with
    {
      DeleteUnprotectedOffMapWorldPawns = true,
      UnprotectedOffMapWorldPawnThreshold = 2,
    };
    int deletedBelowTotal = RelationCleanupEngine.PruneUnprotectedOffMapWorldPawns(
      accessor,
      [candidate],
      thresholdTwo);
    Assert.Equal(0, deletedBelowTotal);
    Assert.Equal(0, inspections);
  }

  [Fact]
  public void PruneUnprotectedOffMapWorldPawns_DoesNotDeleteWhenEligibleBelowThreshold()
  {
    var eligible = new FakeWorldPawn(
      "Eligible",
      new WorldPawnPruneInfo(WorldOffMapRole, false, false, false));
    var onMap = new FakeWorldPawn(
      "OnMap",
      new WorldPawnPruneInfo(WorldOnMapRole, true, false, false));
    var leader = new FakeWorldPawn(
      "Leader",
      new WorldPawnPruneInfo(new PawnRoleInfo(false, false, false, false, false, true), false, false, false));
    var questPawn = new FakeWorldPawn(
      "Quest",
      new WorldPawnPruneInfo(WorldOffMapRole, false, true, false));
    var related = new FakeWorldPawn(
      "Related",
      new WorldPawnPruneInfo(WorldOffMapRole, false, false, true));

    int? notifiedCount = null;
    RelationCleanupOptions options = RelationCleanupOptions.Default with
    {
      DeleteUnprotectedOffMapWorldPawns = true,
      UnprotectedOffMapWorldPawnThreshold = 2,
    };

    int deleted = RelationCleanupEngine.PruneUnprotectedOffMapWorldPawns(
      new FakeWorldPawnPruneAccessor(),
      [eligible, onMap, leader, questPawn, related],
      options,
      c => notifiedCount = c);

    Assert.Equal(0, deleted);
    Assert.Null(notifiedCount);
    Assert.False(eligible.Discarded);
    Assert.False(onMap.Discarded);
    Assert.False(leader.Discarded);
    Assert.False(questPawn.Discarded);
    Assert.False(related.Discarded);
  }

  [Fact]
  public void PruneUnprotectedOffMapWorldPawns_DeletesAllEligibleAtOrAboveThreshold_AndReportsCount()
  {
    var eligible1 = new FakeWorldPawn(
      "Eligible1",
      new WorldPawnPruneInfo(WorldOffMapRole, false, false, false));
    var eligible2 = new FakeWorldPawn(
      "Eligible2",
      new WorldPawnPruneInfo(WorldOffMapRole, false, false, false));
    var eligible3 = new FakeWorldPawn(
      "Eligible3",
      new WorldPawnPruneInfo(new PawnRoleInfo(false, false, true, false, false, false), false, false, false));
    var colonist = new FakeWorldPawn(
      "Colonist",
      new WorldPawnPruneInfo(new PawnRoleInfo(true, true, false, false, false, false), false, false, false));
    var questPawn = new FakeWorldPawn(
      "Quest",
      new WorldPawnPruneInfo(WorldOffMapRole, false, true, false));
    var related = new FakeWorldPawn(
      "Related",
      new WorldPawnPruneInfo(WorldOffMapRole, false, false, true));

    var notifications = new List<int>();
    var logs = new List<string>();
    RelationCleanupOptions options = RelationCleanupOptions.Default with
    {
      DeleteUnprotectedOffMapWorldPawns = true,
      UnprotectedOffMapWorldPawnThreshold = 2,
      VerboseLogging = true,
    };

    int deleted = RelationCleanupEngine.PruneUnprotectedOffMapWorldPawns(
      new FakeWorldPawnPruneAccessor(),
      [eligible1, colonist, eligible2, questPawn, related, eligible3],
      options,
      notifications.Add,
      logs.Add);

    Assert.Equal(3, deleted);
    Assert.Equal([3], notifications);
    Assert.Equal(3, logs.Count);
    Assert.True(eligible1.Discarded);
    Assert.True(eligible2.Discarded);
    Assert.True(eligible3.Discarded);
    Assert.False(colonist.Discarded);
    Assert.False(questPawn.Discarded);
    Assert.False(related.Discarded);
  }

  [Fact]
  public void PruneUnprotectedOffMapWorldPawns_DoesNotNotifyWhenAllDiscardsFail()
  {
    var eligible1 = new FakeWorldPawn(
      "E1",
      new WorldPawnPruneInfo(WorldOffMapRole, false, false, false))
    {
      AllowDiscard = false,
    };
    var eligible2 = new FakeWorldPawn(
      "E2",
      new WorldPawnPruneInfo(WorldOffMapRole, false, false, false))
    {
      AllowDiscard = false,
    };

    var notifications = new List<int>();
    RelationCleanupOptions options = RelationCleanupOptions.Default with
    {
      DeleteUnprotectedOffMapWorldPawns = true,
      UnprotectedOffMapWorldPawnThreshold = 2,
    };

    int deleted = RelationCleanupEngine.PruneUnprotectedOffMapWorldPawns(
      new FakeWorldPawnPruneAccessor(),
      [eligible1, eligible2],
      options,
      notifications.Add);

    Assert.Equal(0, deleted);
    Assert.Empty(notifications);
  }

  [Fact]
  public void PruneUnprotectedOffMapWorldPawns_StopsScanningEarlyWhenRemainingCannotReachThreshold()
  {
    int inspections = 0;
    var accessor = new FakeWorldPawnPruneAccessor(() => inspections++);
    var protectedInfo = new WorldPawnPruneInfo(WorldOnMapRole, true, false, false);
    var eligibleInfo = new WorldPawnPruneInfo(WorldOffMapRole, false, false, false);

    // Total = 4, threshold = 3. After 2 protected pawns, remaining 2 cannot reach 3.
    var pawns = new[]
    {
      new FakeWorldPawn("Protected1", protectedInfo),
      new FakeWorldPawn("Protected2", protectedInfo),
      new FakeWorldPawn("Eligible1", eligibleInfo),
      new FakeWorldPawn("Eligible2", eligibleInfo),
    };

    RelationCleanupOptions options = RelationCleanupOptions.Default with
    {
      DeleteUnprotectedOffMapWorldPawns = true,
      UnprotectedOffMapWorldPawnThreshold = 3,
    };

    int deleted = RelationCleanupEngine.PruneUnprotectedOffMapWorldPawns(
      accessor,
      pawns,
      options);

    Assert.Equal(0, deleted);
    Assert.Equal(2, inspections);
  }

  [Fact]
  public async Task PruneUnprotectedOffMapWorldPawns_ConcurrentCalls_AreThreadSafe()
  {
    var pawns = Enumerable.Range(0, 60)
      .Select(i => new FakeWorldPawn(
        $"Pawn_{i}",
        new WorldPawnPruneInfo(WorldOffMapRole, false, false, false)))
      .ToList();

    RelationCleanupOptions options = RelationCleanupOptions.Default with
    {
      DeleteUnprotectedOffMapWorldPawns = true,
      UnprotectedOffMapWorldPawnThreshold = 10,
    };

    int totalReported = 0;
    Task[] tasks = Enumerable.Range(0, 8)
      .Select(_ => Task.Run(() =>
      {
        RelationCleanupEngine.PruneUnprotectedOffMapWorldPawns(
          new FakeWorldPawnPruneAccessor(),
          pawns,
          options,
          count => Interlocked.Add(ref totalReported, count));
      }))
      .ToArray();

    await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));

    Assert.Equal(60, totalReported);
    Assert.All(pawns, p => Assert.True(p.Discarded));
  }
}
