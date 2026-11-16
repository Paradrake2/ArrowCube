using System.IO;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowCube.Game
{
    /// <summary>Thin authored-scene bootstrap using the standard gameplay, input and rendering components.</summary>
    [DefaultExecutionOrder(-80)]
    [RequireComponent(typeof(GameplayController))]
    public sealed class SurfaceGeometryDemo : MonoBehaviour
    {
        [SerializeField] private bool foldedPrism;
        [SerializeField] private bool geometricEdgeExits;
        [SerializeField,Range(.5f,20)] private float animationSpeed=4;
        private GameplayController game;
        private RectTransform selector;
        private Canvas selectorCanvas;
        public bool FoldedPrism { get => foldedPrism; set => foldedPrism=value; }
        public SurfaceDemoDefinition Definition { get; private set; }
        private void Awake()
        {
            game=GetComponent<GameplayController>();
            if(game.SavePathOverride==null) game.SavePathOverride=Path.Combine(Application.persistentDataPath,"arrow-cube-surface-demo.json");
        }
        private void Start()
        {
            SelectShape(foldedPrism);
            var root=new GameObject("Surface demo selector",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); root.transform.SetParent(transform,false);
            var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=150; selectorCanvas=canvas;
            var row=new GameObject("Demos",typeof(RectTransform),typeof(HorizontalLayoutGroup),typeof(Image)); row.transform.SetParent(root.transform,false);
            var rect=(RectTransform)row.transform; selector=rect; rect.anchorMin=rect.anchorMax=new Vector2(.5f,1); rect.pivot=new Vector2(.5f,1); rect.anchoredPosition=new Vector2(0,-94); rect.sizeDelta=new Vector2(540,40);
            row.GetComponent<Image>().color=new Color(.06f,.12f,.2f,.96f); row.GetComponent<HorizontalLayoutGroup>().spacing=5;
            Button("L-shaped solid",()=>SelectShape(false)); Button("Oblique prism",()=>SelectShape(true));
            Button("Toggle edge exits",()=> { geometricEdgeExits=!geometricEdgeExits; SelectShape(foldedPrism); });
            void Button(string title,UnityEngine.Events.UnityAction action)
            {
                var go=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button)); go.transform.SetParent(row.transform,false); go.GetComponent<Image>().color=new Color(.15f,.32f,.46f);
                go.GetComponent<Button>().onClick.AddListener(action);
                var textObject=new GameObject("Label",typeof(RectTransform),typeof(Text)); textObject.transform.SetParent(go.transform,false);
                var text=textObject.GetComponent<Text>(); text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text=title; text.fontSize=14; text.alignment=TextAnchor.MiddleCenter; text.raycastTarget=false;
                var tr=(RectTransform)text.transform; tr.anchorMin=Vector2.zero; tr.anchorMax=Vector2.one; tr.offsetMin=tr.offsetMax=Vector2.zero;
            }
        }
        public void SelectShape(bool folded)
        {
            foldedPrism=folded;
            var rules=new ExitRules(geometricEdgeExits,true);
            Definition=folded ? AuthoredSurfaceDemos.CreateFolded(rules) : AuthoredSurfaceDemos.CreateL(rules);
            game.LoadPuzzle(Definition.Puzzle,1,true,Definition.Geometry); game.Movement.AnimationCellsPerSecond=animationSpeed;
            StartCoroutine(FitView());
        }
        private System.Collections.IEnumerator FitView()
        {
            yield return null;
            var camera=game.Movement.InspectionCamera; var bounds=game.Movement.SurfaceBounds;
            float scale=Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.y),Mathf.Abs(transform.lossyScale.z));
            float zoom=Mathf.Max(game.Input.Orbit.Zoom,bounds.extents.magnitude*scale*1.12f/Mathf.Min(1,camera.aspect));
            game.Input.Orbit.RestoreView(game.Input.Orbit.Rotation,zoom);
        }
        private void LateUpdate()
        {
            if(selector==null) return;
            float scale=Screen.dpi>0 ? Mathf.Clamp(Screen.dpi/120,1,2.5f) : 1;
            selectorCanvas.scaleFactor=scale;
            selector.sizeDelta=new Vector2(Mathf.Max(140,Mathf.Min(540,Screen.safeArea.width/scale-16)),44);
            selector.anchoredPosition=new Vector2(0,-(Screen.height-Screen.safeArea.yMax)/scale-94);
            selector.gameObject.SetActive(game.Page==GamePage.Game || game.Page==GamePage.Victory || game.Page==GamePage.Failed);
        }
    }
}
