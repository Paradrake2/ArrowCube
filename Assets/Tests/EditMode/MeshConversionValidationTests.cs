using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArrowCube.Generation;
using ArrowCube.Gameplay;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace ArrowCube.Tests
{
    public class MeshConversionValidationTests
    {
        private readonly List<Object> owned=new List<Object>();
        private T Own<T>(T obj) where T:Object { owned.Add(obj); return obj; }
        private Mesh Cube(bool triangles=false) => Own(MeshConversionFixtures.Create(MeshConversionFixture.RectangularPrism,triangles));
        [TearDown] public void Cleanup() { foreach(var obj in owned) if(obj!=null) Object.DestroyImmediate(obj); owned.Clear(); }
        private static void Error(string code,TestDelegate operation)
        { var error=Assert.Throws<MeshConversionException>(operation); Assert.That(error.Code,Is.EqualTo(code),error.Message); Assert.That(error.Message.Length,Is.GreaterThan(20)); }
        [Test] public void OpenBoundariesAreRejectedWithSpatialDiagnostics()
        {
            var mesh=Cube(); var indices=mesh.GetIndices(0); mesh.SetIndices(indices.Take(indices.Length-4).ToArray(),MeshTopology.Quads,0);
            var error=Assert.Throws<MeshConversionException>(()=>MeshSurfaceConverter.Convert(mesh)); Assert.That(error.Code,Is.EqualTo("OpenBoundary")); Assert.That(error.Locations.Count,Is.EqualTo(2));
        }
        [Test] public void DegenerateTrianglesAreRejectedBeforePairing()
        { var mesh=Cube(true); var vertices=mesh.vertices; vertices[1]=vertices[0]; mesh.vertices=vertices; Error("Degenerate",()=>MeshSurfaceConverter.Convert(mesh)); }
        [Test] public void WarpedAndConcaveQuadsAreRejected()
        {
            var mesh=Cube(); var vertices=mesh.vertices; vertices[2]+=new Vector3(.2f,.3f,.4f); mesh.vertices=vertices;
            Error("InvalidQuad",()=>MeshSurfaceConverter.Convert(mesh));
        }
        [Test] public void MixedWindingIsNeverRepairedByTheUniformRepairOption()
        {
            var mesh=Cube(); var indices=mesh.GetIndices(0); Array.Reverse(indices,0,4); mesh.SetIndices(indices,MeshTopology.Quads,0);
            Error("Winding",()=>MeshSurfaceConverter.Convert(mesh,new MeshConversionSettings {RepairUniformInwardWinding=true}));
        }
        [Test] public void UniformInwardWindingRequiresExplicitRepair()
        {
            var mesh=Cube(); var indices=mesh.GetIndices(0); for(int i=0;i<indices.Length;i+=4) Array.Reverse(indices,i,4); mesh.SetIndices(indices,MeshTopology.Quads,0);
            Error("InwardWinding",()=>MeshSurfaceConverter.Convert(mesh));
            var result=MeshSurfaceConverter.Convert(mesh,new MeshConversionSettings {RepairUniformInwardWinding=true}); Assert.That(result.Report.Warnings.Any(w=>w.Contains("inward")),Is.True); Assert.That(result.Report.ConvexBoundaries,Is.EqualTo(48));
        }
        [Test] public void DisconnectedAndNonManifoldShellsAreRejected()
        {
            var cube=Cube(); var disconnected=Combine(cube,new Vector3(12,0,0)); Error("Disconnected",()=>MeshSurfaceConverter.Convert(disconnected));
            var joined=Combine(cube,new Vector3(5.6f,2.6f,0)); Error("NonManifoldEdge",()=>MeshSurfaceConverter.Convert(joined));
        }
        private Mesh Combine(Mesh source,Vector3 offset)
        {
            var vertices=source.vertices.ToList(); int count=vertices.Count; vertices.AddRange(source.vertices.Select(v=>v+offset)); var indices=source.GetIndices(0).ToList(); indices.AddRange(source.GetIndices(0).Select(i=>i+count));
            var mesh=Own(new Mesh {indexFormat=IndexFormat.UInt32}); mesh.SetVertices(vertices); mesh.SetIndices(indices,source.GetTopology(0),0); return mesh;
        }
        [Test] public void AmbiguousTrianglePairingIsRejectedAndExplicitCompanionResolvesIt()
        {
            var mesh=HexPrism(out var groups);
            Error("AmbiguousPairing",()=>MeshSurfaceConverter.Convert(mesh));
            var companion=Own(ScriptableObject.CreateInstance<MeshQuadDefinition>()); companion.sourceMesh=mesh; companion.quadVertices=groups;
            var result=MeshSurfaceConverter.Convert(mesh,companion:companion); Assert.That(result.Report.ParentQuads,Is.EqualTo(12)); Assert.That(result.Report.Cells,Is.EqualTo(192));
            companion.quadVertices[0]=companion.quadVertices[1]; Error("InvalidQuad",()=>MeshSurfaceConverter.Convert(mesh,companion:companion));
        }
        private Mesh HexPrism(out int[] groups)
        {
            var vertices=new List<Vector3>(); for(int z=0;z<2;z++) for(int i=0;i<6;i++) vertices.Add(new Vector3(Mathf.Cos(i*Mathf.PI/3),Mathf.Sin(i*Mathf.PI/3),z==0 ? -1 : 1));
            vertices.Add(new Vector3(0,0,-1)); vertices.Add(new Vector3(0,0,1)); var indices=new List<int>(); var quads=new List<int>();
            for(int i=0;i<6;i++)
            {
                int next=(i+1)%6; indices.AddRange(new[] {13,6+i,6+next,12,next,i,i,next,6+next,i,6+next,6+i}); quads.AddRange(new[] {i,next,6+next,6+i});
            }
            for(int i=0;i<6;i+=2) { quads.AddRange(new[] {13,6+i,6+(i+1)%6,6+(i+2)%6}); quads.AddRange(new[] {12,(i+2)%6,(i+1)%6,i}); }
            groups=quads.ToArray(); var mesh=Own(new Mesh()); mesh.SetVertices(vertices); mesh.SetIndices(indices,MeshTopology.Triangles,0); return mesh;
        }
        [Test] public void NativeAndExplicitTriangleQuadDefinitionsHaveIdenticalIdentity()
        {
            var native=Cube(); var triangles=Cube(true); var definition=Own(ScriptableObject.CreateInstance<MeshQuadDefinition>()); definition.sourceMesh=triangles; definition.quadVertices=native.GetIndices(0);
            Assert.That(MeshSurfaceConverter.Convert(triangles,companion:definition).ContentDigest,Is.EqualTo(MeshSurfaceConverter.Convert(native).ContentDigest));
            definition.sourceMesh=native; Error("QuadDefinition",()=>MeshSurfaceConverter.Convert(triangles,companion:definition));
        }
        [Test] public void SubdivisionsPropagateAcrossRotatedPatchAxesOrFailExplicitly()
        {
            var mesh=Cube(); var settings=new MeshConversionSettings(); settings.PatchSubdivisions.Add(new PatchSubdivision {Patch=0,Columns=3,Rows=5});
            var result=MeshSurfaceConverter.Convert(mesh,settings); Assert.That(result.Report.PropagatedAxes,Is.GreaterThan(0));
            settings.PropagateSubdivisions=false; Error("SubdivisionMismatch",()=>MeshSurfaceConverter.Convert(mesh,settings));
        }
        [Test] public void TargetCellSizeSupportsUnequalPatchDimensionsAndEnforcesCaps()
        {
            var mesh=Cube(); var result=MeshSurfaceConverter.Convert(mesh,new MeshConversionSettings {TargetCellSize=.8f}); Assert.That(result.Report.Cells,Is.EqualTo(188));
            Error("CellLimit",()=>MeshSurfaceConverter.Convert(mesh,new MeshConversionSettings {MaximumCells=20}));
            Error("SmallCell",()=>MeshSurfaceConverter.Convert(mesh,new MeshConversionSettings {MinimumCellSize=2}));
            Error("SubdivisionLimit",()=>MeshSurfaceConverter.Convert(mesh,new MeshConversionSettings {TargetCellSize=1e-10f}));
            Error("SharpFold",()=>MeshSurfaceConverter.Convert(mesh,new MeshConversionSettings {MaximumFoldDegrees=10}));
        }
        [Test] public void WeldingIsDeterministicTolerantAndRejectsAmbiguousAnchors()
        {
            var mesh=Cube(); var original=MeshSurfaceConverter.Convert(mesh); var positions=mesh.vertices;
            // One duplicate render-seam vertex may differ slightly without opening a logical edge.
            positions[4]+=Vector3.one*.000001f; mesh.vertices=positions; var snapped=MeshSurfaceConverter.Convert(mesh); Assert.That(snapped.Report.Warnings,Is.Not.Empty);
            Error("OpenBoundary",()=>MeshSurfaceConverter.Convert(mesh,new MeshConversionSettings {WeldTolerance=0}));
            var ambiguous=Cube(); var vertices=ambiguous.vertices.ToList(); vertices.AddRange(new[] {new Vector3(0,0,10),new Vector3(0,.000015f,10),new Vector3(.000001f,.0000075f,10)}); ambiguous.SetVertices(vertices);
            Error("AmbiguousWeld",()=>MeshSurfaceConverter.Convert(ambiguous));
            Error("Settings",()=>MeshSurfaceConverter.Convert(mesh,new MeshConversionSettings {WeldTolerance=.1f}));
        }
        [Test] public void ReorderingVerticesFacesAndQuadStartingCornersKeepsIdentity()
        {
            var mesh=Cube(); var first=MeshSurfaceConverter.Convert(mesh); var vertices=mesh.vertices; var indices=mesh.GetIndices(0); int count=vertices.Length; Array.Reverse(vertices);
            var reordered=new List<int>(); for(int f=indices.Length-4;f>=0;f-=4) for(int i=0;i<4;i++) reordered.Add(count-1-indices[f+(i+1)%4]);
            mesh.vertices=vertices; mesh.SetIndices(reordered,MeshTopology.Quads,0);
            Assert.That(MeshSurfaceConverter.Convert(mesh).ContentDigest,Is.EqualTo(first.ContentDigest));
        }
        [Test] public void TransformGeometryAndSettingsArePartOfTheReproducibleIdentity()
        {
            var mesh=Cube(); var first=MeshSurfaceConverter.Convert(mesh); var matrix=Matrix4x4.TRS(new Vector3(2,3,-1),Quaternion.Euler(17,21,29),new Vector3(1.1f,.9f,1.2f));
            var transformed=MeshSurfaceConverter.Convert(mesh,matrix); Assert.That(transformed.Report.Cells,Is.EqualTo(first.Report.Cells)); Assert.That(transformed.ContentDigest,Is.Not.EqualTo(first.ContentDigest));
            foreach(var p in mesh.vertices.Select(matrix.MultiplyPoint3x4)) Assert.That(transformed.Geometry.Bounds.SqrDistance(p),Is.LessThan(1e-9));
            Assert.That(MeshSurfaceConverter.Convert(mesh,new MeshConversionSettings {WeldTolerance=0}).ContentDigest,Is.Not.EqualTo(first.ContentDigest));
            Error("Transform",()=>MeshSurfaceConverter.Convert(mesh,Matrix4x4.Scale(new Vector3(0,1,1))));
        }
        [Test] public void EdgeExitOverrideKeepsPhysicalFoldsAndSupportsGateOnlyGeneration()
        {
            var result=MeshSurfaceConverter.Convert(Cube(),new MeshConversionSettings {EdgeExits=MeshEdgeExitPolicy.Disabled}); Assert.That(result.Report.ConvexBoundaries,Is.GreaterThan(0));
            Assert.That(result.Shape.Topology.EnumerateCellIds().All(c=>Enumerable.Range(0,4).All(d=>!result.Shape.Topology.GetBoundary(c,(ArrowCube.Topology.SurfaceDirection)d).AllowsEdgeExit)),Is.True);
            var recipe=new PuzzleRecipe(71,result.Shape,AuthoredGenerationSurfaces.Profile(result.Report.Cells),ExitRules.GateOnly); var puzzle=new PuzzleGenerator().Generate(recipe); Assert.That(puzzle.Statistics.EdgeExitsInSolution,Is.Zero);
            Assert.Throws<PuzzleGenerationException>(()=>new PuzzleGenerator().Generate(new PuzzleRecipe(71,result.Shape,recipe.Profile,ExitRules.EdgeOnly)));
        }
        [Test] public void BakedAssetPreservesExactGeometryAndRestoresSavedGeneratedPuzzle()
        {
            var result=MeshSurfaceConverter.Convert(Cube()); var asset=Own(ScriptableObject.CreateInstance<ConvertedSurfaceAsset>()); asset.Initialize(result);
            var restored=Own(ScriptableObject.CreateInstance<ConvertedSurfaceAsset>()); JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(asset),restored); var snapshot=restored.Build(); Assert.That(snapshot.ContentDigest,Is.EqualTo(result.ContentDigest));
            ConvertedSurfaceAsset.Register(restored); var recipe=new PuzzleRecipe(12345,snapshot.Shape,AuthoredGenerationSurfaces.Profile(snapshot.Report.Cells),ExitRules.GateOnly); var puzzle=new PuzzleGenerator().Generate(recipe);
            var save=PuzzleSave.Capture(puzzle); var loaded=save.Restore(); var geometry=AuthoredSurfaceDemos.Resolve(loaded); Assert.That(geometry.Surface,Is.SameAs(loaded.Surface));
            Assert.That(new PuzzleGenerator().Generate(RecipeSave.Capture(recipe).Restore()).Fingerprint,Is.EqualTo(puzzle.Fingerprint));
            Assert.That(ReferenceEquals(restored.Build(),snapshot),Is.True,"Gameplay reuses baked immutable data without mesh conversion.");
        }
        [Test] public void QuadObjPreservesExplicitFacesAndRejectsTrianglesAndBadIndices()
        {
            var mesh=Cube(); var text=new System.Text.StringBuilder(); foreach(var p in mesh.vertices) text.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"v {0:R} {1:R} {2:R}\n",p.x,p.y,p.z);
            var indices=mesh.GetIndices(0); for(int i=0;i<indices.Length;i+=4) text.Append($"f {indices[i]+1}/1/1 {indices[i+1]+1}/2/1 {indices[i+2]+1}/3/1 {indices[i+3]+1}/4/1\n");
            var obj=Own(QuadObjReader.Read(text.ToString())); Assert.That(MeshSurfaceConverter.Convert(obj).ContentDigest,Is.EqualTo(MeshSurfaceConverter.Convert(mesh).ContentDigest));
            Error("OBJ",()=>QuadObjReader.Read("v 0 0 0\nf 1 2 3")); Error("OBJ",()=>QuadObjReader.Read("v 0 0 0\nf 1 2 3 4"));
        }
        [Test] public void ConnectedSelfIntersectingShellIsRejectedBeforeQuadReconstruction()
        {
            var mesh=Own(new Mesh()); mesh.vertices=new[] {new Vector3(1,0,0),new Vector3(0,1,0),new Vector3(-1,0,0),new Vector3(0,-1,0),new Vector3(2,.3f,-2),new Vector3(0,0,-1)};
            var indices=new List<int>(); for(int i=0;i<4;i++) { int n=(i+1)%4; indices.AddRange(new[] {4,i,n,5,n,i}); } mesh.SetIndices(indices,MeshTopology.Triangles,0);
            Error("SelfIntersection",()=>MeshSurfaceConverter.Convert(mesh));
        }
        [Test] public void DeliveredBakedAssetsMatchConversionOfTheirSourceMeshBuffers()
        {
            foreach(MeshConversionFixture fixture in Enum.GetValues(typeof(MeshConversionFixture)))
            {
                var mesh=Resources.Load<Mesh>("ArrowCubeMeshInputs/"+fixture); var asset=Resources.Load<ConvertedSurfaceAsset>("ArrowCubeSurfaces/Converted-"+fixture);
                Assert.That(mesh,Is.Not.Null); Assert.That(asset,Is.Not.Null); Assert.That(asset.Build().ContentDigest,Is.EqualTo(MeshSurfaceConverter.Convert(mesh).ContentDigest));
            }
        }
        [Test] public void CancellationAndInvalidCoordinateDataFailWithoutPartialResults()
        {
            var mesh=Cube(); using(var token=new CancellationTokenSource()) { token.Cancel(); Assert.Throws<OperationCanceledException>(()=>MeshSurfaceConverter.Convert(mesh,cancellationToken:token.Token)); }
            var vertices=mesh.vertices; vertices[0]=new Vector3(float.NaN,0,0); mesh.vertices=vertices; Error("Coordinates",()=>MeshSurfaceConverter.Convert(mesh));
        }
        [TestCase(2)] [TestCase(4)] [TestCase(8)]
        public void ConversionAndGenerationPerformanceAcrossFourModels(int subdivisions)
        {
            foreach(MeshConversionFixture kind in Enum.GetValues(typeof(MeshConversionFixture)))
            {
                var mesh=Own(MeshConversionFixtures.Create(kind)); var result=MeshSurfaceConverter.Convert(mesh,new MeshConversionSettings {Subdivisions=subdivisions});
                var generated=new PuzzleGenerator().Generate(new PuzzleRecipe(12345,result.Shape,AuthoredGenerationSurfaces.Profile(result.Report.Cells),ExitRules.GateOnly));
                Assert.That(new PuzzleValidator().Validate(generated,generated.VerifiedSolution).Succeeded,Is.True);
                Debug.Log($"MESH PERFORMANCE {kind} n={subdivisions} cells={result.Report.Cells} conversion_ms={result.Report.ConversionMilliseconds:F2} validation_ms={result.Report.ValidationMilliseconds:F2} memory_delta={result.Report.ManagedMemoryDelta} generation_ms={generated.Report.Milliseconds:F2} attempts={generated.Report.CandidateAttempts} rejected={generated.Report.RejectedCandidates}");
            }
        }
    }
}
