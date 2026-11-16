using System;
using System.IO;
using ArrowCube.Controls;
using ArrowCube.Gameplay;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ArrowCube.Game
{
    public enum GamePage { Menu, Game, Pause, Settings, Victory, Failed, Generating, Error }

    [RequireComponent(typeof(Stage2MovementDemo),typeof(PuzzleInputManager))]
    [DefaultExecutionOrder(-90)]
    public sealed class GameplayController : MonoBehaviour
    {
        [SerializeField] private GameplaySettingsAsset settings;
        private GameplaySettingsAsset ownedSettings;
        private LocalSaveStore store,sandboxStore;
        private ActiveLevelSave active;
        private GenerationOperation generation;
        private int pendingLevel;
        private string expectedFingerprint;
        private bool settingsDirty;
        private float settingsSaveAt;
        private GamePage settingsReturn;
        private ISurfaceGeometry activeGeometry;
        public string SavePathOverride { get; set; }
        public GameplaySettingsAsset Settings { get => settings; set => settings=value; }
        public PlayerSave Player { get; private set; }
        public GameplaySession Session { get; private set; }
        public Stage2MovementDemo Movement { get; private set; }
        public PuzzleInputManager Input { get; private set; }
        public GamePage Page { get; private set; }=GamePage.Menu;
        public string Message { get; private set; }="";
        public string SaveWarning { get; private set; }="";
        public bool HammerArmed { get; private set; }
        public bool IsGenerating => generation!=null;
        public bool GenerateOnMainThread { get; set; }=GenerationOperation.RequiresMainThread;
        public bool IsSandbox => active?.Sandbox??false;
        public int Level => active?.Level??1;
        public string Fingerprint => active?.Fingerprint??"";
        public string SaveFilePath => store?.Path;
        public bool HasSavedSandbox => sandboxStore!=null && (File.Exists(sandboxStore.Path) || File.Exists(sandboxStore.Path+".bak"));
        public bool CanAct => Page==GamePage.Game && Session!=null && Session.CanPlay && !Movement.IsAnimating;

        private void Awake()
        {
            Movement=GetComponent<Stage2MovementDemo>(); Movement.StartupOwnedExternally=true; Movement.ShowControls=false;
            Input=GetComponent<PuzzleInputManager>();
        }
        private void Start()
        {
            if(settings==null) settings=Resources.Load<GameplaySettingsAsset>("GameplaySettings");
            if(settings==null) { ownedSettings=ScriptableObject.CreateInstance<GameplaySettingsAsset>(); settings=ownedSettings; }
            string path=SavePathOverride??Path.Combine(Application.persistentDataPath,"arrow-cube-player.json");
            store=new LocalSaveStore(path); sandboxStore=new LocalSaveStore(path+".sandbox");
            try
            {
                Player=store.Load(out string warning)??new PlayerSave { Options=settings.defaults.Copy(),BaseSeed=settings.baseSeed };
                Player.Validate(); SaveWarning=warning;
            }
            catch(Exception error) when(error is ArgumentException || error is IOException || error is UnauthorizedAccessException)
            { Player=new PlayerSave(); SaveWarning="Could not load local settings: "+error.Message; }
            Movement.ArrowThickness=Player.ArrowThickness; Movement.AnimationCellsPerSecond=40;
            Input.Orbit.Sensitivity=Player.Sensitivity; Input.Orbit.ZoomSpeed=Player.ZoomSpeed;
            Input.InvertMouseRotation=Player.InvertMouseRotation; Input.InvertTouchRotation=Player.InvertTouchRotation;
            Input.PathActivationHandler=ActivatePath; Movement.PlanProvider=id=>CanAct ? Session.BeginAttempt(id) : null;
            Movement.AttemptCompleted+=AttemptCompleted;
            if(GetComponent<GameplayHUD>()==null) gameObject.AddComponent<GameplayHUD>();
            var launch=GameSceneLaunch.Take();
            if(launch!=null)
            {
                Movement.ArrowThickness=launch.Thickness; Input.Orbit.Sensitivity=launch.Sensitivity; Input.Orbit.ZoomSpeed=launch.Zoom;
                Input.InvertMouseRotation=launch.InvertMouseRotation; Input.InvertTouchRotation=launch.InvertTouchRotation;
                LoadPuzzle(launch.Puzzle,1,true);
            }
            else SetPage(GamePage.Menu);
        }
        private void Update()
        {
            if(Player==null) return;
            if(settingsDirty && Time.unscaledTime>=settingsSaveAt) Save();
            generation?.Tick();
            if(generation!=null && generation.IsCompleted)
            {
                var job=generation; generation=null; job.Dispose();
                if(job.IsCanceled) SetPage(GamePage.Menu);
                else if(job.Error!=null) { Message=job.Error.Message; SetPage(GamePage.Error); }
                else if(!string.IsNullOrEmpty(expectedFingerprint) && job.Result.Fingerprint!=expectedFingerprint)
                { Message="This saved level could not be reproduced exactly. Its saved recipe has been kept."; SetPage(GamePage.Error); }
                else LoadPuzzle(job.Result,pendingLevel,false);
            }
            else if(generation?.Progress!=null)
            {
                Message=generation.Progress.Phase+"...";
            }
            if(Page==GamePage.Game) Session?.Tick(Time.unscaledDeltaTime);
            var keyboard=Keyboard.current;
            if(keyboard==null || Typing()) return;
            if(keyboard.escapeKey.wasPressedThisFrame)
            {
                if(HammerArmed) { HammerArmed=false; Message="Hammer selection cancelled."; }
                else if(Page==GamePage.Game) Pause();
                else if(Page==GamePage.Pause) Resume();
                else if(Page==GamePage.Settings) CloseSettings();
            }
            if(keyboard.hKey.wasPressedThisFrame) Hint();
            if(keyboard.zKey.wasPressedThisFrame) Undo();
        }
        private static bool Typing()
        {
            var selected=EventSystem.current==null ? null : EventSystem.current.currentSelectedGameObject;
            return selected!=null && (selected.GetComponent<UnityEngine.UI.InputField>()!=null || selected.GetComponent("TMP_InputField")!=null);
        }
        public bool StartLevel(int level)
        {
            if(Player==null || !Player.Progress.IsUnlocked(level)) return false;
            try
            {
                var archive=Player.Progress.Find(level);
                bool archived=archive?.Recipe!=null && archive.Recipe.Version>0;
                var recipe=archived ? archive.Recipe.Restore() : settings.RecipeFor(level,Player.BaseSeed);
                Save(); CancelGeneration(false); pendingLevel=level;
                expectedFingerprint=archived ? archive.Fingerprint : null;
                generation=new GenerationOperation(recipe,GenerateOnMainThread);
                Message="Building a verified puzzle..."; SetPage(GamePage.Generating); return true;
            }
            catch(ArgumentException error) { Message=error.Message; SetPage(GamePage.Error); return false; }
        }
        public void LoadPuzzle(PuzzleDefinition puzzle,int level=1,bool sandbox=true,ISurfaceGeometry geometry=null)
        {
            activeGeometry=geometry??AuthoredSurfaceDemos.Resolve(puzzle);
            CancelGeneration(false);
            var generated=puzzle as GeneratedPuzzle;
            active=new ActiveLevelSave { Level=level,Sandbox=sandbox,Puzzle=PuzzleSave.Capture(puzzle),Recipe=generated==null ? null : RecipeSave.Capture(generated.Recipe),
                Fingerprint=generated?.Fingerprint??"Manual test puzzle" };
            Session=new GameplaySession(puzzle,Player.Options); HammerArmed=false;
            RefreshPuzzle(false); Message=sandbox ? "Custom test puzzle. Campaign progress is kept separately." : "Clear every arrow from the cube.";
            SetPage(GamePage.Game); Save();
        }
        public bool Continue()
        {
            if(Session!=null) { Resume(); return true; }
            if(Player?.Active==null) return StartLevel(1);
            try
            {
                active=Player.Active; Session=active.Restore(); Session.Configure(Player.Options);
                RefreshPuzzle(false); Resume(); return true;
            }
            catch(ArgumentException error) { Message=error.Message; SetPage(GamePage.Error); return false; }
        }
        public bool ContinueSandbox()
        {
            try
            {
                var saved=sandboxStore.Load(out var warning); SaveWarning=warning;
                if(saved?.Active==null || !saved.Active.Sandbox) { Message="No saved lab test is available."; return false; }
                active=saved.Active; Session=active.Restore(); Session.Configure(Player.Options);
                Movement.ArrowThickness=saved.ArrowThickness; Input.Orbit.Sensitivity=saved.Sensitivity; Input.Orbit.ZoomSpeed=saved.ZoomSpeed;
                Input.InvertMouseRotation=saved.InvertMouseRotation; Input.InvertTouchRotation=saved.InvertTouchRotation;
                RefreshPuzzle(false); Resume(); return true;
            }
            catch(ArgumentException error) { Message=error.Message; return false; }
        }
        public bool ActivatePath(int id)
        {
            if(!CanAct) return false;
            if(HammerArmed)
            {
                if(!Session.Hammer(id)) return false;
                HammerArmed=false; RefreshPuzzle(true); Message="Hammer removed the selected arrow."; SessionChanged(); return true;
            }
            return Movement.TryActivate(id);
        }
        private void AttemptCompleted(MovementPlan plan,bool committed)
        {
            if(Session==null || !Session.CompleteAttempt(plan,committed)) return;
            Message=plan.Succeeded ? "Arrow cleared." : Player.Options.UnlimitedAttempts ? "That arrow is blocked. Try another route." : "Blocked arrow. One heart lost.";
            SessionChanged();
        }
        public bool Hint()
        {
            if(!CanAct) return false;
            var id=Session.Hint();
            if(!id.HasValue) { Message="No hints available."; return false; }
            HammerArmed=false; Movement.HighlightPath(id.Value); Movement.FocusCell(Session.Grid.Paths[id.Value].HeadCellId);
            Message="The green arrow has a clear exit."; Save(); return true;
        }
        public bool ArmHammer()
        {
            if(!CanAct || !Session.Options.UnlimitedPowerUps && Session.HammersLeft==0) return false;
            HammerArmed=!HammerArmed; Message=HammerArmed ? "Select the arrow to remove with the hammer." : "Hammer selection cancelled."; return true;
        }
        public bool Undo()
        {
            if(Session==null || Movement.IsAnimating || Page!=GamePage.Game && Page!=GamePage.Victory || !Session.Undo()) return false;
            HammerArmed=false; SetPage(GamePage.Game); RefreshPuzzle(true); Message="Restored the last removed arrow."; Save(); return true;
        }
        public void Restart()
        {
            if(Session==null) return;
            Session.Restart(); active.VictoryRecorded=false; HammerArmed=false;
            RefreshPuzzle(false); SetPage(GamePage.Game); Message="Restarted the exact same puzzle."; Save();
        }
        public void NextLevel()
        {
            if(Session?.State!=PlayState.Victory || IsSandbox) return;
            StartLevel(Level+1);
        }
        public void Pause() { if(Page==GamePage.Game) { SetPage(GamePage.Pause); Save(); } }
        public void Resume()
        {
            if(Session==null) return;
            Session.Pause(false);
            SetPage(Session.State==PlayState.Failed ? GamePage.Failed : Session.State==PlayState.Victory ? GamePage.Victory : GamePage.Game);
        }
        public void Menu() { CancelGeneration(false); HammerArmed=false; SetPage(GamePage.Menu); Save(); }
        public void OpenSettings() { settingsReturn=Page; SetPage(GamePage.Settings); }
        public void CloseSettings() { if(settingsReturn==GamePage.Menu) SetPage(GamePage.Menu); else Resume(); Save(); }
        public void ApplyOptions(GameplayOptions options)
        {
            options.Validate(); Player.Options=options.Copy(); Session?.Configure(options); ScheduleSettingsSave();
        }
        private void ScheduleSettingsSave() { settingsDirty=true; settingsSaveAt=Time.unscaledTime+.25f; }
        public void SetSensitivity(float value) { Input.Orbit.Sensitivity=value; Player.Sensitivity=Input.Orbit.Sensitivity; ScheduleSettingsSave(); }
        public void SetInvertMouseRotation(bool value) { Input.InvertMouseRotation=value; Player.InvertMouseRotation=value; ScheduleSettingsSave(); }
        public void SetInvertTouchRotation(bool value) { Input.InvertTouchRotation=value; Player.InvertTouchRotation=value; ScheduleSettingsSave(); }
        public void SetZoomSpeed(float value) { Input.Orbit.ZoomSpeed=value; Player.ZoomSpeed=Input.Orbit.ZoomSpeed; ScheduleSettingsSave(); }
        public void SetThickness(float value) { Movement.ArrowThickness=value; Player.ArrowThickness=Movement.ArrowThickness; ScheduleSettingsSave(); }
        private void SetPage(GamePage page)
        {
            Page=page; bool playing=page==GamePage.Game;
            Session?.Pause(!playing); Movement.AnimationPaused=!playing;
            // Enabling the shared input component configures its camera. Preserve the
            // current pose across pause/settings/victory, including undo after a win.
            bool restore=playing && !Input.enabled && Input.Orbit.View!=null;
            var rotation=Input.Orbit.Rotation; float zoom=Input.Orbit.Zoom;
            Input.enabled=playing;
            if(restore) Input.Orbit.RestoreView(rotation,zoom);
            if(!playing) Input.Orbit.StopMotion();
        }
        private void RefreshPuzzle(bool keepView)
        {
            var rotation=Input.Orbit.Rotation; float zoom=Input.Orbit.Zoom;
            if(activeGeometry==null || !ReferenceEquals(activeGeometry.Surface,Session.Puzzle.Surface)) activeGeometry=AuthoredSurfaceDemos.Resolve(Session.Puzzle);
            Movement.LoadSession(Session.Puzzle,Session.Grid,activeGeometry);
            // PuzzleLoaded configures the orbit when enabled. Also configure while in menus.
            Input.Orbit.Configure(Movement.InspectionCamera,transform,Movement.SurfaceBounds);
            if(keepView) Input.Orbit.RestoreView(rotation,zoom);
        }
        private void SessionChanged()
        {
            if(Session.State==PlayState.Victory)
            {
                if(!IsSandbox && !active.VictoryRecorded)
                {
                    Player.Progress.Complete(Level,Session); active.VictoryRecorded=true;
                    var record=Player.Progress.Find(Level);
                    if(record.Recipe==null || record.Recipe.Version==0) { record.Recipe=active.Recipe; record.Fingerprint=active.Fingerprint; }
                }
                SetPage(GamePage.Victory);
            }
            else if(Session.State==PlayState.Failed) SetPage(GamePage.Failed);
            Save();
        }
        public void Save()
        {
            if(Player==null || store==null) return;
            try
            {
                if(Session!=null)
                {
                    active.Session=Session.Capture();
                    if(IsSandbox)
                    {
                        sandboxStore.Save(new PlayerSave { BaseSeed=Player.BaseSeed,Options=Player.Options.Copy(),Active=active,
                            Sensitivity=Input.Orbit.Sensitivity,ZoomSpeed=Input.Orbit.ZoomSpeed,ArrowThickness=Movement.ArrowThickness,
                            InvertMouseRotation=Input.InvertMouseRotation,InvertTouchRotation=Input.InvertTouchRotation });
                    }
                    else Player.Active=active;
                }
                store.Save(Player); SaveWarning=""; settingsDirty=false;
            }
            catch(Exception error) when(error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            { SaveWarning="Progress is playable, but could not be saved: "+error.Message; settingsDirty=false; }
        }
        public void ReturnToLab()
        {
            Save(); GameSceneLaunch.ReturningToLab=true;
            if(Application.CanStreamedLevelBeLoaded("TestScene")) SceneManager.LoadScene("TestScene");
            else Message="Open Assets/Scenes/TestScene.unity to return to the settings lab.";
        }
        public void CancelGeneration(bool showMenu=true)
        {
            if(generation!=null)
            {
                generation.Cancel(); generation.Dispose(); generation=null;
            }
            if(showMenu) SetPage(GamePage.Menu);
        }
        private void OnApplicationPause(bool paused) { if(paused) { Pause(); Save(); } }
        private void OnApplicationFocus(bool focused) { if(!focused) { Pause(); Save(); } }
        private void OnApplicationQuit() => Save();
        private void OnDestroy()
        {
            CancelGeneration(false);
            if(Movement!=null) { Movement.PlanProvider=null; Movement.AttemptCompleted-=AttemptCompleted; }
            if(Input!=null) Input.PathActivationHandler=null;
            if(ownedSettings!=null) Destroy(ownedSettings);
        }
    }
}
