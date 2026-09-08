using System.Text.Json;

namespace Yggdrasilnet.Server.Simulation.Content;

internal static class DefinitionJson {
    public static readonly JsonSerializerOptions Options = new() {
        PropertyNameCaseInsensitive = true
    };
}

public sealed class DefinitionRegistry<T> where T : IDefinition {
    private readonly Dictionary<string, T> _definitions = new();

    public void Load(string path) {
        foreach (var file in Directory.GetFiles(path, "*.json")) {
            var definition = JsonSerializer.Deserialize<T>(File.ReadAllText(file), DefinitionJson.Options);
            if (definition is not null) {
                _definitions[definition.Id] = definition;
            }
        }
    }

    public bool TryGet(string id, out T definition) {
        return _definitions.TryGetValue(id, out definition!);
    }
}
