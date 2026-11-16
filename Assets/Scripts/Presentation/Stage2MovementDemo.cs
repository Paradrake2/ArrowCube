using System.Collections;
using System.Collections.Generic;
using ArrowCube.Movement;
using ArrowCube.Generation;
using ArrowCube.Topology;
using UnityEngine;

namespace ArrowCube.Presentation
{
/// <summary>Shared puzzle renderer and animation driver; development controls remain optional.</summary>
public sealed class Stage2MovementDemo : MonoBehaviour
{
    [SerializeField, Range(0, ManualPuzzles.Count - 1)] private int puzzleIndex = 6;
    [SerializeField, Min(0.1f)] private float cellsPerSecond = 6;
    [SerializeField, Range(0.05f, 0.45f)]
    [Tooltip("Thickness of every arrow body, as a fraction of a grid cell. Filled triangle heads scale to match. Updates live in Play mode.")]
    private float arrowThickness = 0.22f;
    private float appliedArrowThickness = float.NaN;
    [SerializeField] private bool usePuzzleExitRules = true;
    [SerializeField] private bool enableCubeEdgeExits = true;
    [SerializeField] private bool enableDirectionalGates = true;
    private readonly PathMovementSimulator simulator = new PathMovementSimulator();
    private readonly Dictionary<int, PathVisual> visuals = new Dictionary<int, PathVisual>();
    private readonly List<Vector3> points = new List<Vector3>();
    private readonly List<Vector3> segmentNormals = new List<Vector3>();
    private readonly List<Material> materials = new List<Material>();
    private PuzzleDefinition puzzle;
    private bool manualPuzzle = true;
    private SurfaceGrid grid;
    private ExitRules rules;
    private ISurfaceGeometry geometry;
    private PathAnimationGeometry animationGeometry;
    private Transform content;
    private Camera demoCamera;
    private bool busy;
    private string status;
    private float collisionUntil;
    private Vector3 collisionPoint;
    private SurfacePathRenderer contactMarker;
    private Vector2 scrollPosition;
    private bool debugDifficulty;
    private long debugRevision=-1;
    public bool DebugDifficulty { get => debugDifficulty; set { debugDifficulty=value; debugRevision=-1; } }
    public bool DebugDependencies { get; set; }
    public bool DebugSolverSelections { get; set; }
    private bool debugInspect;
    private int debugSelected=-1;
    private readonly RemovalTrajectoryCache debugTrajectories=new RemovalTrajectoryCache(128);
    public bool DebugInspectDependencies { get => debugInspect; set { debugInspect=value; debugRevision=-1; } }
    public int DebugSelectedPath => debugSelected;
    public bool InspectDependency(int id)
    {
        if(!(puzzle is GeneratedPuzzle generated) || generated.DependencyGraph==null || !grid.Paths.ContainsKey(id)) return false;
        debugSelected=id; debugRevision=-1; return true;
    }
    public void InspectNextDependency()
    {
        if(puzzle==null) return;
        int selected=-1;
        foreach(var path in puzzle.Paths) if(grid.Paths.ContainsKey(path.Id)) { if(selected<0) selected=path.Id; if(path.Id>debugSelected) { selected=path.Id; break; } }
        if(selected>=0) InspectDependency(selected);
    }
    public string DependencyDescription()
    {
        if(!(puzzle is GeneratedPuzzle generated) || generated.DependencyGraph==null) return "Generate a version-4 measured puzzle to inspect dependencies.";
        if(debugSelected<0) return "Enable inspection, then tap an arrow or choose Next arrow. Inspection taps do not move arrows.";
        var node=generated.DependencyGraph.GetNode(debugSelected);
        int faceMask=node.FaceMask; foreach(int id in node.ConfirmedBlockers) faceMask|=generated.DependencyGraph.GetNode(id).FaceMask;
        var names=new List<string>(); for(int face=0;face<6;face++) if((faceMask&(1<<face))!=0) names.Add(((CubeFace)face).ToString());
        int remaining=0; foreach(int id in node.ConfirmedBlockers) if(grid.Paths.ContainsKey(id)) remaining++;
        return $"Arrow {node.PathId}: depth {node.Depth}\nPotential: {string.Join(", ",node.PotentialBlockers)}\nConfirmed: {string.Join(", ",node.ConfirmedBlockers)}\nIndependent group: {string.Join(", ",node.IndependentBlockers)}\nBlocks: {string.Join(", ",node.PathsBlocked)}\nRemaining blockers: {remaining}\nFaces involved: {string.Join(", ",names)}\nSubset checks: {node.SubsetChecks}; exhaustive: {node.ExhaustiveSubsetsVerified}\nIndependence width exact: {node.IndependentWidthExact}\nGraph is for the original fixed geometry. Colored spheres show the full body sweep.";
    }
    public SurfaceGrid Grid => grid;
    public MovementPlan LastPlan { get; private set; }
    public bool IsAnimating => busy;
    public ExitRules Rules => rules;
    public PuzzleDefinition Puzzle => puzzle;
    public Camera InspectionCamera => demoCamera;
    public ISurfaceGeometry SurfaceGeometry => geometry;
    public Bounds SurfaceBounds => geometry?.Bounds??new Bounds(Vector3.zero,Vector3.one*4);
    public float SurfaceSideLength => Mathf.Max(SurfaceBounds.size.x,SurfaceBounds.size.y,SurfaceBounds.size.z);
    public float ArrowBodyWidth => geometry==null ? 0 : geometry.ReferenceCellSize * arrowThickness;
    public float ArrowThickness
    {
        get => arrowThickness;
        set
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new System.ArgumentOutOfRangeException(nameof(value));
            arrowThickness = Mathf.Clamp(value, 0.05f, 0.45f);
            ApplyArrowThickness();
        }
    }
    public string StatusMessage => status;
    public event System.Action<PuzzleDefinition> PuzzleLoaded;
    public event System.Action<CubeFace> FaceFocusRequested;
    public event System.Action<int> CellFocusRequested;
    public Transform GetArrowVisual(int id) => visuals.TryGetValue(id,out var visual) ? visual.Root.transform : null;
    public System.Func<int,MovementPlan> PlanProvider { get; set; }
    public event System.Action<MovementPlan,bool> AttemptCompleted;
    public bool StartupOwnedExternally { get; set; }
    public bool AnimationPaused { get; set; }
    public bool ShowControls { get; set; } = true;
    public float AnimationCellsPerSecond
    {
        get => cellsPerSecond;
        set
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(value));
            cellsPerSecond = value;
        }
    }

    private sealed class PathVisual
    {
        public GameObject Root;
        public SurfacePathRenderer Body;
        public TriangleArrowHead Arrow;
        public SurfaceRail Rail;
        public ArrowPath Path;
        public SurfaceRail CurrentRail;
        public float Progress;
    }

    private void Start()
    {
        // The generator owns startup when present, including reporting failed recipes.
        // Never replace a failed custom generation with the manual four-arrow demo.
        if (puzzle == null && !StartupOwnedExternally && GetComponent<Stage3GenerationDemo>() == null) LoadPuzzle(puzzleIndex, usePuzzleExitRules ? null
            : new ExitRules(enableCubeEdgeExits, enableDirectionalGates));
    }

    private void OnValidate() => arrowThickness = Mathf.Clamp(arrowThickness, 0.05f, 0.45f);

    private void LateUpdate()
    {
        // Inspector serialization writes the field directly, bypassing the public setter.
        if (geometry != null && arrowThickness != appliedArrowThickness) ApplyArrowThickness();
        if(grid!=null && debugRevision!=grid.Revision)
        {
            debugRevision=grid.Revision;
            foreach(var pair in visuals)
            {
                if(!grid.Paths.ContainsKey(pair.Key)) continue;
                pair.Value.Body.SharedMaterial.color=debugDifficulty
                    ? simulator.Simulate(grid,pair.Key,puzzle.Exits,rules).Succeeded ? new Color(.1f,.8f,.3f) : new Color(.95f,.2f,.2f)
                    : PathColor(pair.Key);
                if(debugInspect && puzzle is GeneratedPuzzle generated && generated.DependencyGraph!=null && debugSelected>=0)
                {
                    var node=generated.DependencyGraph.GetNode(debugSelected);
                    if(pair.Key==debugSelected) pair.Value.Body.SharedMaterial.color=Color.yellow;
                    for(int i=0;i<node.ConfirmedBlockers.Length;i++) if(node.ConfirmedBlockers[i]==pair.Key)
                        pair.Value.Body.SharedMaterial.color=Color.HSVToRGB((i*.23f+.48f)%1,.85f,1);
                }
            }
        }
    }

    private void ApplyArrowThickness()
    {
        if (geometry == null) return;
        foreach (var visual in visuals.Values)
        {
            Draw(visual,visual.Path,visual.CurrentRail,visual.Progress);
        }
        appliedArrowThickness = arrowThickness;
    }

    public void LoadPuzzle(int index, ExitRules overrideRules = null)
    {
        var definition = ManualPuzzles.Create(index);
        puzzleIndex = index;
        manualPuzzle = true;
        LoadContent(definition, overrideRules);
    }

    public void LoadDefinition(PuzzleDefinition definition,ISurfaceGeometry surfaceGeometry=null)
    {
        if (definition == null) throw new System.ArgumentNullException(nameof(definition));
        manualPuzzle = false;
        LoadContent(definition, null,null,surfaceGeometry);
    }
    public void LoadSession(PuzzleDefinition definition,SurfaceGrid activeGrid,ISurfaceGeometry surfaceGeometry=null)
    {
        if(definition==null || activeGrid==null || !ReferenceEquals(definition.Surface,activeGrid.Surface))
            throw new System.ArgumentException("Session and renderer must share the same topology and grid.");
        foreach(var pair in activeGrid.Paths)
        {
            bool found=false; foreach(var path in definition.Paths) if(ReferenceEquals(path,pair.Value)) { found=true; break; }
            if(!found) throw new System.ArgumentException("Session contains a foreign path.");
        }
        manualPuzzle=false; LoadContent(definition,null,activeGrid,surfaceGeometry);
    }

    private void LoadContent(PuzzleDefinition definition, ExitRules overrideRules,SurfaceGrid activeGrid=null,ISurfaceGeometry surfaceGeometry=null)
    {
        var nextGeometry=surfaceGeometry ?? (geometry!=null && ReferenceEquals(geometry.Surface,definition.Surface) ? geometry : AuthoredSurfaceDemos.Resolve(definition));
        if(!ReferenceEquals(nextGeometry.Surface,definition.Surface)) throw new System.ArgumentException("Geometry and puzzle must share the same topology.");
        StopAllCoroutines();
        busy = false;
        LastPlan = null;
        collisionUntil = 0;
        contactMarker = null;
        if (content != null) { content.gameObject.SetActive(false); Destroy(content.gameObject); }
        foreach (var material in materials) Destroy(material);
        materials.Clear(); visuals.Clear();
        puzzle = definition;
        rules = overrideRules ?? puzzle.Rules;
        usePuzzleExitRules = overrideRules == null;
        enableCubeEdgeExits = rules.EnableCubeEdgeExits;
        enableDirectionalGates = rules.EnableDirectionalGates;
        grid = activeGrid ?? puzzle.CreateGrid();
        debugSelected=-1;
        debugRevision=-1;
        geometry = nextGeometry;
        animationGeometry = new PathAnimationGeometry(geometry);
        content = new GameObject("Stage 2 runtime objects").transform;
        content.SetParent(transform, false);
        var cube = new GameObject("Puzzle surface");
        cube.transform.SetParent(content, false);
        var faceMaterials=new Material[geometry.RenderQuads.Count];
        for(int face=0;face<faceMaterials.Length;face++) faceMaterials[face]=Material(geometry.RenderQuads[face].Color);
        if(geometry is CubeSurfaceGeometry cubeGeometry) cube.AddComponent<ColoredCubeSurface>().Initialize(puzzle.Topology,cubeGeometry.SideLength,faceMaterials);
        else cube.AddComponent<SurfaceMeshRenderer>().Initialize(geometry,faceMaterials);

        foreach (var path in puzzle.Paths)
        {
            if(!grid.Paths.ContainsKey(path.Id)) continue;
            var visual = new PathVisual { Root = new GameObject($"Arrow {path.Id}"), Rail = SurfaceRail.FromPath(path), Path=path };
            visual.Root.transform.SetParent(content, false);
            var material = Material(PathColor(path.Id));
            var bodyObject=new GameObject("Body"); bodyObject.transform.SetParent(visual.Root.transform,false);
            visual.Body=bodyObject.AddComponent<SurfacePathRenderer>(); visual.Body.Initialize(material);
            var headObject=new GameObject("Arrowhead"); headObject.transform.SetParent(visual.Root.transform,false);
            visual.Arrow=headObject.AddComponent<TriangleArrowHead>(); visual.Arrow.Initialize(material);
            visuals.Add(path.Id, visual);
            Draw(visual, path, visual.Rail, 0);
        }
        appliedArrowThickness = arrowThickness;
        if (rules.EnableDirectionalGates)
            foreach (var gate in puzzle.Exits.CellGates) DrawGate(gate);
        if (demoCamera == null)
        {
            var cameraObject = new GameObject("Stage 2 inspection camera");
            cameraObject.transform.SetParent(transform, false);
            demoCamera = cameraObject.AddComponent<Camera>();
            demoCamera.clearFlags = CameraClearFlags.SolidColor;
            demoCamera.backgroundColor = new Color(0.22f, 0.42f, 0.67f);
            demoCamera.orthographic = true;
            demoCamera.orthographicSize = 4.4f;
            demoCamera.rect = new Rect(0.42f, 0, 0.58f, 1);
            demoCamera.nearClipPlane = 0.05f;
            demoCamera.farClipPlane = 100;
            demoCamera.depth = 10;
        }
        demoCamera.transform.localPosition = new Vector3(7, 6, 9);
        demoCamera.transform.LookAt(transform.position, transform.up);
        status = manualPuzzle && puzzleIndex >= 5 ? "Red -> Green -> Orange. Blue can leave independently."
            : "Activate a path below. Cyan markers are directional exit gates.";
        PuzzleLoaded?.Invoke(puzzle);
    }

    public void ConfigureExits(bool cubeEdgeExits, bool directionalGates)
    {
        if (!manualPuzzle) throw new System.InvalidOperationException("Regenerate a generated level with its new exit rules.");
        LoadPuzzle(puzzleIndex, new ExitRules(cubeEdgeExits, directionalGates));
    }

    public void RestartPuzzle()
    {
        if (manualPuzzle) LoadPuzzle(puzzleIndex, rules); else LoadContent(puzzle, rules);
    }

    public bool TryActivate(int id)
    {
        if (busy || grid == null || !grid.Paths.ContainsKey(id)) return false;
        LastPlan = PlanProvider==null ? simulator.Simulate(grid, id, puzzle.Exits, rules) : PlanProvider(id);
        if(LastPlan==null) return false;
        busy = true;
        StartCoroutine(Animate(LastPlan));
        return true;
    }

    private IEnumerator Animate(MovementPlan plan)
    {
        bool committed=false;
        var visual = visuals[plan.Path.Id];
        var movementRail=SurfaceRail.FromPlan(plan);
        if (plan.Succeeded)
        {
            status = $"{PathLabel(plan.Path.Id)}: clear trajectory; "
                + (plan.ExitType == ExitType.CubeEdge ? "leaving through face edge." : "leaving through directional gate.");
            float progress = 0;
            while (progress < plan.StepCount)
            {
                if(!AnimationPaused) progress = Mathf.Min(plan.StepCount, progress + Time.deltaTime * Mathf.Max(0.1f, cellsPerSecond));
                Draw(visual, plan.Path, movementRail, progress);
                yield return null;
            }
            if (grid.TryCommit(plan))
            {
                committed=true;
                visual.Root.SetActive(false);
                Destroy(visual.Root);
                visuals.Remove(plan.Path.Id);
                status = grid.Paths.Count == 0 ? "All paths cleared." : $"Removed path {plan.Path.Id}. Try the remaining path.";
            }
            else { Draw(visual, plan.Path, visual.Rail, 0); status = "Puzzle changed during animation; move cancelled."; }
        }
        else
        {
            // The simulator retains every safe step. Extend just the final step to
            // approach the visible obstruction, without ever committing this plan.
            var attemptRail=SurfaceRail.FromPlan(plan,true);
            bool obstructed=plan.BlockedCellId.HasValue;
            float travel=obstructed ? ContactProgress(plan,attemptRail) : Mathf.Min(.15f,plan.StepCount);
            status = plan.Outcome == MovementOutcome.BlockedByPath
                ? $"Path {plan.Path.Id} blocked by {plan.BlockingPathId} at cell {plan.BlockedCellId}."
                : plan.Outcome == MovementOutcome.NoExit ? "No enabled exit is reachable: the surface route loops."
                : "The moving head would hit its own body.";
            float progress=0;
            while(progress<travel)
            {
                if(!AnimationPaused) progress=Mathf.Min(travel,progress+Time.deltaTime*Mathf.Max(.1f,cellsPerSecond));
                Draw(visual,plan.Path,attemptRail,progress);
                yield return null;
            }
            visual.Body.SharedMaterial.color=Color.red;
            PathVisual blocker=null; Color blockerColor=default;
            if(obstructed)
            {
                collisionPoint=animationGeometry.Evaluate(attemptRail,plan.Path.CellIds.Count-1+travel,geometry.ReferenceCellSize*.04f);
                collisionUntil=Time.unscaledTime+.8f;
                ShowContact(plan.BlockedCellId.Value);
                if(plan.BlockingPathId.HasValue && plan.BlockingPathId.Value!=plan.Path.Id && visuals.TryGetValue(plan.BlockingPathId.Value,out blocker))
                { blockerColor=blocker.Body.SharedMaterial.color; blocker.Body.SharedMaterial.color=new Color(1,.4f,.02f); }
            }
            for(float elapsed=0;elapsed<.18f;)
            {
                if(!AnimationPaused) elapsed+=Time.deltaTime;
                yield return null;
            }
            while(progress>0)
            {
                if(!AnimationPaused) progress=Mathf.Max(0,progress-Time.deltaTime*Mathf.Max(.1f,cellsPerSecond)*1.5f);
                Draw(visual,plan.Path,attemptRail,progress);
                yield return null;
            }
            if(contactMarker!=null) contactMarker.gameObject.SetActive(false);
            if(blocker!=null) blocker.Body.SharedMaterial.color=blockerColor;
            visual.Body.SharedMaterial.color = PathColor(plan.Path.Id);
            debugRevision=-1;
            Draw(visual, plan.Path, visual.Rail, 0);
        }
        busy = false;
        AttemptCompleted?.Invoke(plan,committed);
    }

    private float ContactProgress(MovementPlan plan,SurfaceRail rail)
    {
        float fraction=.75f; // Conservative clearance for the moving body's self-collision.
        if(plan.Outcome==MovementOutcome.BlockedByPath && plan.BlockingPathId.HasValue && visuals.TryGetValue(plan.BlockingPathId.Value,out var blocker))
        {
            fraction=1;
            int last=rail.Count-2;
            bool seam=animationGeometry.HasBoundaryKnot(rail,last);
            float split=animationGeometry.BoundaryFraction(rail,last);
            var body=blocker.Body.GetComponent<MeshFilter>().sharedMesh;
            var head=blocker.Arrow.GetComponent<MeshFilter>().sharedMesh;
            for(int leg=0;leg<(seam ? 2 : 1);leg++)
            {
                float start=seam && leg==1 ? split : 0,length=seam ? leg==0 ? split : 1-split : 1;
                var from=animationGeometry.Evaluate(rail,last+start,geometry.ReferenceCellSize*.04f);
                var to=animationGeometry.Evaluate(rail,last+start+length,geometry.ReferenceCellSize*.04f);
                float hit=Mathf.Min(ArrowContactGeometry.FirstContact(from,to,body),ArrowContactGeometry.FirstContact(from,to,head));
                if(!float.IsPositiveInfinity(hit)) fraction=Mathf.Min(fraction,start+length*hit);
            }
        }
        return plan.StepCount+Mathf.Max(0,fraction-.02f);
    }

    private void ShowContact(int cellId)
    {
        if(contactMarker==null)
        {
            var marker=new GameObject("Block contact"); marker.transform.SetParent(content,false);
            contactMarker=marker.AddComponent<SurfacePathRenderer>(); contactMarker.Initialize(Material(new Color(1,.3f,.02f)));
        }
        var right=geometry.GetDirection(cellId,SurfaceDirection.Right);
        var normal=geometry.GetCellNormal(cellId); var up=Vector3.Cross(normal,right).normalized;
        var ring=new List<Vector3>(); var normals=new List<Vector3>();
        for(int i=0;i<=20;i++)
        {
            float angle=i*Mathf.PI*.1f;
            ring.Add(collisionPoint+normal*(geometry.ReferenceCellSize*.03f)+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*(geometry.ReferenceCellSize*.20f));
            if(i>0) normals.Add(normal);
        }
        contactMarker.Draw(ring,normals,geometry.ReferenceCellSize*.045f); contactMarker.gameObject.SetActive(true);
    }

    public bool HighlightPath(int id)
    {
        if(busy || !visuals.TryGetValue(id,out var visual)) return false;
        StartCoroutine(Highlight(visual)); return true;
    }
    private IEnumerator Highlight(PathVisual visual)
    {
        float end=Time.unscaledTime+1.5f;
        while(Time.unscaledTime<end && visual.Root!=null)
        {
            if(!busy) visual.Body.SharedMaterial.color=new Color(.1f,.85f,.35f);
            yield return null;
        }
        if(visual.Root!=null && !busy) visual.Body.SharedMaterial.color=PathColor(visual.Path.Id);
    }

    private Color PathColor(int id)
    {
        if (!manualPuzzle || puzzleIndex < 5) return Color.black;
        switch (id)
        {
            case 10: return new Color(0.95f, 0.08f, 0.13f);
            case 20: return new Color(0.05f, 0.65f, 0.17f);
            case 30: return new Color(1f, 0.38f, 0.1f);
            default: return new Color(0, 0.58f, 0.95f);
        }
    }

    private string PathLabel(int id)
    {
        if (!manualPuzzle || puzzleIndex < 5) return $"Path {id}";
        string color = id == 10 ? "Red" : id == 20 ? "Green" : id == 30 ? "Orange" : "Blue";
        return $"{color} ({id})";
    }

    private void Draw(PathVisual visual, ArrowPath path, SurfaceRail rail, float progress)
    {
        visual.CurrentRail=rail; visual.Progress=progress;
        animationGeometry.WritePolyline(rail, path.CellIds.Count, progress, points, geometry.ReferenceCellSize * 0.04f,segmentNormals);
        float distance = path.CellIds.Count - 1 + progress;
        var head = points[0];
        Vector3 forward;
        if (distance < rail.Count - 1)
            forward = animationGeometry.Evaluate(rail, Mathf.Min(rail.Count - 1, distance + 0.001f), geometry.ReferenceCellSize * 0.04f) - head;
        else if (distance > 0)
            forward = head - animationGeometry.Evaluate(rail, Mathf.Max(0, distance - 0.001f), geometry.ReferenceCellSize * 0.04f);
        else forward = geometry.GetDirection(path.HeadCellId,path.HeadDirection);
        forward.Normalize();
        var normal=distance>0 ? animationGeometry.GetSegmentNormal(rail,Mathf.Max(0,distance-.00001f))
            : geometry.GetCellNormal(path.HeadCellId);
        visual.Arrow.DrawAndTrimBody(points,segmentNormals,forward,normal,geometry.ReferenceCellSize,ArrowBodyWidth);
        visual.Body.Draw(points,segmentNormals,ArrowBodyWidth);
    }

    private void DrawGate(CellGate gate)
    {
        var center = geometry.GetCellCenter(gate.CellId, geometry.ReferenceCellSize * 0.015f);
        var forward = geometry.GetDirection(gate.CellId,gate.Direction);
        var normal = geometry.GetCellNormal(gate.CellId);
        var side = Vector3.Cross(normal, forward);
        var material = Material(new Color(0, 0.7f, 0.8f));
        var line = Line(content, $"Exit {gate.CellId} {gate.Direction}", material, geometry.ReferenceCellSize * 0.04f);
        line.positionCount = 5;
        line.SetPosition(0, center - side * geometry.ReferenceCellSize * 0.35f);
        line.SetPosition(1, center + forward * geometry.ReferenceCellSize * 0.35f);
        line.SetPosition(2, center + side * geometry.ReferenceCellSize * 0.35f);
        line.SetPosition(3, center - forward * geometry.ReferenceCellSize * 0.35f);
        line.SetPosition(4, center - side * geometry.ReferenceCellSize * 0.35f);
        var heading = Line(content, $"Exit heading {gate.Direction}", material, geometry.ReferenceCellSize * 0.055f);
        heading.positionCount = 3;
        heading.SetPosition(0, center - forward * geometry.ReferenceCellSize * 0.15f + side * geometry.ReferenceCellSize * 0.17f);
        heading.SetPosition(1, center + forward * geometry.ReferenceCellSize * 0.25f);
        heading.SetPosition(2, center - forward * geometry.ReferenceCellSize * 0.15f - side * geometry.ReferenceCellSize * 0.17f);
        var stem = Line(content, "Outward departure", material, geometry.ReferenceCellSize * 0.04f);
        stem.positionCount = 2;
        stem.SetPosition(0, center);
        stem.SetPosition(1, center + normal * geometry.ReferenceCellSize * 0.5f);
    }

    private LineRenderer Line(Transform parent, string objectName, Material material, float width)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.sharedMaterial = material;
        line.startWidth = line.endWidth = width;
        line.numCapVertices = line.numCornerVertices = 4;
        return line;
    }

    private Material Material(Color color)
    {
        var material = PuzzleMaterials.Create(color);
        materials.Add(material);
        return material;
    }

    private void OnGUI()
    {
        if (puzzle == null || !ShowControls || !(puzzle.Surface is CubeTopology)) return;
        float scale = Mathf.Clamp(Screen.width / 1000f, 0.7f, 1.5f);
        var oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        GUILayout.BeginArea(new Rect(12, 12, 410, Mathf.Max(150, Screen.height / scale - 24)), GUI.skin.box);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        GUILayout.Label("ARROW CUBE - STAGE 2 MOVEMENT LAB");
        GUILayout.Label(puzzle.Name);
        GUILayout.Label($"Remaining paths: {grid.Paths.Count}");
        string levelType = rules.EnableCubeEdgeExits
            ? (rules.EnableDirectionalGates ? "Hybrid" : "Ordinary edge exits")
            : (rules.EnableDirectionalGates ? "Gate-only" : "Both exits disabled");
        GUILayout.Label($"Level type: {levelType}");
        GUILayout.Label(status, GUILayout.MinHeight(48));
        GUI.enabled = !busy;
        foreach (var path in puzzle.Paths)
            if (grid.Paths.ContainsKey(path.Id) && GUILayout.Button($"Activate {PathLabel(path.Id)} ({path.Head.Face}, {path.HeadDirection})", GUILayout.Height(28))) TryActivate(path.Id);
        GUI.enabled = true;
        if (GUILayout.Button("Restart exact puzzle / cancel animation", GUILayout.Height(28))) RestartPuzzle();
        GUI.enabled = manualPuzzle;
        bool edges = GUILayout.Toggle(enableCubeEdgeExits, "Enable cube-edge exits (changing rules restarts puzzle)");
        bool gates = GUILayout.Toggle(enableDirectionalGates, "Enable directional gates");
        GUI.enabled = true;
        if (manualPuzzle && (edges != enableCubeEdgeExits || gates != enableDirectionalGates)) ConfigureExits(edges, gates);
        GUILayout.Space(10);
        GUILayout.Label("Manual puzzles");
        for (int i = 0; i < ManualPuzzles.Count; i++)
            if (GUILayout.Button(ManualPuzzles.Names[i])) LoadPuzzle(i);
        GUILayout.Space(10);
        GUILayout.Label("Inspect face");
        GUILayout.BeginHorizontal();
        for (int f = 0; f < 6; f++)
        {
            var face = (CubeFace)f;
            if (GUILayout.Button(face.ToString()))
            {
                FocusFace(face);
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.Label((rules.EnableCubeEdgeExits ? "Arrowheads leave through face edges. " : "Arrowheads wrap across ordinary edges. ")
            + (rules.EnableDirectionalGates ? "Cyan markers: directional gates." : "Directional gates are disabled.")
            + "\nBodies always follow their original bends and face crossings.");
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUI.matrix = oldMatrix;
    }

    public void FocusFace(CubeFace face)
    {
        if (puzzle == null || demoCamera == null || !(puzzle.Surface is CubeTopology)) return;
        if (FaceFocusRequested != null) { FaceFocusRequested.Invoke(face); return; }
        var frame = puzzle.Topology.GetOrientation(face);
        demoCamera.transform.position = transform.TransformPoint(CubeSurfaceGeometry.ToVector(frame.Normal) * 10);
        demoCamera.transform.LookAt(transform.position, transform.TransformDirection(CubeSurfaceGeometry.ToVector(frame.Up)));
    }

    public void FocusCell(int cellId)
    {
        if(geometry==null || demoCamera==null) return;
        if(CellFocusRequested!=null) { CellFocusRequested.Invoke(cellId); return; }
        var normal=transform.localToWorldMatrix.inverse.transpose.MultiplyVector(geometry.GetCellNormal(cellId)).normalized;
        demoCamera.transform.position=transform.TransformPoint(geometry.Bounds.center)+normal*SurfaceSideLength*3;
        demoCamera.transform.LookAt(transform.TransformPoint(geometry.Bounds.center),transform.TransformDirection(geometry.GetDirection(cellId,SurfaceDirection.Up)));
    }
    private Vector3 WorldCell(SurfaceCoordinate cell,float lift) => transform.TransformPoint(geometry.GetCellCenter(puzzle.Topology.GetCellIndex(cell),lift));

    private void OnDrawGizmos()
    {
        DrawDifficultyGizmos();
        if (!Application.isPlaying || Time.unscaledTime > collisionUntil || geometry == null) return;
        var matrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(collisionPoint, geometry.ReferenceCellSize * 0.4f);
        Gizmos.matrix = matrix;
    }

    private void DrawDifficultyGizmos()
    {
        if(!Application.isPlaying || geometry==null || !(puzzle is GeneratedPuzzle generated) || generated.Difficulty==null) return;
        if(debugInspect && debugSelected>=0 && generated.DependencyGraph!=null)
        {
            ArrowPath selected=null; foreach(var path in puzzle.Paths) if(path.Id==debugSelected) { selected=path; break; }
            if(selected!=null)
            {
                var sweep=debugTrajectories.Get(puzzle.Topology,selected,puzzle.Exits,puzzle.Rules);
                Gizmos.color=Color.yellow;
                foreach(var cell in sweep.SweptCells) Gizmos.DrawWireSphere(WorldCell(cell,.05f),geometry.ReferenceCellSize*.18f);
            }
        }
        if(DebugDependencies)
        {
            Gizmos.color=new Color(1,.5f,.1f,.65f);
            foreach(var link in generated.Difficulty.Dependencies)
                if(grid.Paths.TryGetValue(link.Path,out var from) && grid.Paths.TryGetValue(link.Blocker,out var to))
                    Gizmos.DrawLine(WorldCell(from.Head,.04f),WorldCell(to.Head,.04f));
        }
        if(DebugSolverSelections)
        {
            Gizmos.color=Color.yellow; var attempts=generated.Difficulty.Solvers[0].AttemptedPaths;
            for(int i=0;i<Mathf.Min(20,attempts.Length);i++)
                if(grid.Paths.TryGetValue(attempts[i],out var path)) Gizmos.DrawWireSphere(WorldCell(path.Head,.04f),geometry.ReferenceCellSize*.35f);
        }
    }

    private void OnDestroy()
    { foreach (var material in materials) if (material != null) Destroy(material); }
}
}
