using System;
using System.Collections.Generic;
using ArrowCube.Movement;

namespace ArrowCube.Gameplay
{
    public enum PlayState { Playing, Paused, Failed, Victory }
    [Serializable]
    public sealed class GameplayOptions
    {
        public bool UnlimitedAttempts=true, UnlimitedPowerUps=true;
        public int StartingHearts=3, Hints=3, Hammers=3, Undos=3;
        public GameplayOptions Copy() => (GameplayOptions)MemberwiseClone();
        public void Validate()
        {
            if(StartingHearts<1 || StartingHearts>99 || Hints<0 || Hints>999 || Hammers<0 || Hammers>999 || Undos<0 || Undos>999)
                throw new ArgumentOutOfRangeException(nameof(StartingHearts));
        }
    }
    [Serializable]
    public sealed class RemovalSave { public int PathId; public bool Hammer,SpentHammer; }

    /// <summary>The renderer and session share a single occupancy grid. An attempt is
    /// reserved before animation; hearts/history change exactly once after it finishes.</summary>
    public sealed class GameplaySession
    {
        private readonly PathMovementSimulator simulator=new PathMovementSimulator();
        private readonly List<RemovalSave> history=new List<RemovalSave>();
        private MovementPlan pending;
        private long pendingRevision;
        private bool paused;
        public PuzzleDefinition Puzzle { get; }
        public SurfaceGrid Grid { get; private set; }
        public GameplayOptions Options { get; private set; }
        public int Hearts { get; private set; }
        public int Mistakes { get; private set; }
        public int HintUses { get; private set; }
        public int HammerUses { get; private set; }
        public int UndoUses { get; private set; }
        public int HintsLeft { get; private set; }
        public int HammersLeft { get; private set; }
        public int UndosLeft { get; private set; }
        public double Seconds { get; private set; }
        public bool HasPendingAttempt => pending!=null;
        public PlayState State => Grid.Paths.Count==0 ? PlayState.Victory : !Options.UnlimitedAttempts && Hearts==0 ? PlayState.Failed
            : paused ? PlayState.Paused : PlayState.Playing;
        public bool CanPlay => State==PlayState.Playing && pending==null;
        public bool CanUndo => pending==null && history.Count>0 && (State==PlayState.Playing || State==PlayState.Victory)
            && (Options.UnlimitedPowerUps || UndosLeft>0);
        public int Stars => State!=PlayState.Victory ? 0 : Mistakes==0 && HintUses+HammerUses+UndoUses==0 ? 3 : Mistakes<=2 && HammerUses==0 ? 2 : 1;
        public GameplaySession(PuzzleDefinition puzzle,GameplayOptions options=null)
        {
            Puzzle=puzzle ?? throw new ArgumentNullException(nameof(puzzle));
            Options=(options??new GameplayOptions()).Copy(); Options.Validate(); Restart();
        }
        public MovementPlan BeginAttempt(int id)
        {
            if(!CanPlay || !Grid.Paths.ContainsKey(id)) return null;
            pending=simulator.Simulate(Grid,id,Puzzle.Exits,Puzzle.Rules); pendingRevision=Grid.Revision; return pending;
        }
        public bool CompleteAttempt(MovementPlan plan,bool rendererCommitted=false)
        {
            if(plan==null || !ReferenceEquals(plan,pending)) return false;
            if(plan.Succeeded)
            {
                bool accepted=rendererCommitted ? Grid.Revision==pendingRevision+1 && !Grid.Paths.ContainsKey(plan.Path.Id)
                    : Grid.TryCommit(plan);
                pending=null;
                if(!accepted) return false;
                history.Add(new RemovalSave { PathId=plan.Path.Id });
            }
            else
            {
                if(Grid.Revision!=pendingRevision) { pending=null; return false; }
                pending=null; Mistakes++;
                if(!Options.UnlimitedAttempts) Hearts=Math.Max(0,Hearts-1);
            }
            return true;
        }
        public int? Hint()
        {
            if(!CanPlay || !Options.UnlimitedPowerUps && HintsLeft==0) return null;
            foreach(var path in Puzzle.Paths)
                if(Grid.Paths.ContainsKey(path.Id) && simulator.Simulate(Grid,path.Id,Puzzle.Exits,Puzzle.Rules).Succeeded)
                { HintUses++; if(!Options.UnlimitedPowerUps) HintsLeft--; return path.Id; }
            return null;
        }
        public bool Hammer(int id)
        {
            if(!CanPlay || !Options.UnlimitedPowerUps && HammersLeft==0 || !Grid.RemovePath(id)) return false;
            bool spent=!Options.UnlimitedPowerUps; if(spent) HammersLeft--;
            history.Add(new RemovalSave { PathId=id,Hammer=true,SpentHammer=spent }); HammerUses++; return true;
        }
        public bool Undo()
        {
            if(!CanUndo) return false;
            var last=history[history.Count-1];
            foreach(var path in Puzzle.Paths) if(path.Id==last.PathId) { Grid.AddPath(path); break; }
            history.RemoveAt(history.Count-1);
            if(last.SpentHammer) HammersLeft++;
            if(!Options.UnlimitedPowerUps) UndosLeft--;
            UndoUses++; return true;
        }
        public void Configure(GameplayOptions options)
        {
            if(options==null) throw new ArgumentNullException(nameof(options));
            options.Validate(); Options=options.Copy();
        }
        public void Pause(bool value) { paused=value; }
        public void Tick(double seconds) { if(State==PlayState.Playing && seconds>0 && !double.IsNaN(seconds) && !double.IsInfinity(seconds)) Seconds+=seconds; }
        public void Restart()
        {
            pending=null; paused=false; Grid=Puzzle.CreateGrid(); Hearts=Options.StartingHearts;
            Mistakes=HintUses=HammerUses=UndoUses=0; Seconds=0; history.Clear();
            HintsLeft=Options.Hints; HammersLeft=Options.Hammers; UndosLeft=Options.Undos;
        }
        public SessionSave Capture()
        {
            var result=new SessionSave { Hearts=Hearts,Mistakes=Mistakes,HintUses=HintUses,HammerUses=HammerUses,UndoUses=UndoUses,
                HintsLeft=HintsLeft,HammersLeft=HammersLeft,UndosLeft=UndosLeft,Seconds=Seconds,Options=Options.Copy() };
            foreach(var item in history) result.History.Add(new RemovalSave { PathId=item.PathId,Hammer=item.Hammer,SpentHammer=item.SpentHammer });
            return result;
        }
        public static GameplaySession Restore(PuzzleDefinition puzzle,SessionSave saved)
        {
            if(saved==null || saved.Options==null || saved.History==null) throw new ArgumentException("Incomplete session save.");
            var session=new GameplaySession(puzzle,saved.Options);
            if(saved.Hearts<0 || saved.Hearts>99 || saved.Mistakes<0 || saved.HintUses<0 || saved.HammerUses<0 || saved.UndoUses<0
                || saved.HintsLeft<0 || saved.HintsLeft>999 || saved.HammersLeft<0 || saved.HammersLeft>999 || saved.UndosLeft<0 || saved.UndosLeft>999
                || saved.Seconds<0 || double.IsNaN(saved.Seconds) || double.IsInfinity(saved.Seconds)) throw new ArgumentException("Invalid session counters.");
            foreach(var item in saved.History)
            {
                if(item==null || !session.Grid.Paths.ContainsKey(item.PathId) || item.SpentHammer && !item.Hammer)
                    throw new ArgumentException("Invalid removal history.");
                if(item.Hammer) session.Grid.RemovePath(item.PathId);
                else if(!session.Grid.TryCommit(session.simulator.Simulate(session.Grid,item.PathId,puzzle.Exits,puzzle.Rules)))
                    throw new ArgumentException("Saved normal removal is blocked.");
                session.history.Add(new RemovalSave { PathId=item.PathId,Hammer=item.Hammer,SpentHammer=item.SpentHammer });
            }
            session.Hearts=saved.Hearts; session.Mistakes=saved.Mistakes; session.HintUses=saved.HintUses; session.HammerUses=saved.HammerUses;
            session.UndoUses=saved.UndoUses; session.HintsLeft=saved.HintsLeft; session.HammersLeft=saved.HammersLeft; session.UndosLeft=saved.UndosLeft; session.Seconds=saved.Seconds;
            return session;
        }
    }
    [Serializable]
    public sealed class SessionSave
    {
        public GameplayOptions Options;
        public int Hearts,Mistakes,HintUses,HammerUses,UndoUses,HintsLeft,HammersLeft,UndosLeft;
        public double Seconds;
        public List<RemovalSave> History=new List<RemovalSave>();
    }
}
