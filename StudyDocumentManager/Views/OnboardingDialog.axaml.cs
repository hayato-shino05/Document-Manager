using Avalonia.Controls;
using Avalonia.Interactivity;
using StudyDocumentManager.Models;

namespace StudyDocumentManager.Views;

public partial class OnboardingDialog : Window
{
    public OnboardingDialog()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            var target = this.FindControl<Button>("NextButton")
                         ?? this.FindControl<Button>("FinishButton")
                         ?? this.FindControl<Button>("SkipButton");
            target?.Focus();
        };
    }

    public OnboardingDialog(OnboardingModel model) : this()
    {
        DataContext = model;
        model.Completed += OnCompleted;
    }

    private void OnCompleted(object? sender, EventArgs e)
    {
        if (sender is OnboardingModel model)
            model.Completed -= OnCompleted;
        Close();
    }
}
