using System;
using System.Collections.Generic;
using ArrowCube.Movement;
using ArrowCube.Topology;
using UnityEngine;

namespace ArrowCube.Presentation
{
    public sealed class SurfaceDemoDefinition
    {
        public PuzzleDefinition Puzzle { get; }
        public ISurfaceGeometry Geometry { get; }
        public SurfaceDemoDefinition(PuzzleDefinition puzzle,ISurfaceGeometry geometry) { Puzzle=puzzle; Geometry=geometry; }
    }

    /// <summary>Two fixed, explicitly connected solid fixtures. No mesh-to-topology extraction.</summary>
    public static class AuthoredSurfaceDemos
    {
        public const string LName="Surface demo: L-shaped solid";
        public const string FoldName="Surface demo: oblique folded prism";
        private static readonly Color front=new Color(.72f,.84f,.98f),back=new Color(.94f,.72f,.8f);
        public static SurfaceDemoDefinition CreateL(ExitRules rules=null) => Build(false,rules);
        public static SurfaceDemoDefinition CreateFolded(ExitRules rules=null) => Build(true,rules);
        public static ISurfaceGeometry Resolve(PuzzleDefinition puzzle)
        {
            if(puzzle.Surface is CubeTopology cube) return new CubeSurfaceGeometry(cube,4);
            bool folded=puzzle.Name==FoldName;
            if(!folded && puzzle.Name!=LName) throw new ArgumentException("Supply physical geometry for this authored surface.");
            return new QuadSurfaceGeometry(puzzle.Surface,Quads(folded));
        }
        private static SurfaceDemoDefinition Build(bool folded,ExitRules rules)
        {
            var quads=Quads(folded); var builder=new SurfaceTopologyBuilder();
            foreach(int id in quads.Keys) builder.AddCell(id);
            int count=folded ? 6 : 8;
            for(int i=0;i<count;i++)
            {
                bool flat=folded ? i%2==0 : i==0 || i==6;
                bool concave=!folded && i==3;
                builder.Connect(100+i,SurfaceDirection.Right,100+(i+1)%count,SurfaceDirection.Right,!flat,!flat && !concave);
            }
            if(!folded)
            {
                Link(200,SurfaceDirection.Right,201,SurfaceDirection.Right,false); Link(200,SurfaceDirection.Up,202,SurfaceDirection.Up,false);
                Link(300,SurfaceDirection.Left,301,SurfaceDirection.Left,false); Link(300,SurfaceDirection.Up,302,SurfaceDirection.Up,false);
                // Explicit cap-to-side connections. Side Up is the front, Down is the back.
                Front(200,SurfaceDirection.Down,100); Front(200,SurfaceDirection.Left,107);
                Front(201,SurfaceDirection.Down,101); Front(201,SurfaceDirection.Right,102); Front(201,SurfaceDirection.Up,103);
                Front(202,SurfaceDirection.Right,104); Front(202,SurfaceDirection.Up,105); Front(202,SurfaceDirection.Left,106);
                Back(300,SurfaceDirection.Down,100); Back(300,SurfaceDirection.Right,107);
                Back(301,SurfaceDirection.Down,101); Back(301,SurfaceDirection.Left,102); Back(301,SurfaceDirection.Up,103);
                Back(302,SurfaceDirection.Left,104); Back(302,SurfaceDirection.Up,105); Back(302,SurfaceDirection.Right,106);
            }
            else
            {
                Link(200,SurfaceDirection.Right,201,SurfaceDirection.Down,false); Link(200,SurfaceDirection.Up,202,SurfaceDirection.Left,false);
                Link(201,SurfaceDirection.Right,202,SurfaceDirection.Down,false);
                Link(300,SurfaceDirection.Right,302,SurfaceDirection.Down,false); Link(300,SurfaceDirection.Up,301,SurfaceDirection.Left,false);
                Link(301,SurfaceDirection.Up,302,SurfaceDirection.Left,false);
                Front(200,SurfaceDirection.Down,100); Front(200,SurfaceDirection.Left,105);
                Front(201,SurfaceDirection.Down,102); Front(201,SurfaceDirection.Left,101);
                Front(202,SurfaceDirection.Down,104); Front(202,SurfaceDirection.Left,103);
                Back(300,SurfaceDirection.Down,105); Back(300,SurfaceDirection.Left,100);
                Back(301,SurfaceDirection.Down,101); Back(301,SurfaceDirection.Left,102);
                Back(302,SurfaceDirection.Down,103); Back(302,SurfaceDirection.Left,104);
            }
            var surface=builder.Build();
            var paths=folded ? new[] {
                new ArrowPath(10,surface,new[] {101,100},SurfaceDirection.Right),
                new ArrowPath(20,surface,new[] {201,102},SurfaceDirection.Up),
                new ArrowPath(30,surface,new[] {300,302},SurfaceDirection.Left)
            } : new[] {
                new ArrowPath(10,surface,new[] {103,102,101},SurfaceDirection.Right),
                new ArrowPath(20,surface,new[] {202,104},SurfaceDirection.Left),
                new ArrowPath(30,surface,new[] {300,301},SurfaceDirection.Right)
            };
            var gates=folded ? new[] {new CellGate(104,SurfaceDirection.Right),new CellGate(201,SurfaceDirection.Up),new CellGate(300,SurfaceDirection.Left)}
                : new[] {new CellGate(106,SurfaceDirection.Right),new CellGate(202,SurfaceDirection.Left),new CellGate(300,SurfaceDirection.Right)};
            var puzzle=new PuzzleDefinition(folded ? FoldName : LName,surface,paths,new ExitMap(surface,gates),rules??ExitRules.GateOnly);
            return new SurfaceDemoDefinition(puzzle,new QuadSurfaceGeometry(surface,quads));

            void Link(int a,SurfaceDirection d,int b,SurfaceDirection heading,bool edge) => builder.Connect(a,d,b,heading,edge,edge);
            void Front(int cap,SurfaceDirection d,int side) => Link(cap,d,side,SurfaceDirection.Down,true);
            void Back(int cap,SurfaceDirection d,int side) => Link(cap,d,side,SurfaceDirection.Up,true);
        }
        private static Dictionary<int,SurfaceQuad> Quads(bool folded)
        {
            var result=new Dictionary<int,SurfaceQuad>();
            var polygon=folded ? new[] {new Vector2(-2,-1),new Vector2(0,-1),new Vector2(2,-1),new Vector2(.6f,.3f),new Vector2(-.8f,1.6f),new Vector2(-1.4f,.3f)}
                : new[] {new Vector2(-2,-2),new Vector2(0,-2),new Vector2(2,-2),new Vector2(2,0),new Vector2(0,0),new Vector2(0,2),new Vector2(-2,2),new Vector2(-2,0)};
            float half=folded ? 1.2f : 1.5f;
            var rotation=folded ? Quaternion.Euler(17,11,23) : Quaternion.identity;
            Vector3 Point(Vector2 p,float z) => rotation*new Vector3(p.x,p.y,z);
            for(int i=0;i<polygon.Length;i++)
            {
                var a=polygon[i]; var b=polygon[(i+1)%polygon.Length];
                var color=i%3==0 ? new Color(.62f,.83f,.72f) : i%3==1 ? new Color(1,.9f,.64f) : new Color(.82f,.7f,.95f);
                result.Add(100+i,new SurfaceQuad(Point(a,-half),Point(b,-half),Point(b,half),Point(a,half),color));
            }
            if(!folded)
            {
                RectCap(200,300,new Vector2(-2,-2),new Vector2(0,0));
                RectCap(201,301,new Vector2(0,-2),new Vector2(2,0));
                RectCap(202,302,new Vector2(-2,0),new Vector2(0,2));
            }
            else
            {
                var center=new Vector2(-.8f/3,(-1-1+1.6f)/3);
                for(int i=0;i<3;i++)
                {
                    var p=polygon[i*2]; var next=polygon[(i*2+1)%6]; var previous=polygon[(i*2+5)%6];
                    result.Add(200+i,new SurfaceQuad(Point(p,half),Point(next,half),Point(center,half),Point(previous,half),front));
                    result.Add(300+i,new SurfaceQuad(Point(p,-half),Point(previous,-half),Point(center,-half),Point(next,-half),back));
                }
            }
            return result;
            void RectCap(int f,int b,Vector2 min,Vector2 max)
            {
                var br=new Vector2(max.x,min.y); var tl=new Vector2(min.x,max.y);
                result.Add(f,new SurfaceQuad(Point(min,half),Point(br,half),Point(max,half),Point(tl,half),front));
                result.Add(b,new SurfaceQuad(Point(br,-half),Point(min,-half),Point(tl,-half),Point(max,-half),back));
            }
        }
    }
}
