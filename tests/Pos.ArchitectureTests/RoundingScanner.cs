using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Pos.ArchitectureTests;

/// <summary>
/// Finds every call to a rounding method on decimal or Math (Round, Floor, Ceiling, Truncate) in an assembly's IL,
/// including calls inside lambdas and async methods. Rounding an amount is part of pricing, which only Domain does.
/// </summary>
internal static class RoundingScanner
{
    private static readonly HashSet<string> RoundingTypes = ["System.Decimal", "System.Math", "System.MathF"];

    private static readonly HashSet<string> RoundingMethods = ["Round", "Floor", "Ceiling", "Truncate"];

    /// <summary>Each call as <c>Declaring.Type.Method: called method</c>, where the declaring type is the source type.</summary>
    public static IEnumerable<(string Caller, string Call)> Scan(Assembly assembly)
    {
        using var module = ModuleDefinition.ReadModule(assembly.Location);

        foreach (var type in module.GetTypes())
        foreach (var method in type.Methods.Where(m => m.HasBody))
        foreach (var instruction in method.Body.Instructions)
        {
            if (instruction.OpCode.Code is Code.Call or Code.Callvirt or Code.Ldftn
                && instruction.Operand is MethodReference called
                && RoundingTypes.Contains(called.DeclaringType.FullName)
                && RoundingMethods.Contains(called.Name))
            {
                yield return ($"{SourceType(type).FullName}.{SourceMethodName(type, method)}", called.FullName);
            }
        }
    }

    /// <summary>The type written in source: lambdas and async state machines are nested compiler-generated types.</summary>
    private static TypeDefinition SourceType(TypeDefinition type)
    {
        while (type.DeclaringType is not null && type.Name.StartsWith('<')) type = type.DeclaringType;
        return type;
    }

    /// <summary><c>&lt;HandleAsync&gt;d__1.MoveNext</c> → <c>HandleAsync</c>; other methods keep their name.</summary>
    private static string SourceMethodName(TypeDefinition type, MethodDefinition method) =>
        type.Name.StartsWith('<') && type.Name.IndexOf('>', StringComparison.Ordinal) > 1
            ? type.Name[1..type.Name.IndexOf('>', StringComparison.Ordinal)]
            : method.Name;
}
