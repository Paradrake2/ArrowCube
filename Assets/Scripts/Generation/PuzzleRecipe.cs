using System;
using ArrowCube.Movement;
using ArrowCube.Topology;

namespace ArrowCube.Generation
{
    /// <summary>All deterministic inputs. Persist this rather than only a seed.</summary>
    public sealed class PuzzleRecipe
    {
        public SurfaceShapeDefinition Shape { get; }
        public ISurfaceTopology Surface { get; private set; }
        public int Seed { get; }
        public int GenerationVersion { get; }
        public DifficultyProfile Profile { get; }
        public ExitRules Rules { get; }
        public int MaxRestarts { get; }
        public int AttemptsPerCell { get; }
        internal DifficultySettings DifficultySettings { get; }
        public DifficultySettings Difficulty => DifficultySettings?.Copy();
        internal DependencyControlSettings DependencyControlSettings { get; }
        public DependencyControlSettings DependencyControls => DependencyControlSettings?.Copy();
        public PuzzleRecipe(int seed, DifficultyProfile profile, ExitRules rules = null,
            int generationVersion = PuzzleGenerator.LegacyCubeVersion, int maxRestarts = 8, int attemptsPerCell = 48,DifficultySettings difficulty=null,DependencyControlSettings dependencyControls=null)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Rules = rules ?? ExitRules.Hybrid;
            if (!Rules.EnableCubeEdgeExits && !Rules.EnableDirectionalGates)
                throw new ArgumentException("Generation requires at least one enabled exit mechanic.", nameof(rules));
            if (generationVersion < 1 || generationVersion > PuzzleGenerator.CurrentVersion)
                throw new ArgumentOutOfRangeException(nameof(generationVersion), "Unsupported generation version; refusing to silently change a saved puzzle.");
            if (maxRestarts < 1 || maxRestarts > 100 || attemptsPerCell < 1 || attemptsPerCell > 1000)
                throw new ArgumentOutOfRangeException(nameof(maxRestarts));
            if(difficulty!=null)
            {
                if(generationVersion<3) throw new ArgumentException("Advanced difficulty requires generation version 3.");
                difficulty.Validate(); DifficultySettings=difficulty.Copy();
            }
            Surface=new CubeTopology(profile.GridSize);
            Seed=seed; GenerationVersion=generationVersion; MaxRestarts=maxRestarts; AttemptsPerCell=attemptsPerCell;
            if(dependencyControls!=null)
            {
                if(generationVersion<5) throw new ArgumentException("Independent dependency controls require generation version 5.");
                dependencyControls.Validate(); DependencyControlSettings=dependencyControls.Copy();
            }
        }
        public PuzzleRecipe(int seed,SurfaceShapeDefinition shape,DifficultyProfile profile,ExitRules rules=null,
            int maxRestarts=8,int attemptsPerCell=48,DifficultySettings difficulty=null,DependencyControlSettings dependencyControls=null)
            : this(seed,profile,rules,6,maxRestarts,attemptsPerCell,difficulty,dependencyControls)
        {
            Shape=shape??throw new ArgumentNullException(nameof(shape)); Surface=shape.Topology;
        }

    }

    // PCG-XSH-RR with specified integer arithmetic. No Unity random state or runtime-specific System.Random.
    internal sealed class StableRandom
    {
        private ulong state;
        public StableRandom(int seed,int restart)
        {
            state=0; NextUInt();
            state=unchecked(state + (uint)seed + (ulong)(uint)restart*0x9e3779b97f4a7c15UL); NextUInt();
        }
        public uint NextUInt()
        {
            ulong old=state;
            state=unchecked(old*6364136223846793005UL+1442695040888963407UL);
            uint value=(uint)(((old>>18)^old)>>27);
            int rotation=(int)(old>>59);
            return (value>>rotation)|(value<<((-rotation)&31));
        }
        public int Next(int count)
        {
            if (count<=0) throw new ArgumentOutOfRangeException(nameof(count));
            uint bound=(uint)count, threshold=unchecked(0u-bound)%bound, sample;
            do { sample=NextUInt(); } while(sample<threshold);
            return (int)(sample%bound);
        }
        public bool Chance(double probability) => NextUInt()/(double)4294967296UL < probability;
    }
}
