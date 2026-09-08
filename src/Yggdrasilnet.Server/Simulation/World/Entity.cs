using System.Numerics;
using Yggdrasilnet.Server.Simulation.World.Component;

namespace Yggdrasilnet.Server.Simulation.World;

public sealed class Entity {
    public int Id { get; }
    public Vector3 Position { get; set; }

    private readonly Dictionary<Type, IComponent> _components = new();
    
    public IEnumerable<IComponent> Components => _components.Values;

    public Entity(int id, Vector3 position = default) {
        Id = id;
        Position = position;
    }

    public void AddComponent(IComponent component) {
        _components[component.GetType()] = component;
    }

    public bool RemoveComponent<T>() where T : IComponent {
        return _components.Remove(typeof(T));
    }

    public bool HasComponent<T>() where T : IComponent {
        return _components.ContainsKey(typeof(T));
    }

    public bool TryGetComponent<T>(out T component) where T : class, IComponent {
        if (_components.TryGetValue(typeof(T), out var value)) {
            component = (T)value;
            return true;
        }

        component = null!;
        return false;
    }
}
