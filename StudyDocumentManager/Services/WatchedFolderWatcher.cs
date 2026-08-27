using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Timers;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

/// <summary>
/// 設定された 1 フォルダを監視し、新しく見つかったファイルを Import Inbox へ
/// Pending として引き渡す。元ファイルの移動・削除は行わない。検出はデバウンス・
/// 統合され、デバウンス窓内の連続変更イベントはパスごとに 1 回の投入にまとまり、
/// inbox リポジトリがソースパスで重複排除するため再スキャンも安全である。
/// </summary>
public sealed class WatchedFolderWatcher : IWatchedFolderWatcher
{
    private readonly WatchedFolder _config;
    private readonly IImportInboxRepository _inbox;
    private readonly IWatchedFolderRepository _folders;
    private readonly IFileSystemWatcherAdapterFactory _adapterFactory;
    private readonly ILog _log;
    private readonly TimeSpan _debounce;

    private IFileSystemWatcherAdapter? _adapter;
    private System.Timers.Timer? _timer;
    private readonly HashSet<string> _buffer = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _failedPaths = new();
    private readonly object _sync = new();
    private readonly object _failSync = new();
    // Start/Stop 遷移全体を直列化する。これにより 2 回目の Start が重複したアダプタを
    // 生成する（最初のものをリークする）ことや、並行する Stop が IsRunning==false を
    // 見た直後に Start が実行中のウォッチャを公開するのを防ぐ。タイマの Elapsed コールバックも
    // このロックを取得するため、先に始まった Stop は（世代を進め、アダプタを破棄してからでないと）
    // コールバックを実行できず、Stop 前に発火したキュー済みコールバックが停止後に状態を
    // 変更することはない。
    private readonly object _lifecycle = new();
    // Start ごと、および Stop ごとにインクリメントする。これにより Stop 完了前にすでに
    // ディスパッチされていたタイマコールバックは、自分が古いことを検出して処理をスキップできる。
    private int _generation;
    // 終端フラグ。設定は Dispose のみ。Stop() は可逆（後で Start できる）であるため _disposed はセットしない。
    private bool _disposed;

    public bool IsRunning { get; private set; }

    public IReadOnlyList<string> FailedPaths
    {
        get { lock (_failSync) return new List<string>(_failedPaths); }
    }

    public event EventHandler? AdapterError;

    public WatchedFolderWatcher(
        WatchedFolder config,
        IImportInboxRepository inbox,
        IWatchedFolderRepository folders,
        IFileSystemWatcherAdapterFactory adapterFactory,
        ILog log,
        TimeSpan? debounce = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
        _folders = folders ?? throw new ArgumentNullException(nameof(folders));
        _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _debounce = debounce ?? TimeSpan.FromMilliseconds(800);
    }

    public void Start()
    {
        if (!_config.Enabled)
        {
            _log.Information($"Watched folder '{_config.FolderPath}' is disabled; watcher not started.");
            return;
        }
        if (!Directory.Exists(_config.FolderPath))
        {
            _log.Warning($"Watched folder '{_config.FolderPath}' does not exist; watcher not started.");
            return;
        }

        // 開始遷移全体は _lifecycle で直列化される：アダプタ生成と実行状態の設定は、任意の
        // 並行する Start/Stop に対して原子的に行われる。そのため重複アダプタはリークせず、
        // 先に始まった Stop が常に勝つ。同じインスタンスでの再起動もサポートする：Stop() は
        // オブジェクトを破棄せず一時停止のみなので、新しい Start は実行状態をリセットし世代を
        // 進め、古いタイマコールバックを無効化して新しいタイマが正しい世代を観測する。
        // _disposed の判定もロック内で行う。Dispose が先に完了してからこの Start が実行された
        // 場合でも、ロック内の判定により破棄後のアダプタ生成・開始を確実に回避する。
        lock (_lifecycle)
        {
            if (_disposed)
                return;
            if (IsRunning)
                return;

            IFileSystemWatcherAdapter? adapter = null;
            try
            {
                adapter = _adapterFactory.Create(_config.FolderPath, _config.IncludeSubdirectories);
                adapter.FileCreated += OnFileCreated;
                adapter.WatcherError += OnAdapterError;
                // Create はブロックする可能性があり、その間に並行する Dispose が _disposed を
                // セットしてロック待ちになることがある。生成後に再判定し、破棄後にアダプタを
                // 開始・公開しないよう、生成済みのアダプタを破棄して戻る。
                if (_disposed)
                {
                    adapter.Dispose();
                    return;
                }
                adapter.Start();
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to start watcher for '{_config.FolderPath}'.", ex);
                adapter?.Dispose();
                return;
            }

            var timer = new System.Timers.Timer(_debounce.TotalMilliseconds) { AutoReset = false };
            var gen = Interlocked.Increment(ref _generation);
            // このコールバックは Stop 実行前にすでにスレッドプールへキューされている可能性がある。
            // OnTimerElapsed は _lifecycle を取得し世代を照合するため、古いコールバックは
            // ウォッチャ停止後に状態を変更できない。
            timer.Elapsed += (_, _) => OnTimerElapsed(gen);
            timer.Start();

            lock (_sync)
            {
                _adapter = adapter;
                _timer = timer;
                _buffer.Clear();
                IsRunning = true;
            }

            // 単一ユーザフォルダの上限付きスキャン。_lifecycle 内で実行するため並行する Stop が
            // 割り込むことはない。ScanNow は内部で細粒度の _sync のみを取得し _lifecycle は取得
            // しないためデッドロックしない。
            ScanNow();
        }
    }

    /// <summary>
    /// バッファされた変更のデバウンス処理を実行する。_lifecycle で排他し、Stop() と互斥とする。
    /// 先に始まった Stop は（世代を進め、アダプタを破棄してからでないと）このコールバックを
    /// 実行できないため、Stop 前に発火したキュー済みコールバックが停止後にインボックスや
    /// フォルダ状態を変更することはない。
    /// </summary>
    internal void OnTimerElapsed(int generation)
    {
        lock (_lifecycle)
        {
            if (generation == _generation && !_disposed)
                ProcessBufferedChanges();
        }
    }

    internal int CurrentGeneration => _generation;

    internal WatcherStatus CurrentStatus => _config.WatcherStatus;

    private void OnFileCreated(object? sender, FileSystemWatcherActivityEventArgs e) => BufferPath(e.FullPath);

    /// <summary>
    /// パスが設定ルート（サブフォルダ監視時はその配下）内にあるかを判定する。
    /// ルート外のパスは投入しない。ソースの移動・削除は行わず、安全性を維持する。
    /// </summary>
    private bool IsWithinRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        try
        {
            var root = Path.GetFullPath(_config.FolderPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var full = Path.GetFullPath(path);
            if (full.Length <= root.Length)
                return false;
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return false;
            var sep = full[root.Length];
            if (sep != Path.DirectorySeparatorChar && sep != Path.AltDirectorySeparatorChar)
                return false;
            if (!_config.IncludeSubdirectories)
            {
                var sub = full[(root.Length + 1)..];
                if (sub.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                    sub.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
                    return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"Watched folder could not resolve path '{path}'.", ex);
            return false;
        }
    }

    private void BufferPath(string path)
    {
        if (_disposed || string.IsNullOrWhiteSpace(path))
            return;
        // 設定ルート外のパスは投入しない（ソースの移動・削除は行わず安全性を維持する）。
        if (!IsWithinRoot(path))
            return;
        // ロック内でタイマを取得する。これにより並行する Stop() が _timer を破棄・null 化しても、
        // null やすでに破棄されたタイマを参照する（以前は NRE / ObjectDisposedException を投げていた）
        // ことがない。
        System.Timers.Timer? timer;
        lock (_sync)
        {
            _buffer.Add(path);
            timer = _timer;
        }
        if (timer is null)
            return;
        try
        {
            timer.Stop();
            timer.Start();
        }
        catch (ObjectDisposedException)
        {
            // Stop() と競合した。ウォッチャは停止中のため再開は不要。
        }
    }

    /// <summary>
    /// バッファされたパスを Import Inbox へ Pending として投入する。ロック中/不在/権限不足の
    /// ファイルに対して安全：各失敗は後で再試行するために記録され、残りのバッチを中断せずに
    /// ログ出力される。
    /// </summary>
    public void ProcessBufferedChanges()
    {
        // 破棄後のみブロックする。手動 ScanNow は停止中でも投入する契約を維持する。
        // 本番のタイマコールバックは世代＋_lifecycle でゲートされ、Stop 後に自動投入されることはない。
        if (_disposed)
            return;
        List<string> paths;
        lock (_sync)
        {
            paths = new List<string>(_buffer);
            _buffer.Clear();
        }

        foreach (var path in paths)
        {
            // 防御層：バッファ内にもルート外パスが混ざらないよう再確認。
            if (!IsWithinRoot(path))
                continue;
            try
            {
                if (!File.Exists(path))
                    continue;
                _inbox.Add(new ImportInboxItem
                {
                    SourcePath = path,
                    DisplayName = Path.GetFileName(path),
                    State = ImportInboxState.Pending
                });
            }
            catch (IOException ex)
            {
                _log.Warning($"Watched folder could not read '{path}'.", ex);
                RecordFailed(path);
            }
            catch (UnauthorizedAccessException ex)
            {
                _log.Warning($"Watched folder access denied for '{path}'.", ex);
                RecordFailed(path);
            }
            catch (Exception ex)
            {
                _log.Error($"Watched folder failed to enqueue '{path}'.", ex);
                RecordFailed(path);
            }
        }

        try
        {
            _folders.RecordScan(_config.Id, DateTime.Now);
        }
        catch (Exception ex)
        {
            _log.Warning("Failed to record watched folder scan time.", ex);
        }
    }

    /// <summary>
    /// 設定フォルダの一回限りのスキャン。見つかったファイルをバッファに入れて引き渡す。
    /// 初回のキャッチアップおよび再起動後に使用する。
    /// </summary>
    public void ScanNow()
    {
        if (!Directory.Exists(_config.FolderPath))
            return;
        string[] files;
        try
        {
            var option = _config.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            files = Directory.GetFiles(_config.FolderPath, "*", option);
        }
        catch (Exception ex)
        {
            _log.Warning($"Watched folder scan failed for '{_config.FolderPath}'.", ex);
            return;
        }

        foreach (var file in files)
            BufferPath(file);
        ProcessBufferedChanges();
    }

    private void OnAdapterError(object? sender, FileSystemWatcherErrorEventArgs e)
    {
        _log.Error($"Watcher error for '{_config.FolderPath}'.", e.Exception);
        AdapterError?.Invoke(this, EventArgs.Empty);
    }

    private void RecordFailed(string path)
    {
        lock (_failSync)
        {
            if (!_failedPaths.Contains(path))
                _failedPaths.Add(path);
        }
    }

    /// <summary>
    /// 過去に失敗したパスを再度投入を試みる。成功したパスは失敗リストから削除する。
    /// それでも失敗するパス（ロック中/不在/権限不足/その他）は、後で再試行できるよう保持し、
    /// 失敗はログに記録される（飲み込まない）。フォルダ状態は未処理の失敗が残っているかを反映する。
    /// </summary>
    public void RetryEnqueues()
    {
        // 破棄後のみブロックする。手動リトライは停止中でも再試行する契約を維持する。
        if (_disposed)
            return;

        List<string> paths;
        lock (_failSync)
        {
            paths = new List<string>(_failedPaths);
        }
        if (paths.Count == 0)
            return;

        var stillFailing = new List<string>();
        foreach (var path in paths)
        {
            try
            {
                if (!File.Exists(path))
                {
                    // 元ファイルが消えたため、このパスの再試行は不要。
                    _log.Information($"Watched folder retry skipped missing file '{path}'.");
                    lock (_failSync) _failedPaths.Remove(path);
                    continue;
                }
                _inbox.Add(new ImportInboxItem
                {
                    SourcePath = path,
                    DisplayName = Path.GetFileName(path),
                    State = ImportInboxState.Pending
                });
                lock (_failSync) _failedPaths.Remove(path);
            }
            catch (Exception ex)
            {
                // 失敗をログに記録し、パスを保持する。これにより後続の再試行で再投入を
                // 試せるようにし、ユーザのファイルを黙って破棄しない。
                _log.Error($"Watched folder retry failed for '{path}'.", ex);
                stillFailing.Add(path);
                lock (_failSync)
                {
                    if (!_failedPaths.Contains(path))
                        _failedPaths.Add(path);
                }
            }
        }

        if (stillFailing.Count > 0)
        {
            _config.WatcherStatus = WatcherStatus.Error;
        }
        else
        {
            _config.WatcherStatus = IsRunning ? WatcherStatus.Running : WatcherStatus.Stopped;
            _config.WatcherError = null;
            _config.WatcherErrorKey = null;
        }

        try
        {
            _folders.RecordScan(_config.Id, DateTime.Now);
        }
        catch (Exception ex)
        {
            _log.Warning("Failed to record watched folder scan time.", ex);
        }
    }

    public void Stop()
    {
        System.Timers.Timer? timer;
        IFileSystemWatcherAdapter? adapter;
        lock (_lifecycle)
        {
            if (!IsRunning)
                return;
            // 保留中のタイマコールバックを無効化：以前の世代を持つキュー済みコールバックは不一致を
            // 検出して処理をスキップする。ここでは _disposed をセットしない — Stop は可逆であり、
            // 後で Start で再実行できる。
            Interlocked.Increment(ref _generation);
            lock (_sync)
            {
                timer = _timer;
                _timer = null;
                adapter = _adapter;
                _adapter = null;
                _buffer.Clear();
            }
            // 停止状態を先に確定する。以降の解体で例外が起きても IsRunning は false のままであり、
            // 後続の処理が残りのリソースをスキップしない。
            IsRunning = false;
        }
        // 各リソースを独立した try/catch で解体する。1 件が例外を投げても残りの解体は
        // 続行され、いずれかの失敗は安全なコンテキストのみをログに記録する。
        if (timer is not null)
        {
            try { timer.Stop(); }
            catch (Exception ex) { _log.Warning("Failed to stop watcher timer.", ex); }
            try { timer.Dispose(); }
            catch (Exception ex) { _log.Warning("Failed to dispose watcher timer.", ex); }
        }
        if (adapter is not null)
        {
            try { adapter.FileCreated -= OnFileCreated; }
            catch (Exception ex) { _log.Warning("Failed to detach watcher file events.", ex); }
            try { adapter.WatcherError -= OnAdapterError; }
            catch (Exception ex) { _log.Warning("Failed to detach watcher error events.", ex); }
            try { adapter.Stop(); }
            catch (Exception ex) { _log.Warning("Failed to stop watcher adapter.", ex); }
            try { adapter.Dispose(); }
            catch (Exception ex) { _log.Warning("Failed to dispose watcher adapter.", ex); }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Interlocked.Increment(ref _generation);
        Stop();
        GC.SuppressFinalize(this);
    }
}
