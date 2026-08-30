using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Models;

public partial class OnboardingModel : ModelBase
{
    public const string CompletionKey = "onboarding_completed";

    private readonly ISettingsService _settingsService;

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
    private void Skip() => Complete();

    [RelayCommand]
    private void Finish() => Complete();

    private void Complete()
    {
        _settingsService.SetSetting(CompletionKey, "true");
        Completed?.Invoke(this, EventArgs.Empty);
    }
}
