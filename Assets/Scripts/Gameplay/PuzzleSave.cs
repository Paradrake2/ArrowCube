using System;
using System.Collections.Generic;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Topology;

namespace ArrowCube.Gameplay
{
    [Serializable]
    public sealed class PathSave { public int Id,Heading; public int[] Cells; public int[] Directions; }
    [Serializable]
    public sealed class GateSave { public int Cell,Direction; }
    [Serializable]
    public sealed class CellBoundarySave { public int Neighbor,Direction; public bool GeometricEdge,EdgeExit; }
    [Serializable]
    public sealed class SurfaceCellSave { public int Id; public List<CellBoundarySave> Boundaries=new List<CellBoundarySave>(); }
    [Serializable]
    public sealed class PuzzleSave
    {
        public string Name;
        public int GridSize;
        public bool EdgeExits,DirectionalGates;
        public List<PathSave> Paths=new List<PathSave>();
        public List<GateSave> Gates=new List<GateSave>();
        // Zero/missing retains the existing cube schema. One stores an explicit closed graph.
        public int SurfaceFormat;
        public List<SurfaceCellSave> SurfaceCells;
        public static PuzzleSave Capture(PuzzleDefinition puzzle)
        {
            if(puzzle==null) throw new ArgumentNullException(nameof(puzzle));
            var cube=puzzle.Surface as CubeTopology;
            var result=new PuzzleSave { Name=puzzle.Name,GridSize=cube?.GridSize??0,SurfaceFormat=cube==null ? 1 : 0,
                EdgeExits=puzzle.Rules.EnableCubeEdgeExits,DirectionalGates=puzzle.Rules.EnableDirectionalGates };
            if(cube==null)
            {
                result.SurfaceCells=new List<SurfaceCellSave>();
                foreach(int id in puzzle.Surface.EnumerateCellIds())
                {
                    var cell=new SurfaceCellSave { Id=id };
                    for(int d=0;d<4;d++)
                    {
                        var boundary=puzzle.Surface.GetBoundary(id,(SurfaceDirection)d);
                        if(boundary.Kind!=BoundaryConnectionKind.Connected) throw new NotSupportedException("Open-surface saves are not implemented.");
                        cell.Boundaries.Add(new CellBoundarySave { Neighbor=boundary.NeighborCellId.Value,Direction=(int)boundary.NewDirection,
                            GeometricEdge=boundary.IsGeometricEdge,EdgeExit=boundary.AllowsEdgeExit });
                    }
                    result.SurfaceCells.Add(cell);
                }
            }
            foreach(var path in puzzle.Paths)
            {
                var cells=new List<int>(path.CellIds).ToArray();
                int[] directions=null;
                if(cube==null) { directions=new int[path.BodyDirections.Count]; for(int i=0;i<directions.Length;i++) directions[i]=(int)path.BodyDirections[i]; }
                result.Paths.Add(new PathSave { Id=path.Id,Heading=(int)path.HeadDirection,Cells=cells,Directions=directions });
            }
            foreach(var gate in puzzle.Exits.CellGates) result.Gates.Add(new GateSave { Cell=gate.CellId,Direction=(int)gate.Direction });
            return result;
        }
        public PuzzleDefinition Restore()
        {
            if(SurfaceFormat<0 || SurfaceFormat>1 || SurfaceFormat==0 && (GridSize<1 || GridSize>64)
                || Paths==null || Paths.Count<1 || Paths.Count>30000 || Gates==null
                || string.IsNullOrWhiteSpace(Name) || !EdgeExits && !DirectionalGates) throw new ArgumentException("Invalid saved puzzle bounds.");
            ISurfaceTopology t;
            if(SurfaceFormat==0) t=new CubeTopology(GridSize);
            else
            {
                if(SurfaceCells==null || SurfaceCells.Count<1 || SurfaceCells.Count>100000) throw new ArgumentException("Invalid saved surface bounds.");
                var builder=new SurfaceTopologyBuilder();
                foreach(var cell in SurfaceCells)
                { if(cell==null) throw new ArgumentException("Missing saved cell."); builder.AddCell(cell.Id); }
                foreach(var cell in SurfaceCells)
                {
                    if(cell.Boundaries==null || cell.Boundaries.Count!=4) throw new ArgumentException("Invalid saved boundaries.");
                    for(int d=0;d<4;d++)
                    {
                        var boundary=cell.Boundaries[d]; if(boundary==null) throw new ArgumentException("Missing saved boundary.");
                        if(boundary.Direction<0 || boundary.Direction>3) throw new ArgumentException("Invalid saved boundary heading.");
                        builder.SetBoundary(cell.Id,(SurfaceDirection)d,new CellBoundary(boundary.Neighbor,(SurfaceDirection)boundary.Direction,boundary.GeometricEdge,boundary.EdgeExit));
                    }
                }
                try { t=builder.Build(); } catch(InvalidOperationException error) { throw new ArgumentException("Invalid saved topology: "+error.Message,error); }
            }
            if(Gates.Count>t.CellCount*4) throw new ArgumentException("Too many saved gates.");
            var paths=new List<ArrowPath>(); var gates=new List<CellGate>();
            int total=0;
            foreach(var item in Paths)
            {
                if(item==null || item.Cells==null || item.Cells.Length<(SurfaceFormat==0 ? 2 : 1) || item.Cells.Length>t.CellCount || item.Heading<0 || item.Heading>3)
                    throw new ArgumentException("Invalid saved path.");
                total+=item.Cells.Length; if(total>t.CellCount) throw new ArgumentException("Saved puzzle exceeds the surface.");
                List<SurfaceDirection> directions=null;
                if(SurfaceFormat==1 && item.Directions!=null)
                { directions=new List<SurfaceDirection>(); foreach(int direction in item.Directions) { if(direction<0 || direction>3) throw new ArgumentException("Invalid saved body direction."); directions.Add((SurfaceDirection)direction); } }
                paths.Add(new ArrowPath(item.Id,t,item.Cells,(SurfaceDirection)item.Heading,directions));
            }
            foreach(var item in Gates)
            {
                if(item==null || item.Direction<0 || item.Direction>3 || !t.ContainsCell(item.Cell)) throw new ArgumentException("Invalid saved gate.");
                gates.Add(new CellGate(item.Cell,(SurfaceDirection)item.Direction));
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
                if(!(definition.Surface is CubeTopology)) throw new ArgumentException("Procedural recipes currently require a cube surface.");
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
