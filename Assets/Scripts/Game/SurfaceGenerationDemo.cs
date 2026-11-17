using System;
using System.IO;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowCube.Game
{
    /// <summary>Authored-surface recipe editor. Uses the standard generation operation,
    /// gameplay controller, camera, picking and animation pipeline.</summary>
    [DefaultExecutionOrder(-80),RequireComponent(typeof(GameplayController))]
    public sealed class SurfaceGenerationDemo : MonoBehaviour
    {
        [SerializeField] private AuthoredGenerationShape shape;
        [SerializeField] private ConvertedSurfaceAsset convertedSurface;
        [SerializeField] private ConvertedSurfaceAsset[] convertedExamples=Array.Empty<ConvertedSurfaceAsset>();
        [SerializeField] private int seed=12345;
        [SerializeField,Range(2,8)] private int subdivisions=4;
        [SerializeField,Range(0,2)] private int exitMode=1;
        [SerializeField,Range(.4f,.9f)] private float targetCoverage=.65f;
        [SerializeField,Range(4,24)] private int maximumLength=12;
        [SerializeField,Range(1,12)] private int maximumStartingFreeArrows=3;
        [SerializeField,Range(0,1)] private float dependencyAmount=.5f;
        [SerializeField] private bool measuredDifficultySearch;
        [SerializeField,Range(0,100)] private float targetScore=50;
        [SerializeField,Range(1,8)] private int searchCandidates=3;
        [SerializeField,Range(1,32)] private int restarts=8;
        private GameplayController game;
        private RectTransform panel,viewport;
        private Canvas canvas;
        private Font font;
        private Text status;
        private bool expanded;
        private string fittedFingerprint;
        public string LastGenerationError { get; private set; }
        public AuthoredGenerationShape Shape { get=>shape; set { shape=value; convertedSurface=null; } }
        public ConvertedSurfaceAsset ConvertedSurface { get=>convertedSurface; set { convertedSurface=value; if(value!=null) ConvertedSurfaceAsset.Register(value); } }
        public ConvertedSurfaceAsset[] ConvertedExamples { get=>convertedExamples; set=>convertedExamples=value??Array.Empty<ConvertedSurfaceAsset>(); }
        private string ShapeName() => convertedSurface!=null ? "Mesh / "+convertedSurface.name : shape.ToString();
        public int Seed { get=>seed; set=>seed=value; }
        public int Subdivisions { get=>subdivisions; set=>subdivisions=value; }
        public bool MeasuredDifficultySearch { get=>measuredDifficultySearch; set=>measuredDifficultySearch=value; }
        public AuthoredGenerationSurface Authored { get; private set; }
        private void Awake()
        {
            game=GetComponent<GameplayController>();
            if(convertedSurface!=null) ConvertedSurfaceAsset.Register(convertedSurface);
            foreach(var asset in convertedExamples) if(asset!=null) ConvertedSurfaceAsset.Register(asset);
            if(game.SavePathOverride==null) game.SavePathOverride=Path.Combine(Application.persistentDataPath,convertedSurface==null ? "arrow-cube-procedural-surfaces.json" : "arrow-cube-imported-surfaces.json");
        }
        private void Start() { BuildUI(); Generate(); }
        public void Generate()
        {
            LastGenerationError=null;
            try
            {
                SurfaceShapeDefinition definition; ISurfaceGeometry geometry;
                if(convertedSurface!=null) { var baked=convertedSurface.Build(); definition=baked.Shape; geometry=baked.Geometry; }
                else { Authored=AuthoredGenerationSurfaces.Create(shape,subdivisions); definition=Authored.Shape; geometry=Authored.Geometry; }
                if(definition.Topology.CellCount<maximumLength) throw new ArgumentException($"This surface has {definition.Topology.CellCount} cells, but maximum arrow length is {maximumLength}. Increase conversion density or lower the arrow length setting.");
                var baseProfile=AuthoredGenerationSurfaces.Profile(definition.Topology.CellCount,targetCoverage,maximumLength);
                var difficulty=measuredDifficultySearch ? new DifficultySettings { TargetScore=targetScore,MaxCandidates=searchCandidates,
                    OptimizationIterations=4,CandidatePoolSize=12,MaxConstructionAttempts=12000 } : null;
                var recipe=new PuzzleRecipe(seed,definition,baseProfile,exitMode==0 ? ExitRules.EdgeOnly : exitMode==1 ? ExitRules.GateOnly : ExitRules.Hybrid,
                    maxRestarts:restarts,difficulty:difficulty,dependencyControls:new DependencyControlSettings { Amount=dependencyAmount,MaximumStartingFreeArrows=maximumStartingFreeArrows });
                game.StartSandboxGeneration(recipe,geometry); expanded=false;
            }
            catch(ArgumentException error) { LastGenerationError=error.Message; expanded=true; if(status!=null) status.text=error.Message; }
        }
        private void BuildUI()
        {
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root=new GameObject("Procedural surface settings",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); root.transform.SetParent(transform,false);
            canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=140;
            panel=Rect("Surface lab",root.transform); panel.anchorMin=panel.anchorMax=new Vector2(0,1); panel.pivot=new Vector2(0,1);
            var header=Rect("Open settings",panel); header.anchorMin=new Vector2(0,1); header.anchorMax=Vector2.one; header.pivot=new Vector2(.5f,1); header.sizeDelta=new Vector2(0,34);
            header.gameObject.AddComponent<Image>().color=new Color(.12f,.28f,.4f,.98f); header.gameObject.AddComponent<Button>().onClick.AddListener(()=>expanded=!expanded); Text(header,"SURFACE LAB / Settings",15);
            viewport=Rect("Viewport",panel); viewport.anchorMin=Vector2.zero; viewport.anchorMax=Vector2.one; viewport.offsetMin=Vector2.zero; viewport.offsetMax=new Vector2(0,-36);
            viewport.gameObject.AddComponent<Image>().color=new Color(.05f,.11f,.18f,.98f); viewport.gameObject.AddComponent<RectMask2D>();
            var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.scrollSensitivity=6; scroll.viewport=viewport;
            var content=Rect("Settings",viewport); content.anchorMin=new Vector2(0,1); content.anchorMax=Vector2.one; content.pivot=new Vector2(.5f,1); content.sizeDelta=Vector2.zero;
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding=new RectOffset(8,8,6,6); layout.spacing=5; layout.childForceExpandHeight=false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize; scroll.content=content;
            var shapeButton=Button(content,"Shape: "+ShapeName(),()=>{});
            shapeButton.transform.parent.GetComponent<Button>().onClick.AddListener(()=> { if(convertedExamples.Length>0) { int index=Array.IndexOf(convertedExamples,convertedSurface); ConvertedSurface=convertedExamples[(index+1)%convertedExamples.Length]; }
                else { convertedSurface=null; shape=(AuthoredGenerationShape)(((int)shape+1)%3); }
                shapeButton.text="Shape: "+ShapeName(); });
            // Label Text is a child; the actual clickable Button is on its parent.
            var seedRow=Rect("Seed",content); Height(seedRow,34); seedRow.gameObject.AddComponent<Image>().color=new Color(.18f,.25f,.34f);
            var input=seedRow.gameObject.AddComponent<InputField>(); input.textComponent=Text(seedRow,"",14); input.contentType=InputField.ContentType.IntegerNumber; input.text=seed.ToString(); input.onEndEdit.AddListener(v=> { if(int.TryParse(v,out int parsed)) seed=parsed; else input.text=seed.ToString(); });
            Button(content,"Generate this seed",Generate); Button(content,"Next seed",()=> { seed=unchecked(seed+1); input.text=seed.ToString(); Generate(); });
            Button(content,"Cancel generation",()=>game.CancelGeneration());
            var mode=Button(content,"Exits: "+ModeName(),()=>{});
            mode.transform.parent.GetComponent<Button>().onClick.AddListener(()=> { exitMode=(exitMode+1)%3; mode.text="Exits: "+ModeName(); });
            if(convertedSurface==null) Slider(content,"Cells per authored quad side",2,8,subdivisions,true,v=>subdivisions=(int)v);
            Slider(content,"Target coverage",.4f,.9f,targetCoverage,false,v=>targetCoverage=v);
            Slider(content,"Maximum arrow length",4,24,maximumLength,true,v=>maximumLength=(int)v);
            Slider(content,"Maximum starting free arrows",1,12,maximumStartingFreeArrows,true,v=>maximumStartingFreeArrows=(int)v);
            Slider(content,"Blocker amount",0,1,dependencyAmount,false,v=>dependencyAmount=v);
            var measured=Button(content,"Measured search: "+measuredDifficultySearch,()=>{});
            measured.transform.parent.GetComponent<Button>().onClick.AddListener(()=> { measuredDifficultySearch=!measuredDifficultySearch; measured.text="Measured search: "+measuredDifficultySearch; });
            Slider(content,"Target measured score",0,100,targetScore,true,v=>targetScore=v);
            Slider(content,"Measured candidates",1,8,searchCandidates,true,v=>searchCandidates=(int)v);
            Slider(content,"Maximum restarts",1,32,restarts,true,v=>restarts=(int)v);
            var results=Rect("Results",content); Height(results,130); status=Text(results,"",13);
            string ModeName() => exitMode==0 ? "Edge only" : exitMode==1 ? "Gate only" : "Hybrid";
        }
        private void LateUpdate()
        {
            if(panel==null) return;
            canvas.scaleFactor=Screen.dpi>0 ? Mathf.Clamp(Screen.dpi/120,1,2.5f) : 1; canvas.GetComponent<CanvasScaler>().scaleFactor=canvas.scaleFactor;
            float width=Mathf.Min(370,Screen.safeArea.width/canvas.scaleFactor-16),height=Mathf.Min(540,Screen.safeArea.height/canvas.scaleFactor-180);
            panel.sizeDelta=new Vector2(Mathf.Max(150,width),expanded ? Mathf.Max(180,height) : 34); panel.anchoredPosition=new Vector2(Screen.safeArea.xMin/canvas.scaleFactor+8,-(Screen.height-Screen.safeArea.yMax)/canvas.scaleFactor-94); viewport.gameObject.SetActive(expanded);
            if(!string.IsNullOrEmpty(LastGenerationError)) { status.text=LastGenerationError; expanded=true; return; }
            if(game.IsGenerating) status.text=game.Message;
            else if(game.Page==GamePage.Error) { status.text=game.Message; expanded=true; }
            else if(game.Session?.Puzzle is GeneratedPuzzle puzzle)
            {
                var s=puzzle.Statistics;
                status.text=$"{ShapeName()}: {puzzle.Surface.CellCount} cells / {puzzle.Paths.Count} arrows\nCoverage {s.Coverage:P0}; free {s.ImmediatelyRemovablePaths}; depth {s.DependencyDepth}\nDependencies {s.DependencyCount}; folds {s.GeometricFolds}; occupied regions {s.OccupiedRegions}/{s.RegionCount}\n"+(puzzle.Difficulty?.Warning??"");
                if(fittedFingerprint!=puzzle.Fingerprint)
                {
                    fittedFingerprint=puzzle.Fingerprint; game.Movement.AnimationCellsPerSecond=20;
                    var camera=game.Movement.InspectionCamera; float zoom=game.Movement.SurfaceBounds.extents.magnitude*1.2f/Mathf.Min(1,camera.aspect);
                    game.Input.Orbit.RestoreView(game.Input.Orbit.Rotation,Mathf.Max(game.Input.Orbit.Zoom,zoom));
                }
            }
        }
        private static RectTransform Rect(string name,Transform parent) { var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); return (RectTransform)go.transform; }
        private static void Height(RectTransform rect,float height) { rect.gameObject.AddComponent<LayoutElement>().preferredHeight=height; }
        private Text Text(RectTransform parent,string value,int size)
        {
            var rect=Rect("Label",parent); rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=new Vector2(6,0); rect.offsetMax=new Vector2(-6,0);
            var text=rect.gameObject.AddComponent<Text>(); text.font=font; text.fontSize=size; text.color=Color.white; text.text=value; text.alignment=TextAnchor.MiddleLeft; text.raycastTarget=false; return text;
        }
        private Text Button(Transform parent,string label,UnityEngine.Events.UnityAction action)
        { var rect=Rect(label,parent); Height(rect,32); rect.gameObject.AddComponent<Image>().color=new Color(.16f,.32f,.46f); rect.gameObject.AddComponent<Button>().onClick.AddListener(action); return Text(rect,label,14); }
        private void Slider(Transform parent,string label,float min,float max,float value,bool whole,UnityEngine.Events.UnityAction<float> changed)
        {
            var rect=Rect(label,parent); Height(rect,52); var text=Text(rect,label+": "+value.ToString("0.##"),13); text.rectTransform.anchorMin=new Vector2(0,.5f);
            var track=Rect("Slider",rect); track.anchorMin=new Vector2(0,0); track.anchorMax=new Vector2(1,.4f); track.offsetMin=new Vector2(8,0); track.offsetMax=new Vector2(-8,0); track.gameObject.AddComponent<Image>().color=new Color(.2f,.3f,.4f);
            var slider=track.gameObject.AddComponent<Slider>(); slider.minValue=min; slider.maxValue=max; slider.wholeNumbers=whole;
            var handle=Rect("Handle",track); handle.anchorMin=Vector2.zero; handle.anchorMax=new Vector2(0,1); handle.sizeDelta=new Vector2(18,0); var graphic=handle.gameObject.AddComponent<Image>(); graphic.color=new Color(.6f,.85f,1); slider.handleRect=handle; slider.targetGraphic=graphic; slider.value=value;
            slider.onValueChanged.AddListener(v=> { changed(v); text.text=label+": "+v.ToString("0.##"); });
        }
    }
}
