namespace StudyDocumentManager.DesktopSmokeTests;

[Collection(DesktopTestCollection.Name)]
public sealed class DesktopAppFixtureTests
{
    private readonly DesktopAppFixture _fixture;

    public DesktopAppFixtureTests(DesktopAppFixture fixture) => _fixture = fixture;

    [Fact]
    public void 起動後にメインウィンドウとシェルルートを取得できる()
    {
        Assert.NotNull(_fixture.App);
        Assert.NotNull(_fixture.Window);
        Assert.True(_fixture.Window.IsAvailable);
        _fixture.MainWindow.AssertRootVisible("Screen_Dashboard");
    }
}
