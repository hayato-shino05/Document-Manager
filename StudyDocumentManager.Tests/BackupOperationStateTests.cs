using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Core.Services;
using Xunit;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Tests;

public sealed class BackupOperationStateTests
{
    [Fact]
    public async Task BackupAsync_TracksNonBlockingStateAndResetsAfterSuccess()
    {
        var backup = new ControlledBackupService { BackupResult = (true, "backup.db", null) };
        var model = CreateModel(backup);

        var operation = model.BackupDatabaseCommand.ExecuteAsync(null);
        await backup.Started.Task;

        Assert.True(model.IsBackingUp);
        Assert.False(model.IsRestoring);
        Assert.Equal(0, model.BackupProgress);

        backup.Release();
        await operation;

        Assert.False(model.IsBackingUp);
        Assert.Equal(100, model.BackupProgress);
        Assert.Empty(model.BackupError);
    }

    [Fact]
    public async Task BackupAsync_FailureExposesErrorAndResetsState()
    {
        var backup = new ControlledBackupService { BackupResult = (false, null, "disk full") };
        var model = CreateModel(backup);

        var operation = model.BackupDatabaseCommand.ExecuteAsync(null);
        await backup.Started.Task;
        backup.Release();
        await operation;

        Assert.False(model.IsBackingUp);
        Assert.Equal(0, model.BackupProgress);
        Assert.Equal("Dashboard_BackupFailed", model.BackupError);
    }

    [Fact]
    public async Task BackupAsync_ExceptionUsesLocalizedErrorAndResetsState()
    {
        var backup = new ControlledBackupService
        {
            ExceptionToThrow = new IOException("C:\\private\\study.db; SELECT * FROM documents")
        };
        var model = CreateModel(backup);

        var operation = model.BackupDatabaseCommand.ExecuteAsync(null);
        await backup.Started.Task;
        backup.Release();
        await operation;

        Assert.False(model.IsBackingUp);
        Assert.Equal(0, model.BackupProgress);
        Assert.Equal("Dashboard_BackupFailed", model.BackupError);
        Assert.DoesNotContain("study.db", model.BackupError);
        Assert.DoesNotContain("documents", model.BackupError);
    }

    [Fact]
    public async Task BackupAsync_CancelAfterCompletionWinsAndKeepsSuccessState()
    {
        var backup = new ControlledBackupService { BackupResult = (true, "backup.db", null) };
        var model = CreateModel(backup);
        var operation = model.BackupDatabaseCommand.ExecuteAsync(null);
        await backup.Started.Task;

        model.CancelBackupCommand.Execute(null);
        backup.Release();
        await operation;

        Assert.False(model.IsBackingUp);
        Assert.False(model.BackupCancelled);
        Assert.Equal(100, model.BackupProgress);
        Assert.Empty(model.BackupError);
    }

    [Fact]
    public async Task RestoreAsync_CancelAfterCompletionWinsAndKeepsSuccessState()
    {
        var backup = new ControlledBackupService { RestoreResult = (true, null), BlockRestore = true };
        var model = CreateModel(backup);
        var operation = model.RestoreDatabaseCommand.ExecuteAsync(null);
        await backup.Started.Task;

        model.CancelRestoreCommand.Execute(null);
        backup.Release();
        await operation;

        Assert.False(model.IsRestoring);
        Assert.False(model.RestoreCancelled);
        Assert.Equal(100, model.RestoreProgress);
        Assert.Empty(model.RestoreError);
    }

    [Fact]
    public async Task RestoreAsync_ExceptionUsesLocalizedErrorAndResetsState()
    {
        var backup = new ControlledBackupService
        {
            ExceptionToThrow = new InvalidOperationException("C:\\private\\database.db; SQL error"),
            BlockRestore = true
        };
        var model = CreateModel(backup);
        var operation = model.RestoreDatabaseCommand.ExecuteAsync(null);
        await backup.Started.Task;
        backup.Release();
        await operation;

        Assert.False(model.IsRestoring);
        Assert.Equal(0, model.RestoreProgress);
        Assert.Equal("Dashboard_RestoreFailed", model.RestoreError);
        Assert.DoesNotContain("database.db", model.RestoreError);
        Assert.DoesNotContain("SQL error", model.RestoreError);
    }

    [Fact]
    public async Task RestoreAsync_SuccessCompletesWithoutError()
    {
        var backup = new ControlledBackupService { RestoreResult = (true, null) };
        var model = CreateModel(backup);

        await model.RestoreDatabaseCommand.ExecuteAsync(null);

        Assert.False(model.IsRestoring);
        Assert.Equal(100, model.RestoreProgress);
        Assert.Empty(model.RestoreError);
    }

    [Fact]
    public async Task RestoreAsync_FailureExposesErrorAndResetsState()
    {
        var backup = new ControlledBackupService { RestoreResult = (false, "invalid backup") };
        var model = CreateModel(backup);

        await model.RestoreDatabaseCommand.ExecuteAsync(null);

        Assert.False(model.IsRestoring);
        Assert.Equal(0, model.RestoreProgress);
        Assert.Equal("Dashboard_RestoreFailed", model.RestoreError);
    }

    private static DashboardModel CreateModel(IBackupService backup)
        => new(
            null!, null!, null!, null!, null!, new TestDialog(), null!, null!, null!, null!, null!, null!, backup,
            new TestLocalization());

    private sealed class ControlledBackupService : IBackupService
    {
        public (bool Success, string? Path, string? Error) BackupResult { get; set; }
        public (bool Success, string? Error) RestoreResult { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public bool BlockRestore { get; set; }
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<(bool Success, string? Path, string? Error)> BackupAsync(CancellationToken cancellationToken)
            => BackupAsync(cancellationToken, BackupResult);

        private async Task<(bool Success, string? Path, string? Error)> BackupAsync(
            CancellationToken cancellationToken,
            (bool Success, string? Path, string? Error) result)
        {
            Started.TrySetResult(true);
            await _release.Task;
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;
            return result;
        }

        public async Task<(bool Success, string? Path, string? Error)> BackupAsync()
        {
            Started.TrySetResult(true);
            await _release.Task;
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;
            return BackupResult;
        }

        public Task<(bool Success, string? Error)> RestoreAsync(CancellationToken cancellationToken)
            => RestoreAsync(cancellationToken, RestoreResult);

        private async Task<(bool Success, string? Error)> RestoreAsync(
            CancellationToken cancellationToken,
            (bool Success, string? Error) result)
        {
            Started.TrySetResult(true);
            if (!BlockRestore)
                _release.TrySetResult(true);
            await _release.Task;
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;
            return result;
        }

        public async Task<(bool Success, string? Error)> RestoreAsync()
        {
            Started.TrySetResult(true);
            if (!BlockRestore)
                _release.TrySetResult(true);
            await _release.Task;
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;
            return RestoreResult;
        }

        public void Release() => _release.TrySetResult(true);
    }

    private sealed class TestDialog : IDialogService
    {
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(true);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
    }

    private sealed class TestLocalization : ILocalizationService
    {
        public string this[string key] => key;
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [];
        public event EventHandler? LanguageChanged { add { } remove { } }
        public void SetLanguage(SupportedLanguage language) { }
    }
}
