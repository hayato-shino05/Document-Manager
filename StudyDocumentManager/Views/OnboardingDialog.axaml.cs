using Avalonia.Controls;
using Avalonia.Interactivity;
using StudyDocumentManager.Models;

namespace StudyDocumentManager.Views;

public partial class OnboardingDialog : Window
{
    public OnboardingDialog()
    {
        InitializeComponent();
        Opened += (_, _) => this.FindControl<Button>("FinishButton")?.Focus();
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
