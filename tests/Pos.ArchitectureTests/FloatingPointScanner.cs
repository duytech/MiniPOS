using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Pos.ArchitectureTests;

/// <summary>
/// Finds every use of double/float/Half in an assembly's IL: signatures, locals, constants, conversions and calls.
/// NetArchTest only sees type references in signatures, so <c>(double)amount</c> inside a method body passes it.
/// </summary>
internal static class FloatingPointScanner
{
    private static readonly HashSet<string> FloatingPointTypes = ["System.Double", "System.Single", "System.Half"];

    private static readonly HashSet<Code> FloatingPointOpCodes =
        [Code.Conv_R4, Code.Conv_R8, Code.Conv_R_Un, Code.Ldc_R4, Code.Ldc_R8];

    public static IEnumerable<string> Scan(Assembly assembly)
    {
        using var module = ModuleDefinition.ReadModule(assembly.Location);

        // ToList: the module is disposed when this method returns.
        return module.GetTypes().SelectMany(ScanType).ToList();
    }

    private static IEnumerable<string> ScanType(TypeDefinition type)
    {
        foreach (var field in type.Fields.Where(f => IsFloatingPoint(f.FieldType)))
            yield return $"{type.FullName}.{field.Name}: field of type {field.FieldType.Name}";

        foreach (var finding in type.Methods.SelectMany(ScanMethod))
            yield return finding;
    }

    private static IEnumerable<string> ScanMethod(MethodDefinition method)
    {
        var where = $"{method.DeclaringType.FullName}.{method.Name}";

        if (IsFloatingPoint(method.ReturnType) || method.Parameters.Any(p => IsFloatingPoint(p.ParameterType)))
            yield return $"{where}: signature";

        if (!method.HasBody) yield break;

        if (method.Body.Variables.Any(v => IsFloatingPoint(v.VariableType)))
            yield return $"{where}: local variable";

        foreach (var instruction in method.Body.Instructions.Where(UsesFloatingPoint))
            yield return $"{where}: {instruction.OpCode} {instruction.Operand}";
    }

    private static bool UsesFloatingPoint(Instruction instruction) =>
        FloatingPointOpCodes.Contains(instruction.OpCode.Code) || instruction.Operand switch
        {
            MethodReference m => IsFloatingPoint(m.ReturnType) || m.Parameters.Any(p => IsFloatingPoint(p.ParameterType)),
            FieldReference f => IsFloatingPoint(f.FieldType),
            TypeReference t => IsFloatingPoint(t),
            _ => false,
        };

    private static bool IsFloatingPoint(TypeReference type) => type switch
    {
        GenericInstanceType generic => generic.GenericArguments.Any(IsFloatingPoint),
        TypeSpecification specification => IsFloatingPoint(specification.ElementType),
        _ => FloatingPointTypes.Contains(type.FullName),
    };
}
