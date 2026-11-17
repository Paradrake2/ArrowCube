using System.Collections;
using System;
using System.IO;
using System.Collections.Generic;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Topology;
using UnityEngine;

namespace ArrowCube.Presentation
{
    public enum GenerationLabShape { Cube, L, T, ObliquePrism, ConvertedSurface }
    public sealed class GenerationLabShapeOption
    {
        public string Label { get; }
        public GenerationLabShape Shape { get; }
        public ConvertedSurfaceAsset Asset { get; }
        internal GenerationLabShapeOption(string label,GenerationLabShape shape,ConvertedSurfaceAsset asset=null) { Label=label; Shape=shape; Asset=asset; }
    }
    /// <summary>Generation lab, reusing the verified stage 2 renderer/animation rather than a second movement implementation.</summary>
    [RequireComponent(typeof(Stage2MovementDemo))]
    public sealed class Stage3GenerationDemo : MonoBehaviour
    {
        [SerializeField] private int seed=1;
        [SerializeField,Range(0,5)] private int difficulty=1;
        [SerializeField] private bool enableCubeEdgeExits=true;
        [SerializeField] private bool enableDirectionalGates=true;
        [SerializeField] private DifficultyProfileAsset customProfile;
        [Header("Surface for next generation")]
        [SerializeField] private GenerationLabShape desiredShape;
        [SerializeField,Range(2,12)] private int surfaceSubdivisions=4;
        [SerializeField] private ConvertedSurfaceAsset convertedSurface;
        private Stage2MovementDemo movement;
        private Vector2 scroll;
        private string seedText;
        private string message;
        private bool autoPlaying;
        private GenerationOperation generation;
        private DifficultyProfileAsset editingProfile;
        private bool useEditedProfile;
        private int pendingProfile;
        private ISurfaceGeometry pendingGeometry;
        private string pendingShapeName;
        private IReadOnlyList<GenerationLabShapeOption> shapeOptions;
        private int focusIndex;
        public string DisplayedShapeName { get; private set; }="Cube";
        public GenerationLabShape DesiredShape { get=>desiredShape; set=>desiredShape=value; }
        public int SurfaceSubdivisions { get=>surfaceSubdivisions; set { if(value<2 || value>12) throw new ArgumentOutOfRangeException(nameof(value)); surfaceSubdivisions=value; } }
        public ConvertedSurfaceAsset ConvertedSurface { get=>convertedSurface; set { convertedSurface=value; shapeOptions=null; } }
        public IReadOnlyList<GenerationLabShapeOption> ShapeOptions
        {
            get
            {
                if(shapeOptions==null)
                {
                    var options=new List<GenerationLabShapeOption> { new GenerationLabShapeOption("Cube",GenerationLabShape.Cube),new GenerationLabShapeOption("Authored L",GenerationLabShape.L),new GenerationLabShapeOption("Authored T",GenerationLabShape.T),new GenerationLabShapeOption("Authored oblique prism",GenerationLabShape.ObliquePrism) };
                    var assets=new List<ConvertedSurfaceAsset>(Resources.LoadAll<ConvertedSurfaceAsset>("ArrowCubeSurfaces"));
                    if(convertedSurface!=null && !assets.Contains(convertedSurface)) assets.Add(convertedSurface);
                    assets.Sort((a,b)=>string.CompareOrdinal(a.name,b.name));
                    foreach(var asset in assets) if(asset!=null) options.Add(new GenerationLabShapeOption("Converted: "+asset.name,GenerationLabShape.ConvertedSurface,asset));
                    shapeOptions=options.AsReadOnly();
                }
                return shapeOptions;
            }
        }
        public string DesiredShapeName
        {
            get { foreach(var option in ShapeOptions) if(option.Shape==desiredShape && (desiredShape!=GenerationLabShape.ConvertedSurface || option.Asset==convertedSurface)) return option.Label; return "Converted surface (assign an asset)"; }
        }
        public string DesiredSurfaceDescription
        {
            get
            {
                if(desiredShape==GenerationLabShape.Cube) return "Cube grid size is controlled in Advanced settings. All settings apply to the next generation.";
                if(desiredShape==GenerationLabShape.ConvertedSurface)
                {
                    if(convertedSurface==null) return "Assign a Converted Surface asset in the Inspector or choose a converted example.";
                    try { return $"{convertedSurface.Build().Shape.Topology.CellCount} baked cells. Reconvert/rebake the asset to change density; all puzzle settings below still apply."; }
                    catch(ArgumentException error) { return error.Message; }
                }
                int parents=desiredShape==GenerationLabShape.L ? 14 : desiredShape==GenerationLabShape.T ? 18 : 12;
                return $"{parents*surfaceSubdivisions*surfaceSubdivisions} cells at resolution {surfaceSubdivisions}. Cube grid size is unused; cross-face settings mean surface regions.";
            }
        }
        public void SelectShape(int index)
        { if(index<0 || index>=ShapeOptions.Count) throw new ArgumentOutOfRangeException(nameof(index)); var selected=ShapeOptions[index]; desiredShape=selected.Shape; convertedSurface=selected.Asset; }
        private void OnValidate() { shapeOptions=null; }
        public GeneratedPuzzle Generated { get; private set; }
        public Stage2MovementDemo Movement => movement;
        public bool IsAutoPlaying => autoPlaying;
        public DifficultyProfileAsset CustomProfile
        {
            get => customProfile;
            set { customProfile=value; ClearEditingProfile(); }
        }
        public bool IsGenerating => generation!=null;
        public bool GenerateOnMainThread { get; set; }=GenerationOperation.RequiresMainThread;
        public bool GenerationFailed { get; private set; }
        public GenerationProgress Progress => generation?.Progress;
        public int Seed { get => seed; set { seed=value; seedText=value.ToString(); } }
        public int PresetIndex => Math.Min(difficulty,DifficultyPresets.All.Count-1);
        public bool EnableCubeEdgeExits { get => enableCubeEdgeExits; set => enableCubeEdgeExits=value; }
        public bool EnableDirectionalGates { get => enableDirectionalGates; set => enableDirectionalGates=value; }
        public bool UsingCustomSettings => useEditedProfile || customProfile!=null;
        public DifficultyProfileAsset EditingProfile
        {
            get
            {
                if(editingProfile==null)
                {
                    editingProfile=customProfile!=null ? Instantiate(customProfile) : ScriptableObject.CreateInstance<DifficultyProfileAsset>();
                    if(customProfile==null) editingProfile.ApplyPreset(PresetIndex);
                    if(editingProfile.difficultySettings==null) editingProfile.difficultySettings=new DifficultySettings();
                    editingProfile.hideFlags=HideFlags.HideAndDontSave;
                }
                return editingProfile;
            }
        }
        public bool ShowControls { get; set; }=true;
        public string StatusMessage => message;

        private void Awake()
        { movement=GetComponent<Stage2MovementDemo>(); movement.ShowControls=false; seedText=seed.ToString(); }
        private void Start() { if(Generated==null) BeginGeneration(seed,difficulty,new ExitRules(enableCubeEdgeExits,enableDirectionalGates)); }

        public void UseEditedProfile() => useEditedProfile=true;
        public void SelectPreset(int index)
        {
            if(index<0 || index>=DifficultyPresets.All.Count) throw new ArgumentOutOfRangeException(nameof(index));
            difficulty=index; EditingProfile.ApplyPreset(index); useEditedProfile=true;
        }
        public void ApplyMasterDifficulty(float value)
        { EditingProfile.ApplyMasterDifficulty(value); useEditedProfile=true; }
        public void ResetPresetDefaults()
        { SelectPreset(PresetIndex); enableCubeEdgeExits=enableDirectionalGates=true; }
        private void ClearEditingProfile()
        { if(editingProfile!=null) Destroy(editingProfile); editingProfile=null; useEditedProfile=false; }

        private PuzzleRecipe CreateRecipe(int requestedSeed,int profileIndex,ExitRules rules,out ISurfaceGeometry geometry)
        {
            if(profileIndex<0 || profileIndex>=DifficultyProfiles.All.Count) throw new ArgumentOutOfRangeException(nameof(profileIndex));
            var asset=useEditedProfile ? EditingProfile : customProfile;
            var preset=DifficultyPresets.All[Math.Min(profileIndex,DifficultyPresets.All.Count-1)];
            var settings=asset==null ? preset.CreateSettings() : asset.advancedDifficulty ? asset.difficultySettings?.Copy()??new DifficultySettings() : null;
            geometry=null;
            if(desiredShape==GenerationLabShape.Cube)
                return new PuzzleRecipe(requestedSeed,asset==null ? preset.CreateProfile() : asset.ToProfile(),rules??ExitRules.Hybrid,
                    maxRestarts:settings?.MaxRestarts??8,difficulty:settings,dependencyControls:asset?.ToDependencyControls());
            SurfaceShapeDefinition surface;
            if(desiredShape==GenerationLabShape.ConvertedSurface)
            {
                if(convertedSurface==null) throw new ArgumentException("Choose a converted surface asset before generating.");
                var baked=convertedSurface.Build(); ConvertedSurfaceAsset.Register(convertedSurface); surface=baked.Shape; geometry=baked.Geometry;
            }
            else
            {
                if(!Enum.IsDefined(typeof(GenerationLabShape),desiredShape)) throw new ArgumentException("Unknown surface selection.");
                var authored=AuthoredGenerationSurfaces.Create(desiredShape==GenerationLabShape.L ? AuthoredGenerationShape.L : desiredShape==GenerationLabShape.T ? AuthoredGenerationShape.T : AuthoredGenerationShape.ObliquePrism,surfaceSubdivisions);
                surface=authored.Shape; geometry=authored.Geometry;
            }
            DifficultyProfile profile;
            if(asset!=null) profile=asset.ToSurfaceProfile(surface.Topology.CellCount);
            else
            {
                var p=preset.CreateProfile(); profile=new DifficultyProfile(p.Name,p.GridSize,p.MinPathLength,p.MaxPathLength,p.MinPathCount,p.MinCrossFacePaths,
                    p.MinimumCoverage,p.TargetCoverage,p.MinimumBlockedFraction,p.MinimumAverageTurns,p.TurnProbability,p.MaxGates,p.MaxPathCount,surface.Topology.CellCount);
            }
            return new PuzzleRecipe(requestedSeed,surface,profile,rules??ExitRules.Hybrid,maxRestarts:settings?.MaxRestarts??8,difficulty:settings,dependencyControls:asset?.ToDependencyControls());
        }

        public bool Generate(int requestedSeed,int profileIndex,ExitRules rules=null)
        {
            CancelGeneration();
            try
            {
                // Build before replacing the current puzzle: a rejected recipe leaves the existing level playable.
                var recipe=CreateRecipe(requestedSeed,profileIndex,rules,out var geometry); var name=DesiredShapeName;
                var next=new PuzzleGenerator().Generate(recipe);
                ApplyGenerated(next,profileIndex,geometry,name);
                return true;
            }
            catch(System.ArgumentException error) { GenerationFailed=true; message=error.Message; return false; }
            catch(PuzzleGenerationException error) { GenerationFailed=true; message=error.Message; return false; }
        }

        public bool BeginGeneration(int requestedSeed,int profileIndex,ExitRules rules=null)
        {
            CancelGeneration();
            try
            {
                // Snapshot ScriptableObjects and UI on the main thread. The worker owns pure C# data only.
                var recipe=CreateRecipe(requestedSeed,profileIndex,rules,out var geometry);
                pendingGeometry=geometry; pendingShapeName=DesiredShapeName;
                pendingProfile=profileIndex;
                generation=new GenerationOperation(recipe,GenerateOnMainThread);
                GenerationFailed=false; message="Generating and measuring puzzle..."; return true;
            }
            catch(ArgumentException error) { GenerationFailed=true; message=error.Message; return false; }
        }
        private void Update()
        {
            var job=generation; if(job==null) return;
            job.Tick();
            if(!job.IsCompleted)
            {
                var current=Progress;
                if(current!=null) message=$"{current.Phase}: {current.Completed}/{current.Total}; best {current.BestScore:F1}";
                return;
            }
            generation=null; job.Dispose();
            if(job.IsCanceled) { message="Generation cancelled. The current puzzle is unchanged."; return; }
            if(job.Error!=null) { GenerationFailed=true; message=job.Error.Message; return; }
            ApplyGenerated(job.Result,pendingProfile,pendingGeometry,pendingShapeName);
        }
        private void ApplyGenerated(GeneratedPuzzle next,int profileIndex,ISurfaceGeometry geometry,string shapeName)
        {
            StopAllCoroutines(); autoPlaying=false;
            GenerationFailed=false;
            Generated=next; Seed=next.Recipe.Seed; difficulty=profileIndex;
            enableCubeEdgeExits=next.Rules.EnableCubeEdgeExits; enableDirectionalGates=next.Rules.EnableDirectionalGates;
            movement.LoadDefinition(next,geometry); DisplayedShapeName=shapeName; focusIndex=0;
            message=$"Verified {next.VerifiedSolution.Count} removals. Unlimited attempts.";
            if(next.DependencyControlReport!=null)
                message+=$" Initially free: {next.DependencyControlReport.InitiallyFree}/{next.DependencyControlReport.MaximumStartingFreeArrows} maximum; blockers/blocked arrow: {next.DependencyControlReport.AverageBlockersPerBlockedArrow:F1}.";
            if(next.Difficulty!=null) message+=$" Measured difficulty {next.Difficulty.MeasuredScore:F1}/100. "+next.Difficulty.Warning;
        }
        public void CancelGeneration()
        {
            if(generation==null) return;
            generation.Cancel(); generation.Dispose(); generation=null;
            message="Generation cancelled. The current puzzle is unchanged.";
        }
        private void OnDestroy() { CancelGeneration(); ClearEditingProfile(); }
        public string ExportDifficultyReport(string path=null)
        {
            if(Generated?.Difficulty==null) throw new InvalidOperationException("Generate a measured puzzle first.");
            path=path??Path.Combine(Application.persistentDataPath,$"arrow-cube-difficulty-{Generated.Recipe.Seed}.json");
            string directory=Path.GetDirectoryName(Path.GetFullPath(path)); Directory.CreateDirectory(directory);
            File.WriteAllText(path,JsonUtility.ToJson(Generated.Difficulty,true)); message="Exported difficulty report: "+path; return path;
        }
        public string ExportDependencyGraph(string path=null)
        {
            if(Generated?.DependencyGraph==null) throw new InvalidOperationException("Generate a version-4 measured puzzle first.");
            path=path??Path.Combine(Application.persistentDataPath,$"arrow-cube-dependencies-{Generated.Recipe.Seed}.dot");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))); File.WriteAllText(path,Generated.DependencyGraph.ToDot());
            message="Exported dependency graph: "+path; return path;
        }

        public bool FocusNextArrow()
        {
            if(Generated==null || Generated.Paths.Count==0) return false;
            for(int i=0;i<Generated.Paths.Count;i++) { var path=Generated.Paths[focusIndex++%Generated.Paths.Count]; if(movement.Grid.Paths.ContainsKey(path.Id)) { movement.FocusCell(path.HeadCellId); return true; } }
            return false;
        }
        public void Restart()
        {
            if(Generated==null) return;
            StopAllCoroutines(); autoPlaying=false; movement.RestartPuzzle(); message="Restored the exact generated layout.";
        }

        public bool PlayNextVerifiedMove()
        {
            if(Generated==null || movement.IsAnimating) return false;
            foreach(int id in Generated.VerifiedSolution)
                if(movement.Grid.Paths.ContainsKey(id)) return movement.TryActivate(id);
            return false;
        }

        public bool PlayVerifiedSolution()
        {
            if(Generated==null || movement.IsAnimating || autoPlaying) return false;
            StartCoroutine(PlaySolution()); return true;
        }

        private IEnumerator PlaySolution()
        {
            autoPlaying=true;
            while(PlayNextVerifiedMove())
            { while(movement.IsAnimating) yield return null; yield return null; }
            autoPlaying=false; message=movement.Grid.Paths.Count==0 ? "Verified solution completed." : "Solution playback stopped.";
        }

        private void OnGUI()
        {
            if(!ShowControls) return;
            float scale=Mathf.Clamp(Screen.width/1000f,.7f,1.5f);
            var matrix=GUI.matrix; GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            GUILayout.BeginArea(new Rect(12,12,410,Mathf.Max(150,Screen.height/scale-24)),GUI.skin.box);
            scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label("ARROW CUBE - STAGE 3 GENERATION LAB");
            if(Generated!=null)
            {
                GUILayout.Label(Generated.Name);
                var stats=Generated.Statistics;
                GUILayout.Label($"Remaining: {movement.Grid.Paths.Count}/{stats.PathCount} | Coverage: {stats.Coverage:P0}");
                GUILayout.Label($"Cross-face: {stats.CrossFacePaths} | Initially safe: {stats.ImmediatelyRemovablePaths}\nDependencies: {stats.DependencyCount} | Depth: {stats.DependencyDepth}");
                GUILayout.Label($"Version {Generated.Recipe.GenerationVersion} | {Generated.Report.Milliseconds:F1} ms\nFingerprint: {Generated.Fingerprint.Substring(0,16)}");
            }
            GUILayout.Label(message);
            GUILayout.Label("Shape for next puzzle: "+DesiredShapeName);
            for(int i=0;i<ShapeOptions.Count;i++) if(GUILayout.Button(ShapeOptions[i].Label)) SelectShape(i);
            GUILayout.Label(DesiredSurfaceDescription);
            seedText=GUILayout.TextField(seedText);
            if(customProfile==null)
            {
                int next=GUILayout.SelectionGrid(PresetIndex,new[] { "Easy","Normal","Hard","Expert","Extreme" },3);
                if(next!=PresetIndex) SelectPreset(next);
            }
            else GUILayout.Label($"Custom profile asset: {customProfile.name}");
            float target=(float)EditingProfile.difficultySettings.TargetScore;
            GUILayout.Label($"Master difficulty: {target:F0}/100 (advanced controls in Stage 4 / profile Inspector)");
            float master=GUILayout.HorizontalSlider(target,0,100);
            if(Mathf.Abs(master-target)>.01f) ApplyMasterDifficulty(master);
            enableCubeEdgeExits=GUILayout.Toggle(enableCubeEdgeExits,"Enable cube-edge exits for next generation");
            enableDirectionalGates=GUILayout.Toggle(enableDirectionalGates,"Enable directional gates for next generation");
            if(GUILayout.Button("Generate / regenerate this seed"))
            {
                if(int.TryParse(seedText,out int inputSeed)) BeginGeneration(inputSeed,difficulty,new ExitRules(enableCubeEdgeExits,enableDirectionalGates));
                else message="Enter a signed 32-bit integer seed.";
            }
            if(GUILayout.Button("Next seed")) BeginGeneration(unchecked(seed+1),difficulty,new ExitRules(enableCubeEdgeExits,enableDirectionalGates));
            if(IsGenerating && GUILayout.Button("Cancel generation")) CancelGeneration();
            GUI.enabled=Generated!=null;
            if(GUILayout.Button("Restart exact puzzle / cancel playback")) Restart();
            GUI.enabled=Generated!=null && !movement.IsAnimating && !autoPlaying;
            if(GUILayout.Button("Play next verified move")) PlayNextVerifiedMove();
            if(GUILayout.Button("Play complete verified solution")) PlayVerifiedSolution();
            if(Generated?.Surface is CubeTopology) { GUILayout.Label("Inspect face"); GUILayout.BeginHorizontal();
            for(int f=0;f<6;f++) if(GUILayout.Button(((CubeFace)f).ToString())) movement.FocusFace((CubeFace)f);
            GUILayout.EndHorizontal(); }
            if(GUILayout.Button("Focus next arrow")) FocusNextArrow();
            GUILayout.Label("Activate a path");
            if(Generated!=null)
                foreach(var path in Generated.Paths)
                    if(movement.Grid.Paths.ContainsKey(path.Id) && GUILayout.Button($"Path {path.Id} | {path.CellIds.Count} cells | cell {path.HeadCellId}")) movement.TryActivate(path.Id);
            GUI.enabled=true; GUILayout.EndScrollView(); GUILayout.EndArea(); GUI.matrix=matrix;
        }
    }
}
