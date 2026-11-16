using System;
using System.IO;

namespace ArrowCube.Generation
{
    [Serializable]
    public sealed class MultiBlockerSettings
    {
        public bool Enabled=true,EnableTargetedPlacement=true,ReplaceExistingPaths=true;
        public double TargetAverageBlockers=2.5,MultiBlockerPathFraction=.50,CrossFacePreference=.50,BranchingPreference=.60,MaxBlockerConcentration=.25;
        public int MaxTargetBlockers=4,TargetedBlockerAttempts=6;
        // Last bucket includes its index and all larger counts. Values are relative weights.
        public double[] BlockerDistribution={ .20,.25,.30,.15,.10,0,0 };
        public MultiBlockerSettings Copy() { var result=(MultiBlockerSettings)MemberwiseClone(); result.BlockerDistribution=(double[])BlockerDistribution?.Clone(); return result; }
        public void Validate()
        {
            foreach(double value in new[] { MultiBlockerPathFraction,CrossFacePreference,BranchingPreference,MaxBlockerConcentration })
                if(double.IsNaN(value) || value<0 || value>1) throw new ArgumentOutOfRangeException("Multi-blocker fraction");
            if(double.IsNaN(TargetAverageBlockers) || TargetAverageBlockers<0 || TargetAverageBlockers>32 || MaxTargetBlockers<1 || MaxTargetBlockers>32
                || TargetedBlockerAttempts<0 || TargetedBlockerAttempts>64) throw new ArgumentOutOfRangeException("Multi-blocker targets/budget");
            if(BlockerDistribution==null || BlockerDistribution.Length<2 || BlockerDistribution.Length>17) throw new ArgumentException("Use 2..17 blocker distribution buckets; the final bucket includes larger counts.");
            double sum=0; foreach(double weight in BlockerDistribution) { if(double.IsNaN(weight) || double.IsInfinity(weight) || weight<0) throw new ArgumentException("Blocker distribution weights must be finite and nonnegative."); sum+=weight; }
            if(sum<=0 || double.IsInfinity(sum)) throw new ArgumentException("Blocker distribution needs positive finite total weight.");
        }
        internal void Write(BinaryWriter w)
        {
            w.Write(Enabled); w.Write(EnableTargetedPlacement); w.Write(ReplaceExistingPaths); w.Write(TargetAverageBlockers);
            w.Write(MultiBlockerPathFraction); w.Write(CrossFacePreference); w.Write(BranchingPreference); w.Write(MaxBlockerConcentration);
            w.Write(MaxTargetBlockers); w.Write(TargetedBlockerAttempts); w.Write(BlockerDistribution.Length); foreach(double value in BlockerDistribution) w.Write(value);
        }
        internal static MultiBlockerSettings ForScore(double score)
        {
            int band=Math.Min(4,(int)(score/20));
            return new MultiBlockerSettings { Enabled=band>0,TargetAverageBlockers=new[] { .7,1.7,2.7,3.5,4.5 }[band],
                MultiBlockerPathFraction=new[] { .05,.22,.45,.65,.80 }[band],MaxTargetBlockers=new[] { 2,3,4,6,8 }[band],
                CrossFacePreference=.20+.65*score/100,BranchingPreference=.25+.55*score/100,TargetedBlockerAttempts=band==0 ? 0 : 4+band*2,
                BlockerDistribution=band==0 ? new[] { .75,.20,.05,0,0,0,0 } : band==1 ? new[] { .45,.30,.18,.05,.02,0,0 }
                    : band==2 ? new[] { .25,.25,.27,.15,.08,0,0 } : band==3 ? new[] { .18,.15,.22,.22,.15,.06,.02 } : new[] { .10,.10,.15,.20,.20,.15,.10 } };
        }
    }
}
