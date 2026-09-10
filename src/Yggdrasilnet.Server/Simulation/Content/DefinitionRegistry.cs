using System.Text.Json;

namespace Yggdrasilnet.Server.Simulation.Content;

internal static class DefinitionJson {
    public static readonly JsonSerializerOptions Options = new() {
        PropertyNameCaseInsensitive = true
    };
}

public sealed class DefinitionRegistry<T> where T : IDefinition {
    private readonly Dictionary<string, T> _definitions = new();
    private readonly Dictionary<string, byte> _indexById = new();
    private readonly List<string> _idsByIndex = [];

    public void Load(string path) {
        foreach (var file in Directory.GetFiles(path, "*.json").OrderBy(f => f, StringComparer.Ordinal)) {
            var definition = JsonSerializer.Deserialize<T>(File.ReadAllText(file), DefinitionJson.Options);
            if (definition is null) {
                continue;
            }

            _definitions[definition.Id] = definition;

            if (!_indexById.ContainsKey(definition.Id)) {
                if (_idsByIndex.Count > byte.MaxValue) {
                    throw new InvalidOperationException("Too many entity definitions to fit in a byte index.");
                }

                _indexById[definition.Id] = (byte)_idsByIndex.Count;
                _idsByIndex.Add(definition.Id);
            }
        }
    }

    public bool TryGet(string id, out T definition) {
        return _definitions.TryGetValue(id, out definition!);
    }

    public bool TryGetIndex(string id, out byte index) {
        return _indexById.TryGetValue(id, out index);
    }

    public IEnumerable<(byte Index, string Id)> Entries {
        get {
            for (var i = 0; i < _idsByIndex.Count; i++) {
                yield return ((byte)i, _idsByIndex[i]);
            }
        }
    }
}
