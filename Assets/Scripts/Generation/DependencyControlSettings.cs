using System;
using System.IO;

namespace ArrowCube.Generation
{
    [Serializable]
    public sealed class DependencyControlSettings
    {
        public double Amount=.75;
        public int MaximumStartingFreeArrows=3;
        public int AdjustmentPasses=0; // Zero uses a bounded count proportional to arrow count.
        public int EndpointAttempts=0;
        public DependencyControlSettings Copy() => (DependencyControlSettings)MemberwiseClone();
        public double TargetBlockersPerBlockedArrow => 1+4*Amount;
        public void Validate()
        {
            if(double.IsNaN(Amount) || Amount<0 || Amount>1 || MaximumStartingFreeArrows<1 || MaximumStartingFreeArrows>30000
                || AdjustmentPasses<0 || AdjustmentPasses>30000 || EndpointAttempts<0 || EndpointAttempts>10000)
                throw new ArgumentOutOfRangeException("Dependency controls");
        }
        internal void Write(BinaryWriter w) { w.Write(Amount); w.Write(MaximumStartingFreeArrows); w.Write(AdjustmentPasses); w.Write(EndpointAttempts); }
    }
    [Serializable]
    public sealed class DependencyControlReport
    {
        public int InitiallyFree,MaximumStartingFreeArrows,OrientationChanges,EndpointChanges;
        public double AverageBlockersPerBlockedArrow,TargetAverageBlockers;
        public bool BlockerTargetReached;
    }
}
