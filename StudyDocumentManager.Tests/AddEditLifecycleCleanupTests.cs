using Avalonia.Controls;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class AddEditLifecycleCleanupTests
{
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void AddEdit_Detach_UnsubscribesLocalizationAndValidationHandlers()
    {
        var localization = new TrackingLocalizationService();
        var model = CreateModel(localization);
        var view = new StudyDocumentManager.Views.AddEdit { DataContext = model };
        var window = new Avalonia.Controls.Window { Content = view };

        try
        {
            window.Show();
            FlushAvaloniaBindings();
            var nameBox = view.FindControl<TextBox>("txtName")!;

            Assert.Equal(1, localization.SubscriberCount);
            var initialTitle = model.PageTitle;

            var replacement = CreateModel(localization);
            view.DataContext = replacement;
            model.HasNameValidationError = true;

            Assert.Equal(1, localization.SubscriberCount);
            Assert.NotSame(nameBox, Avalonia.Controls.TopLevel.GetTopLevel(view)?.FocusManager?.GetFocusedElement());

            window.Close();
            Assert.Equal(0, localization.SubscriberCount);

            localization.RaiseLanguageChanged();
            Assert.Equal(initialTitle, model.PageTitle);
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void AddEdit_ReassigningDetachedModel_ResynchronizesLocalization()
    {
        var localization = new TrackingLocalizationService();
        var model = CreateModel(localization);
        var view = new StudyDocumentManager.Views.AddEdit { DataContext = model };
        var window = new Avalonia.Controls.Window { Content = view };

        try
        {
            window.Show();
            FlushAvaloniaBindings();

            model.DetachLocalization();
            model.IsEditing = true;
            view.DataContext = null;
            view.DataContext = model;

            Assert.Equal("AddEdit_PageTitleEdit", model.PageTitle);
            Assert.Equal(1, localization.SubscriberCount);
        }
        finally
        {
            window.Close();
        }
    }

    private static AddEditModel CreateModel(ILocalizationService localization)
        => new(null!, new CategoryRepositoryStub(), null!, null!, null!, localization);

    private static void FlushAvaloniaBindings()
        => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private sealed class TrackingLocalizationService : ILocalizationService
    {
        private EventHandler? _languageChanged;

        public string this[string key] => key;
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public IReadOnlyList<SupportedLanguage> AvailableLanguages => [SupportedLanguage.Japanese];
        public int SubscriberCount => _languageChanged?.GetInvocationList().Length ?? 0;
        public event EventHandler? LanguageChanged
        {
            add => _languageChanged += value;
            remove => _languageChanged -= value;
        }

        public void SetLanguage(SupportedLanguage language) { }
        public void RaiseLanguageChanged() => _languageChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class CategoryRepositoryStub : ICategoryRepository
    {
        public List<string> GetAllSubjects() => [];
        public List<string> GetAllTypes() => [];
        public List<(string Name, int Count)> GetSubjectsWithCount() => [];
        public List<(string Name, int Count)> GetTypesWithCount() => [];
        public bool AddSubject(string name) => false;
        public bool AddType(string name) => false;
        public bool UpdateSubjectName(string oldName, string newName) => false;
        public bool UpdateTypeName(string oldName, string newName) => false;
        public bool DeleteDocumentsBySubject(string subjectName) => false;
        public bool DeleteDocumentsByType(string typeName) => false;
        public int GetTotalDocumentCount() => 0;
    }
}
