using System.Reflection;
using Pos.Application.Common;
using Pos.Application.Orders;
using Pos.Domain.Common;

namespace Pos.ArchitectureTests;

public class MoneyTypeTests
{
    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>Types allowed to expose a raw <see cref="decimal"/> in Domain/Application signatures.</summary>
    private static readonly Dictionary<Type, string> RawDecimalAllowed = new()
    {
        [typeof(Money)] = "the money value object wraps the decimal amount",
        [typeof(Percentage)] = "the percentage value object wraps the decimal rate",
        [typeof(PricingOptions)] = "bound from configuration, converted to Percentage when snapshotted onto an order",
        [typeof(MoneyLimits)] = "constant used by validators before a Money exists",
        [typeof(PercentageLimits)] = "constant used by validators, compared with Percentage.Value",
    };

    [Fact]
    public void No_layer_uses_floating_point_types()
    {
        var offenders = Layers.All.SelectMany(FloatingPointScanner.Scan);

        AssertNone(offenders, "Use Money (or decimal inside Money/Percentage) instead of double/float/Half");
    }

    /// <summary>Methods outside Domain allowed to call a rounding method, keyed by <c>Type.FullName.Method</c>.</summary>
    private static readonly Dictionary<string, string> RoundingAllowed = new()
    {
        ["Pos.Api.Json.MoneyJsonConverter.Read"] = "rejects JSON with more than 2 decimals; computes no amount",
        ["Pos.Api.Json.PercentageJsonConverter.Read"] = "rejects JSON with more than 4 decimals; computes no amount",
    };

    [Fact]
    public void Only_domain_rounds_amounts()
    {
        var offenders =
            from layer in new[] { Layers.Application, Layers.Infrastructure, Layers.Api }
            from call in RoundingScanner.Scan(layer)
            where !RoundingAllowed.ContainsKey(call.Caller)
            select $"{call.Caller}: {call.Call}";

        AssertNone(offenders,
            "Totals are computed only by Domain: call the order's pricing method instead of rounding here " +
            "(or add the method to RoundingAllowed with a reason)");
    }

    [Fact]
    public void Domain_and_application_signatures_use_money_not_raw_decimal()
    {
        var offenders =
            from layer in new[] { Layers.Domain, Layers.Application }
            from type in layer.GetTypes()
            where IsVisible(type) && !RawDecimalAllowed.ContainsKey(type)
            from member in RawDecimalMembers(type)
            select $"{type.FullName}.{member}";

        AssertNone(offenders, "Take Money or Percentage instead of raw decimal (or add the type to RawDecimalAllowed with a reason)");
    }

    [Fact]
    public void Repositories_return_domain_types_not_application_types()
    {
        var offenders =
            from type in Layers.Application.GetTypes()
            where type.IsInterface && type.Name.EndsWith("Repository", StringComparison.Ordinal)
            from method in type.GetMethods()
            where Flatten(method.ReturnType).Any(t => t.Assembly == Layers.Application)
            select $"{type.FullName}.{method.Name}";

        AssertNone(offenders, "Repositories return domain entities; build read models in Application");
    }

    private static void AssertNone(IEnumerable<string> offenders, string fix)
    {
        var list = offenders.ToList();
        Assert.True(list.Count == 0, $"{fix}:{Environment.NewLine}{string.Join(Environment.NewLine, list)}");
    }

    private static IEnumerable<string> RawDecimalMembers(Type type)
    {
        foreach (var field in type.GetFields(Declared).Where(f => IsVisible(f.IsPublic, f.IsFamily, f.IsFamilyOrAssembly)))
        {
            if (ContainsDecimal(field.FieldType)) yield return field.Name;
        }

        foreach (var property in type.GetProperties(Declared))
        {
            var getter = property.GetMethod ?? property.SetMethod!;
            if (IsVisible(getter.IsPublic, getter.IsFamily, getter.IsFamilyOrAssembly) && ContainsDecimal(property.PropertyType))
                yield return property.Name;
        }

        foreach (var method in type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)))
        {
            if (method.IsSpecialName && method is not ConstructorInfo) continue;
            if (!IsVisible(method.IsPublic, method.IsFamily, method.IsFamilyOrAssembly)) continue;

            var returnType = (method as MethodInfo)?.ReturnType;
            if ((returnType is not null && ContainsDecimal(returnType))
                || method.GetParameters().Any(p => ContainsDecimal(p.ParameterType)))
                yield return method.Name;
        }
    }

    private static bool IsVisible(Type type) =>
        (type.IsPublic || type.IsNestedPublic) && !type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute));

    private static bool IsVisible(bool isPublic, bool isFamily, bool isFamilyOrAssembly) =>
        isPublic || isFamily || isFamilyOrAssembly;

    private static bool ContainsDecimal(Type type) => Flatten(type).Any(t => t == typeof(decimal));

    /// <summary>The type itself plus element types and generic arguments, e.g. Task&lt;decimal?&gt; → decimal.</summary>
    private static IEnumerable<Type> Flatten(Type type)
    {
        if (type.IsByRef || type.IsArray || type.IsPointer)
        {
            foreach (var inner in Flatten(type.GetElementType()!)) yield return inner;
            yield break;
        }

        yield return type;
        if (!type.IsGenericType) yield break;

        foreach (var argument in type.GetGenericArguments())
        foreach (var inner in Flatten(argument))
            yield return inner;
    }
}
