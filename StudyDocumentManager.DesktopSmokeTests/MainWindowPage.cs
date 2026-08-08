using FlaUI.Core.AutomationElements;
using Xunit;

namespace StudyDocumentManager.DesktopSmokeTests;

public sealed class MainWindowPage
{
    private readonly Window _window;

    public MainWindowPage(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public AutomationElement FindByAutomationId(string automationId)
    {
        if (string.IsNullOrWhiteSpace(automationId))
            throw new ArgumentException("Automation id は空にできません。", nameof(automationId));

        var element = _window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
        Assert.True(
            element is not null && element.IsAvailable,
            $"Automation id '{automationId}' が window '{_window.Title}' に見つかりません。");
        return element!;
    }

    public void InvokeButton(string automationId)
    {
        FindByAutomationId(automationId).AsButton().Invoke();
    }

    public void NavigateByAutomationId(string automationId)
    {
        InvokeButton(automationId);
    }

    public void AssertRootVisible(string automationId)
    {
        var element = FindByAutomationId(automationId);
        Assert.False(
            element.Properties.IsOffscreen.Value,
            $"Automation id '{automationId}' が window '{_window.Title}' に表示されていません。");
    }

    public AutomationElement WaitForAutomationId(string automationId, TimeSpan? timeout = null, bool requireVisible = false)
    {
        var waitTimeout = timeout ?? TimeSpan.FromSeconds(5);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Exception? lastError = null;
        while (stopwatch.Elapsed < waitTimeout)
        {
            try
            {
                var element = _window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                if (element is not null
                    && element.IsAvailable
                    && (!requireVisible || !element.Properties.IsOffscreen.Value))
                    return element;
            }
            catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
            {
                lastError = ex;
            }

            Task.Delay(100).GetAwaiter().GetResult();
        }

        throw new TimeoutException(
            $"Automation id '{automationId}' が {waitTimeout.TotalSeconds:0} 秒以内に表示されませんでした。{lastError?.Message}");
    }
}
