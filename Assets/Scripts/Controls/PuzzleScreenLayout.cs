using UnityEngine;

namespace ArrowCube.Controls
{
    public readonly struct PuzzleScreenLayout
    {
        public Rect Panel { get; }
        public Rect View { get; }
        // Both rectangles use screen-pixel coordinates with a bottom-left origin, like Input System pointers.
        private PuzzleScreenLayout(Rect panel,Rect view) { Panel=panel; View=view; }
        public static PuzzleScreenLayout Calculate(Rect safeArea,float uiScale=1)
        {
            uiScale=Mathf.Clamp(uiScale,1,2.5f);
            float margin=Mathf.Min(12*uiScale,safeArea.width*.02f,safeArea.height*.02f);
            if(safeArea.width>=600*uiScale && safeArea.width>=safeArea.height*1.1f)
            {
                float panel=Mathf.Clamp(safeArea.width*.28f,220*uiScale,360*uiScale);
                return new PuzzleScreenLayout(new Rect(safeArea.x+margin,safeArea.y+margin,panel,safeArea.height-2*margin),
                    new Rect(safeArea.x+panel+2*margin,safeArea.y+margin,safeArea.width-panel-3*margin,safeArea.height-2*margin));
            }
            float height=Mathf.Min(Mathf.Clamp(safeArea.height*.28f,160*uiScale,280*uiScale),safeArea.height*.45f);
            return new PuzzleScreenLayout(new Rect(safeArea.x+margin,safeArea.y+margin,safeArea.width-2*margin,height),
                new Rect(safeArea.x+margin,safeArea.y+height+2*margin,safeArea.width-2*margin,safeArea.height-height-3*margin));
        }
    }
}
