using Xunit;

namespace PhieuFlow.Tests.E2E.Infrastructure;

/// <summary>
/// Binds <see cref="AppHostFixture"/> to a single xUnit collection so the topology and
/// browser start once for the whole assembly. Every E2E test class is
/// <c>[Collection(E2ECollection.Name)]</c>.
/// </summary>
[CollectionDefinition(Name)]
public sealed class E2ECollection : ICollectionFixture<AppHostFixture>
{
    public const string Name = "e2e";
}
