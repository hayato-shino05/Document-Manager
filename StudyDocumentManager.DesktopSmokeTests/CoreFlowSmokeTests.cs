namespace StudyDocumentManager.DesktopSmokeTests;

[Collection(DesktopTestCollection.Name)]
public sealed class CoreFlowSmokeTests
{
    private readonly DesktopAppFixture _fixture;

    public CoreFlowSmokeTests(DesktopAppFixture fixture) => _fixture = fixture;

    [Fact]
    public void メインシェルの分類メニューが存在する()
    {
        var menuIds = new[]
        {
            "Menu_File",
            "Menu_Organize",
            "Menu_Import",
            "Menu_Maintenance",
            "Menu_Analytics",
            "Menu_Help"
        };

        foreach (var menuId in menuIds)
            Assert.True(_fixture.MainWindow.FindByAutomationId(menuId).IsAvailable);
    }

    [Fact]
    public void ツールバーの追加とインポートが画面遷移できる()
    {
        _fixture.MainWindow.InvokeButton("Toolbar_Add");
        _fixture.MainWindow.WaitForAutomationId("Screen_AddEdit");
        _fixture.MainWindow.WaitForAutomationId("Toolbar_Back", requireVisible: true);
        _fixture.MainWindow.InvokeButton("Toolbar_Back");
        _fixture.MainWindow.AssertRootVisible("Screen_Dashboard");

        _fixture.MainWindow.InvokeButton("Toolbar_Import");
        _fixture.MainWindow.WaitForAutomationId("Screen_BatchImport");
        _fixture.MainWindow.WaitForAutomationId("Toolbar_Back", requireVisible: true);
        _fixture.MainWindow.InvokeButton("Toolbar_Back");
        _fixture.MainWindow.AssertRootVisible("Screen_Dashboard");

        _fixture.MainWindow.InvokeButton("Toolbar_Report");
        _fixture.MainWindow.WaitForAutomationId("Screen_Report");
        _fixture.MainWindow.WaitForAutomationId("Toolbar_Back", requireVisible: true);
        _fixture.MainWindow.InvokeButton("Toolbar_Back");
        _fixture.MainWindow.AssertRootVisible("Screen_Dashboard");

        _fixture.MainWindow.InvokeButton("Toolbar_TreeMap");
        _fixture.MainWindow.WaitForAutomationId("Screen_TreeMap");
        _fixture.MainWindow.WaitForAutomationId("Toolbar_Back", requireVisible: true);
        _fixture.MainWindow.InvokeButton("Toolbar_Back");
        _fixture.MainWindow.AssertRootVisible("Screen_Dashboard");
    }

    [Fact]
    public void 更新ボタンを起動してもアプリケーションが利用可能なままである()
    {
        _fixture.MainWindow.InvokeButton("Toolbar_Refresh");

        Assert.True(_fixture.App.HasExited is false);
        Assert.True(_fixture.Window.IsAvailable);
        _fixture.MainWindow.AssertRootVisible("Screen_Dashboard");
    }
}
