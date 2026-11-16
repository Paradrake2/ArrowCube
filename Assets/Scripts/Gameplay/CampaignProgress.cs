using System;
using System.Collections.Generic;

namespace ArrowCube.Gameplay
{
    [Serializable]
    public sealed class LevelRecord
    {
        public int Level,BestStars,BestMistakes,Completions;
        public double BestSeconds;
        public RecipeSave Recipe;
        public string Fingerprint;
    }
    [Serializable]
    public sealed class CampaignProgress
    {
        public int HighestUnlocked=1;
        public List<LevelRecord> Records=new List<LevelRecord>();
        public bool IsUnlocked(int level) => level>=1 && level<=HighestUnlocked;
        public LevelRecord Find(int level) => Records.Find(record=>record.Level==level);
        public void Complete(int level,GameplaySession session)
        {
            if(!IsUnlocked(level) || session.State!=PlayState.Victory) throw new InvalidOperationException("Only an unlocked, completed level can advance progression.");
            var record=Find(level);
            if(record==null) { record=new LevelRecord { Level=level,BestMistakes=session.Mistakes,BestSeconds=session.Seconds }; Records.Add(record); }
            record.Completions++; record.BestStars=Math.Max(record.BestStars,session.Stars);
            record.BestMistakes=Math.Min(record.BestMistakes,session.Mistakes); record.BestSeconds=Math.Min(record.BestSeconds,session.Seconds);
            HighestUnlocked=Math.Max(HighestUnlocked,level+1);
        }
        public void Validate()
        {
            if(HighestUnlocked<1 || HighestUnlocked>1000000 || Records==null || Records.Count>1000000) throw new ArgumentException("Invalid campaign progression.");
            var seen=new HashSet<int>();
            foreach(var item in Records)
                if(item==null || item.Level<1 || item.Level>=HighestUnlocked || !seen.Add(item.Level) || item.BestStars<1 || item.BestStars>3
                    || item.Completions<1 || item.BestMistakes<0 || item.BestSeconds<0 || double.IsNaN(item.BestSeconds) || double.IsInfinity(item.BestSeconds))
                    throw new ArgumentException("Invalid completed-level record.");
        }
    }
    [Serializable]
    public sealed class PlayerSave
    {
        public int Schema=1,BaseSeed=1729;
        public GameplayOptions Options=new GameplayOptions();
        public CampaignProgress Progress=new CampaignProgress();
        public ActiveLevelSave Active;
        public bool HasActive;
        public float Sensitivity=1,ZoomSpeed=.35f,ArrowThickness=.35f;
        public bool InvertMouseRotation,InvertTouchRotation;
        public void Validate()
        {
            if(Schema!=1 || Options==null || Progress==null) throw new ArgumentException("Unsupported or incomplete save.");
            Options.Validate(); Progress.Validate();
            if(float.IsNaN(Sensitivity) || Sensitivity<.1f || Sensitivity>3 || float.IsNaN(ZoomSpeed) || ZoomSpeed<.05f || ZoomSpeed>2
                || float.IsNaN(ArrowThickness) || ArrowThickness<.05f || ArrowThickness>.45f) throw new ArgumentException("Invalid saved controls.");
            if(Active!=null && !Active.Sandbox && !Progress.IsUnlocked(Active.Level)) throw new ArgumentException("Active level is locked.");
        }
    }
}
