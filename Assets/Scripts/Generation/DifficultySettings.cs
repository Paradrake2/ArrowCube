using System;
using System.IO;

namespace ArrowCube.Generation
{
    public enum PlacementStrategy { UniformRandom, InteriorBiased, EdgeBiased, Mixed, DifficultyOptimized }
    public enum GenerationQuality { Fast, Balanced, HighQuality }

    /// <summary>Editable authoring values. Recipes take a private deep copy; no Unity APIs.</summary>
    [Serializable]
    public sealed class DifficultySettings
    {
        public double TargetScore=30, ScoreTolerance=10;
        public double DesiredSafeFraction=.45, DesiredDependencyDepth=5, EdgeResistance=.5, SpatialUnpredictability=.5;
        public double CrossFaceFrequency=.15, DependencyStrength=.5;
        public PlacementStrategy Placement=PlacementStrategy.DifficultyOptimized;
        public GenerationQuality Quality=GenerationQuality.Balanced;
        public int MaxCandidates=4, MaxConstructionAttempts=12000, MaxRestarts=8, OptimizationIterations=2, CandidatePoolSize=6;
        public int AlternativeOrderSamples=2;
        public DifficultyWeights Weights=new DifficultyWeights();
        public MultiBlockerSettings MultiBlocker=new MultiBlockerSettings();
        public DifficultySettings Copy()
        { var copy=(DifficultySettings)MemberwiseClone(); copy.Weights=Weights?.Copy(); copy.MultiBlocker=MultiBlocker?.Copy(); return copy; }
        public double ConfigurationEstimate()
        {
            var w=Weights??new DifficultyWeights();
            double total=w.EdgeResistance+w.Dependencies+w.SafeMoveScarcity+w.SpatialUnpredictability+w.CrossFace+w.VisualComplexity;
            if(total<=0) return 0;
            return Math.Max(0,Math.Min(100,100*(w.EdgeResistance*EdgeResistance+w.Dependencies*(DesiredDependencyDepth-1)/(DesiredDependencyDepth+3)
                +w.SafeMoveScarcity*(1-DesiredSafeFraction)+w.SpatialUnpredictability*SpatialUnpredictability
                +w.CrossFace*CrossFaceFrequency+w.VisualComplexity*DependencyStrength)/total));
        }
        public void Validate()
        {
            Fraction(TargetScore/100); Fraction(ScoreTolerance/100); Fraction(DesiredSafeFraction); Fraction(EdgeResistance);
            Fraction(SpatialUnpredictability); Fraction(CrossFaceFrequency); Fraction(DependencyStrength);
            if(double.IsNaN(DesiredDependencyDepth) || DesiredDependencyDepth<1 || DesiredDependencyDepth>1000) throw new ArgumentOutOfRangeException(nameof(DesiredDependencyDepth));
            if(MaxCandidates<1 || MaxCandidates>32 || OptimizationIterations<0 || OptimizationIterations>64 || CandidatePoolSize<1 || CandidatePoolSize>32
                || MaxConstructionAttempts<1 || MaxConstructionAttempts>2000000 || MaxRestarts<1 || MaxRestarts>100 || AlternativeOrderSamples<1 || AlternativeOrderSamples>8) throw new ArgumentOutOfRangeException("Generation budgets");
            if(!Enum.IsDefined(typeof(PlacementStrategy),Placement) || !Enum.IsDefined(typeof(GenerationQuality),Quality)) throw new ArgumentOutOfRangeException(nameof(Placement));
            if(Weights==null) throw new ArgumentNullException(nameof(Weights)); Weights.Validate();
            if(MultiBlocker==null) throw new ArgumentNullException(nameof(MultiBlocker)); MultiBlocker.Validate();
        }
        private static void Fraction(double value)
        { if(double.IsNaN(value) || value<0 || value>1) throw new ArgumentOutOfRangeException("Difficulty fraction"); }
        public void ApplyQuality(GenerationQuality quality)
        {
            Quality=quality;
            MaxCandidates=quality==GenerationQuality.Fast ? 1 : quality==GenerationQuality.Balanced ? DependencyStrength>.9 ? 8 : 4 : 12;
            OptimizationIterations=quality==GenerationQuality.Fast ? 0 : quality==GenerationQuality.Balanced ? DependencyStrength>.9 ? 6 : 2 : 12;
            CandidatePoolSize=quality==GenerationQuality.Fast ? 2 : quality==GenerationQuality.Balanced ? 4+(int)Math.Round(8*DependencyStrength) : 12+(int)Math.Round(12*DependencyStrength);
            AlternativeOrderSamples=quality==GenerationQuality.Fast ? 1 : quality==GenerationQuality.Balanced ? 2 : 4;
            if(MultiBlocker!=null && MultiBlocker.Enabled && DependencyStrength>.7 && quality!=GenerationQuality.Fast)
            {
                int tier=DependencyStrength>.9 ? 2 : DependencyStrength>.8 ? 1 : 0;
                MaxCandidates=quality==GenerationQuality.Balanced ? 8+tier*8 : 16+tier*8;
                OptimizationIterations=quality==GenerationQuality.Balanced ? 4+tier*8 : 12+tier*8;
            }
            if(MultiBlocker!=null) MultiBlocker.TargetedBlockerAttempts=quality==GenerationQuality.Fast ? 0 : quality==GenerationQuality.Balanced ? 2+MultiBlocker.MaxTargetBlockers : 12+MultiBlocker.MaxTargetBlockers;
        }
        internal void Write(BinaryWriter w,int version)
        {
            w.Write(TargetScore); w.Write(ScoreTolerance); w.Write(DesiredSafeFraction); w.Write(DesiredDependencyDepth);
            w.Write(EdgeResistance); w.Write(SpatialUnpredictability); w.Write(CrossFaceFrequency); w.Write(DependencyStrength);
            w.Write((int)Placement); w.Write((int)Quality); w.Write(MaxCandidates); w.Write(MaxConstructionAttempts); w.Write(MaxRestarts);
            w.Write(OptimizationIterations); w.Write(CandidatePoolSize);
            w.Write(AlternativeOrderSamples);
            w.Write(Weights.EdgeResistance); w.Write(Weights.Dependencies); w.Write(Weights.SafeMoveScarcity);
            w.Write(Weights.SpatialUnpredictability); w.Write(Weights.CrossFace); w.Write(Weights.VisualComplexity);
            if(version>=4) MultiBlocker.Write(w);
        }
    }

    public sealed class DifficultyPreset
    {
        public string Name { get; }
        public double Target { get; }
        public double MinimumScore => Math.Max(0,Target-10);
        public double MaximumScore => Math.Min(100,Target+10);
        internal DifficultyPreset(string name,double target) { Name=name; Target=target; }
        public DifficultySettings CreateSettings() => DifficultyPresets.SettingsForMaster(Target);
        public DifficultySettings CreateLegacySettings() => DifficultyPresets.SettingsForMaster(Target,false);
        public DifficultyProfile CreateProfile() => DifficultyPresets.ProfileForMaster(Target,Name);
        public PuzzleRecipe CreateRecipe(int seed,ArrowCube.Movement.ExitRules rules=null,GenerationQuality quality=GenerationQuality.Balanced)
        {
            var settings=CreateSettings(); settings.ApplyQuality(quality);
            return new PuzzleRecipe(seed,CreateProfile(),rules,maxRestarts:settings.MaxRestarts,difficulty:settings);
        }
    }

    public static class DifficultyPresets
    {
        public static readonly System.Collections.Generic.IReadOnlyList<DifficultyPreset> All=Array.AsReadOnly(new[] {
            new DifficultyPreset("Easy",10),new DifficultyPreset("Normal",30),new DifficultyPreset("Hard",50),
            new DifficultyPreset("Expert",70),new DifficultyPreset("Extreme",90) });

        public static DifficultySettings SettingsForMaster(double score,bool multiBlockerDefaults=true)
        {
            if(double.IsNaN(score) || score<0 || score>100) throw new ArgumentOutOfRangeException(nameof(score));
            double t=score/100;
            var settings=new DifficultySettings { TargetScore=score,DesiredSafeFraction=.9-.82*t,DesiredDependencyDepth=1+18*t,
                EdgeResistance=t,SpatialUnpredictability=t,CrossFaceFrequency=.06+.32*t,DependencyStrength=score<40 ? t : .45+.55*t,
                Placement=score<20 ? PlacementStrategy.UniformRandom : PlacementStrategy.DifficultyOptimized };
            settings.MultiBlocker=MultiBlockerSettings.ForScore(score);
            if(!multiBlockerDefaults) settings.MultiBlocker.Enabled=false;
            if(multiBlockerDefaults && score>=40)
            {
                settings.DesiredSafeFraction=Math.Max(.08,.70-.8*t);
                settings.EdgeResistance=Math.Min(1,.55+.50*t);
            }
            settings.ApplyQuality(GenerationQuality.Balanced); return settings;
        }
        public static DifficultyProfile ProfileForMaster(double score,string name="Custom")
        {
            if(double.IsNaN(score) || score<0 || score>100) throw new ArgumentOutOfRangeException(nameof(score));
            double t=score/100;
            int size=score<20 ? 6 : 8;
            // Logical controls change even when the grid, path count and length limits stay identical.
            int gates=score<20 ? 12*size : score<40 ? 8*size : score<60 ? 4*size : 3*size;
            return new DifficultyProfile(name,size,2,12,8,5,.40+.20*t,.55+.25*t,.02+.35*t,.35+.8*t,.35+.35*t,
                gates,6*size*size/2);
        }
        public static DifficultyPreset ForLevel(int level)
        { if(level<1) throw new ArgumentOutOfRangeException(nameof(level)); return All[level<=5 ? 0 : level<=20 ? 1 : level<=50 ? 2 : level<=100 ? 3 : 4]; }
    }

    public sealed class GenerationProgress
    {
        public int Completed, Total;
        public string Phase;
        public double BestScore;
    }
}
