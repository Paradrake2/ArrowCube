namespace ArrowCube.Movement
{
    public enum ExitType { CubeEdge, DirectionalGate }

    /// <summary>Immutable per-level switches. New configurations enable both exit mechanics.</summary>
    public sealed class ExitRules
    {
        public bool EnableCubeEdgeExits { get; }
        public bool EnableDirectionalGates { get; }
        public static ExitRules Hybrid { get; } = new ExitRules();
        public static ExitRules EdgeOnly { get; } = new ExitRules(true, false);
        public static ExitRules GateOnly { get; } = new ExitRules(false, true);
        public static ExitRules None { get; } = new ExitRules(false, false);

        public ExitRules(bool enableCubeEdgeExits = true, bool enableDirectionalGates = true)
        {
            EnableCubeEdgeExits = enableCubeEdgeExits;
            EnableDirectionalGates = enableDirectionalGates;
        }
    }
}
