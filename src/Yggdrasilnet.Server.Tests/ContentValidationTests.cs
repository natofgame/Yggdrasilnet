using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yggdrasilnet.Server.Simulation.Content;
using Yggdrasilnet.Server.Simulation.Content.Entity;
using Yggdrasilnet.Server.Simulation.Content.Spell;
using Yggdrasilnet.Server.Simulation.Content.Spell.Definitions;
using Yggdrasilnet.Server.Simulation.Content.Spell.Effect;
using Yggdrasilnet.Server.Simulation.World;
using Yggdrasilnet.Server.Simulation.World.Component;
using Yggdrasilnet.Server.Utils;
using Yggdrasilnet.Shared.Spell;

namespace Yggdrasilnet.Server.Tests;

public sealed class ContentValidationTests : IDisposable {
    private const string ProjectileJson = """
        {"id":"fireball","projectileEntityId":"fireball_projectile","projectileSpeed":14,"damage":25}
        """;

    private readonly string _scratch = Path.Combine(
        Directory.GetParent(ContentPaths.Resolve("Entities"))!.Parent!.FullName,
        $".content-validation-tests-{Guid.NewGuid():N}");

    public static IEnumerable<object[]> InvalidSpells() {
        foreach (var field in new[] { "anticipationSeconds", "strikeSeconds", "impactSeconds", "returnSeconds", "range", "damage" }) {
            foreach (var value in new[] { "-1", "1e100", "\"NaN\"", "\"Infinity\"" }) {
                yield return [field, value];
            }
        }
        foreach (var field in new[] { "projectileSpeed", "projectileLifetimeSeconds", "projectileHitRadius" }) {
            foreach (var value in new[] { "0", "-1", "1e100", "\"NaN\"" }) {
                yield return [field, value];
            }
        }
        foreach (var value in new[] { "99", "-1", "256", "\"Unknown\"", "\"99\"", "null" }) {
            yield return ["type", value];
        }
        foreach (var value in new[] { "\"\"", "\" \"", "null" }) {
            yield return ["id", value];
            yield return ["projectileEntityId", value];
        }
        foreach (var value in new[] {
            """[{"type":99,"amount":1}]""",
            """[{"type":-1,"amount":1}]""",
            """[{"type":"Unknown","amount":1}]""",
            """[{"type":"99","amount":1}]""",
            """[{"type":"Damage","amount":-1}]""",
            """[{"type":"Heal","amount":1e100}]""",
            """[{"type":"Heal","amount":"NaN"}]""",
            "[null]", "null"
        }) {
            yield return ["effects", value];
        }
    }

    [Theory]
    [MemberData(nameof(InvalidSpells))]
    public void LoadingRejectsInvalidSpellFields(string field, string value) {
        var json = JsonNode.Parse(ProjectileJson)!;
        json[field] = JsonNode.Parse(value);
        var registry = new DefinitionRegistry<SpellDefinition>();

        AssertInvalid(() => Load(registry, "Spells", json.ToJsonString()));

        Assert.Empty(registry.Entries);
    }

    public static IEnumerable<object[]> InvalidEntities() {
        foreach (var value in new[] { "0", "-1", "1e100", "\"NaN\"" }) {
            yield return [$$"""[{"type":"health","max":{{value}}}]"""];
            yield return [$$"""[{"type":"spellbook","spells":["fireball"]},{"type":"autocast","intervalSeconds":{{value}}}]"""];
        }
        foreach (var value in new[] { "-1", "1e100", "\"NaN\"" }) {
            yield return [$$"""[{"type":"target","range":{{value}}}]"""];
        }
        foreach (var value in new[] { "-1", "1", "256" }) {
            yield return [$$"""[{"type":"spellbook","spells":["fireball"]},{"type":"autocast","spellIndex":{{value}}}]"""];
        }
        yield return ["""[{"type":"autocast"}]"""];
        yield return ["""[{"type":"spellbook","spells":[]},{"type":"autocast"}]"""];
        yield return ["""[{"type":"spellbook","spells":null}]"""];
        yield return ["""[{"type":"spellbook","spells":[null]}]"""];
        yield return ["""[{"type":"spellbook","spells":[" "]}]"""];
        yield return [$$"""[{"type":"spellbook","spells":{{JsonSerializer.Serialize(Enumerable.Repeat("fireball", 257))}}}]"""];
        yield return ["""[{"type":"Unknown"}]"""];
        yield return ["[null]"];
        yield return ["null"];
    }

    [Theory]
    [MemberData(nameof(InvalidEntities))]
    public void LoadingRejectsInvalidEntityComponents(string components) {
        var registry = new DefinitionRegistry<EntityDefinition>();

        AssertInvalid(() => Load(registry, "Entities", $$"""{"id":"invalid","components":{{components}}}"""));

        Assert.Empty(registry.Entries);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"id":null}""")]
    [InlineData("""{"id":" "}""")]
    [InlineData("null")]
    public void LoadingRejectsMissingDefinitionsAndIds(string json) {
        AssertInvalid(() => Load(new DefinitionRegistry<EntityDefinition>(), "Entities", json));
        AssertInvalid(() => Load(new DefinitionRegistry<SpellDefinition>(), "Spells", json));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void ReusableValidationRejectsNonfiniteValues(float value) {
        foreach (var property in new[] {
            nameof(SpellDefinition.AnticipationSeconds), nameof(SpellDefinition.StrikeSeconds),
            nameof(SpellDefinition.ImpactSeconds), nameof(SpellDefinition.ReturnSeconds),
            nameof(SpellDefinition.Range), nameof(SpellDefinition.Damage),
            nameof(SpellDefinition.ProjectileSpeed), nameof(SpellDefinition.ProjectileLifetimeSeconds),
            nameof(SpellDefinition.ProjectileHitRadius)
        }) {
            var spell = new SpellDefinition { Id = "test", ProjectileEntityId = "projectile", ProjectileSpeed = 1 };
            typeof(SpellDefinition).GetProperty(property)!.SetValue(spell, value);
            Assert.Throws<InvalidDataException>(() => ContentValidator.Validate(spell));
        }

        Assert.Throws<InvalidDataException>(() => ContentValidator.Validate(new SpellDefinition {
            Id = "heal", Type = SpellType.Self, Effects = [new() { Type = SpellEffectType.Heal, Amount = value }]
        }));
        foreach (var component in new EntityComponentDefinition[] {
            new HealthComponentDefinition { Max = value },
            new TargetComponentDefinition { Range = value },
            new AutocastComponentDefinition { IntervalSeconds = value }
        }) {
            Assert.Throws<InvalidDataException>(() => ContentValidator.Validate(new EntityDefinition {
                Id = "test", Components = [new SpellbookComponentDefinition { Spells = ["fireball"] }, component]
            }));
        }
    }

    [Theory]
    [InlineData("Projectile", 0)]
    [InlineData("Self", 1)]
    [InlineData("Melee", 2)]
    [InlineData("Targeted", 3)]
    public void LoadingAcceptsDefinedStringAndNumericEnums(string name, int number) {
        foreach (var type in new[] { JsonSerializer.Serialize(name), number.ToString() }) {
            var json = JsonNode.Parse(ProjectileJson)!;
            json["type"] = JsonNode.Parse(type);
            json["effects"] = JsonNode.Parse("""[{"type":"Damage","amount":0},{"type":1,"amount":2}]""");
            var registry = new DefinitionRegistry<SpellDefinition>();
            Load(registry, "Spells", json.ToJsonString());

            Assert.True(registry.TryGet("fireball", out var spell));
            Assert.Equal((SpellType)number, spell.Type);
            Assert.Equal(SpellEffectType.Damage, spell.Effects[0].Type);
            Assert.Equal(SpellEffectType.Heal, spell.Effects[1].Type);
        }
    }

    [Fact]
    public void LegacyDamageConvertsOnceWithoutChangingProjectileDefaults() {
        var spells = new DefinitionRegistry<SpellDefinition>();
        Load(spells, "Spells", ProjectileJson);

        Assert.True(spells.TryGet("fireball", out var spell));
        ContentValidator.Validate(spell);
        var effect = Assert.Single(spell.Effects);
        Assert.Equal(SpellEffectType.Damage, effect.Type);
        Assert.Equal(25f, effect.Amount);
        Assert.Equal(25f, spell.Damage);
        Assert.Equal(SpellType.Projectile, spell.Type);
        Assert.Equal(5f, spell.ProjectileLifetimeSeconds);
        Assert.Equal(0.6f, spell.ProjectileHitRadius);
    }

    [Fact]
    public void ExplicitEffectsTakePrecedenceOverLegacyDamage() {
        var spells = new DefinitionRegistry<SpellDefinition>();
        Load(spells, "Spells", """{"id":"heal","type":"Self","damage":25,"effects":[{"type":"Heal","amount":10}]}""");

        Assert.True(spells.TryGet("heal", out var spell));
        var effect = Assert.Single(spell.Effects);
        Assert.Equal(SpellEffectType.Heal, effect.Type);
        Assert.Equal(10f, effect.Amount);
    }

    [Fact]
    public void ZeroLegacyDamageDoesNotAddAnEffect() {
        var spells = new DefinitionRegistry<SpellDefinition>();
        Load(spells, "Spells", """{"id":"heal","type":"Self","damage":0}""");
        Assert.True(spells.TryGet("heal", out var spell));
        Assert.Empty(spell.Effects);
    }

    [Fact]
    public void AutocastDefaultsAndLastByteSlotAreValid() {
        var entities = new DefinitionRegistry<EntityDefinition>();
        Load(entities, "Entities", $$"""
            {"id":"caster","components":[
                {"type":"autocast","spellIndex":255},
                {"type":"spellbook","spells":{{JsonSerializer.Serialize(Enumerable.Repeat("fireball", 256))}}},
                {"type":"target","range":0}
            ]}
            """);

        Assert.True(entities.TryGet("caster", out var definition));
    }

    [Fact]
    public void CrossRegistryValidationRejectsMissingSpellbookReferences() {
        var entities = new DefinitionRegistry<EntityDefinition>();
        var spells = new DefinitionRegistry<SpellDefinition>();
        Load(entities, "Entities", """{"id":"caster","components":[{"type":"spellbook","spells":["missing"]}]}""");

        var error = Assert.Throws<InvalidDataException>(() => ContentValidator.Validate(entities, spells));

        Assert.Contains("caster", error.Message);
        Assert.Contains("missing", error.Message);
    }

    [Fact]
    public void CrossRegistryValidationRunsAfterBothRegistriesLoad() {
        var entities = new DefinitionRegistry<EntityDefinition>();
        var spells = new DefinitionRegistry<SpellDefinition>();
        Load(spells, "Spells", ProjectileJson);

        var error = Assert.Throws<InvalidDataException>(() => ContentValidator.Validate(entities, spells));
        Assert.Contains("fireball_projectile", error.Message);

        Load(entities, "Entities", """{"id":"fireball_projectile","components":[]}""");
        ContentValidator.Validate(entities, spells);
    }

    [Fact]
    public void FactoryCopiesSpellbooksAndDeterministicallyStaggersAutocast() {
        var definitions = new DefinitionRegistry<EntityDefinition>();
        Load(definitions, "Entities", """
            {"id":"caster","components":[
                {"type":"autocast","spellIndex":1,"intervalSeconds":2},
                {"type":"health","max":75},
                {"type":"target","range":20,"autocastersOnly":true},
                {"type":"spellbook","spells":["fireball","heal"]}
            ]}
            """);
        Assert.True(definitions.TryGet("caster", out var definition));
        var factory = new EntityFactory();
        var world = new World();
        var first = factory.Create(definition, world, Vector3.Zero);
        var second = factory.Create(definition, world, Vector3.Zero);
        var replay = factory.Create(definition, new World(), Vector3.Zero);

        Assert.True(first.TryGetComponent<HealthComponent>(out var health));
        Assert.Equal(75f, health.Max);
        Assert.Equal(75f, health.Current);
        
        Assert.True(first.TryGetComponent<SpellbookComponent>(out var firstBook));
        Assert.True(second.TryGetComponent<SpellbookComponent>(out var secondBook));
        firstBook.Spells.Clear();
        Assert.Equal(new[] { "fireball", "heal" }, secondBook.Spells);
        Assert.Equal(new[] { "fireball", "heal" }, definition.Components.OfType<SpellbookComponentDefinition>().Single().Spells);
    }

    [Fact]
    public void ShippedContentValidatesAndPreservesPlayerSlotsAndCrowdSteering() {
        var entities = new DefinitionRegistry<EntityDefinition>();
        var spells = new DefinitionRegistry<SpellDefinition>();
        entities.Load(ContentPaths.Resolve("Entities"));
        spells.Load(ContentPaths.Resolve("Spells"));

        ContentValidator.Validate(entities, spells);

        Assert.True(entities.TryGet("player", out var player));
        Assert.Equal(new[] { "fireball", "heal", "melee" }, player.Components.OfType<SpellbookComponentDefinition>().Single().Spells);
        Assert.Equal(100f, player.Components.OfType<HealthComponentDefinition>().Single().Max);
        var playerTarget = player.Components.OfType<TargetComponentDefinition>().Single();
        Assert.Equal(20f, playerTarget.Range);
        Assert.False(playerTarget.AutocastersOnly);

        Assert.True(entities.TryGet("crowd", out var crowd));
        Assert.Equal(new[] { "fireball" }, crowd.Components.OfType<SpellbookComponentDefinition>().Single().Spells);
        Assert.Equal(75f, crowd.Components.OfType<HealthComponentDefinition>().Single().Max);
        Assert.Equal(20f, crowd.Components.OfType<TargetComponentDefinition>().Single().Range);
        Assert.True(crowd.Components.OfType<TargetComponentDefinition>().Single().AutocastersOnly);
        var autocast = crowd.Components.OfType<AutocastComponentDefinition>().Single();
        Assert.Equal((byte)0, autocast.SpellIndex);
        Assert.Equal(2f, autocast.IntervalSeconds);
        var steering = crowd.Components.OfType<SteeringComponentDefinition>().Single();
        Assert.Equal(2f, steering.Speed);
        Assert.Equal(4f, steering.SpawnSpacing);
        Assert.Equal(0.6f, steering.WanderWeight);
        Assert.Equal(1.5f, steering.WanderJitter);

        foreach (var (id, type, effectType) in new[] {
            ("fireball", SpellType.Projectile, SpellEffectType.Damage),
            ("heal", SpellType.Self, SpellEffectType.Heal),
            ("melee", SpellType.Melee, SpellEffectType.Damage)
        }) {
            Assert.True(spells.TryGet(id, out var spell));
            Assert.Equal(type, spell.Type);
            Assert.Equal(effectType, Assert.Single(spell.Effects).Type);
        }
    }

    [Fact]
    public void ValidationDoesNotChangeExistingSteeringSemantics() {
        var registry = new DefinitionRegistry<EntityDefinition>();
        Load(registry, "Entities", """{"id":"wanderer","components":[{"type":"steering","roamRadius":-1}]}""");
        Assert.True(registry.TryGet("wanderer", out var definition));
        var entity = new EntityFactory().Create(definition, new World(), Vector3.Zero);
        Assert.True(entity.TryGetComponent<SteeringComponent>(out var steering));
        Assert.Equal(0f, steering.RoamRadius);
    }

    private void Load<T>(DefinitionRegistry<T> registry, string folder, string json) where T : IDefinition {
        var path = Path.Combine(_scratch, folder);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "definition.json"), json);
        registry.Load(path);
    }

    private static void AssertInvalid(Action load) {
        var error = Record.Exception(load);
        Assert.True(error is InvalidDataException or JsonException, $"Expected content validation error, got: {error}");
    }

    public void Dispose() {
        if (Directory.Exists(_scratch)) {
            Directory.Delete(_scratch, recursive: true);
        }
    }
}
