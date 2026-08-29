using Xunit;
using StudyDocumentManager.Models;
using StudyDocumentManager.Tests.TestDoubles;

namespace StudyDocumentManager.Tests;

public sealed class OnboardingModelTests
{
    [Fact]
    public void Missing_or_non_true_setting_requires_onboarding()
    {
        var settings = new InMemorySettingsService();
        var model = new OnboardingModel(settings);

        Assert.True(model.ShouldShow);

        settings.SetSetting(OnboardingModel.CompletionKey, "false");
        Assert.True(new OnboardingModel(settings).ShouldShow);
    }

    [Fact]
    public void Help_reentry_does_not_reset_completed_state()
    {
        var settings = new InMemorySettingsService();
        settings.SetSetting(OnboardingModel.CompletionKey, "true");

        var reentered = new OnboardingModel(settings);

        Assert.False(reentered.ShouldShow);
        Assert.Equal("true", settings.GetSetting(OnboardingModel.CompletionKey));
    }

    [Fact]
    public void Language_setting_change_does_not_reset_onboarding_completion()
    {
        var settings = new InMemorySettingsService();
        var model = new OnboardingModel(settings);
        model.FinishCommand.Execute(null);

        settings.SetSetting("language", "English");

        Assert.False(new OnboardingModel(settings).ShouldShow);
        Assert.Equal("true", settings.GetSetting(OnboardingModel.CompletionKey));
    }

    [Theory]
    [InlineData("SkipCommand")]
    [InlineData("FinishCommand")]
    public void Completing_onboarding_persists_true_and_raises_event(string commandName)
    {
        var settings = new InMemorySettingsService();
        var model = new OnboardingModel(settings);
        var completed = 0;
        model.Completed += (_, _) => completed++;

        var command = commandName == "SkipCommand" ? model.SkipCommand : model.FinishCommand;
        command.Execute(null);

        Assert.Equal("true", settings.GetSetting(OnboardingModel.CompletionKey));
        Assert.Equal(1, completed);
        Assert.False(new OnboardingModel(settings).ShouldShow);
    }
}
