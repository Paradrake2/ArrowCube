using System;
using System.Collections.Generic;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Topology;

namespace ArrowCube.Gameplay
{
    [Serializable]
    public sealed class PathSave { public int Id,Heading; public int[] Cells; }
    [Serializable]
    public sealed class GateSave { public int Cell,Direction; }
    [Serializable]
    public sealed class PuzzleSave
    {
        public string Name;
        public int GridSize;
        public bool EdgeExits,DirectionalGates;
        public List<PathSave> Paths=new List<PathSave>();
        public List<GateSave> Gates=new List<GateSave>();
        public static PuzzleSave Capture(PuzzleDefinition puzzle)
        {
            var result=new PuzzleSave { Name=puzzle.Name,GridSize=puzzle.Topology.GridSize,EdgeExits=puzzle.Rules.EnableCubeEdgeExits,DirectionalGates=puzzle.Rules.EnableDirectionalGates };
            foreach(var path in puzzle.Paths)
            {
                var cells=new int[path.Cells.Count]; for(int i=0;i<cells.Length;i++) cells[i]=puzzle.Topology.GetCellIndex(path.Cells[i]);
                result.Paths.Add(new PathSave { Id=path.Id,Heading=(int)path.HeadDirection,Cells=cells });
            }
            foreach(var gate in puzzle.Exits.Gates) result.Gates.Add(new GateSave { Cell=puzzle.Topology.GetCellIndex(gate.Cell),Direction=(int)gate.Direction });
            return result;
        }
        public PuzzleDefinition Restore()
        {
            if(GridSize<1 || GridSize>64 || Paths==null || Paths.Count<1 || Paths.Count>30000 || Gates==null || Gates.Count>6*GridSize*GridSize*4
                || string.IsNullOrWhiteSpace(Name) || !EdgeExits && !DirectionalGates) throw new ArgumentException("Invalid saved puzzle bounds.");
            var t=new CubeTopology(GridSize); var paths=new List<ArrowPath>(); var gates=new List<ExitGate>();
            int total=0;
            foreach(var item in Paths)
            {
                if(item==null || item.Cells==null || item.Cells.Length<2 || item.Cells.Length>t.CellCount || item.Heading<0 || item.Heading>3)
                    throw new ArgumentException("Invalid saved path.");
                total+=item.Cells.Length; if(total>t.CellCount) throw new ArgumentException("Saved puzzle exceeds the surface.");
                var cells=new SurfaceCoordinate[item.Cells.Length]; for(int i=0;i<cells.Length;i++) cells[i]=t.GetCoordinate(item.Cells[i]);
                paths.Add(new ArrowPath(item.Id,t,cells,(SurfaceDirection)item.Heading));
            }
            foreach(var item in Gates)
            {
                if(item==null || item.Direction<0 || item.Direction>3) throw new ArgumentException("Invalid saved gate.");
                gates.Add(new ExitGate(t.GetCoordinate(item.Cell),(SurfaceDirection)item.Direction));
            }
            var result=new PuzzleDefinition(Name,t,paths,new ExitMap(t,gates),new ExitRules(EdgeExits,DirectionalGates));
            var validation=new PuzzleValidator().Validate(result);
            if(!validation.Succeeded) throw new ArgumentException("Saved puzzle cannot be solved: "+validation.Error);
            return result;
        }
    }
    [Serializable]
    public sealed class ProfileSave
    {
        public string Name;
        public int GridSize,MinPathLength,MaxPathLength,MinPathCount,MinCrossFacePaths,MaxGates,MaxPathCount;
        public double MinimumCoverage,TargetCoverage,MinimumBlockedFraction,MinimumAverageTurns,TurnProbability;
        public static ProfileSave Capture(DifficultyProfile p) => new ProfileSave { Name=p.Name,GridSize=p.GridSize,MinPathLength=p.MinPathLength,
            MaxPathLength=p.MaxPathLength,MinPathCount=p.MinPathCount,MinCrossFacePaths=p.MinCrossFacePaths,MaxGates=p.MaxGates,MaxPathCount=p.MaxPathCount,
            MinimumCoverage=p.MinimumCoverage,TargetCoverage=p.TargetCoverage,MinimumBlockedFraction=p.MinimumBlockedFraction,MinimumAverageTurns=p.MinimumAverageTurns,TurnProbability=p.TurnProbability };
        public DifficultyProfile Restore() => new DifficultyProfile(Name,GridSize,MinPathLength,MaxPathLength,MinPathCount,MinCrossFacePaths,
            MinimumCoverage,TargetCoverage,MinimumBlockedFraction,MinimumAverageTurns,TurnProbability,MaxGates,MaxPathCount);
    }
    [Serializable]
    public sealed class RecipeSave
    {
        public int Seed,Version,MaxRestarts,AttemptsPerCell;
        public bool EdgeExits,DirectionalGates;
        public bool HasDifficulty,HasDependencies;
        public ProfileSave Profile;
        public DifficultySettings Difficulty;
        public DependencyControlSettings Dependencies;
        public string ExactData;
        public static RecipeSave Capture(PuzzleRecipe r) => new RecipeSave { Seed=r.Seed,Version=r.GenerationVersion,MaxRestarts=r.MaxRestarts,
            AttemptsPerCell=r.AttemptsPerCell,EdgeExits=r.Rules.EnableCubeEdgeExits,DirectionalGates=r.Rules.EnableDirectionalGates,
            Profile=ProfileSave.Capture(r.Profile),Difficulty=r.Difficulty,Dependencies=r.DependencyControls,
            HasDifficulty=r.Difficulty!=null,HasDependencies=r.DependencyControls!=null,ExactData=RecipeBinary.Encode(r) };
        public PuzzleRecipe Restore()
        {
            var recipe=RecipeBinary.Decode(ExactData);
            if(recipe.Seed!=Seed || recipe.GenerationVersion!=Version) throw new ArgumentException("Saved recipe header does not match its data.");
            return recipe;
        }
    }
    [Serializable]
    public sealed class ActiveLevelSave
    {
        public int Level=1;
        public bool Sandbox,VictoryRecorded;
        public string Fingerprint;
        public RecipeSave Recipe;
        public PuzzleSave Puzzle;
        public SessionSave Session;
        public GameplaySession Restore()
        {
            if(Level<1 || Puzzle==null || Session==null) throw new ArgumentException("Incomplete active level.");
            var definition=Puzzle.Restore();
            // Unity's inline JSON serializer recreates null custom-class fields as empty
            // objects. Version zero is the explicit absence marker for manual fixtures.
            if(Recipe!=null && Recipe.Version!=0)
            {
                var recipe=Recipe.Restore();
                if(recipe.Profile.GridSize!=definition.Topology.GridSize || recipe.Rules.EnableCubeEdgeExits!=definition.Rules.EnableCubeEdgeExits
                    || recipe.Rules.EnableDirectionalGates!=definition.Rules.EnableDirectionalGates) throw new ArgumentException("Saved recipe does not match the puzzle.");
                if(recipe.GenerationVersion>=5 && !new PuzzleValidator().Validate(definition,requireClearInitialExit:true).Succeeded)
                    throw new ArgumentException("Saved new-version puzzle points into itself.");
            }
            return GameplaySession.Restore(definition,Session);
        }
    }
}
