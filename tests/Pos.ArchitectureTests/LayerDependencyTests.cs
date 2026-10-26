using System.Reflection;
using NetArchTest.Rules;

namespace Pos.ArchitectureTests;

public class LayerDependencyTests
{
    [Fact]
    public void Domain_depends_on_no_other_layer_or_framework()
    {
        AssertNoDependency(Layers.Domain,
            "Pos.Application", "Pos.Infrastructure", "Pos.Api",
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "FluentValidation");
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_api()
    {
        AssertNoDependency(Layers.Application,
            "Pos.Infrastructure", "Pos.Api", "Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore");
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        AssertNoDependency(Layers.Infrastructure, "Pos.Api", "Microsoft.AspNetCore");
    }

    private static void AssertNoDependency(Assembly layer, params string[] forbidden)
    {
        var result = Types.InAssembly(layer).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        Assert.True(result.IsSuccessful,
            $"{layer.GetName().Name} must not depend on [{string.Join(", ", forbidden)}]. Offending types: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
