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

    [Fact]
    public void Step_navigation_walks_through_all_5_steps_and_completes()
    {
        var settings = new InMemorySettingsService();
        var model = new OnboardingModel(settings);
        var completed = 0;
        model.Completed += (_, _) => completed++;

        Assert.Equal(0, model.CurrentStepIndex);
        Assert.Equal(5, model.TotalSteps);
        Assert.False(model.CanGoPrevious);
        Assert.True(model.CanGoNext);
        Assert.False(model.IsLastStep);
        Assert.Equal("1 / 5", model.StepNumberText);
        Assert.Equal(20.0, model.StepProgress);
        Assert.True(model.IsStep0);

        // Previous on step 0 does nothing
        model.PreviousStepCommand.Execute(null);
        Assert.Equal(0, model.CurrentStepIndex);

        // Step 1
        model.NextStepCommand.Execute(null);
        Assert.Equal(1, model.CurrentStepIndex);
        Assert.True(model.CanGoPrevious);
        Assert.True(model.CanGoNext);
        Assert.True(model.IsStep1);
        Assert.Equal("2 / 5", model.StepNumberText);
        Assert.Equal(40.0, model.StepProgress);

        // Step 2
        model.NextStepCommand.Execute(null);
        Assert.Equal(2, model.CurrentStepIndex);
        Assert.True(model.IsStep2);

        // Step 3
        model.NextStepCommand.Execute(null);
        Assert.Equal(3, model.CurrentStepIndex);
        Assert.True(model.IsStep3);

        // Step 4 (Last Step)
        model.NextStepCommand.Execute(null);
        Assert.Equal(4, model.CurrentStepIndex);
        Assert.True(model.IsStep4);
        Assert.False(model.CanGoNext);
        Assert.True(model.IsLastStep);
        Assert.Equal("5 / 5", model.StepNumberText);
        Assert.Equal(100.0, model.StepProgress);

        // Next on last step completes
        model.NextStepCommand.Execute(null);
        Assert.Equal(1, completed);
        Assert.Equal("true", settings.GetSetting(OnboardingModel.CompletionKey));
    }

    [Fact]
    public void GoToStep_jumps_to_valid_indices()
    {
        var settings = new InMemorySettingsService();
        var model = new OnboardingModel(settings);

        model.GoToStepCommand.Execute(3);
        Assert.Equal(3, model.CurrentStepIndex);
        Assert.True(model.IsStep3);

        model.GoToStepCommand.Execute(10); // Out of bounds
        Assert.Equal(3, model.CurrentStepIndex);

        model.GoToStepCommand.Execute(-1); // Out of bounds
        Assert.Equal(3, model.CurrentStepIndex);

        model.GoToStepCommand.Execute(0);
        Assert.Equal(0, model.CurrentStepIndex);
        Assert.True(model.IsStep0);
    }
}
