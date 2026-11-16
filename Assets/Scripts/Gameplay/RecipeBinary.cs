using System;
using System.IO;
using System.Text;
using ArrowCube.Generation;
using ArrowCube.Movement;

namespace ArrowCube.Gameplay
{
    /// <summary>Preserves the exact IEEE double bits used by recipe fingerprints.
    /// Unity JSON's numeric formatting is not used for deterministic generation inputs.</summary>
    internal static class RecipeBinary
    {
        public static string Encode(PuzzleRecipe recipe)
        {
            using(var stream=new MemoryStream())
            using(var w=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                w.Write(recipe.Shape==null ? 1 : 2); w.Write(recipe.Seed); w.Write(recipe.GenerationVersion); w.Write(recipe.MaxRestarts); w.Write(recipe.AttemptsPerCell);
                w.Write(recipe.Rules.EnableCubeEdgeExits); w.Write(recipe.Rules.EnableDirectionalGates);
                var p=recipe.Profile; if(recipe.Shape!=null) w.Write(p.SurfaceCellCount); w.Write(p.Name); w.Write(p.GridSize); w.Write(p.MinPathLength); w.Write(p.MaxPathLength); w.Write(p.MinPathCount);
                w.Write(p.MinCrossFacePaths); w.Write(p.MaxGates); w.Write(p.MaxPathCount);
                Numbers(w,p.MinimumCoverage,p.TargetCoverage,p.MinimumBlockedFraction,p.MinimumAverageTurns,p.TurnProbability);
                var d=recipe.Difficulty; w.Write(d!=null);
                if(d!=null)
                {
                    Numbers(w,d.TargetScore,d.ScoreTolerance,d.DesiredSafeFraction,d.DesiredDependencyDepth,d.EdgeResistance,d.SpatialUnpredictability,d.CrossFaceFrequency,d.DependencyStrength);
                    w.Write((int)d.Placement); w.Write((int)d.Quality); w.Write(d.MaxCandidates); w.Write(d.MaxConstructionAttempts); w.Write(d.MaxRestarts);
                    w.Write(d.OptimizationIterations); w.Write(d.CandidatePoolSize); w.Write(d.AlternativeOrderSamples);
                    var weights=d.Weights; Numbers(w,weights.EdgeResistance,weights.Dependencies,weights.SafeMoveScarcity,weights.SpatialUnpredictability,weights.CrossFace,weights.VisualComplexity);
                    var m=d.MultiBlocker; w.Write(m.Enabled); w.Write(m.EnableTargetedPlacement); w.Write(m.ReplaceExistingPaths);
                    Numbers(w,m.TargetAverageBlockers,m.MultiBlockerPathFraction,m.CrossFacePreference,m.BranchingPreference,m.MaxBlockerConcentration);
                    w.Write(m.MaxTargetBlockers); w.Write(m.TargetedBlockerAttempts); w.Write(m.BlockerDistribution.Length); Numbers(w,m.BlockerDistribution);
                }
                var c=recipe.DependencyControls; w.Write(c!=null);
                if(c!=null) { w.Write(c.Amount); w.Write(c.MaximumStartingFreeArrows); w.Write(c.AdjustmentPasses); w.Write(c.EndpointAttempts); }
                if(recipe.Shape!=null) recipe.Shape.Write(w);
                w.Flush(); return Convert.ToBase64String(stream.ToArray());
            }
        }
        public static PuzzleRecipe Decode(string data)
        {
            if(string.IsNullOrEmpty(data) || data.Length>4000000) throw new ArgumentException("Missing or oversized saved recipe.");
            using(var stream=new MemoryStream(Convert.FromBase64String(data)))
            using(var r=new BinaryReader(stream,Encoding.UTF8))
            {
                int format=r.ReadInt32(); if(format!=1 && format!=2) throw new ArgumentException("Unsupported recipe encoding.");
                int seed=r.ReadInt32(),version=r.ReadInt32(),restarts=r.ReadInt32(),attempts=r.ReadInt32();
                var rules=new ExitRules(r.ReadBoolean(),r.ReadBoolean());
                int surfaceCells=format==2 ? r.ReadInt32() : 0;
                string name=r.ReadString(); int grid=r.ReadInt32(),minLength=r.ReadInt32(),maxLength=r.ReadInt32(),minPaths=r.ReadInt32(),cross=r.ReadInt32(),gates=r.ReadInt32(),maxPaths=r.ReadInt32();
                var profile=new DifficultyProfile(name,grid,minLength,maxLength,minPaths,cross,r.ReadDouble(),r.ReadDouble(),r.ReadDouble(),r.ReadDouble(),r.ReadDouble(),gates,maxPaths,surfaceCells);
                DifficultySettings d=null;
                if(r.ReadBoolean())
                {
                    d=new DifficultySettings { TargetScore=r.ReadDouble(),ScoreTolerance=r.ReadDouble(),DesiredSafeFraction=r.ReadDouble(),DesiredDependencyDepth=r.ReadDouble(),
                        EdgeResistance=r.ReadDouble(),SpatialUnpredictability=r.ReadDouble(),CrossFaceFrequency=r.ReadDouble(),DependencyStrength=r.ReadDouble(),
                        Placement=(PlacementStrategy)r.ReadInt32(),Quality=(GenerationQuality)r.ReadInt32(),MaxCandidates=r.ReadInt32(),MaxConstructionAttempts=r.ReadInt32(),MaxRestarts=r.ReadInt32(),
                        OptimizationIterations=r.ReadInt32(),CandidatePoolSize=r.ReadInt32(),AlternativeOrderSamples=r.ReadInt32() };
                    d.Weights=new DifficultyWeights { EdgeResistance=r.ReadDouble(),Dependencies=r.ReadDouble(),SafeMoveScarcity=r.ReadDouble(),SpatialUnpredictability=r.ReadDouble(),CrossFace=r.ReadDouble(),VisualComplexity=r.ReadDouble() };
                    d.MultiBlocker=new MultiBlockerSettings { Enabled=r.ReadBoolean(),EnableTargetedPlacement=r.ReadBoolean(),ReplaceExistingPaths=r.ReadBoolean(),
                        TargetAverageBlockers=r.ReadDouble(),MultiBlockerPathFraction=r.ReadDouble(),CrossFacePreference=r.ReadDouble(),BranchingPreference=r.ReadDouble(),MaxBlockerConcentration=r.ReadDouble(),
                        MaxTargetBlockers=r.ReadInt32(),TargetedBlockerAttempts=r.ReadInt32() };
                    int buckets=r.ReadInt32(); if(buckets<2 || buckets>17) throw new ArgumentException("Invalid saved blocker distribution.");
                    d.MultiBlocker.BlockerDistribution=new double[buckets]; for(int i=0;i<buckets;i++) d.MultiBlocker.BlockerDistribution[i]=r.ReadDouble();
                }
                DependencyControlSettings c=null;
                if(r.ReadBoolean()) c=new DependencyControlSettings { Amount=r.ReadDouble(),MaximumStartingFreeArrows=r.ReadInt32(),AdjustmentPasses=r.ReadInt32(),EndpointAttempts=r.ReadInt32() };
                var shape=format==2 ? SurfaceShapeDefinition.Read(r) : null;
                if(format==2 && version!=6) throw new ArgumentException("Shape recipes require generation version 6.");
                if(stream.Position!=stream.Length) throw new ArgumentException("Unexpected data after saved recipe.");
                return shape==null ? new PuzzleRecipe(seed,profile,rules,version,restarts,attempts,d,c) : new PuzzleRecipe(seed,shape,profile,rules,restarts,attempts,d,c);
            }
        }
        private static void Numbers(BinaryWriter writer,params double[] values) { foreach(double value in values) writer.Write(value); }
    }
}
