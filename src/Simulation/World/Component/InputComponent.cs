namespace Yggdrasilnet.Server.Simulation.World.Component;

public sealed class InputComponent : IComponent {
    private bool _hasSequence;

    public float Speed { get; set; }
    public float MoveX { get; set; }
    public float MoveZ { get; set; }
    public uint LastSequence { get; set; }

    public bool ApplyInput(float moveX, float moveZ, uint sequence) {
        if (_hasSequence && !IsNewerSequence(sequence, LastSequence)) {
            return false;
        }

        MoveX = moveX;
        MoveZ = moveZ;
        LastSequence = sequence;
        _hasSequence = true;
        return true;
    }

    private static bool IsNewerSequence(uint candidate, uint current) {
        var diff = candidate - current;
        return diff != 0 && diff < 0x80000000;
    }
}