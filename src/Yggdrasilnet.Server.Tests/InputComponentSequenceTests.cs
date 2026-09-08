using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Tests;

public sealed class InputComponentSequenceTests {
    [Fact]
    public void AppliesFirstInputSequence() {
        var input = new InputComponent();

        var applied = input.ApplyInput(1f, -0.25f, 42);

        Assert.True(applied);
        Assert.Equal(42u, input.LastSequence);
        Assert.Equal(1f, input.MoveX);
        Assert.Equal(-0.25f, input.MoveZ);
    }

    [Fact]
    public void IgnoresOlderSequence() {
        var input = new InputComponent();
        input.ApplyInput(0.5f, 0.5f, 10);

        var applied = input.ApplyInput(-1f, 0f, 9);

        Assert.False(applied);
        Assert.Equal(10u, input.LastSequence);
        Assert.Equal(0.5f, input.MoveX);
        Assert.Equal(0.5f, input.MoveZ);
    }

    [Fact]
    public void IgnoresDuplicateSequence() {
        var input = new InputComponent();
        input.ApplyInput(0.25f, 0.75f, 15);

        var applied = input.ApplyInput(-0.75f, -0.25f, 15);

        Assert.False(applied);
        Assert.Equal(15u, input.LastSequence);
        Assert.Equal(0.25f, input.MoveX);
        Assert.Equal(0.75f, input.MoveZ);
    }

    [Fact]
    public void AcceptsSequenceAfterUintWrapAround() {
        var input = new InputComponent();
        input.ApplyInput(0f, 1f, uint.MaxValue);

        var applied = input.ApplyInput(1f, 0f, 0);

        Assert.True(applied);
        Assert.Equal(0u, input.LastSequence);
        Assert.Equal(1f, input.MoveX);
        Assert.Equal(0f, input.MoveZ);
    }
}
