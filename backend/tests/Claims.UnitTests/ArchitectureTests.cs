using System.Reflection;

namespace Claims.UnitTests;

public sealed class ArchitectureTests
{
    [Theory]
    [InlineData("Claims.Domain")]
    [InlineData("Claims.Application", "Claims.Domain")]
    public void Layer_references_only_inner_layers(string assemblyName, params string[] allowed)
    {
        var claimsReferences = Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("Claims.", StringComparison.Ordinal));

        claimsReferences.ShouldBeSubsetOf(allowed);
    }
}
