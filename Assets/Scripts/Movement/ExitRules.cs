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

        /// <summary>Shared exit policy. Geometry supplies eligibility; the puzzle supplies gates.
        /// A geometric edge wins a tie with a compatible gate, preserving existing cube behavior.</summary>
        public ExitType? SelectExit(bool allowsEdgeExit,bool hasDirectionalGate)
        {
            if(EnableCubeEdgeExits && allowsEdgeExit) return ExitType.CubeEdge;
            if(EnableDirectionalGates && hasDirectionalGate) return ExitType.DirectionalGate;
            return null;
        }
    }
}
