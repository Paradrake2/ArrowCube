using System;
using System.Linq;
using System.Threading;
using ArrowCube.Generation;
using ArrowCube.Gameplay;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using ArrowCube.Topology;
using NUnit.Framework;
using UnityEngine;

namespace ArrowCube.Tests
{
    public class GenericSurfaceGenerationTests
    {
        [TestCase(AuthoredGenerationShape.L,0)] [TestCase(AuthoredGenerationShape.L,1)] [TestCase(AuthoredGenerationShape.L,2)]
        [TestCase(AuthoredGenerationShape.ObliquePrism,0)] [TestCase(AuthoredGenerationShape.ObliquePrism,1)] [TestCase(AuthoredGenerationShape.ObliquePrism,2)]
        [TestCase(AuthoredGenerationShape.T,0)] [TestCase(AuthoredGenerationShape.T,1)] [TestCase(AuthoredGenerationShape.T,2)]
        public void GeneratedSurfacesHaveVerifiedDependenciesAndDeterministicIdentity(AuthoredGenerationShape shape,int mode)
        {
            var authored=AuthoredGenerationSurfaces.Create(shape,4);
            var rules=mode==0 ? ExitRules.EdgeOnly : mode==1 ? ExitRules.GateOnly : ExitRules.Hybrid;
            var recipe=new PuzzleRecipe(12345,authored.Shape,AuthoredGenerationSurfaces.Profile(authored.Shape.Topology.CellCount),rules,
                dependencyControls:new DependencyControlSettings { Amount=.5,MaximumStartingFreeArrows=3 });
            var generated=new PuzzleGenerator().Generate(recipe);
            var graph=PuzzleDependencyGraph.Analyze(generated);
            Assert.That(graph.IsValid,Is.True); Assert.That(graph.HasCycle,Is.False);
            Assert.That(graph.Nodes.Any(n=>n.IndependentBlockers.Length>=2),Is.True,"Produce independently removable blocker branches, not only a chain.");
            Assert.That(generated.Statistics.DependencyCount,Is.GreaterThan(0));
            Assert.That(generated.Statistics.ImmediatelyRemovablePaths,Is.InRange(1,3));
            Assert.That(generated.Statistics.OccupiedRegions,Is.GreaterThan(2));
            Assert.That(generated.Statistics.Coverage,Is.EqualTo(generated.Paths.Sum(p=>p.CellIds.Count)/(double)generated.Surface.CellCount));
            foreach(var p in generated.Paths)
                Assert.That(InitialExitClearance.IsClear(new PathMovementSimulator().Simulate(new SurfaceGrid(generated.Surface,new[] {p}),p.Id,generated.Exits,generated.Rules)),Is.True);
            var validation=new PuzzleValidator().Validate(generated,generated.VerifiedSolution,requireClearInitialExit:true);
            Assert.That(validation.Succeeded,Is.True,validation.Error);
            var repeated=new PuzzleGenerator().Generate(recipe);
            Assert.That(repeated.Fingerprint,Is.EqualTo(generated.Fingerprint));
            var saved=RecipeSave.Capture(recipe).Restore();
            Assert.That(saved.Shape.Digest,Is.EqualTo(recipe.Shape.Digest));
            Assert.That(new PuzzleGenerator().Generate(saved).Fingerprint,Is.EqualTo(generated.Fingerprint));
            var restored=PuzzleSave.Capture(generated).Restore();
            Assert.That(AuthoredSurfaceDemos.Resolve(restored).Surface,Is.SameAs(restored.Surface));
            var measured=new PuzzleDifficultyAnalyzer().Analyze(generated);
            Assert.That(measured.SafeByFace.Length,Is.EqualTo(generated.Statistics.RegionCount));
            Debug.Log($"PHASE4 {shape}/{mode} cells={generated.Surface.CellCount} paths={generated.Paths.Count} safe={generated.Statistics.ImmediatelyRemovablePaths} dependencies={generated.Statistics.DependencyCount} depth={generated.Statistics.DependencyDepth} independent={graph.Metrics.IndependentMultiBlockerFraction:F3} coverage={generated.Statistics.Coverage:F3} ms={generated.Report.Milliseconds:F1} fingerprint={generated.Fingerprint}");
        }
        [TestCase(AuthoredGenerationShape.L)] [TestCase(AuthoredGenerationShape.ObliquePrism)] [TestCase(AuthoredGenerationShape.T)]
        public void MeasuredSearchTargetingMutationAndTuningUseTheSameGenericDAG(AuthoredGenerationShape shape)
        {
            var authored=AuthoredGenerationSurfaces.Create(shape,4);
            var settings=new DifficultySettings { MaxCandidates=2,OptimizationIterations=3,CandidatePoolSize=8,AlternativeOrderSamples=2 };
            settings.MultiBlocker.Enabled=true; settings.MultiBlocker.TargetedBlockerAttempts=6;
            var recipe=new PuzzleRecipe(12345,authored.Shape,AuthoredGenerationSurfaces.Profile(authored.Shape.Topology.CellCount),ExitRules.GateOnly,
                difficulty:settings,dependencyControls:new DependencyControlSettings { MaximumStartingFreeArrows=3,Amount=.6 });
            var result=new PuzzleGenerator().Generate(recipe);
            Assert.That(result.Difficulty,Is.Not.Null); Assert.That(result.DependencyGraph.IsValid,Is.True);
            Assert.That(result.Report.LayoutCandidates,Is.EqualTo(2)); Assert.That(result.Report.OptimizationAttempts,Is.EqualTo(3));
            Assert.That(result.Report.TargetedAttempts,Is.EqualTo(6)); Assert.That(result.Report.TargetedCandidateAttempts,Is.GreaterThan(0));
            Assert.That(result.Statistics.ImmediatelyRemovablePaths,Is.InRange(1,3));
            Assert.That(result.Difficulty.RegionCoverage.Length,Is.EqualTo(result.Difficulty.SurfaceRegionCount));
            Assert.That(new PuzzleGenerator().Generate(recipe).Fingerprint,Is.EqualTo(result.Fingerprint));
        }
        [Test]
        public void NoEligibleEdgesSupportsGateOnlyAndHybridButRejectsEdgeOnlyClearly()
        {
            var original=AuthoredGenerationSurfaces.Create(AuthoredGenerationShape.L,3).Shape.Topology;
            var builder=new SurfaceTopologyBuilder(); foreach(int c in original.EnumerateCellIds()) builder.AddCell(c);
            foreach(int c in original.EnumerateCellIds()) for(int d=0;d<4;d++)
            { var b=original.GetBoundary(c,(SurfaceDirection)d); builder.SetBoundary(c,(SurfaceDirection)d,new CellBoundary(b.NeighborCellId.Value,b.NewDirection,b.IsGeometricEdge,false)); }
            var shape=new SurfaceShapeDefinition("closed/no-edge-exits",1,builder.Build());
            foreach(var rules in new[] {ExitRules.GateOnly,ExitRules.Hybrid})
            {
                var puzzle=new PuzzleGenerator().Generate(new PuzzleRecipe(71,shape,AuthoredGenerationSurfaces.Profile(shape.Topology.CellCount),rules));
                Assert.That(puzzle.Statistics.GateExitsInSolution,Is.EqualTo(puzzle.Paths.Count)); Assert.That(puzzle.Statistics.EdgeExitsInSolution,Is.Zero);
            }
            var error=Assert.Throws<PuzzleGenerationException>(()=>new PuzzleGenerator().Generate(new PuzzleRecipe(71,shape,AuthoredGenerationSurfaces.Profile(shape.Topology.CellCount),ExitRules.EdgeOnly)));
            StringAssert.Contains("no eligible edge exits",error.Message);
        }
        [Test]
        public void CooperativeProgressCancellationAndImpossibleRecipesTerminateWithoutPublishingPartialPuzzles()
        {
            var authored=AuthoredGenerationSurfaces.Create(AuthoredGenerationShape.L,4);
            var recipe=new PuzzleRecipe(12345,authored.Shape,AuthoredGenerationSurfaces.Profile(authored.Shape.Topology.CellCount),ExitRules.GateOnly);
            using(var job=new GenerationOperation(recipe,true))
            {
                for(int i=0;i<5;i++) job.Tick(0);
                Assert.That(job.IsCompleted,Is.False); job.Cancel(); job.Tick();
                Assert.That(job.IsCanceled,Is.True); Assert.That(job.Result,Is.Null);
            }
            var progress=new RecordingProgress(); int yields=0; GeneratedPuzzle result=null;
            foreach(var step in new PuzzleGenerator().GenerateSteps(recipe,progress)) { if(step==null) yields++; else result=step; }
            Assert.That(yields,Is.GreaterThan(20)); Assert.That(progress.Reports,Is.GreaterThan(0)); Assert.That(result,Is.Not.Null);
            using(var cancel=new CancellationTokenSource()) { cancel.Cancel(); Assert.Throws<OperationCanceledException>(()=>new PuzzleGenerator().Generate(recipe,cancel.Token)); }
            var impossible=new DifficultyProfile("Impossible",4,2,12,10000,3,.8,.9,.99,6,.5,20,surfaceCellCount:authored.Shape.Topology.CellCount);
            using(var job=new GenerationOperation(new PuzzleRecipe(1,authored.Shape,impossible,maxRestarts:1,attemptsPerCell:1),true))
            { int ticks=0; while(!job.IsCompleted && ticks++<20000) job.Tick(0); Assert.That(job.IsCompleted,Is.True); Assert.That(job.Error,Is.TypeOf<PuzzleGenerationException>()); Assert.That(job.Result,Is.Null); }
        }
        [Test]
        public void ShapeIdentityIncludesBoundaryMetadataVersionAndStableOrdering()
        {
            var authored=AuthoredGenerationSurfaces.Create(AuthoredGenerationShape.L,3).Shape;
            var builder=new SurfaceTopologyBuilder(); foreach(int c in authored.Topology.EnumerateCellIds().Reverse()) builder.AddCell(c);
            foreach(int c in authored.Topology.EnumerateCellIds().Reverse()) for(int d=3;d>=0;d--) builder.SetBoundary(c,(SurfaceDirection)d,authored.Topology.GetBoundary(c,(SurfaceDirection)d));
            var reordered=new SurfaceShapeDefinition(authored.Id,authored.Version,builder.Build());
            Assert.That(reordered.Digest,Is.EqualTo(authored.Digest));
            Assert.That(new SurfaceShapeDefinition(authored.Id,2,authored.Topology).Digest,Is.Not.EqualTo(authored.Digest));
            Assert.That(new SurfaceShapeDefinition("another-shape",1,authored.Topology).Digest,Is.Not.EqualTo(authored.Digest));
            var profile=AuthoredGenerationSurfaces.Profile(authored.Topology.CellCount);
            Assert.That(new PuzzleGenerator().Generate(new PuzzleRecipe(71,reordered,profile)).Fingerprint,
                Is.EqualTo(new PuzzleGenerator().Generate(new PuzzleRecipe(71,authored,profile)).Fingerprint));
        }
        [Test]
        public void GenericActiveSaveRestoresSessionAndRejectsMismatchedShapeSnapshots()
        {
            var authored=AuthoredGenerationSurfaces.Create(AuthoredGenerationShape.T,3);
            var recipe=new PuzzleRecipe(71,authored.Shape,AuthoredGenerationSurfaces.Profile(authored.Shape.Topology.CellCount),ExitRules.GateOnly);
            var puzzle=new PuzzleGenerator().Generate(recipe); var session=new GameplaySession(puzzle);
            var move=session.BeginAttempt(puzzle.VerifiedSolution[0]); Assert.That(session.CompleteAttempt(move),Is.True);
            var save=new ActiveLevelSave { Recipe=RecipeSave.Capture(recipe),Puzzle=PuzzleSave.Capture(puzzle),Session=session.Capture(),Sandbox=true };
            var restored=JsonUtility.FromJson<ActiveLevelSave>(JsonUtility.ToJson(save)).Restore();
            Assert.That(restored.Grid.Paths.Count,Is.EqualTo(puzzle.Paths.Count-1)); Assert.That(AuthoredSurfaceDemos.Resolve(restored.Puzzle),Is.Not.Null);
            save.Puzzle.SurfaceAssetVersion=2; Assert.Throws<ArgumentException>(()=>save.Restore());
        }
        [TestCase(2)] [TestCase(4)] [TestCase(6)]
        public void GenerationScalesWithActualCellCountAndTraversesConcaveAndObliqueFolds(int subdivisions)
        {
            foreach(var kind in new[] {AuthoredGenerationShape.L,AuthoredGenerationShape.ObliquePrism,AuthoredGenerationShape.T})
            {
                var authored=AuthoredGenerationSurfaces.Create(kind,subdivisions);
                var puzzle=new PuzzleGenerator().Generate(new PuzzleRecipe(12345,authored.Shape,AuthoredGenerationSurfaces.Profile(authored.Shape.Topology.CellCount),ExitRules.GateOnly));
                int folds=0,inside=0;
                foreach(var path in puzzle.Paths) foreach(var segment in path.CellSegments)
                {
                    var a=authored.Geometry.GetCell(segment.A); var b=authored.Geometry.GetCell(segment.B);
                    if(segment.CrossesGeometricEdge) { folds++; if(Vector3.Dot(b.Normal,a.Direction(segment.DirectionFromA))<0) inside++; }
                }
                Assert.That(folds,Is.GreaterThan(0));
                Debug.Log($"SURFACE PERFORMANCE {kind}/n={subdivisions}: cells={puzzle.Surface.CellCount} paths={puzzle.Paths.Count} folds={folds} concave={inside} coverage={puzzle.Statistics.Coverage:F3} ms={puzzle.Report.Milliseconds:F1}");
            }
        }

        [Test]
        public void GenericCyclesAreRejectedAndRegionEvidenceDoesNotUseSixFaceMasks()
        {
            var surface=Toroid(18,true); int Cell(int x,int y) => 1000+y*18+x;
            var a=new ArrowPath(10,surface,new[] {Cell(0,0),Cell(17,0)},SurfaceDirection.Right);
            var b=new ArrowPath(20,surface,new[] {Cell(1,0),Cell(2,0)},SurfaceDirection.Left);
            var cyclic=new PuzzleDefinition("cycle",surface,new[] {a,b},new ExitMap(surface,new[] {new CellGate(Cell(2,0),SurfaceDirection.Right),new CellGate(Cell(17,0),SurfaceDirection.Left)}),ExitRules.GateOnly);
            Assert.That(PuzzleDependencyGraph.Analyze(cyclic).HasCycle,Is.True);
            Assert.That(new PuzzleValidator().Validate(cyclic).Succeeded,Is.False);
            var valid=new PuzzleDefinition("one blocker",surface,new[] {a,b},new ExitMap(surface,new[] {new CellGate(Cell(2,0),SurfaceDirection.Right),new CellGate(Cell(1,0),SurfaceDirection.Left)}),ExitRules.GateOnly);
            var graph=PuzzleDependencyGraph.Analyze(valid); Assert.That(graph.IsValid,Is.True);
            Assert.That(graph.GetNode(10).ConfirmedBlockers,Is.EqualTo(new[] {20}));
            Assert.That(graph.GetNode(10).Interferences[0].CellIds,Is.Not.Empty);
            Assert.That(graph.GetNode(10).Interferences[0].Regions.Distinct().Count(),Is.GreaterThan(1));
            var report=new PuzzleDifficultyAnalyzer().Analyze(valid);
            Assert.That(report.SurfaceRegionCount,Is.EqualTo(324)); Assert.That(report.SafeByFace.Length,Is.EqualTo(324));
            Assert.That(report.DependencyFaceMatrix,Is.Empty,"Large region graphs use explicit dependency and contact evidence, avoiding quadratic matrices.");
        }
        [Test]
        public void SingleCoplanarRegionCanGenerateWithoutPhysicalEdges()
        {
            var surface=Toroid(4,false); var shape=new SurfaceShapeDefinition("flat/logical-loop",1,surface);
            var profile=new DifficultyProfile("Flat",4,2,5,2,0,.25,.5,.1,0,.5,12,surfaceCellCount:16);
            var puzzle=new PuzzleGenerator().Generate(new PuzzleRecipe(12345,shape,profile,ExitRules.GateOnly));
            Assert.That(puzzle.Statistics.RegionCount,Is.EqualTo(1)); Assert.That(puzzle.Statistics.GeometricFolds,Is.Zero);
            Assert.That(new PuzzleValidator().Validate(puzzle,puzzle.VerifiedSolution).Succeeded,Is.True);
        }
        private static ISurfaceTopology Toroid(int n,bool folds)
        {
            int Cell(int x,int y) => 1000+y*n+x;
            var builder=new SurfaceTopologyBuilder();
            for(int y=0;y<n;y++) for(int x=0;x<n;x++) builder.AddCell(Cell(x,y));
            for(int y=0;y<n;y++) for(int x=0;x<n;x++)
            { builder.Connect(Cell(x,y),SurfaceDirection.Right,Cell((x+1)%n,y),SurfaceDirection.Right,folds,false); builder.Connect(Cell(x,y),SurfaceDirection.Up,Cell(x,(y+1)%n),SurfaceDirection.Up,folds,false); }
            return builder.Build();
        }
        private sealed class RecordingProgress : IProgress<GenerationProgress>
        { public int Reports; public void Report(GenerationProgress progress) { Reports++; Assert.That(progress.Phase,Is.Not.Empty); } }

    }
}
