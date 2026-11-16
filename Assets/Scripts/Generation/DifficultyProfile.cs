using System;
using System.Collections.Generic;

namespace ArrowCube.Generation
{
    /// <summary>Immutable quality bounds and construction targets, included in the generation recipe.</summary>
    public sealed class DifficultyProfile
    {
        public string Name { get; }
        public int GridSize { get; }
        public int MinPathLength { get; }
        public int MaxPathLength { get; }
        public int MinPathCount { get; }
        public int MinCrossFacePaths { get; }
        public double MinimumCoverage { get; }
        public double TargetCoverage { get; }
        public double MinimumBlockedFraction { get; }
        public double MinimumAverageTurns { get; }
        public double TurnProbability { get; }
        public int MaxGates { get; }
        public int MaxPathCount { get; }

        public DifficultyProfile(string name, int gridSize, int minPathLength, int maxPathLength,
            int minPathCount, int minCrossFacePaths, double minimumCoverage, double targetCoverage,
            double minimumBlockedFraction, double minimumAverageTurns, double turnProbability, int maxGates,int maxPathCount=int.MaxValue)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Profile needs a name.", nameof(name));
            if (gridSize < 4 || gridSize > 64) throw new ArgumentOutOfRangeException(nameof(gridSize), "Generation supports 4..64 cells per face side.");
            if (minPathLength < 2 || maxPathLength < minPathLength || maxPathLength > 6*gridSize*gridSize)
                throw new ArgumentOutOfRangeException(nameof(minPathLength));
            if (minPathCount < 1 || minCrossFacePaths < 1 || minCrossFacePaths > 6*gridSize*gridSize/minPathLength)
                throw new ArgumentOutOfRangeException(nameof(minCrossFacePaths));
            if(maxPathCount<minPathCount) throw new ArgumentOutOfRangeException(nameof(maxPathCount));
            Fraction(minimumCoverage,nameof(minimumCoverage)); Fraction(targetCoverage,nameof(targetCoverage));
            Fraction(minimumBlockedFraction,nameof(minimumBlockedFraction)); Fraction(turnProbability,nameof(turnProbability));
            if (minimumCoverage <= 0 || minimumCoverage > targetCoverage || minimumBlockedFraction >= 1
                || double.IsNaN(minimumAverageTurns) || double.IsInfinity(minimumAverageTurns)
                || minimumAverageTurns < 0 || maxGates < 1) throw new ArgumentOutOfRangeException(nameof(minimumCoverage));
            Name=name; GridSize=gridSize; MinPathLength=minPathLength; MaxPathLength=maxPathLength;
            MinPathCount=minPathCount; MinCrossFacePaths=minCrossFacePaths; MinimumCoverage=minimumCoverage;
            TargetCoverage=targetCoverage; MinimumBlockedFraction=minimumBlockedFraction;
            MinimumAverageTurns=minimumAverageTurns; TurnProbability=turnProbability; MaxGates=maxGates;
            MaxPathCount=maxPathCount;
        }

        private static void Fraction(double value,string name)
        { if (double.IsNaN(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(name); }
    }

    public static class DifficultyProfiles
    {
        public static IReadOnlyList<DifficultyProfile> All { get; } = Array.AsReadOnly(new[]
        {
            new DifficultyProfile("Tutorial",6,3,10,8,6,.52,.68,.10,.60,.48,36),
            new DifficultyProfile("Easy",8,3,12,14,8,.60,.75,.20,1.0,.52,48),
            new DifficultyProfile("Medium",10,4,14,22,10,.66,.81,.30,1.4,.56,60),
            new DifficultyProfile("Hard",12,2,16,30,12,.72,.86,.38,1.8,.60,72),
            new DifficultyProfile("Very Hard",16,2,18,55,16,.76,.90,.44,2.1,.64,96),
            new DifficultyProfile("Expert",20,2,20,80,20,.80,.93,.50,2.4,.68,120)
        });

        public static DifficultyProfile ForLevel(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            return All[level <= 5 ? 0 : level <= 15 ? 1 : level <= 30 ? 2 : level <= 60 ? 3 : level <= 100 ? 4 : 5];
        }
    }
}
