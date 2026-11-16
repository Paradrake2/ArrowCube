using System.Collections.Generic;
using ArrowCube.Presentation;
using ArrowCube.Topology;
using UnityEngine;

/// <summary>Editor gizmos only: inspect the connected surface without creating gameplay objects.</summary>
[RequireComponent(typeof(GameManager))]
public sealed class CubeTopologyPreview : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float sideLength = 4;
    [SerializeField] private bool showCells = true;
    [SerializeField] private bool showSeamLinks = true;
    private readonly List<Vector3> points = new List<Vector3>();
    private CubeTopology cachedTopology;
    private ArrowPath example;

    private static readonly Color[] FaceColors =
    {
        new Color(0.3f, 0.65f, 1f), new Color(1f, 0.65f, 0.25f),
        new Color(0.7f, 0.4f, 1f), new Color(0.3f, 0.9f, 0.65f),
        new Color(1f, 0.85f, 0.3f), new Color(1f, 0.4f, 0.6f)
    };

    private void OnDrawGizmos()
    {
        var manager = GetComponent<GameManager>();
        if (manager == null) return;
        var topology = manager.Topology;
        if (cachedTopology != topology)
        {
            cachedTopology = topology;
            example = BuildExample(topology);
        }

        var geometry = new CubeSurfaceGeometry(topology, Mathf.Max(0.1f, sideLength));
        var oldMatrix = Gizmos.matrix;
        var oldColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        try
        {
            Gizmos.color = new Color(0.7f, 0.85f, 1f, 0.5f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one * geometry.SideLength);
            foreach (var cell in topology.EnumerateCells())
            {
                Gizmos.color = FaceColors[(int)cell.Face];
                if (showCells) Gizmos.DrawSphere(geometry.GetLocalPosition(cell), geometry.CellSize * 0.035f);
                if (!showSeamLinks) continue;
                for (int d = 0; d < 4; d++)
                {
                    var step = topology.Step(cell, (SurfaceDirection)d);
                    if (!step.CrossedEdge || topology.GetCellIndex(cell) > topology.GetCellIndex(step.Coordinate)) continue;
                    var seam = CubeSurfaceGeometry.ToVector(topology.GetDoubledEdgeMidpoint(cell, (SurfaceDirection)d))
                        * (geometry.CellSize * 0.5f);
                    Gizmos.DrawLine(geometry.GetLocalPosition(cell), seam);
                    Gizmos.DrawLine(seam, geometry.GetLocalPosition(step.Coordinate));
                }
            }

            geometry.WritePolyline(example, points, geometry.CellSize * 0.06f);
            Gizmos.color = Color.black;
            for (int i = 1; i < points.Count; i++) Gizmos.DrawLine(points[i - 1], points[i]);
            var head = points[0];
            var forward = geometry.GetLocalDirection(example.Head.Face, example.HeadDirection);
            var normal = CubeSurfaceGeometry.ToVector(topology.GetOrientation(example.Head.Face).Normal);
            var sideways = Vector3.Cross(normal, forward);
            Gizmos.DrawLine(head, head - forward * geometry.CellSize * 0.35f + sideways * geometry.CellSize * 0.2f);
            Gizmos.DrawLine(head, head - forward * geometry.CellSize * 0.35f - sideways * geometry.CellSize * 0.2f);

#if UNITY_EDITOR
            for (int face = 0; face < 6; face++)
            {
                var frame = topology.GetOrientation((CubeFace)face);
                var center = CubeSurfaceGeometry.ToVector(frame.Normal) * (geometry.SideLength * 0.5f + 0.15f);
                UnityEditor.Handles.Label(transform.TransformPoint(center), ((CubeFace)face).ToString());
            }
#endif
        }
        finally { Gizmos.matrix = oldMatrix; Gizmos.color = oldColor; }
    }

    private static ArrowPath BuildExample(CubeTopology topology)
    {
        var route = new List<SurfaceCoordinate>();
        var cell = new SurfaceCoordinate(CubeFace.Front, 0, 0);
        route.Add(cell);
        var direction = SurfaceDirection.Right;
        Append(topology.GridSize);
        direction = SurfaceDirection.Up;
        Append(topology.GridSize);
        Append(topology.GridSize - 1);
        route.Reverse();
        return new ArrowPath(0, topology, route, direction);

        void Append(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var step = topology.Step(cell, direction);
                cell = step.Coordinate;
                direction = step.Direction;
                route.Add(cell);
            }
        }
    }
}
