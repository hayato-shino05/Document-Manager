using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class RecycleBinReproTests
{
    private sealed class FakeRecycleRepo : IRecycleBinRepository
    {
        private readonly List<StudyDocument> _docs;
        public FakeRecycleRepo(List<StudyDocument> docs) => _docs = docs;
        public List<StudyDocument> GetDeletedDocuments() => _docs;
        public bool RestoreDocument(int id) => false;
        public bool PermanentDeleteDocument(int id) => false;
        public int EmptyRecycleBin() => 0;
        public int GetDeletedDocumentCount() => _docs.Count;
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void RecycleBin_Renders_Without_Binding_Loop()
    {
        var services = App.Services!;
        var dialog = services.GetRequiredService<IDialogService>();
        var loc = services.GetRequiredService<ILocalizationService>();

        var docs = new List<StudyDocument>
        {
            new() { Id = 1, Name = "doc-word", Type = "Word", Subject = "Project", CreatedAt = new DateTime(2026, 8, 12, 10, 22, 14) },
            new() { Id = 2, Name = "doc-ppt", Type = "PowerPoint", Subject = "", CreatedAt = new DateTime(2026, 8, 12, 10, 22, 42) },
            new() { Id = 3, Name = "doc-pdf", Type = "PDF", Subject = "ImportSubject", CreatedAt = new DateTime(2026, 8, 17, 5, 52, 13) },
        };

        var model = new RecycleBinModel(new FakeRecycleRepo(docs), dialog, loc);
        Assert.Equal(3, model.DeletedDocuments.Count);

        var view = new RecycleBin { DataContext = model };
        var window = new Window { Content = view };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, model.DeletedDocuments.Count);
        window.Close();
    }
}
