using ArrowCube.Gameplay;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using UnityEngine;

namespace ArrowCube.Game
{
    [CreateAssetMenu(menuName="Arrow Cube/Gameplay Settings",fileName="GameplaySettings")]
    public sealed class GameplaySettingsAsset : ScriptableObject
    {
        public GameplayOptions defaults=new GameplayOptions();
        public int baseSeed=1729;
        public bool cubeEdgeExits=true,directionalGates=false;
        [Tooltip("Optional campaign profile override. Leave empty to use the five progression bands. The lab can also launch its exact puzzle in the game scene.")]
        public DifficultyProfileAsset profileOverride;
        [Tooltip("Used when there is no profile override. With an override, that asset supplies its independent measured-search and dependency settings.")]
        public bool measuredDifficultySearch=false;
        public bool controlDependencies=true;
        [Range(0,1)] public float dependencyAmount=.75f;
        [Min(1)] public int maximumStartingFreeArrows=3;
        public PuzzleRecipe RecipeFor(int level,int campaignSeed)
        {
            if(level<1 || level>999999) throw new System.ArgumentOutOfRangeException(nameof(level));
            var preset=DifficultyPresets.ForLevel(level);
            var profile=profileOverride==null ? preset.CreateProfile() : profileOverride.ToProfile();
            var difficulty=profileOverride==null ? measuredDifficultySearch ? preset.CreateSettings() : null
                : profileOverride.advancedDifficulty ? profileOverride.difficultySettings?.Copy()??new DifficultySettings() : null;
            var controls=profileOverride==null ? controlDependencies ? new DependencyControlSettings { Amount=dependencyAmount,MaximumStartingFreeArrows=maximumStartingFreeArrows } : null
                : profileOverride.ToDependencyControls();
            int seed=unchecked(campaignSeed+level*104729);
            return new PuzzleRecipe(seed,profile,new ExitRules(cubeEdgeExits,directionalGates),maxRestarts:difficulty?.MaxRestarts??8,difficulty:difficulty,dependencyControls:controls);
        }
    }
}
