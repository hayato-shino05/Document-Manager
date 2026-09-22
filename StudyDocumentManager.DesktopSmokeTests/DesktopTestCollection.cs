using Xunit;

namespace StudyDocumentManager.DesktopSmokeTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DesktopTestCollection : ICollectionFixture<DesktopAppFixture>
{
    public const string Name = "Desktop application";
}
