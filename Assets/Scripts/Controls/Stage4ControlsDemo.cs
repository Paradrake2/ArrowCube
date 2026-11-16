using System;
using System.Collections.Generic;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using ArrowCube.Topology;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ArrowCube.Controls
{
    /// <summary>Playable control lab with a touch-capable, safe-area-aware UI and the existing generated puzzles.</summary>
    [RequireComponent(typeof(Stage3GenerationDemo),typeof(PuzzleInputManager))]
    [DefaultExecutionOrder(-100)]
    public sealed class Stage4ControlsDemo : MonoBehaviour
    {
        private Stage3GenerationDemo generation;
        private PuzzleInputManager input;
        private Canvas canvas;
        private RectTransform panel;
        private GridLayoutGroup faces;
        private Text stats,status,profileText;
        private Font font;
        private int profile=1;
        private bool edges=true,gates=true;
        private string error;
        private StandaloneInputModule legacyModule;
        private InputSystemUIInputModule addedExternalModule;
        private readonly List<Action> refreshSettings=new List<Action>();
        private Text developerReport,tooltip,estimate;
        private Text blockerReport,selectedDependency;
        private PuzzleDifficultyReport displayedReport;
        public Rect PanelScreenRect { get; private set; }
        public PuzzleInputManager Input => input;
        public Stage3GenerationDemo Generation => generation;
        public event Action<GeneratedPuzzle> GameplayRequested;

        private void Awake()
        {
            generation=GetComponent<Stage3GenerationDemo>(); generation.ShowControls=false;
            input=GetComponent<PuzzleInputManager>();
            input.AdditionalUIBlocker=point=>PanelScreenRect.Contains(point);
        }
        private void Start()
        {
            // Required components exist in Awake, but their own Awake may not have run yet.
            // Start runs after every Awake and before the first EventSystem update.
            BuildUI(); UpdateLayout();
        }
        private void Update()
        {
            UpdateLayout();
            estimate.text=$"Next config estimate: {generation.EditingProfile.difficultySettings.ConfigurationEstimate():F1}/100 (unverified)";
            var puzzle=generation.Generated;
            if(puzzle==null) { stats.text=generation.IsGenerating ? "Measuring the next puzzle..." : "Create a puzzle to begin."; status.text=generation.StatusMessage; return; }
            string mode=puzzle.Rules.EnableCubeEdgeExits ? (puzzle.Rules.EnableDirectionalGates ? "Hybrid" : "Edge-only") : "Gate-only";
            stats.text=$"Seed {puzzle.Recipe.Seed} | {puzzle.Recipe.Profile.Name}\nPaths {generation.Movement.Grid.Paths.Count}/{puzzle.Paths.Count} | {mode}";
            if(puzzle.Difficulty!=null) stats.text+=$"\nTarget {puzzle.Difficulty.RequestedScore:F0} | Measured {puzzle.Difficulty.MeasuredScore:F1}/100";
            if(puzzle.DependencyControlReport!=null) stats.text+=$"\nFree at start {puzzle.DependencyControlReport.InitiallyFree}/{puzzle.DependencyControlReport.MaximumStartingFreeArrows} max | Blockers {puzzle.DependencyControlReport.AverageBlockersPerBlockedArrow:F1}";
            status.text=generation.IsGenerating || generation.GenerationFailed || !string.IsNullOrEmpty(puzzle.Difficulty?.Warning) ? generation.StatusMessage
                : string.IsNullOrEmpty(error) ? generation.Movement.StatusMessage : error;
            if(developerReport!=null && displayedReport!=puzzle.Difficulty)
            {
                displayedReport=puzzle.Difficulty; developerReport.text=DifficultyReportText(puzzle);
                var m=puzzle.Difficulty?.MultiBlocker;
                blockerReport.text=m==null ? "No confirmed multi-blocker report for this legacy/basic puzzle." :
                    $"Confirmed blockers/path: {m.AverageBlockersPerPath:F2}\nPer blocked path: {m.AverageBlockersPerBlockedPath:F2}\nMaximum: {m.MaximumBlockers}\nAt least 2: {m.AtLeastTwoFraction:P1}\nAt least 3: {m.AtLeastThreeFraction:P1}\nIndependent multi-blocker paths: {m.IndependentMultiBlockerFraction:P1}\nGeometry cross-face links: {m.CrossFaceGeometryFraction:P1}\nMean branching: {m.AverageBranching:F2}\nLargest blocker reach: {m.MaxBlockerConcentration:P1}\nIndependent openings: {m.IndependentOpeningMoves}\nCounts: {string.Join(", ",m.BlockerHistogram)}\nTargeted replacements accepted: {puzzle.Report.TargetedAccepted}/{puzzle.Report.TargetedAttempts}";
                if(m!=null) blockerReport.text+=$"\nRequested independent fraction: {puzzle.Difficulty.RequestedIndependentMultiFraction:P0}\nRequested blockers/blocked path: {puzzle.Difficulty.RequestedAverageBlockers:F1}";
            }
            if(selectedDependency!=null) selectedDependency.text=generation.Movement.DependencyDescription();
        }
        public void UpdateLayout()
        {
            var safe=Screen.safeArea;
            if(safe.width<=0 || safe.height<=0) safe=new Rect(0,0,Screen.width,Screen.height);
            float uiScale=Screen.dpi>0 ? Mathf.Clamp(Screen.dpi/120,1,2.5f) : 1;
            var layout=PuzzleScreenLayout.Calculate(safe,uiScale);
            bool changed=PanelScreenRect!=layout.Panel;
            PanelScreenRect=layout.Panel;
            if(panel!=null)
            {
                canvas.GetComponent<CanvasScaler>().scaleFactor=uiScale;
                canvas.scaleFactor=uiScale;
                float scale=Mathf.Max(.01f,canvas.scaleFactor);
                panel.anchoredPosition=layout.Panel.position/scale; panel.sizeDelta=layout.Panel.size/scale;
                if(faces!=null) faces.cellSize=new Vector2(Mathf.Max(45,(panel.sizeDelta.x-40)/3),32);
            }
            var camera=generation.Movement.InspectionCamera;
            if(camera!=null)
            {
                var view=new Rect(layout.View.x/Screen.width,layout.View.y/Screen.height,layout.View.width/Screen.width,layout.View.height/Screen.height);
                changed|=camera.rect!=view; camera.rect=view;
            }
            if(changed) { input.CancelActiveGestures(); Canvas.ForceUpdateCanvases(); }
        }
        private void BuildUI()
        {
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject=new GameObject("Stage 4 controls UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform,false); canvas=canvasObject.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=100;
            var scaler=canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            panel=Rect("Control panel",canvas.transform);
            panel.anchorMin=panel.anchorMax=panel.pivot=Vector2.zero;
            panel.gameObject.AddComponent<Image>().color=new Color(.08f,.15f,.25f,.98f);
            var scroll=panel.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=6;
            var viewport=Rect("Viewport",panel); Stretch(viewport,10); viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Content",viewport); content.anchorMin=new Vector2(0,1); content.anchorMax=Vector2.one; content.pivot=new Vector2(.5f,1);
            content.offsetMin=content.offsetMax=Vector2.zero;
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing=7; layout.childControlHeight=true; layout.childControlWidth=true;
            layout.childForceExpandHeight=false; layout.childForceExpandWidth=true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport; scroll.content=content;
            Label(content,"ARROW CUBE",26,16);
            stats=Label(content,"Loading puzzle...",88,14);
            status=Label(content,"",92,13);
            profile=generation.PresetIndex; edges=generation.EnableCubeEdgeExits; gates=generation.EnableDirectionalGates;
            profileText=Button(content,"Difficulty: "+DifficultyPresets.All[profile].Name,()=>
            { profile=(profile+1)%DifficultyPresets.All.Count; generation.SelectPreset(profile); RefreshSettings(); });
            SettingSlider(content,"Master difficulty",0,100,true,
                ()=> (float)generation.EditingProfile.difficultySettings.TargetScore,value=> { generation.ApplyMasterDifficulty(value); RefreshSettings(); },
                "Maps 0-100 to a coherent set of size, coverage, path and logical settings. Use Advanced for independent changes.",false);
            estimate=Label(content,"Configuration estimate pending...",36,12);
            Button(content,"Reset to preset defaults",()=> { generation.ResetPresetDefaults(); RefreshSettings(); });
            Label(content,"Click/tap a black path to activate it.\nDrag empty space or right-drag to orbit.\nTouch-drag anywhere; pinch or wheel to zoom.",70,13);
            Label(content,"Unlimited attempts at every difficulty.",24,12);
            Button(content,"Reset camera (R)",()=> { input.CancelActiveGestures(); input.Orbit.ResetView(); });
            var edgeToggle=Toggle(content,"Cube-edge exits for next puzzle",edges,value=> { edges=value; generation.EnableCubeEdgeExits=value; });
            var gateToggle=Toggle(content,"Directional gates for next puzzle",gates,value=> { gates=value; generation.EnableDirectionalGates=value; });
            refreshSettings.Add(()=> { edges=generation.EnableCubeEdgeExits; gates=generation.EnableDirectionalGates; edgeToggle.SetIsOnWithoutNotify(edges); gateToggle.SetIsOnWithoutNotify(gates); });
            AddSeedField(content);
            Button(content,"Regenerate this seed",()=>Generate(false));
            Button(content,"Next seed",()=>Generate(true));
            Button(content,"Cancel generation",()=>generation.CancelGeneration());
            Button(content,"Restart exact puzzle",()=> { input.CancelActiveGestures(); generation.Restart(); error=null; });
            if(GameplayRequested!=null) Button(content,"Play this puzzle in game scene",()=>
            {
                if(generation.IsGenerating || generation.Generated==null) { error="Wait for a puzzle to finish generating."; return; }
                GameplayRequested.Invoke(generation.Generated);
            });
            var advanced=Rect("Advanced difficulty settings",content);
            advanced.gameObject.AddComponent<VerticalLayoutGroup>().childForceExpandHeight=false;
            advanced.gameObject.GetComponent<VerticalLayoutGroup>().spacing=5;
            AddAdvancedSettings(advanced);
            advanced.gameObject.SetActive(false);
            var advancedButton=Button(content,"Show / hide advanced settings",()=>advanced.gameObject.SetActive(!advanced.gameObject.activeSelf));
            advancedButton.transform.parent.SetSiblingIndex(advanced.GetSiblingIndex());
            Label(content,"Inspect a face",22,13);
            var faceRoot=Rect("Face buttons",content);
            faceRoot.gameObject.AddComponent<LayoutElement>().preferredHeight=70;
            faces=faceRoot.gameObject.AddComponent<GridLayoutGroup>(); faces.constraint=GridLayoutGroup.Constraint.FixedColumnCount; faces.constraintCount=3; faces.spacing=new Vector2(4,4);
            for(int i=0;i<6;i++) { var face=(CubeFace)i; Button(faceRoot,face.ToString(),()=>generation.Movement.FocusFace(face)); }
            Label(content,"Camera sensitivity",22,13);
            var sliderRoot=Rect("Sensitivity",content); sliderRoot.gameObject.AddComponent<LayoutElement>().preferredHeight=26;
            var slider=sliderRoot.gameObject.AddComponent<ScrollAwareSlider>();
            sliderRoot.gameObject.AddComponent<Image>().color=new Color(.18f,.28f,.40f);
            // Slider stretches the handle along Y; a negative delta keeps it inside the track.
            var handle=Rect("Handle",sliderRoot); handle.sizeDelta=new Vector2(16,-4); handle.gameObject.AddComponent<Image>().color=new Color(.65f,.88f,1);
            slider.handleRect=handle; slider.targetGraphic=handle.GetComponent<Image>(); slider.direction=Slider.Direction.LeftToRight;
            slider.minValue=.1f; slider.maxValue=3; slider.value=input.Orbit.Sensitivity; slider.onValueChanged.AddListener(value=>input.Orbit.Sensitivity=value);
            var invertMouse=Toggle(content,"Invert mouse rotation",input.InvertMouseRotation,value=>input.InvertMouseRotation=value);
            var invertTouch=Toggle(content,"Invert touch rotation",input.InvertTouchRotation,value=>input.InvertTouchRotation=value);
            refreshSettings.Add(()=>invertMouse.SetIsOnWithoutNotify(input.InvertMouseRotation));
            refreshSettings.Add(()=>invertTouch.SetIsOnWithoutNotify(input.InvertTouchRotation));
            Toggle(content,"Rotation inertia",false,value=>input.Orbit.InertiaEnabled=value);
            Label(content,"F11: fullscreen/windowed\nDrag gestures never activate a path.",42,12);
            var tipRoot=Rect("Parameter help",panel); var tipImage=tipRoot.gameObject.AddComponent<Image>(); tipImage.color=new Color(.04f,.09f,.16f,.99f); tipImage.raycastTarget=false;
            tipRoot.anchorMin=Vector2.zero; tipRoot.anchorMax=new Vector2(1,0); tipRoot.offsetMin=new Vector2(8,8); tipRoot.offsetMax=new Vector2(-8,104);
            tooltip=Label(tipRoot,"",88,12); Stretch(tooltip.rectTransform,8); tipRoot.gameObject.SetActive(false);
            if(EventSystem.current==null)
            {
                var events=new GameObject("Stage 4 EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform,false); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            else
            {
                var system=EventSystem.current;
                var legacy=system.GetComponent<StandaloneInputModule>();
                if(legacy!=null && legacy.enabled) { legacyModule=legacy; legacy.enabled=false; }
                if(system.GetComponent<InputSystemUIInputModule>()==null)
                {
                    addedExternalModule=system.gameObject.AddComponent<InputSystemUIInputModule>();
                    addedExternalModule.AssignDefaultActions();
                }
            }
        }
        private void OnDestroy()
        {
            if(addedExternalModule!=null) Destroy(addedExternalModule);
            if(legacyModule!=null) legacyModule.enabled=true;
        }
        private void Generate(bool next)
        {
            int seed=generation.Seed;
            bool success=generation.BeginGeneration(next ? unchecked(seed+1) : seed,profile,new ExitRules(edges,gates));
            error=success ? null : generation.StatusMessage;
        }
        private void RefreshSettings()
        {
            foreach(var refresh in refreshSettings) refresh();
            profileText.text=generation.UsingCustomSettings ? "Difficulty: Custom (next preset)" : "Difficulty: "+DifficultyPresets.All[profile].Name;
        }
        private void ShowTooltip(string value)
        { if(tooltip==null) return; tooltip.text=value; tooltip.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(value)); }
        private void SettingSlider(Transform parent,string name,float min,float max,bool integer,Func<float> get,Action<float> set,string help,bool custom=true)
        {
            var label=Label(parent,"",22,12); var rect=Rect(name,parent); rect.gameObject.AddComponent<LayoutElement>().preferredHeight=28;
            rect.gameObject.AddComponent<Image>().color=new Color(.18f,.28f,.40f);
            var slider=rect.gameObject.AddComponent<ScrollAwareSlider>(); slider.minValue=min; slider.maxValue=max; slider.wholeNumbers=integer;
            var handle=Rect("Handle",rect); handle.sizeDelta=new Vector2(18,-4); var handleImage=handle.gameObject.AddComponent<Image>(); handleImage.color=new Color(.65f,.88f,1);
            slider.handleRect=handle; slider.targetGraphic=handleImage;
            Action refresh=()=> { float value=get(); slider.SetValueWithoutNotify(value); label.text=name+": "+value.ToString(integer ? "F0" : "F2"); };
            refresh(); refreshSettings.Add(refresh);
            slider.onValueChanged.AddListener(value=> { if(custom) generation.UseEditedProfile(); set(value); refresh(); if(profileText!=null) profileText.text="Difficulty: Custom (next preset)"; });
            var tip=rect.gameObject.AddComponent<ControlTooltip>(); tip.Message=help; tip.Show=ShowTooltip;
        }
        private void AddSeedField(Transform parent)
        {
            Label(parent,"Random seed",22,12); var rect=Rect("Seed",parent);
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight=32;
            rect.gameObject.AddComponent<Image>().color=new Color(.13f,.23f,.34f);
            var field=rect.gameObject.AddComponent<InputField>(); var text=Label(rect,"",30,14); Stretch(text.rectTransform,5);
            field.textComponent=text; field.contentType=InputField.ContentType.IntegerNumber; field.text=generation.Seed.ToString();
            field.onEndEdit.AddListener(value=> { if(int.TryParse(value,out int seed)) { generation.Seed=seed; error=null; } else error="Enter a signed 32-bit integer seed."; });
            refreshSettings.Add(()=>field.SetTextWithoutNotify(generation.Seed.ToString()));
        }
        private void AddAdvancedSettings(Transform parent)
        {
            Func<DifficultyProfileAsset> a=()=>generation.EditingProfile;
            Func<DifficultySettings> d=()=>a().difficultySettings;
            Func<MultiBlockerSettings> m=()=>d().MultiBlocker;
            Label(parent,"CUSTOM: changes apply to the next puzzle",34,13);
            Label(parent,"Hover or press a slider for help.",22,12);
            var measured=Toggle(parent,"Use measured difficulty search",a().advancedDifficulty,v=> { generation.UseEditedProfile(); a().advancedDifficulty=v; });
            refreshSettings.Add(()=>measured.SetIsOnWithoutNotify(a().advancedDifficulty));
            var controlled=Toggle(parent,"Control blockers / few starting moves",a().controlDependencies,v=> { generation.UseEditedProfile(); a().controlDependencies=v; });
            refreshSettings.Add(()=>controlled.SetIsOnWithoutNotify(a().controlDependencies));
            SettingSlider(parent,"Dependency amount",0,1,false,()=>a().dependencyAmount,v=>a().dependencyAmount=v,
                "Works with measured search OFF. Soft target: 1 + 4 times this value distinct blockers per blocked arrow. Layout and solvability can limit the achieved count.");
            SettingSlider(parent,"Maximum starting free arrows",1,30,true,()=>a().maximumStartingFreeArrows,v=>a().maximumStartingFreeArrows=(int)v,
                "Hard limit when dependency controls are ON. Default 3; at least 1. A recipe that cannot meet the limit is rejected, never silently relaxed.");
            var quality=Button(parent,"",()=> { generation.UseEditedProfile(); d().ApplyQuality((GenerationQuality)(((int)d().Quality+1)%3)); RefreshSettings(); });
            refreshSettings.Add(()=>quality.text="Search quality: "+d().Quality); quality.text="Search quality: "+d().Quality;
            var placement=Button(parent,"",()=> { generation.UseEditedProfile(); d().Placement=(PlacementStrategy)(((int)d().Placement+1)%5); RefreshSettings(); });
            refreshSettings.Add(()=>placement.text="Placement: "+d().Placement); placement.text="Placement: "+d().Placement;
            SettingSlider(parent,"Grid size",4,64,true,()=>a().gridSize,v=>a().gridSize=(int)v,"Cells along each face. Size changes do not automatically change other Custom settings.");
            SettingSlider(parent,"Minimum paths",1,30000,true,()=>a().minPathCount,v=>a().minPathCount=(int)v,"Mandatory minimum number of paths.");
            SettingSlider(parent,"Maximum paths",1,30000,true,()=>a().maxPathCount,v=>a().maxPathCount=(int)v,"Hard path-count cap. Incompatible constraints produce an explicit error.");
            SettingSlider(parent,"Minimum coverage",.05f,1,false,()=>a().minimumCoverage,v=>a().minimumCoverage=v,"Mandatory occupied-cell fraction. Must not exceed the target.");
            SettingSlider(parent,"Target coverage",.05f,1,false,()=>a().targetCoverage,v=>a().targetCoverage=v,"Soft packing objective, independent of the mandatory coverage floor.");
            SettingSlider(parent,"Minimum path length",2,128,true,()=>a().minPathLength,v=>a().minPathLength=(int)v,"Minimum cells per arrow, including head and tail.");
            SettingSlider(parent,"Maximum path length",2,128,true,()=>a().maxPathLength,v=>a().maxPathLength=(int)v,"Maximum cells per arrow. Must be at least the minimum.");
            SettingSlider(parent,"Turn probability",0,1,false,()=>a().turnProbability,v=>a().turnProbability=v,"Bias toward body bends. Visual complexity alone does not determine measured difficulty.");
            SettingSlider(parent,"Cross-face frequency",0,1,false,()=>(float)d().CrossFaceFrequency,v=>d().CrossFaceFrequency=v,"Extra probability of proposing a seam-crossing body, beyond the required quota.");
            SettingSlider(parent,"Minimum cross-face paths",1,128,true,()=>a().minCrossFacePaths,v=>a().minCrossFacePaths=(int)v,"Mandatory quota; all six faces must also be connected by path geometry.");
            SettingSlider(parent,"Target difficulty score",0,100,true,()=>(float)d().TargetScore,v=>d().TargetScore=v,"Independent score target. Unlike the master slider, this preserves all other Custom values.");
            SettingSlider(parent,"Minimum blocked fraction",0,.99f,false,()=>a().minimumBlockedFraction,v=>a().minimumBlockedFraction=v,"Mandatory fraction initially blocked by other paths.");
            SettingSlider(parent,"Desired safe fraction",0,1,false,()=>(float)d().DesiredSafeFraction,v=>d().DesiredSafeFraction=v,"Soft target for immediately removable paths. At least one safe opening is always required.");
            SettingSlider(parent,"Desired dependency depth",1,50,true,()=>(float)d().DesiredDependencyDepth,v=>d().DesiredDependencyDepth=v,"Soft target for average fixed-route dependency layers. No retry limits are imposed.");
            SettingSlider(parent,"Dependency strength",0,1,false,()=>(float)d().DependencyStrength,v=>d().DependencyStrength=v,"Proposal preference for varied blockers, branching and multiple dependencies.");
            var multi=Toggle(parent,"Enable multi-blocker generation",m().Enabled,v=> { generation.UseEditedProfile(); m().Enabled=v; });
            refreshSettings.Add(()=>multi.SetIsOnWithoutNotify(m().Enabled));
            var targeting=Toggle(parent,"Target blocker corridors",m().EnableTargetedPlacement,v=> { generation.UseEditedProfile(); m().EnableTargetedPlacement=v; });
            refreshSettings.Add(()=>targeting.SetIsOnWithoutNotify(m().EnableTargetedPlacement));
            var replace=Toggle(parent,"Replace weak paths (keep path count)",m().ReplaceExistingPaths,v=> { generation.UseEditedProfile(); m().ReplaceExistingPaths=v; });
            refreshSettings.Add(()=>replace.SetIsOnWithoutNotify(m().ReplaceExistingPaths));
            SettingSlider(parent,"Target blockers per blocked path",0,8,false,()=>(float)m().TargetAverageBlockers,v=>m().TargetAverageBlockers=v,"Soft target for distinct simulator-confirmed blockers, conditional on being initially blocked.");
            SettingSlider(parent,"Maximum target blockers",1,16,true,()=>m().MaxTargetBlockers,v=>m().MaxTargetBlockers=(int)v,"Placement preference cap, not a gameplay lock or a guarantee that every arrow has this many blockers.");
            SettingSlider(parent,"Independent multi-blocker fraction",0,1,false,()=>(float)m().MultiBlockerPathFraction,v=>m().MultiBlockerPathFraction=v,"Desired fraction with two incomparable blockers. Redundant links in a single chain do not satisfy this target.");
            SettingSlider(parent,"Cross-face dependency preference",0,1,false,()=>(float)m().CrossFacePreference,v=>m().CrossFacePreference=v,"Prefer actual blocking links involving geometry on more than one face.");
            SettingSlider(parent,"Dependency branching preference",0,1,false,()=>(float)m().BranchingPreference,v=>m().BranchingPreference=v,"Prefer distributed blockers and penalize a single arrow blocking most of the cube.");
            SettingSlider(parent,"Maximum desired blocker reach",0,1,false,()=>(float)m().MaxBlockerConcentration,v=>m().MaxBlockerConcentration=v,"Soft cap on the fraction of other paths blocked by one arrow.");
            SettingSlider(parent,"Targeted blocker attempts",0,64,true,()=>m().TargetedBlockerAttempts,v=>m().TargetedBlockerAttempts=(int)v,"Additional bounded replacement/insertion attempts. Each uses at most Proposal Pool Size path trials and full validation.");
            Label(parent,"Blocker distribution (relative weights)",30,12);
            for(int i=0;i<7;i++)
            {
                int bucket=i;
                SettingSlider(parent,"Blocker weight "+i+(i==6 ? "+" : ""),0,1,false,
                    ()=>bucket<m().BlockerDistribution.Length ? (float)m().BlockerDistribution[bucket] : 0,
                    v=> { if(m().BlockerDistribution.Length<7) Array.Resize(ref m().BlockerDistribution,7); m().BlockerDistribution[bucket]=v; },
                    "Relative weight of this blocker count; the last array bucket includes larger counts. The Inspector supports up to 17 buckets.");
            }
            SettingSlider(parent,"Edge-first resistance",0,1,false,()=>(float)d().EdgeResistance,v=>d().EdgeResistance=v,"Prefer edge paths blocked by more interior paths, and penalize predictable edge-first solving.");
            SettingSlider(parent,"Spatial unpredictability",0,1,false,()=>(float)d().SpatialUnpredictability,v=>d().SpatialUnpredictability=v,"Prefer distributed safe moves and interior-to-edge blocking relationships.");
            SettingSlider(parent,"Construction attempts",1,2000000,true,()=>d().MaxConstructionAttempts,v=>d().MaxConstructionAttempts=(int)v,"Maximum proposal attempts per restart of each candidate; deterministic, not a wall-clock timeout.");
            SettingSlider(parent,"Maximum restarts",1,100,true,()=>d().MaxRestarts,v=>d().MaxRestarts=(int)v,"Maximum construction restarts per layout candidate.");
            SettingSlider(parent,"Layout candidates",1,32,true,()=>d().MaxCandidates,v=>d().MaxCandidates=(int)v,"Number of complete candidates evaluated before selection. More search can improve the match.");
            SettingSlider(parent,"Optimization iterations",0,64,true,()=>d().OptimizationIterations,v=>d().OptimizationIterations=(int)v,"Bounded local geometry, orientation or gate mutations. Every proposal is validated before acceptance.");
            SettingSlider(parent,"Proposal pool size",1,32,true,()=>d().CandidatePoolSize,v=>d().CandidatePoolSize=(int)v,"Compare this many reverse-construction path proposals at each placement.");
            SettingSlider(parent,"Alternative random orders",1,8,true,()=>d().AlternativeOrderSamples,v=>d().AlternativeOrderSamples=(int)v,"Number of seeded random heuristic runs per candidate. Reports retain their individual outcomes and mean success rate.");
            SettingSlider(parent,"Maximum gates",1,30000,true,()=>a().maxGates,v=>a().maxGates=(int)v,"Absolute cap unless area scaling is enabled. Ignored for edge-only levels.");
            var scaling=Toggle(parent,"Scale gate budget with grid size",a().scaleGateBudgetWithGridSize,v=> { generation.UseEditedProfile(); a().scaleGateBudgetWithGridSize=v; });
            refreshSettings.Add(()=>scaling.SetIsOnWithoutNotify(a().scaleGateBudgetWithGridSize));
            Label(parent,"Score weights (relative)",24,13);
            SettingSlider(parent,"Weight: edge resistance",0,100,false,()=>(float)d().Weights.EdgeResistance,v=>d().Weights.EdgeResistance=v,"Relative contribution; weights are normalized to sum to 100%.");
            SettingSlider(parent,"Weight: dependencies",0,100,false,()=>(float)d().Weights.Dependencies,v=>d().Weights.Dependencies=v,"Contribution of dependency depth and blocker structure.");
            SettingSlider(parent,"Weight: safe scarcity",0,100,false,()=>(float)d().Weights.SafeMoveScarcity,v=>d().Weights.SafeMoveScarcity=v,"Contribution of initially blocked fraction.");
            SettingSlider(parent,"Weight: spatial unpredictability",0,100,false,()=>(float)d().Weights.SpatialUnpredictability,v=>d().Weights.SpatialUnpredictability=v,"Contribution of distributed, position-independent removability.");
            SettingSlider(parent,"Weight: cross-face",0,100,false,()=>(float)d().Weights.CrossFace,v=>d().Weights.CrossFace=v,"Contribution of cross-face bodies and dependencies classified by head faces.");
            SettingSlider(parent,"Weight: visual complexity",0,100,false,()=>(float)d().Weights.VisualComplexity,v=>d().Weights.VisualComplexity=v,"Small contribution of bends per body length and coverage; no raw grid-size bonus.");
            Toggle(parent,"Debug: safe / blocked colors",false,v=>generation.Movement.DebugDifficulty=v);
            Toggle(parent,"Debug: dependency lines",false,v=>generation.Movement.DebugDependencies=v);
            Toggle(parent,"Debug: edge solver selections",false,v=>generation.Movement.DebugSolverSelections=v);
            Toggle(parent,"Debug: inspect arrow (click selects)",false,v=>generation.Movement.DebugInspectDependencies=v);
            Button(parent,"Inspect next arrow",()=>generation.Movement.InspectNextDependency());
            selectedDependency=Label(parent,"",300,12);
            blockerReport=Label(parent,"Generate a measured puzzle for blocker metrics.",340,12);
            Button(parent,"Export dependency graph DOT",()=> { try { generation.ExportDependencyGraph(); error=generation.StatusMessage; } catch(Exception exception) { error=exception.Message; } });
            Button(parent,"Export difficulty report JSON",()=> { try { generation.ExportDifficultyReport(); error=generation.StatusMessage; } catch(Exception exception) { error=exception.Message; } });
            developerReport=Label(parent,"Generate a measured puzzle for a report.",590,12);
        }
        private static string DifficultyReportText(GeneratedPuzzle puzzle)
        {
            var r=puzzle.Difficulty; if(r==null) return "Basic generation: no measured difficulty report.";
            return $"Seed {r.Seed} | generation v{r.GenerationVersion} / analysis v{r.AnalysisVersion}\nRequested {r.RequestedScore:F0}; measured {r.MeasuredScore:F1}\nConfig estimate (unverified): {r.ConfigurationEstimate:F1}\nCoverage {r.Coverage:P1}; paths {r.PathCount}\nMean length {r.AverageLength:F1}; turns {r.AverageTurns:F1}\nCross-face paths {r.CrossFacePaths}\nInitial safe {r.InitialSafe} ({r.InitialSafeFraction:P0})\nBlocked {r.InitialBlocked} ({1-r.InitialSafeFraction:P0})\nExact dependency layers {r.DependencyLayers}\nMean dependency depth {r.AverageDependencyDepth:F1}\nMean unlocked/move {r.AverageUnlockedPerRemoval:F2}\nEdge solver success {r.Solvers[0].SuccessRate:P1}\nEdge mistakes/removal {r.Solvers[0].FailuresPerRemoval:F2}\nRandom solver success {r.Solvers[3].SuccessRate:P1}\nEdge correlation {r.EdgeRemovabilityCorrelation:F2}\nGeneration {r.GenerationMilliseconds:F0}ms\nCandidates {r.CandidatesEvaluated}; rejected proposals {r.RejectedCandidates}\nSafe orders commute; unlimited attempts.\n"+r.Warning;
        }
        private static RectTransform Rect(string name,Transform parent)
        { var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); return go.GetComponent<RectTransform>(); }
        private static void Stretch(RectTransform rect,float inset)
        { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=Vector2.one*inset; rect.offsetMax=-Vector2.one*inset; }
        private Text Label(Transform parent,string value,float height,int size)
        {
            var rect=Rect("Text",parent); var text=rect.gameObject.AddComponent<Text>(); text.font=font; text.text=value;
            text.fontSize=size; text.color=Color.white; text.raycastTarget=false; text.alignment=TextAnchor.MiddleLeft;
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight=height; return text;
        }
        private Text Button(Transform parent,string label,Action action)
        {
            var rect=Rect(label,parent); rect.gameObject.AddComponent<Image>().color=new Color(.22f,.37f,.53f);
            var button=rect.gameObject.AddComponent<Button>(); button.onClick.AddListener(()=>action());
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight=32;
            var text=Label(rect,label,30,13); Stretch(text.rectTransform,5); text.alignment=TextAnchor.MiddleCenter; return text;
        }
        private Toggle Toggle(Transform parent,string label,bool value,Action<bool> changed)
        {
            var rect=Rect(label,parent); rect.gameObject.AddComponent<LayoutElement>().preferredHeight=30;
            rect.gameObject.AddComponent<Image>().color=new Color(.13f,.23f,.34f);
            var toggle=rect.gameObject.AddComponent<Toggle>();
            var check=Rect("Check",rect); check.anchorMin=check.anchorMax=new Vector2(0,.5f); check.pivot=new Vector2(0,.5f); check.sizeDelta=new Vector2(18,18); check.anchoredPosition=new Vector2(5,0);
            var image=check.gameObject.AddComponent<Image>(); image.color=new Color(.3f,.85f,1); toggle.graphic=image;
            var text=Label(rect,label,30,12); Stretch(text.rectTransform,4); text.rectTransform.offsetMin=new Vector2(30,4);
            toggle.isOn=value; toggle.onValueChanged.AddListener(on=>changed(on));
            return toggle;
        }
    }
}
