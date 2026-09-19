namespace Yggdrasilnet.Server.Simulation.Content.Ai.Handlers;

public interface IAiIntentHandler {
    public void Apply(World.World world, AiContext contex, World.Entity entity);
}