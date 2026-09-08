namespace Yggdrasilnet.Server.Simulation.Content.Entity;

public class EntityDefinition : IDefinition {
    public string Id { get; set; } = string.Empty;
    
    public List<EntityComponentDefinition> Components { get; set; } = [];
}