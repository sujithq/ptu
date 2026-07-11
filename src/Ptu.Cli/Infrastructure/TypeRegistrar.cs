using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Ptu.Cli.Infrastructure;

/// <summary>Bridges Spectre.Console.Cli type registration onto Microsoft.Extensions.DependencyInjection.</summary>
public sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
    public ITypeResolver Build() => new TypeResolver(services.BuildServiceProvider());

    public void Register(Type service, Type implementation) => services.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation) => services.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory) => services.AddSingleton(service, _ => factory());
}
