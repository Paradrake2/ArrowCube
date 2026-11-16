using ArrowCube.Topology;
using UnityEngine;

/// <summary>Stage 1 composition root. Gameplay systems will build on this shared surface.</summary>
public class GameManager : MonoBehaviour
{
    [SerializeField, Range(1, 100)] private int gridSize = 6;
    private CubeTopology topology;

    public CubeTopology Topology
    {
        get
        {
            if (topology == null || topology.GridSize != gridSize)
                topology = new CubeTopology(gridSize);
            return topology;
        }
    }

    private void Awake() => _ = Topology;

    private void OnValidate()
    {
        gridSize = Mathf.Clamp(gridSize, 1, 100);
        topology = null;
    }
}
