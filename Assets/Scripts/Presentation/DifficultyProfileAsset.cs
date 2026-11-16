using ArrowCube.Generation;
using UnityEngine;

namespace ArrowCube.Presentation
{
    /// <summary>Designer-editable configuration; generation takes an immutable snapshot.</summary>
    [CreateAssetMenu(menuName="Arrow Cube/Difficulty Profile",fileName="ArrowCubeDifficultyProfile")]
    public sealed class DifficultyProfileAsset : ScriptableObject
    {
        public string profileName="Custom";
        [Range(4,64)] public int gridSize=8;
        [Min(2)] public int minPathLength=2;
        [Min(2)] public int maxPathLength=14;
        [Min(1)] public int minPathCount=14;
        [Tooltip("Hard upper limit on paths; generation fails if the other bounds cannot fit within this count.")]
        [Min(1)] public int maxPathCount=30000;
        [Min(1)] public int minCrossFacePaths=8;
        [Range(0,1)] public float minimumCoverage=.60f;
        [Range(0,1)] public float targetCoverage=.78f;
        [Range(0,1)] public float minimumBlockedFraction=.20f;
        [Min(0)] public float minimumAverageTurns=1;
        [Range(0,1)] public float turnProbability=.55f;
        [Tooltip("Gate budget at an 8x8 grid when scaling is enabled; otherwise an absolute limit. Edge-only puzzles do not use gates.")]
        [Min(1)] public int maxGates=48;
        [Tooltip("Increase the gate budget with surface area above 8x8. Disable to enforce Max Gates as an absolute cap.")]
        public bool scaleGateBudgetWithGridSize=true;
        [Header("Blockers and starting moves (works without measured search)")]
        [Tooltip("Adjust physical blockers and enforce the starting-free-arrow limit without requiring measured difficulty search.")]
        public bool controlDependencies=false;
        [Tooltip("Higher values favor more distinct blockers per blocked arrow. Zero targets one; one targets five. This is a soft density target.")]
        [Range(0,1)] public float dependencyAmount=.75f;
        [Tooltip("Hard maximum of immediately removable arrows when dependency controls are enabled. At least one opening always remains.")]
        [Min(1)] public int maximumStartingFreeArrows=3;
        [Header("Measured difficulty")]
        [Tooltip("Analyze several valid candidates and select the best measured match. Disable for basic packing without difficulty search.")]
        public bool advancedDifficulty=true;
        public DifficultySettings difficultySettings=new DifficultySettings();

        public int EffectiveMaxGates => scaleGateBudgetWithGridSize && gridSize>8
            ? (int)System.Math.Min(int.MaxValue,System.Math.Ceiling(maxGates*(double)gridSize*gridSize/64)) : maxGates;

        public DifficultyProfile ToProfile() => new DifficultyProfile(profileName,gridSize,minPathLength,maxPathLength,
            minPathCount,minCrossFacePaths,minimumCoverage,targetCoverage,minimumBlockedFraction,minimumAverageTurns,turnProbability,EffectiveMaxGates,maxPathCount);
        public DependencyControlSettings ToDependencyControls() => controlDependencies
            ? new DependencyControlSettings { Amount=dependencyAmount,MaximumStartingFreeArrows=maximumStartingFreeArrows } : null;

        public void ApplyMasterDifficulty(float score)
        {
            var profile=DifficultyPresets.ProfileForMaster(score);
            profileName=profile.Name; gridSize=profile.GridSize; minPathLength=profile.MinPathLength; maxPathLength=profile.MaxPathLength;
            minPathCount=profile.MinPathCount; maxPathCount=profile.MaxPathCount; minCrossFacePaths=profile.MinCrossFacePaths;
            minimumCoverage=(float)profile.MinimumCoverage; targetCoverage=(float)profile.TargetCoverage;
            minimumBlockedFraction=(float)profile.MinimumBlockedFraction; minimumAverageTurns=(float)profile.MinimumAverageTurns;
            turnProbability=(float)profile.TurnProbability; maxGates=profile.MaxGates; scaleGateBudgetWithGridSize=false;
            advancedDifficulty=true; difficultySettings=DifficultyPresets.SettingsForMaster(score);
            controlDependencies=false; dependencyAmount=.75f; maximumStartingFreeArrows=3;
        }
        public void ApplyPreset(int index)
        {
            if(index<0 || index>=DifficultyPresets.All.Count) throw new System.ArgumentOutOfRangeException(nameof(index));
            ApplyMasterDifficulty((float)DifficultyPresets.All[index].Target); profileName=DifficultyPresets.All[index].Name;
        }
    }
}
