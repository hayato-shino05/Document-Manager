using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Models;

public partial class OnboardingModel : ModelBase
{
    public const string CompletionKey = "onboarding_completed";
    public const int TotalStepsCount = 5;

    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    [NotifyPropertyChangedFor(nameof(StepNumberText))]
    [NotifyPropertyChangedFor(nameof(StepProgress))]
    [NotifyPropertyChangedFor(nameof(IsStep0))]
    [NotifyPropertyChangedFor(nameof(IsStep1))]
    [NotifyPropertyChangedFor(nameof(IsStep2))]
    [NotifyPropertyChangedFor(nameof(IsStep3))]
    [NotifyPropertyChangedFor(nameof(IsStep4))]
    private int _currentStepIndex;

    [ObservableProperty]
    private int _selectedTabIndex;

    public int TotalSteps => TotalStepsCount;
    public bool CanGoPrevious => CurrentStepIndex > 0;
    public bool CanGoNext => CurrentStepIndex < TotalStepsCount - 1;
    public bool IsLastStep => CurrentStepIndex == TotalStepsCount - 1;
    public string StepNumberText => $"{CurrentStepIndex + 1} / {TotalStepsCount}";
    public double StepProgress => (double)(CurrentStepIndex + 1) / TotalStepsCount * 100.0;

    public bool IsStep0 => CurrentStepIndex == 0;
    public bool IsStep1 => CurrentStepIndex == 1;
    public bool IsStep2 => CurrentStepIndex == 2;
    public bool IsStep3 => CurrentStepIndex == 3;
    public bool IsStep4 => CurrentStepIndex == 4;

    public bool ShouldShow => !string.Equals(
        _settingsService.GetSetting(CompletionKey),
        "true",
        StringComparison.OrdinalIgnoreCase);

    public event EventHandler? Completed;

    public OnboardingModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [RelayCommand]
    private void NextStep()
    {
        if (CurrentStepIndex < TotalStepsCount - 1)
        {
            CurrentStepIndex++;
        }
        else
        {
            Complete();
        }
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStepIndex > 0)
        {
            CurrentStepIndex--;
        }
    }

    [RelayCommand]
    private void GoToStep(int stepIndex)
    {
        if (stepIndex >= 0 && stepIndex < TotalStepsCount)
        {
            CurrentStepIndex = stepIndex;
        }
    }

    [RelayCommand]
    private void Skip() => Complete();

    [RelayCommand]
    private void Finish() => Complete();

    private void Complete()
    {
        _settingsService.SetSetting(CompletionKey, "true");
        Completed?.Invoke(this, EventArgs.Empty);
    }
}
