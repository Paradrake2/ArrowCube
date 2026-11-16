using System;
using System.Collections.Generic;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Topology;
using UnityEngine;

namespace ArrowCube.Presentation
{
    public enum AuthoredGenerationShape { L, ObliquePrism, T }
    public sealed class AuthoredGenerationSurface
    {
        public SurfaceShapeDefinition Shape { get; }
        public ISurfaceGeometry Geometry { get; }
        internal AuthoredGenerationSurface(SurfaceShapeDefinition shape,ISurfaceGeometry geometry) { Shape=shape; Geometry=geometry; }
    }
    /// <summary>Fixed authored shapes, with regular refinement of their existing quad cells.
    /// Refinement follows explicit parent boundary mappings; it neither builds new shapes
    /// nor infers connectivity from polygon positions.</summary>
    public static class AuthoredGenerationSurfaces
    {
        public static AuthoredGenerationSurface Create(AuthoredGenerationShape shape,int subdivisions=4)
        {
            if(subdivisions<1 || subdivisions>12) throw new ArgumentOutOfRangeException(nameof(subdivisions));
            var source=shape==AuthoredGenerationShape.L ? AuthoredSurfaceDemos.CreateL().Geometry
                : shape==AuthoredGenerationShape.ObliquePrism ? AuthoredSurfaceDemos.CreateFolded().Geometry : CreateT();
            string id="arrowcube/authored/"+(shape==AuthoredGenerationShape.L ? "l" : shape==AuthoredGenerationShape.T ? "t" : "prism")+"/"+subdivisions;
            int n=subdivisions; var builder=new SurfaceTopologyBuilder(); var quads=new Dictionary<int,SurfaceQuad>();
            int Id(int parent,int x,int y) => checked(parent*n*n+y*n+x);
            foreach(int parent in source.Surface.EnumerateCellIds())
            for(int y=0;y<n;y++) for(int x=0;x<n;x++)
            {
                int cell=Id(parent,x,y); builder.AddCell(cell); var q=source.GetCell(parent);
                Vector3 Point(float u,float v) => Vector3.Lerp(Vector3.Lerp(q.Corner(0),q.Corner(1),u),Vector3.Lerp(q.Corner(3),q.Corner(2),u),v);
                quads.Add(cell,new SurfaceQuad(Point(x/(float)n,y/(float)n),Point((x+1f)/n,y/(float)n),Point((x+1f)/n,(y+1f)/n),Point(x/(float)n,(y+1f)/n),q.Color));
            }
            foreach(int parent in source.Surface.EnumerateCellIds())
            for(int y=0;y<n;y++) for(int x=0;x<n;x++) for(int d=0;d<4;d++)
            {
                var direction=(SurfaceDirection)d; int nx=x+(d==0 ? 1 : d==2 ? -1 : 0),ny=y+(d==1 ? 1 : d==3 ? -1 : 0);
                CellBoundary link;
                if(nx>=0 && nx<n && ny>=0 && ny<n) link=new CellBoundary(Id(parent,nx,ny),direction);
                else
                {
                    var boundary=source.Surface.GetBoundary(parent,direction);
                    int index=d==0 ? y : d==1 ? n-1-x : d==2 ? n-1-y : x;
                    index=n-1-index; var entry=boundary.NewDirection.Opposite();
                    nx=entry==SurfaceDirection.Left ? 0 : entry==SurfaceDirection.Right ? n-1 : entry==SurfaceDirection.Down ? index : n-1-index;
                    ny=entry==SurfaceDirection.Down ? 0 : entry==SurfaceDirection.Up ? n-1 : entry==SurfaceDirection.Right ? index : n-1-index;
                    link=new CellBoundary(Id(boundary.NeighborCellId.Value,nx,ny),boundary.NewDirection,boundary.IsGeometricEdge,boundary.AllowsEdgeExit);
                }
                builder.SetBoundary(Id(parent,x,y),direction,link);
            }
            var definition=new SurfaceShapeDefinition(id,1,builder.Build());
            return new AuthoredGenerationSurface(definition,new QuadSurfaceGeometry(definition.Topology,quads));
        }
        public static ISurfaceGeometry Resolve(PuzzleDefinition puzzle)
        {
            if(puzzle.SurfaceAssetVersion!=1) throw new ArgumentException("Unsupported authored geometry version.");
            string[] parts=(puzzle.SurfaceAssetId??"").Split('/');
            if(parts.Length!=4 || parts[0]!="arrowcube" || parts[1]!="authored" || !int.TryParse(parts[3],out int n)) throw new ArgumentException("Unknown surface asset ID.");
            var kind=parts[2]=="l" ? AuthoredGenerationShape.L : parts[2]=="prism" ? AuthoredGenerationShape.ObliquePrism : parts[2]=="t" ? AuthoredGenerationShape.T : throw new ArgumentException("Unknown authored shape.");
            var authored=Create(kind,n);
            if(new SurfaceShapeDefinition(puzzle.SurfaceAssetId,puzzle.SurfaceAssetVersion,puzzle.Surface).Digest!=authored.Shape.Digest)
                throw new ArgumentException("Saved topology does not match its registered geometry asset.");
            var quads=new Dictionary<int,SurfaceQuad>(); foreach(int c in puzzle.Surface.EnumerateCellIds()) quads.Add(c,authored.Geometry.GetCell(c));
            return new QuadSurfaceGeometry(puzzle.Surface,quads);
        }
        public static DifficultyProfile Profile(int cellCount,double coverage=.65,int maxLength=12) =>
            new DifficultyProfile("Authored surface",4,2,maxLength,Math.Max(4,cellCount/40),3,Math.Min(.40,coverage),coverage,.15,.25,.55,Math.Max(8,cellCount/5),surfaceCellCount:cellCount);

        private static ISurfaceGeometry CreateT()
        {
            // Ten boundary segments and four rectangular cap tiles, all authored explicitly.
            var polygon=new[] {new Vector2(-1,-2),new Vector2(1,-2),new Vector2(1,0),new Vector2(3,0),new Vector2(3,2),new Vector2(1,2),new Vector2(-1,2),new Vector2(-3,2),new Vector2(-3,0),new Vector2(-1,0)};
            var builder=new SurfaceTopologyBuilder(); var quads=new Dictionary<int,SurfaceQuad>();
            Vector3 P(Vector2 p,float z) => new Vector3(p.x,p.y,z);
            for(int i=0;i<10;i++)
            {
                int next=(i+1)%10; builder.AddCell(100+i);
                quads.Add(100+i,new SurfaceQuad(P(polygon[i],-1),P(polygon[next],-1),P(polygon[next],1),P(polygon[i],1),i%2==0 ? new Color(.68f,.85f,.72f) : new Color(.96f,.85f,.62f)));
            }
            for(int i=0;i<10;i++) { bool flat=i==4 || i==5,concave=i==1 || i==8; builder.Connect(100+i,SurfaceDirection.Right,100+(i+1)%10,SurfaceDirection.Right,!flat,!flat&&!concave); }
            Cap(0,new Vector2(-1,-2),new Vector2(1,0)); Cap(1,new Vector2(-1,0),new Vector2(1,2));
            Cap(2,new Vector2(1,0),new Vector2(3,2)); Cap(3,new Vector2(-3,0),new Vector2(-1,2));
            builder.Connect(200,SurfaceDirection.Up,201,SurfaceDirection.Up); builder.Connect(201,SurfaceDirection.Right,202,SurfaceDirection.Right); builder.Connect(201,SurfaceDirection.Left,203,SurfaceDirection.Left);
            builder.Connect(300,SurfaceDirection.Up,301,SurfaceDirection.Up); builder.Connect(301,SurfaceDirection.Left,302,SurfaceDirection.Left); builder.Connect(301,SurfaceDirection.Right,303,SurfaceDirection.Right);
            F(200,SurfaceDirection.Down,100); F(200,SurfaceDirection.Right,101); F(200,SurfaceDirection.Left,109);
            F(201,SurfaceDirection.Up,105); F(202,SurfaceDirection.Down,102); F(202,SurfaceDirection.Right,103); F(202,SurfaceDirection.Up,104);
            F(203,SurfaceDirection.Down,108); F(203,SurfaceDirection.Left,107); F(203,SurfaceDirection.Up,106);
            B(300,SurfaceDirection.Down,100); B(300,SurfaceDirection.Left,101); B(300,SurfaceDirection.Right,109);
            B(301,SurfaceDirection.Up,105); B(302,SurfaceDirection.Down,102); B(302,SurfaceDirection.Left,103); B(302,SurfaceDirection.Up,104);
            B(303,SurfaceDirection.Down,108); B(303,SurfaceDirection.Right,107); B(303,SurfaceDirection.Up,106);
            return new QuadSurfaceGeometry(builder.Build(),quads);
            void Cap(int i,Vector2 min,Vector2 max)
            {
                int f=200+i,b=300+i; builder.AddCell(f); builder.AddCell(b); var br=new Vector2(max.x,min.y); var tl=new Vector2(min.x,max.y);
                quads.Add(f,new SurfaceQuad(P(min,1),P(br,1),P(max,1),P(tl,1),new Color(.72f,.84f,.98f)));
                quads.Add(b,new SurfaceQuad(P(br,-1),P(min,-1),P(tl,-1),P(max,-1),new Color(.94f,.72f,.8f)));
            }
            void F(int cap,SurfaceDirection direction,int side) => builder.Connect(cap,direction,side,SurfaceDirection.Down,true,true);
            void B(int cap,SurfaceDirection direction,int side) => builder.Connect(cap,direction,side,SurfaceDirection.Up,true,true);
        }
    }
}
