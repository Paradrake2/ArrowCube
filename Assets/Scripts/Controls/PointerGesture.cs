using UnityEngine;

namespace ArrowCube.Controls
{
    /// <summary>Device-independent press/drag recognition. A cancelled tap never becomes a tap again.</summary>
    public sealed class PointerGesture
    {
        public bool Active { get; private set; }
        public bool Dragging { get; private set; }
        public bool CanOrbit { get; private set; }
        public int CandidatePath { get; private set; }=-1;
        private Vector2 start,last;
        private float threshold;
        private double began;
        private bool tapCancelled;

        public void Begin(Vector2 position,double time,float dragThreshold,int candidatePath,bool mayOrbit,bool blocked)
        {
            Active=true; Dragging=false; CanOrbit=mayOrbit && !blocked; CandidatePath=candidatePath;
            start=last=position; threshold=Mathf.Max(1,dragThreshold); began=time; tapCancelled=blocked;
        }

        public Vector2 Move(Vector2 position)
        {
            if(!Active) return Vector2.zero;
            var delta=position-last; last=position;
            if(!Dragging && (position-start).sqrMagnitude>=threshold*threshold)
            { Dragging=true; tapCancelled=true; return CanOrbit ? position-start : Vector2.zero; }
            return Dragging && CanOrbit ? delta : Vector2.zero;
        }

        public int End(Vector2 position,double time,bool blocked,int releasedPath)
        {
            if(!Active) return -1;
            Move(position); // A release may contain the final motion even if no move event was delivered.
            int result=!tapCancelled && !Dragging && !blocked && time-began<=.6
                && time>=began && CandidatePath>=0 && releasedPath==CandidatePath ? CandidatePath : -1;
            Cancel(); return result;
        }
        public void SuppressTap() { tapCancelled=true; }
        public void Cancel() { Active=false; Dragging=false; CanOrbit=false; CandidatePath=-1; tapCancelled=true; }
    }
}
