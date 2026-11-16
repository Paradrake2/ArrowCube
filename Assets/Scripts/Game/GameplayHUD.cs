using System;
using ArrowCube.Controls;
using ArrowCube.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ArrowCube.Game
{
    /// <summary>Stage 5 functional HUD. The full generation controls stay in the lab scene.</summary>
    public sealed class GameplayHUD : MonoBehaviour
    {
        private GameplayController game;
        private Canvas canvas;
        private RectTransform safe,top,bottom,modal;
        private Text title,remaining,hearts,message;
        private Text generationProgress;
        private Button hint,undo,hammer,pause;
        private Font font;
        private GamePage displayed=(GamePage)(-1);
        private int levelPage;
        private bool rebuild;
        public Rect PuzzleScreenRect { get; private set; }
        public Canvas Canvas => canvas;
        private void Start()
        {
            game=GetComponent<GameplayController>(); font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root=new GameObject("Stage 5 gameplay UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.transform.SetParent(transform,false); canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=100;
            safe=Rect("Safe area",canvas.transform);
            top=Panel("Gameplay header",safe,new Color(.065f,.13f,.22f,.98f));
            top.anchorMin=new Vector2(0,1); top.anchorMax=Vector2.one; top.pivot=new Vector2(.5f,1);
            title=Text(top,"ARROW CUBE",20,30); title.rectTransform.anchorMin=new Vector2(0,.5f); title.rectTransform.anchorMax=new Vector2(.65f,1);
            title.rectTransform.offsetMin=new Vector2(16,0); title.rectTransform.offsetMax=new Vector2(0,-8);
            remaining=Text(top,"",15,24); remaining.rectTransform.anchorMin=Vector2.zero; remaining.rectTransform.anchorMax=new Vector2(.65f,.5f);
            remaining.rectTransform.offsetMin=new Vector2(16,8); remaining.rectTransform.offsetMax=Vector2.zero;
            hearts=Text(top,"",16,24); hearts.alignment=TextAnchor.MiddleRight;
            hearts.rectTransform.anchorMin=new Vector2(.65f,0); hearts.rectTransform.anchorMax=Vector2.one;
            hearts.rectTransform.offsetMin=Vector2.zero; hearts.rectTransform.offsetMax=new Vector2(-16,0);
            bottom=Panel("Gameplay actions",safe,new Color(.065f,.13f,.22f,.98f));
            bottom.anchorMin=bottom.anchorMax=Vector2.zero; bottom.pivot=Vector2.zero;
            var buttons=Rect("Actions",bottom); buttons.anchorMin=Vector2.zero; buttons.anchorMax=new Vector2(1,.6f); buttons.offsetMin=new Vector2(8,6); buttons.offsetMax=new Vector2(-8,0);
            var row=buttons.gameObject.AddComponent<HorizontalLayoutGroup>(); row.spacing=6; row.childForceExpandWidth=true; row.childForceExpandHeight=true;
            hint=Button(buttons,"Hint (H)",()=>game.Hint()); undo=Button(buttons,"Undo (Z)",()=>game.Undo());
            hammer=Button(buttons,"Hammer",()=>game.ArmHammer()); pause=Button(buttons,"Pause",()=>game.Pause());
            message=Text(bottom,"",13,30); message.rectTransform.anchorMin=new Vector2(0,.6f); message.rectTransform.anchorMax=Vector2.one;
            message.rectTransform.offsetMin=new Vector2(10,0); message.rectTransform.offsetMax=new Vector2(-10,-4);
            modal=Panel("Game menu",safe,new Color(.035f,.075f,.14f,.97f)); Stretch(modal,0);
            if(EventSystem.current==null)
            {
                var events=new GameObject("Gameplay EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform,false); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            game.Input.AdditionalUIBlocker=point=>game.Page!=GamePage.Game || !PuzzleScreenRect.Contains(point);
            UpdateLayout(); Refresh();
        }
        private void Update() { if(game==null || game.Player==null) return; UpdateLayout(); Refresh(); }
        public void UpdateLayout()
        {
            var area=Screen.safeArea;
            if(area.width<=0 || area.height<=0) area=new Rect(0,0,Screen.width,Screen.height);
            float scale=Screen.dpi>0 ? Mathf.Clamp(Screen.dpi/120,1,2.5f) : 1;
            canvas.GetComponent<CanvasScaler>().scaleFactor=scale; canvas.scaleFactor=scale;
            safe.anchorMin=safe.anchorMax=Vector2.zero; safe.pivot=Vector2.zero; safe.anchoredPosition=area.position/scale; safe.sizeDelta=area.size/scale;
            top.anchoredPosition=new Vector2(0,-8); top.sizeDelta=new Vector2(0,76);
            bottom.anchoredPosition=new Vector2(0,8); bottom.sizeDelta=new Vector2(safe.sizeDelta.x,100);
            float lower=116*scale,upper=92*scale;
            PuzzleScreenRect=new Rect(area.x,area.y+lower,area.width,Mathf.Max(32,area.height-lower-upper));
            var view=game.Movement.InspectionCamera;
            if(view!=null) view.rect=new Rect(PuzzleScreenRect.x/Screen.width,PuzzleScreenRect.y/Screen.height,
                PuzzleScreenRect.width/Screen.width,PuzzleScreenRect.height/Screen.height);
            if(modal.childCount>0)
            {
                var viewport=(RectTransform)modal.GetChild(modal.childCount-1);
                viewport.anchorMin=new Vector2(.5f,0); viewport.anchorMax=new Vector2(.5f,1);
                viewport.anchoredPosition=Vector2.zero; viewport.sizeDelta=new Vector2(Mathf.Max(160,Mathf.Min(560,safe.sizeDelta.x-32)),-32);
            }
        }
        private void Refresh()
        {
            if(game.Player==null) return;
            var session=game.Session;
            title.text=game.IsSandbox ? "ARROW CUBE / LAB TEST" : $"ARROW CUBE / LEVEL {game.Level}";
            remaining.text=session==null ? "A continuous cube puzzle" : $"{session.Grid.Paths.Count} arrows remaining";
            hearts.gameObject.SetActive(session!=null && !game.Player.Options.UnlimitedAttempts);
            hearts.text=session==null ? "" : $"HEARTS  {session.Hearts}";
            message.text=string.IsNullOrEmpty(game.SaveWarning) ? game.Message : game.SaveWarning;
            if(generationProgress!=null) generationProgress.text=game.Message;
            hint.interactable=game.CanAct && (session.Options.UnlimitedPowerUps || session.HintsLeft>0);
            hammer.interactable=game.CanAct && (session.Options.UnlimitedPowerUps || session.HammersLeft>0);
            undo.interactable=session!=null && session.CanUndo && !game.Movement.IsAnimating && (game.Page==GamePage.Game || game.Page==GamePage.Victory);
            pause.interactable=game.Page==GamePage.Game;
            SetButtonText(hint,session==null || session.Options.UnlimitedPowerUps ? "Hint (H)" : $"Hint {session.HintsLeft}");
            SetButtonText(undo,session==null || session.Options.UnlimitedPowerUps ? "Undo (Z)" : $"Undo {session.UndosLeft}");
            SetButtonText(hammer,game.HammerArmed ? "Cancel hammer" : session==null || session.Options.UnlimitedPowerUps ? "Hammer" : $"Hammer {session.HammersLeft}");
            if(displayed!=game.Page || rebuild) { displayed=game.Page; rebuild=false; BuildModal(); }
        }
        private void BuildModal()
        {
            generationProgress=null;
            foreach(Transform child in modal) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            modal.gameObject.SetActive(game.Page!=GamePage.Game);
            if(game.Page==GamePage.Game) return;
            var viewport=Rect("Menu viewport",modal); Stretch(viewport,16); viewport.gameObject.AddComponent<RectMask2D>();
            var scroll=modal.gameObject.GetComponent<ScrollRect>()??modal.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal=false; scroll.scrollSensitivity=6; scroll.movementType=ScrollRect.MovementType.Clamped;
            var content=Rect("Menu content",viewport); content.anchorMin=new Vector2(0,1); content.anchorMax=Vector2.one; content.pivot=new Vector2(.5f,1); content.sizeDelta=Vector2.zero;
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing=10; layout.padding=new RectOffset(8,8,12,20);
            layout.childControlHeight=layout.childControlWidth=true; layout.childForceExpandHeight=false; layout.childForceExpandWidth=true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport; scroll.content=content; scroll.verticalNormalizedPosition=1;
            switch(game.Page)
            {
                case GamePage.Menu:
                    Heading(content,"ARROW CUBE"); Copy(content,"Clear the cube, one arrow at a time. Offline play. No attempt limits by default.");
                    Button(content,game.Session!=null || game.Player.Active!=null ? "Continue puzzle" : "Play level 1",()=>game.Continue());
                    if(game.HasSavedSandbox && !game.IsSandbox) Button(content,"Resume last lab test",()=>game.ContinueSandbox());
                    Button(content,"Settings",()=>game.OpenSettings());
                    Text(content,$"LEVEL SELECT / unlocked through {game.Player.Progress.HighestUnlocked}",16,28);
                    int start=levelPage*10+1;
                    for(int level=start;level<start+10 && level<=game.Player.Progress.HighestUnlocked;level++)
                    {
                        int selected=level; var record=game.Player.Progress.Find(level);
                        Button(content,record==null ? $"Level {level}" : $"Replay level {level} / {record.BestStars} stars",()=>game.StartLevel(selected));
                    }
                    if(levelPage>0) Button(content,"Previous levels",()=> { levelPage--; rebuild=true; });
                    if(start+10<=game.Player.Progress.HighestUnlocked) Button(content,"More levels",()=> { levelPage++; rebuild=true; });
                    Button(content,"Open settings lab",()=>game.ReturnToLab());
                    if(!string.IsNullOrEmpty(game.SaveWarning)) Copy(content,game.SaveWarning);
                    break;
                case GamePage.Pause:
                    Heading(content,"PAUSED"); Button(content,"Resume",()=>game.Resume()); Button(content,"Restart same puzzle",()=>game.Restart());
                    Button(content,"Settings",()=>game.OpenSettings()); Button(content,"Return to menu",()=>game.Menu()); Button(content,"Open settings lab",()=>game.ReturnToLab()); break;
                case GamePage.Settings: BuildSettings(content); break;
                case GamePage.Victory:
                    Heading(content,game.IsSandbox ? "TEST PUZZLE CLEARED" : $"LEVEL {game.Level} CLEARED");
                    Copy(content,$"Stars: {game.Session.Stars}/3\nMistakes: {game.Session.Mistakes} / Time: {game.Session.Seconds:F0}s");
                    if(game.IsSandbox) Button(content,"Return to settings lab",()=>game.ReturnToLab()); else Button(content,"Next level",()=>game.NextLevel());
                    Button(content,"Replay same puzzle",()=>game.Restart()); Button(content,"Undo last removal",()=>game.Undo()).interactable=game.Session.CanUndo;
                    Button(content,"Return to menu",()=>game.Menu()); break;
                case GamePage.Failed:
                    Heading(content,"OUT OF HEARTS"); Copy(content,"Restart this same puzzle, or enable unlimited attempts in Settings to continue from here.");
                    Button(content,"Restart same puzzle",()=>game.Restart()); Button(content,"Settings",()=>game.OpenSettings()); Button(content,"Return to menu",()=>game.Menu()); break;
                case GamePage.Generating:
                    Heading(content,"CREATING PUZZLE"); Copy(content,"Building and verifying every removal. Your previous puzzle is kept until this one is ready.");
                    generationProgress=Text(content,game.Message,15,40);
                    Button(content,"Cancel",()=>game.CancelGeneration()); break;
                case GamePage.Error:
                    Heading(content,"PUZZLE NOT READY"); Copy(content,game.Message); Button(content,"Return to menu",()=>game.Menu());
                    Button(content,"Open settings lab",()=>game.ReturnToLab()); break;
            }
        }
        private void BuildSettings(Transform parent)
        {
            Heading(parent,"SETTINGS");
            Toggle(parent,"Unlimited attempts",game.Player.Options.UnlimitedAttempts,value=> { var o=game.Player.Options.Copy(); o.UnlimitedAttempts=value; game.ApplyOptions(o); });
            Copy(parent,"Unlimited attempts hides hearts. Changing mode keeps all removed arrows and your remaining hearts.");
            Slider(parent,"Starting hearts",1,10,true,game.Player.Options.StartingHearts,value=> { var o=game.Player.Options.Copy(); o.StartingHearts=(int)value; game.ApplyOptions(o); });
            Toggle(parent,"Unlimited power-ups",game.Player.Options.UnlimitedPowerUps,value=> { var o=game.Player.Options.Copy(); o.UnlimitedPowerUps=value; game.ApplyOptions(o); });
            Slider(parent,"Hints per level",0,10,true,game.Player.Options.Hints,value=> { var o=game.Player.Options.Copy(); o.Hints=(int)value; game.ApplyOptions(o); });
            Slider(parent,"Hammers per level",0,10,true,game.Player.Options.Hammers,value=> { var o=game.Player.Options.Copy(); o.Hammers=(int)value; game.ApplyOptions(o); });
            Slider(parent,"Undos per level",0,10,true,game.Player.Options.Undos,value=> { var o=game.Player.Options.Copy(); o.Undos=(int)value; game.ApplyOptions(o); });
            Copy(parent,"Starting hearts and power-up allowances apply on the next level or restart. Unlimited power-ups is available immediately.");
            Slider(parent,"Camera sensitivity",.1f,3,false,game.Input.Orbit.Sensitivity,game.SetSensitivity);
            Toggle(parent,"Invert mouse rotation",game.Input.InvertMouseRotation,game.SetInvertMouseRotation);
            Toggle(parent,"Invert touch rotation",game.Input.InvertTouchRotation,game.SetInvertTouchRotation);
            Slider(parent,"Zoom speed",.05f,2,false,game.Input.Orbit.ZoomSpeed,game.SetZoomSpeed);
            Slider(parent,"Arrow thickness",.05f,.45f,false,game.Movement.ArrowThickness,game.SetThickness);
            Copy(parent,"Puzzle generation settings remain in the separate settings lab. H: hint / Z: undo / Esc: pause / R: reset camera / F11: fullscreen.");
            Button(parent,"Back",()=>game.CloseSettings());
        }
        private static RectTransform Rect(string name,Transform parent)
        { var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); return go.GetComponent<RectTransform>(); }
        private static void Stretch(RectTransform rect,float inset)
        { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=Vector2.one*inset; rect.offsetMax=-Vector2.one*inset; }
        private static RectTransform Panel(string name,Transform parent,Color color)
        { var rect=Rect(name,parent); rect.gameObject.AddComponent<Image>().color=color; return rect; }
        private Text Text(Transform parent,string value,int size,float height)
        {
            var rect=Rect("Text",parent); var text=rect.gameObject.AddComponent<Text>(); text.font=font; text.text=value; text.fontSize=size;
            text.color=Color.white; text.raycastTarget=false; text.alignment=TextAnchor.MiddleLeft;
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight=height; return text;
        }
        private void Heading(Transform parent,string value)
        { var text=Text(parent,value,26,48); text.resizeTextForBestFit=true; text.resizeTextMinSize=18; text.resizeTextMaxSize=26; }
        private void Copy(Transform parent,string value)
        { var text=Text(parent,value,15,66); var element=text.GetComponent<LayoutElement>(); element.preferredHeight=-1; element.minHeight=48; }
        private Button Button(Transform parent,string label,Action action)
        {
            var rect=Panel(label,parent,new Color(.16f,.35f,.52f)); rect.gameObject.AddComponent<LayoutElement>().preferredHeight=44;
            var button=rect.gameObject.AddComponent<Button>(); button.onClick.AddListener(()=>action());
            var text=Text(rect,label,16,44); Stretch(text.rectTransform,8); text.alignment=TextAnchor.MiddleCenter; return button;
        }
        private static void SetButtonText(Button button,string value) => button.GetComponentInChildren<Text>().text=value;
        private void Toggle(Transform parent,string label,bool value,Action<bool> changed)
        {
            var rect=Panel(label,parent,new Color(.12f,.24f,.36f)); rect.gameObject.AddComponent<LayoutElement>().preferredHeight=44;
            var toggle=rect.gameObject.AddComponent<Toggle>(); var box=Rect("Check",rect);
            box.anchorMin=box.anchorMax=new Vector2(0,.5f); box.sizeDelta=new Vector2(20,20); box.anchoredPosition=new Vector2(20,0);
            var image=box.gameObject.AddComponent<Image>(); image.color=new Color(.2f,.9f,.65f); toggle.graphic=image;
            var text=Text(rect,label,16,44); Stretch(text.rectTransform,8); text.rectTransform.offsetMin=new Vector2(40,8);
            toggle.isOn=value; toggle.onValueChanged.AddListener(on=>changed(on));
        }
        private void Slider(Transform parent,string name,float min,float max,bool integer,float initial,Action<float> changed)
        {
            var label=Text(parent,name+": "+initial.ToString(integer ? "F0" : "F2"),15,24);
            var rect=Panel(name,parent,new Color(.15f,.27f,.38f)); rect.gameObject.AddComponent<LayoutElement>().preferredHeight=30;
            var slider=rect.gameObject.AddComponent<ScrollAwareSlider>(); slider.minValue=min; slider.maxValue=max; slider.wholeNumbers=integer;
            var handle=Rect("Handle",rect); handle.sizeDelta=new Vector2(20,-4); var image=handle.gameObject.AddComponent<Image>(); image.color=new Color(.65f,.88f,1);
            slider.handleRect=handle; slider.targetGraphic=image; slider.value=initial;
            slider.onValueChanged.AddListener(value=> { changed(value); label.text=name+": "+value.ToString(integer ? "F0" : "F2"); });
        }
    }
}
