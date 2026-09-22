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
    public async Task BackupAsync_CancelAfterCompletionNotified_KeepsSuccessState()
    {
        var backup = new ControlledBackupService { BackupResult = (true, "backup.db", null), GateCancelCheck = true };
        var model = CreateModel(backup);
        var operation = model.BackupDatabaseCommand.ExecuteAsync(null);
        await backup.Started.Task;
        backup.Release();
        await backup.CompletionNotified.Task;

        model.CancelBackupCommand.Execute(null);
        backup.CancelGate.TrySetResult(true);
        await operation;

        Assert.True(backup.ObservedCancelAfterRelease);
        Assert.False(model.IsBackingUp);
        Assert.False(model.BackupCancelled);
        Assert.Equal(100, model.BackupProgress);
        Assert.Empty(model.BackupError);
    }

    [Fact]
    public async Task RestoreAsync_CancelAfterCompletionNotified_KeepsSuccessState()
    {
        var backup = new ControlledBackupService { RestoreResult = (true, null), BlockRestore = true, GateCancelCheck = true };
        var model = CreateModel(backup);
        var operation = model.RestoreDatabaseCommand.ExecuteAsync(null);
        await backup.Started.Task;
        backup.Release();
        await backup.CompletionNotified.Task;

        model.CancelRestoreCommand.Execute(null);
        backup.CancelGate.TrySetResult(true);
        await operation;

        Assert.True(backup.ObservedCancelAfterRelease);
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
    public async Task BackupAsync_CancelDuringOperation_ReportsCancelledWithoutError()
    {
        var backup = new ControlledBackupService { BackupResult = (true, "backup.db", null), ObserveCancelAfterRelease = true };
        var model = CreateModel(backup);

        var operation = model.BackupDatabaseCommand.ExecuteAsync(null);
        await backup.Started.Task;

        model.CancelBackupCommand.Execute(null);
        backup.Release();
        await operation;

        Assert.True(backup.ObservedCancelAfterRelease);
        Assert.False(model.IsBackingUp);
        Assert.True(model.BackupCancelled);
        Assert.Equal(0, model.BackupProgress);
        Assert.Empty(model.BackupError);
    }

    [Fact]
    public async Task RestoreAsync_CancelDuringOperation_ReportsCancelledWithoutError()
    {
        var backup = new ControlledBackupService { RestoreResult = (true, null), BlockRestore = true, ObserveCancelAfterRelease = true };
        var model = CreateModel(backup);

        var operation = model.RestoreDatabaseCommand.ExecuteAsync(null);
        await backup.Started.Task;

        model.CancelRestoreCommand.Execute(null);
        backup.Release();
        await operation;

        Assert.True(backup.ObservedCancelAfterRelease);
        Assert.False(model.IsRestoring);
        Assert.True(model.RestoreCancelled);
        Assert.Equal(0, model.RestoreProgress);
        Assert.Empty(model.RestoreError);
    }

    [Fact]
    public async Task BackupService_ObservesTokenBeforeStart_WhenAlreadyCancelled()
    {
        var backup = new ControlledBackupService { BackupResult = (true, "backup.db", null) };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => backup.BackupAsync(new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task RestoreService_ObservesTokenBeforeStart_WhenAlreadyCancelled()
    {
        var backup = new ControlledBackupService { RestoreResult = (true, null) };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => backup.RestoreAsync(new CancellationToken(canceled: true)));
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
        public bool ObserveCancelAfterRelease { get; set; }
        public bool ObservedCancelAfterRelease { get; private set; }
        public CancellationToken CapturedBackupToken { get; private set; }
        public CancellationToken CapturedRestoreToken { get; private set; }
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> CompletionNotified { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> CancelGate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool GateCancelCheck { get; set; }
        private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<(bool Success, string? Path, string? Error)> BackupAsync(CancellationToken cancellationToken)
            => BackupAsync(cancellationToken, BackupResult);

        private async Task<(bool Success, string? Path, string? Error)> BackupAsync(
            CancellationToken cancellationToken,
            (bool Success, string? Path, string? Error) result)
        {
            CapturedBackupToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            Started.TrySetResult(true);
            await _release.Task;
            CompletionNotified.TrySetResult(true);
            if (GateCancelCheck)
                await CancelGate.Task;
            ObservedCancelAfterRelease = cancellationToken.IsCancellationRequested;
            if (ObserveCancelAfterRelease && cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;
            return result;
        }

        public async Task<(bool Success, string? Path, string? Error)> BackupAsync()
        {
            Started.TrySetResult(true);
            await _release.Task;
            CompletionNotified.TrySetResult(true);
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
            CapturedRestoreToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            Started.TrySetResult(true);
            if (!BlockRestore)
                _release.TrySetResult(true);
            await _release.Task;
            CompletionNotified.TrySetResult(true);
            if (GateCancelCheck)
                await CancelGate.Task;
            ObservedCancelAfterRelease = cancellationToken.IsCancellationRequested;
            if (ObserveCancelAfterRelease && cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();
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
            CompletionNotified.TrySetResult(true);
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

    [Fact]
    public void DashboardModel_Dispose_UnsubscribesLanguageChanged()
    {
        var loc = new CountingLocalization();
        var model = new DashboardModel(
            null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, loc);

        Assert.Equal(1, loc.SubscriberCount);
        model.Dispose();
        Assert.Equal(0, loc.SubscriberCount);
        loc.Raise();
    }

    [Fact]
    public async Task Dispose_CancelsInFlightBackupTokenAndResetsState()
    {
        var backup = new ControlledBackupService();
        var model = CreateModel(backup);

        var operation = model.BackupDatabaseCommand.ExecuteAsync(null);
        await backup.Started.Task;

        Assert.False(backup.CapturedBackupToken.IsCancellationRequested);

        model.Dispose();

        Assert.True(backup.CapturedBackupToken.IsCancellationRequested);

        backup.Release();
        await operation;

        Assert.True(model.BackupCancelled);
        Assert.False(model.IsBackingUp);
        Assert.Empty(model.BackupError);
    }

    [Fact]
    public async Task Dispose_CancelsInFlightRestoreTokenAndResetsState()
    {
        var backup = new ControlledBackupService { BlockRestore = true };
        var model = CreateModel(backup);

        var operation = model.RestoreDatabaseCommand.ExecuteAsync(null);
        await backup.Started.Task;

        Assert.False(backup.CapturedRestoreToken.IsCancellationRequested);

        model.Dispose();

        Assert.True(backup.CapturedRestoreToken.IsCancellationRequested);

        backup.Release();
        await operation;

        Assert.True(model.RestoreCancelled);
        Assert.False(model.IsRestoring);
        Assert.Empty(model.RestoreError);
    }

    [Fact]
    public async Task Dispose_AllowsSubsequentBackupAfterCancellation()
    {
        var backup = new ControlledBackupService();
        var model = CreateModel(backup);

        var first = model.BackupDatabaseCommand.ExecuteAsync(null);
        await backup.Started.Task;
        model.Dispose();
        backup.Release();
        await first;
        Assert.True(model.BackupCancelled);

        var second = new ControlledBackupService { BackupResult = (true, "backup.db", null) };
        var fresh = CreateModel(second);
        var op = fresh.BackupDatabaseCommand.ExecuteAsync(null);
        await second.Started.Task;
        second.Release();
        await op;

        Assert.False(fresh.IsBackingUp);
        Assert.False(fresh.BackupCancelled);
        Assert.Equal(100, fresh.BackupProgress);
    }

    [Fact]
    public void Dispose_IsSafeToCallMultipleTimes()
    {
        var model = new DashboardModel(
            null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, new TestLocalization());

        var ex = Record.Exception(() =>
        {
            model.Dispose();
            model.Dispose();
            model.Dispose();
        });

        Assert.Null(ex);
    }

    private sealed class CountingLocalization : ILocalizationService
    {
        private EventHandler? _handler;
        public int SubscriberCount { get; private set; }
        public string this[string key] => key;
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [];
        public event EventHandler? LanguageChanged
        {
            add { _handler += value; SubscriberCount++; }
            remove { _handler -= value; SubscriberCount--; }
        }
        public void SetLanguage(SupportedLanguage language) { }
        public void Raise() => _handler?.Invoke(this, EventArgs.Empty);
    }
}
