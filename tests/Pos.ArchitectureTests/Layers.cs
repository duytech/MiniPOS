using System.Reflection;
using Pos.Application.Common;
using Pos.Domain.Common;

namespace Pos.ArchitectureTests;

internal static class Layers
{
    public static readonly Assembly Domain = typeof(Money).Assembly;
    public static readonly Assembly Application = typeof(ICommandHandler<,>).Assembly;
    public static readonly Assembly Infrastructure = typeof(Pos.Infrastructure.DependencyInjection).Assembly;
    public static readonly Assembly Api = typeof(Program).Assembly;

    public static readonly Assembly[] All = [Domain, Application, Infrastructure, Api];
}
