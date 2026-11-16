using System.Collections;
using System;
using System.IO;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Topology;
using UnityEngine;

namespace ArrowCube.Presentation
{
    /// <summary>Generation lab, reusing the verified stage 2 renderer/animation rather than a second movement implementation.</summary>
    [RequireComponent(typeof(Stage2MovementDemo))]
    public sealed class Stage3GenerationDemo : MonoBehaviour
    {
        [SerializeField] private int seed=1;
        [SerializeField,Range(0,5)] private int difficulty=1;
        [SerializeField] private bool enableCubeEdgeExits=true;
        [SerializeField] private bool enableDirectionalGates=true;
        [SerializeField] private DifficultyProfileAsset customProfile;
        private Stage2MovementDemo movement;
        private Vector2 scroll;
        private string seedText;
        private string message;
        private bool autoPlaying;
        private GenerationOperation generation;
        private DifficultyProfileAsset editingProfile;
        private bool useEditedProfile;
        private int pendingProfile;
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

        private PuzzleRecipe CreateRecipe(int requestedSeed,int profileIndex,ExitRules rules)
        {
            if(profileIndex<0 || profileIndex>=DifficultyProfiles.All.Count) throw new ArgumentOutOfRangeException(nameof(profileIndex));
            var asset=useEditedProfile ? EditingProfile : customProfile;
            var preset=DifficultyPresets.All[Math.Min(profileIndex,DifficultyPresets.All.Count-1)];
            var settings=asset==null ? preset.CreateSettings() : asset.advancedDifficulty ? asset.difficultySettings?.Copy()??new DifficultySettings() : null;
            return new PuzzleRecipe(requestedSeed,asset==null ? preset.CreateProfile() : asset.ToProfile(),rules??ExitRules.Hybrid,
                maxRestarts:settings?.MaxRestarts??8,difficulty:settings,dependencyControls:asset?.ToDependencyControls());
        }

        public bool Generate(int requestedSeed,int profileIndex,ExitRules rules=null)
        {
            CancelGeneration();
            try
            {
                // Build before replacing the current puzzle: a rejected recipe leaves the existing level playable.
                var next=new PuzzleGenerator().Generate(CreateRecipe(requestedSeed,profileIndex,rules));
                ApplyGenerated(next,profileIndex);
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
                var recipe=CreateRecipe(requestedSeed,profileIndex,rules);
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
            ApplyGenerated(job.Result,pendingProfile);
        }
        private void ApplyGenerated(GeneratedPuzzle next,int profileIndex)
        {
            StopAllCoroutines(); autoPlaying=false;
            GenerationFailed=false;
            Generated=next; Seed=next.Recipe.Seed; difficulty=profileIndex;
            enableCubeEdgeExits=next.Rules.EnableCubeEdgeExits; enableDirectionalGates=next.Rules.EnableDirectionalGates;
            movement.LoadDefinition(next);
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
            GUILayout.Label("Inspect face"); GUILayout.BeginHorizontal();
            for(int f=0;f<6;f++) if(GUILayout.Button(((CubeFace)f).ToString())) movement.FocusFace((CubeFace)f);
            GUILayout.EndHorizontal();
            GUILayout.Label("Activate a path");
            if(Generated!=null)
                foreach(var path in Generated.Paths)
                    if(movement.Grid.Paths.ContainsKey(path.Id) && GUILayout.Button($"Path {path.Id} | {path.Cells.Count} cells | {path.Head.Face}")) movement.TryActivate(path.Id);
            GUI.enabled=true; GUILayout.EndScrollView(); GUILayout.EndArea(); GUI.matrix=matrix;
        }
    }
}
