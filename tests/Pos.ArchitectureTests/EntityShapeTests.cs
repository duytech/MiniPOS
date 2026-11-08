using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Pos.Infrastructure.Persistence;

namespace Pos.ArchitectureTests;

/// <summary>
/// Domain classes mapped to the database hold only properties: use-case logic lives in the handlers, pricing,
/// splitting and shared rules in static Domain classes (OrderPricing, OrderSplitting, OrderRules).
/// </summary>
public class EntityShapeTests
{
    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void Mapped_entities_hold_only_auto_properties()
    {
        var entities = MappedEntityTypes();
        Assert.NotEmpty(entities);

        var offenders = entities.SelectMany(type => NonPropertyMembers(type).Select(member => $"{type.FullName}.{member}"));

        var list = offenders.ToList();
        Assert.True(list.Count == 0,
            "A Domain class mapped to the database holds only { get; set; } properties: move this logic into the " +
            $"handler or a static Domain class:{Environment.NewLine}{string.Join(Environment.NewLine, list)}");
    }

    /// <summary>The CLR types of the EF model, read without a database connection.</summary>
    private static List<Type> MappedEntityTypes()
    {
        var options = new DbContextOptionsBuilder<PosDbContext>().UseNpgsql("Host=unused").Options;
        using var context = new PosDbContext(options);
        return context.Model.GetEntityTypes()
            .Where(e => !e.IsOwned() && e.ClrType.Assembly == Layers.Domain)
            .Select(e => e.ClrType)
            .Distinct()
            .ToList();
    }

    private static IEnumerable<string> NonPropertyMembers(Type type)
    {
        var autoProperties = type.GetProperties(Declared)
            .Where(p => type.GetField($"<{p.Name}>k__BackingField", Declared) is not null)
            .ToHashSet();
        var accessors = autoProperties.SelectMany(p => new[] { p.GetMethod, p.SetMethod }).OfType<MethodInfo>().ToHashSet();

        foreach (var property in type.GetProperties(Declared).Where(p => !autoProperties.Contains(p)))
        {
            yield return $"{property.Name} (computed property)";
        }

        foreach (var method in type.GetMethods(Declared).Where(m => !accessors.Contains(m)))
        {
            yield return $"{method.Name}()";
        }

        foreach (var field in type.GetFields(Declared).Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute))))
        {
            yield return $"{field.Name} (field)";
        }

        foreach (var constructor in type.GetConstructors(Declared).Where(c => c.GetParameters().Length > 0 || !c.IsPublic))
        {
            yield return $"{constructor.Name}({string.Join(", ", constructor.GetParameters().Select(p => p.Name))})";
        }
    }
}
