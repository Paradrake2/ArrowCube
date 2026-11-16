using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Topology;
using NUnit.Framework;

namespace ArrowCube.Tests
{
    public class MultiBlockerTests
    {
        private static SurfaceCoordinate F(int x,int y) => new SurfaceCoordinate(CubeFace.Front,x,y);
        internal static PuzzleDefinition Fixture(int count,ExitRules rules,bool crossFace=false,bool earlyGate=false)
        {
            var t=new CubeTopology(8); var paths=new List<ArrowPath> {
                new ArrowPath(0,t,new[] { F(0,3),new SurfaceCoordinate(CubeFace.Left,7,3) },SurfaceDirection.Right) };
            var gates=new List<ExitGate> { new ExitGate(earlyGate ? F(3,3) : new SurfaceCoordinate(CubeFace.Right,1,3),SurfaceDirection.Right) };
            for(int i=0;i<count;i++)
            {
                int x=2+i*2;
                var cells=crossFace && i==1 ? new[] { new SurfaceCoordinate(CubeFace.Top,x,0),F(x,7),F(x,6),F(x,5),F(x,4),F(x,3) } : new[] { F(x,3),F(x,2) };
                paths.Add(new ArrowPath(i+1,t,cells,SurfaceDirection.Up));
                gates.Add(new ExitGate(crossFace && i==1 ? new SurfaceCoordinate(CubeFace.Top,x,7) : F(x,7),SurfaceDirection.Up));
            }
            return new PuzzleDefinition("Independent blockers",t,paths,new ExitMap(t,gates),rules);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void IndependentBlockersAreConfirmedByEverySubsetAndActualGameplay(int count)
        {
            var puzzle=Fixture(count,ExitRules.EdgeOnly); var graph=PuzzleDependencyGraph.Analyze(puzzle); var node=graph.GetNode(0);
            Assert.That(graph.IsValid, Is.True); Assert.That(node.ConfirmedBlockers.Length, Is.EqualTo(count));
            Assert.That(node.IndependentBlockers.Length, Is.EqualTo(count)); Assert.That(node.ExhaustiveSubsetsVerified, Is.True);
            Assert.That(node.SubsetChecks, Is.EqualTo(1<<count));
            Assert.That(PuzzleDependencyGraph.VerifyIndependentBlockers(puzzle,0,Enumerable.Range(1,count).ToArray()), Is.True);
            var sim=new PathMovementSimulator();
            for(int removed=0;removed<(1<<count);removed++)
            {
                var grid=puzzle.CreateGrid();
                for(int i=0;i<count;i++) if((removed&(1<<i))!=0) grid.RemovePath(i+1);
                Assert.That(sim.Simulate(grid,0,puzzle.Exits,puzzle.Rules).Succeeded, Is.EqualTo(removed==(1<<count)-1));
            }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void FullBodySweepsAndCrossFaceBlockersRespectEveryExitMode(int mode)
        {
            var puzzle=Fixture(3,PuzzleGeneratorTests.Rules(mode),true);
            var cache=new RemovalTrajectoryCache(); var sim=new PathMovementSimulator();
            foreach(var path in puzzle.Paths)
            {
                var trajectory=cache.Get(puzzle.Topology,path,puzzle.Exits,puzzle.Rules);
                var plan=sim.Simulate(new SurfaceGrid(puzzle.Topology,new[] { path }),path.Id,puzzle.Exits,puzzle.Rules);
                var explicitCells=new HashSet<SurfaceCoordinate>();
                var explicitSegments=new HashSet<SurfaceSegment>();
                for(int step=0;step<=plan.StepCount;step++)
                for(int segment=0;segment<path.Cells.Count;segment++)
                {
                    var position=plan.GetPosition(step,segment);
                    Assert.That(trajectory.GetBodyPosition(step,segment), Is.EqualTo(position));
                    if(position.IsOnSurface) explicitCells.Add(position.Cell);
                    if(segment>0)
                    {
                        var previous=plan.GetPosition(step,segment-1);
                        if(position.IsOnSurface && previous.IsOnSurface) explicitSegments.Add(new SurfaceSegment(puzzle.Topology,position.Cell,previous.Cell));
                    }
                }
                Assert.That(trajectory.SweptCells, Is.EquivalentTo(explicitCells)); Assert.That(trajectory.HasValidExit, Is.True);
                Assert.That(trajectory.SweptSegments, Is.EquivalentTo(explicitSegments));
                foreach(var visit in trajectory.Visits)
                for(int step=visit.FirstStep;step<=visit.LastStep;step++)
                    Assert.That(Enumerable.Range(0,path.Cells.Count).Any(segment=>plan.GetPosition(step,segment).IsOnSurface && plan.GetPosition(step,segment).Cell==visit.Cell), Is.True);
            }
            var graph=PuzzleDependencyGraph.Analyze(puzzle);
            Assert.That(graph.GetNode(0).ConfirmedBlockers.Length, Is.EqualTo(3)); Assert.That(graph.GetNode(0).IndependentBlockers.Length, Is.EqualTo(3));
            Assert.That(graph.Edges.Any(edge=>edge.Target==0 && edge.Blocker==2 && edge.CrossFaceGeometry), Is.True);
            Assert.That(graph.Edges.Count(edge=>edge.CrossFaceGeometry), Is.EqualTo(1),"The target's unrelated cross-face tail cannot turn same-face blockers into cross-face dependencies.");
            Assert.That(graph.Metrics.CrossFaceGeometryFraction, Is.GreaterThan(0));
            Assert.That(new PuzzleValidator().Validate(puzzle).Succeeded, Is.True);
        }
        [Test]
        public void SeveralIntersectionsWithOneBodyDoNotInventMultipleBlockersAndEarlyGatesTruncateSweeps()
        {
            var t=new CubeTopology(8);
            var target=new ArrowPath(0,t,new[] { F(0,3),new SurfaceCoordinate(CubeFace.Left,7,3) },SurfaceDirection.Right);
            var blocker=new ArrowPath(1,t,new[] { F(2,3),F(2,2),F(3,2),F(3,3),F(4,3) },SurfaceDirection.Up);
            var puzzle=new PuzzleDefinition("Multiple contacts",t,new[] { target,blocker },new ExitMap(t,Array.Empty<ExitGate>()),ExitRules.EdgeOnly);
            var node=PuzzleDependencyGraph.Analyze(puzzle).GetNode(0);
            Assert.That(node.Interferences[0].X.Length, Is.GreaterThanOrEqualTo(3)); Assert.That(node.ConfirmedBlockers, Is.EqualTo(new[] { 1 }));
            var gated=Fixture(3,ExitRules.GateOnly,earlyGate:true);
            Assert.That(PuzzleDependencyGraph.Analyze(gated).GetNode(0).ConfirmedBlockers, Is.EqualTo(new[] { 1 }));
        }
        [Test]
        public void RedundantChainBlockersAreNotReportedAsIndependentBranches()
        {
            var t=new CubeTopology(8); var paths=new[] {
                new ArrowPath(0,t,new[] { F(0,3),new SurfaceCoordinate(CubeFace.Left,7,3) },SurfaceDirection.Right),
                new ArrowPath(1,t,new[] { F(2,3),F(2,2),F(2,1),F(3,1),F(4,1) },SurfaceDirection.Up),
                new ArrowPath(2,t,new[] { F(4,3),F(4,4) },SurfaceDirection.Down) };
            var puzzle=new PuzzleDefinition("Redundant chain",t,paths,new ExitMap(t,Array.Empty<ExitGate>()),ExitRules.EdgeOnly);
            var graph=PuzzleDependencyGraph.Analyze(puzzle);
            Assert.That(graph.IsValid, Is.True); Assert.That(graph.GetNode(0).ConfirmedBlockers.Length, Is.EqualTo(2));
            Assert.That(graph.GetNode(0).IndependentBlockers.Length, Is.EqualTo(1));
            Assert.That(PuzzleDependencyGraph.VerifyIndependentBlockers(puzzle,0,new[] { 1,2 }), Is.False);
        }
        [Test]
        public void CyclesAreDetectedAndNeverTreatedAsAValidDependencyOrder()
        {
            var t=new CubeTopology(8); var paths=new[] {
                new ArrowPath(0,t,new[] { F(1,1),F(0,1) },SurfaceDirection.Right),
                new ArrowPath(1,t,new[] { F(1,3),F(1,4),F(2,4),F(2,3),F(2,2),F(2,1) },SurfaceDirection.Down) };
            var puzzle=new PuzzleDefinition("Cycle",t,paths,new ExitMap(t,Array.Empty<ExitGate>()),ExitRules.EdgeOnly);
            var graph=PuzzleDependencyGraph.Analyze(puzzle);
            Assert.That(graph.HasCycle, Is.True); Assert.That(graph.IsValid, Is.False);
            Assert.That(new PuzzleValidator().Validate(puzzle).Succeeded, Is.False);
            Assert.Throws<ArgumentException>(()=>new PuzzleDifficultyAnalyzer().Analyze(puzzle));
        }
        [Test]
        public void CacheInvalidatesForChangedGeometryExitsOrRulesAndIsBounded()
        {
            var puzzle=Fixture(0,ExitRules.EdgeOnly); var path=puzzle.Paths[0]; var cache=new RemovalTrajectoryCache(2);
            var first=cache.Get(puzzle.Topology,path,puzzle.Exits,puzzle.Rules);
            Assert.That(cache.Get(puzzle.Topology,path,puzzle.Exits,puzzle.Rules), Is.SameAs(first));
            var gate=new ExitMap(puzzle.Topology,new[] { new ExitGate(F(1,3),SurfaceDirection.Right) });
            var shorter=cache.Get(puzzle.Topology,path,gate,ExitRules.Hybrid);
            Assert.That(shorter.SweptCells.Count, Is.LessThan(first.SweptCells.Count));
            var wrapped=cache.Get(puzzle.Topology,path,puzzle.Exits,ExitRules.GateOnly);
            Assert.That(wrapped.ExitType, Is.EqualTo(ExitType.DirectionalGate));
            Assert.That(cache.Get(puzzle.Topology,path,puzzle.Exits,puzzle.Rules), Is.Not.SameAs(first));
            var reversed=new ArrowPath(0,puzzle.Topology,path.Cells.Reverse(),SurfaceDirection.Left);
            Assert.That(cache.Get(puzzle.Topology,reversed,puzzle.Exits,puzzle.Rules).Contains(F(2,3)), Is.False);
            using(var source=new CancellationTokenSource()) { source.Cancel(); Assert.Throws<OperationCanceledException>(()=>PuzzleDependencyGraph.Analyze(puzzle,source.Token)); }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void GenerationIsDeterministicVerifiedAndUsesIndependentMultiBlockers(int mode)
        {
            var preset=DifficultyPresets.All[3]; var settings=preset.CreateSettings(); settings.MaxCandidates=2; settings.OptimizationIterations=1;
            var recipe=new PuzzleRecipe(4,preset.CreateProfile(),PuzzleGeneratorTests.Rules(mode),difficulty:settings);
            var a=new PuzzleGenerator().Generate(recipe); var b=new PuzzleGenerator().Generate(recipe);
            Assert.That(b.Fingerprint, Is.EqualTo(a.Fingerprint)); Assert.That(b.Difficulty.MeasuredScore, Is.EqualTo(a.Difficulty.MeasuredScore));
            Assert.That(a.DependencyGraph.IsValid, Is.True); Assert.That(a.Difficulty.MultiBlocker.IndependentMultiBlockerFraction, Is.GreaterThan(0));
            Assert.That(a.Report.TargetedAttempts, Is.EqualTo(settings.MultiBlocker.TargetedBlockerAttempts));
            Assert.That(a.Report.TargetedCandidateAttempts, Is.LessThanOrEqualTo(settings.MultiBlocker.TargetedBlockerAttempts*settings.CandidatePoolSize));
            Assert.That(new PuzzleValidator().Validate(a,a.VerifiedSolution).Succeeded, Is.True);
        }
        [Test]
        public void UnityMatchesTheVersion4DotnetGolden()
        {
            var preset=DifficultyPresets.All[1];
            var p=new PuzzleGenerator().Generate(new PuzzleRecipe(0,preset.CreateProfile(),generationVersion:4,difficulty:preset.CreateSettings()));
            Assert.That(p.Fingerprint, Is.EqualTo("c3cb0b4c167cbc504de84ba665147442f214f92695d9d57421e4eff42388f721"));
        }
        [TestCase(5)] [TestCase(14)]
        public void LargerBlockerSetsUsePairProofsWithoutClaimingExhaustiveSubsetEnumeration(int count)
        {
            var t=new CubeTopology(32); var paths=new List<ArrowPath> {
                new ArrowPath(0,t,new[] { F(0,3),new SurfaceCoordinate(CubeFace.Left,31,3) },SurfaceDirection.Right) };
            for(int i=1;i<=count;i++) paths.Add(new ArrowPath(i,t,new[] { F(i*2,3),F(i*2,2) },SurfaceDirection.Up));
            var puzzle=new PuzzleDefinition("Bounded subsets",t,paths,new ExitMap(t,Array.Empty<ExitGate>()),ExitRules.EdgeOnly);
            var graph=PuzzleDependencyGraph.Analyze(puzzle); var node=graph.GetNode(0);
            Assert.That(graph.IsValid, Is.True); Assert.That(node.ConfirmedBlockers.Length, Is.EqualTo(count));
            Assert.That(node.IndependentBlockers.Length, Is.EqualTo(count)); Assert.That(node.SubsetVerificationPassed, Is.True);
            Assert.That(node.SubsetChecks, Is.EqualTo(2)); Assert.That(node.ExhaustiveSubsetsVerified, Is.False);
            Assert.That(node.IndependentWidthExact, Is.EqualTo(count<=12));
        }
        [Test]
        public void DisabledFeatureAndCustomDistributionsAreRespectedAndSnapshotsAreDeep()
        {
            var preset=DifficultyPresets.All[2]; var settings=preset.CreateSettings(); settings.MultiBlocker.Enabled=false;
            settings.MaxCandidates=1; settings.OptimizationIterations=0;
            var recipe=new PuzzleRecipe(9,preset.CreateProfile(),difficulty:settings);
            settings.MultiBlocker.Enabled=true; settings.MultiBlocker.BlockerDistribution[0]=99;
            var p=new PuzzleGenerator().Generate(recipe);
            Assert.That(p.Report.TargetedAttempts, Is.Zero); Assert.That(p.Recipe.Difficulty.MultiBlocker.Enabled, Is.False);
            Assert.That(p.Recipe.Difficulty.MultiBlocker.BlockerDistribution[0], Is.Not.EqualTo(99));
            Assert.That(p.DependencyGraph.IsValid, Is.True);
            var invalid=p.Recipe.Difficulty; invalid.MultiBlocker.BlockerDistribution=new double[3];
            Assert.Throws<ArgumentException>(()=>new PuzzleRecipe(1,preset.CreateProfile(),difficulty:invalid));
        }
        [TestCase(16)] [TestCase(32)]
        public void LargerCubesUseTheSameVerifiedGraphAndRespectTheFastBudget(int size)
        {
            var profile=new DifficultyProfile("Large multi",size,2,14,8,6,.30,.45,.05,.3,.55,3*size*size);
            var settings=DifficultyPresets.All[2].CreateSettings(); settings.ApplyQuality(GenerationQuality.Fast);
            settings.MaxConstructionAttempts=30000;
            var p=new PuzzleGenerator().Generate(new PuzzleRecipe(1,profile,difficulty:settings));
            Assert.That(p.DependencyGraph.IsValid, Is.True); Assert.That(p.Report.LayoutCandidates, Is.EqualTo(1));
            Assert.That(p.Report.CandidateAttempts, Is.LessThanOrEqualTo(8*30000));
            Assert.That(new PuzzleValidator().Validate(p,p.VerifiedSolution).Succeeded, Is.True);
        }
        [Test]
        public void PresetsProduceDifferentConfirmedBlockerDistributions()
        {
            double easy=0,expert=0;
            for(int seed=0;seed<6;seed++)
            foreach(int index in new[] { 0,3 })
            {
                var p=new PuzzleGenerator().Generate(DifficultyPresets.All[index].CreateRecipe(seed,quality:GenerationQuality.Fast));
                if(index==0) easy+=p.Difficulty.MultiBlocker.IndependentMultiBlockerFraction; else expert+=p.Difficulty.MultiBlocker.IndependentMultiBlockerFraction;
            }
            Assert.That(expert, Is.GreaterThan(easy+.3));
        }
    }
}
