using ArrowCube.Controls;
using ArrowCube.Generation;
using ArrowCube.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArrowCube.Game
{
    public static class GameSceneLaunch
    {
        public sealed class Request { public GeneratedPuzzle Puzzle; public float Thickness,Sensitivity,Zoom; public bool InvertMouseRotation,InvertTouchRotation; }
        private static Request pending;
        public static bool ReturningToLab;
        public static string LabJson;
        public static int LabSeed;
        public static bool LabEdges,LabGates;
        public static bool LabInvertMouseRotation,LabInvertTouchRotation;
        public static float LabThickness=.35f,LabSensitivity=1,LabZoom=.35f;
        public static void Queue(GeneratedPuzzle puzzle,float thickness,float sensitivity,float zoom,bool invertMouseRotation=false,bool invertTouchRotation=false)
        { pending=new Request { Puzzle=puzzle,Thickness=thickness,Sensitivity=sensitivity,Zoom=zoom,InvertMouseRotation=invertMouseRotation,InvertTouchRotation=invertTouchRotation }; }
        public static Request Take() { var result=pending; pending=null; return result; }
    }

    [RequireComponent(typeof(Stage4ControlsDemo))]
    public sealed class LabGameplayBridge : MonoBehaviour
    {
        private Stage4ControlsDemo lab;
        private DifficultyProfileAsset returnedProfile;
        private void Awake()
        {
            lab=GetComponent<Stage4ControlsDemo>();
            if(GameSceneLaunch.ReturningToLab && !string.IsNullOrEmpty(GameSceneLaunch.LabJson))
            {
                GameSceneLaunch.ReturningToLab=false;
                returnedProfile=ScriptableObject.CreateInstance<DifficultyProfileAsset>();
                returnedProfile.hideFlags=HideFlags.HideAndDontSave;
                JsonUtility.FromJsonOverwrite(GameSceneLaunch.LabJson,returnedProfile);
                var generation=GetComponent<Stage3GenerationDemo>();
                generation.CustomProfile=returnedProfile; generation.Seed=GameSceneLaunch.LabSeed;
                generation.EnableCubeEdgeExits=GameSceneLaunch.LabEdges; generation.EnableDirectionalGates=GameSceneLaunch.LabGates;
                GetComponent<Stage2MovementDemo>().ArrowThickness=GameSceneLaunch.LabThickness;
                GetComponent<OrbitCameraController>().Sensitivity=GameSceneLaunch.LabSensitivity;
                GetComponent<OrbitCameraController>().ZoomSpeed=GameSceneLaunch.LabZoom;
                GetComponent<PuzzleInputManager>().InvertMouseRotation=GameSceneLaunch.LabInvertMouseRotation;
                GetComponent<PuzzleInputManager>().InvertTouchRotation=GameSceneLaunch.LabInvertTouchRotation;
            }
        }
        private void OnEnable() => lab.GameplayRequested+=Launch;
        private void OnDisable() => lab.GameplayRequested-=Launch;
        private void Launch(GeneratedPuzzle puzzle)
        {
            if(!Application.CanStreamedLevelBeLoaded("Game")) { Debug.LogError("Add Assets/Scenes/Game.unity to the build scene list."); return; }
            GameSceneLaunch.LabJson=JsonUtility.ToJson(lab.Generation.EditingProfile);
            GameSceneLaunch.LabSeed=puzzle.Recipe.Seed;
            GameSceneLaunch.LabEdges=lab.Generation.EnableCubeEdgeExits; GameSceneLaunch.LabGates=lab.Generation.EnableDirectionalGates;
            GameSceneLaunch.LabThickness=lab.Generation.Movement.ArrowThickness;
            GameSceneLaunch.LabSensitivity=lab.Input.Orbit.Sensitivity; GameSceneLaunch.LabZoom=lab.Input.Orbit.ZoomSpeed;
            GameSceneLaunch.LabInvertMouseRotation=lab.Input.InvertMouseRotation; GameSceneLaunch.LabInvertTouchRotation=lab.Input.InvertTouchRotation;
            GameSceneLaunch.Queue(puzzle,GameSceneLaunch.LabThickness,GameSceneLaunch.LabSensitivity,GameSceneLaunch.LabZoom,
                GameSceneLaunch.LabInvertMouseRotation,GameSceneLaunch.LabInvertTouchRotation);
            SceneManager.LoadScene("Game");
        }
        private void OnDestroy() { if(returnedProfile!=null) Destroy(returnedProfile); }
    }
}
