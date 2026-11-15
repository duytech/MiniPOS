using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Pos.ArchitectureTests;

/// <summary>
/// Finds calls in an assembly's IL, including calls inside lambdas and async methods, which reflection over signatures
/// does not see. Each call is reported with the method written in source that makes it.
/// </summary>
internal static class CallScanner
{
    private static readonly HashSet<string> RoundingTypes = ["System.Decimal", "System.Math", "System.MathF"];

    private static readonly HashSet<string> RoundingMethods = ["Round", "Floor", "Ceiling", "Truncate"];

    private static readonly HashSet<string> MoneyOperators = ["op_Addition", "op_Subtraction", "op_Multiply"];

    /// <summary>Round, Floor, Ceiling or Truncate on decimal or Math: rounding an amount is part of pricing.</summary>
    public static bool IsRounding(MethodReference called) =>
        RoundingTypes.Contains(called.DeclaringType.FullName) && RoundingMethods.Contains(called.Name);

    /// <summary>Money's +, - and * operators: adding up amounts outside Domain re-implements a total.</summary>
    public static bool IsMoneyArithmetic(MethodReference called) =>
        called.DeclaringType.FullName == typeof(Pos.Domain.Common.Money).FullName && MoneyOperators.Contains(called.Name);

    /// <summary>A call whose value is a Result: the method's return value, or the awaited value of a Task of Result.</summary>
    public static bool IsResultReturning(MethodReference called) =>
        IsResult(called.ReturnType)
        || (called.Name == "GetResult" && called.DeclaringType is GenericInstanceType awaiter
            && awaiter.GenericArguments.Any(IsResult));

    /// <summary>Each matching call as (<c>Type.FullName.Method</c> of the caller in source, called method).</summary>
    public static IEnumerable<(string Caller, string Call)> Scan(Assembly assembly, Func<MethodReference, bool> matches) =>
        Scan(assembly, (called, _) => matches(called));

    /// <summary>
    /// Each call to a method returning a Result whose value is thrown away straight after the call.
    /// shortcut: a Result stored in a variable that is never read is not found, move to a Roslyn analyzer if that bites.
    /// </summary>
    public static IEnumerable<(string Caller, string Call)> ScanDiscardedResults(Assembly assembly) =>
        Scan(assembly, (called, instruction) => IsResultReturning(called) && NextReal(instruction)?.OpCode.Code == Code.Pop);

    private static IEnumerable<(string Caller, string Call)> Scan(
        Assembly assembly, Func<MethodReference, Instruction, bool> matches)
    {
        using var module = ModuleDefinition.ReadModule(assembly.Location);

        foreach (var type in module.GetTypes())
        foreach (var method in type.Methods.Where(m => m.HasBody))
        foreach (var instruction in method.Body.Instructions)
        {
            if (instruction.OpCode.Code is Code.Call or Code.Callvirt or Code.Ldftn
                && instruction.Operand is MethodReference called
                && matches(called, instruction))
            {
                yield return ($"{SourceType(type).FullName}.{SourceMethodName(type, method)}", called.FullName);
            }
        }
    }

    private static bool IsResult(TypeReference type) =>
        type.GetElementType().FullName is "Pos.Domain.Common.Result" or "Pos.Domain.Common.Result`1";

    /// <summary>Debug builds put nops between instructions.</summary>
    private static Instruction? NextReal(Instruction instruction)
    {
        var next = instruction.Next;
        while (next?.OpCode.Code == Code.Nop) next = next.Next;
        return next;
    }

    /// <summary>The type written in source: lambdas and async state machines are nested compiler-generated types.</summary>
    private static TypeDefinition SourceType(TypeDefinition type)
    {
        while (type.DeclaringType is not null && type.Name.StartsWith('<')) type = type.DeclaringType;
        return type;
    }

    /// <summary>
    /// The method written in source: <c>&lt;HandleAsync&gt;d__1.MoveNext</c> (async) and <c>&lt;HandleAsync&gt;b__0</c>
    /// (lambda) → <c>HandleAsync</c>; other methods keep their name.
    /// </summary>
    private static string SourceMethodName(TypeDefinition type, MethodDefinition method) =>
        Unmangle(method.Name) ?? Unmangle(type.Name) ?? method.Name;

    private static string? Unmangle(string name)
    {
        var end = name.IndexOf('>', StringComparison.Ordinal);
        return name.StartsWith('<') && end > 1 ? name[1..end] : null;
    }
}
