using System.Diagnostics;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public class ProcessLauncherService : IProcessLauncherService
{
    private readonly IPlatformInfo _platformInfo;
    private readonly Action<ProcessStartInfo> _startProcess;

    public ProcessLauncherService(IPlatformInfo platformInfo)
        : this(platformInfo, processStartInfo => Process.Start(processStartInfo))
    {
    }

    internal ProcessLauncherService(IPlatformInfo platformInfo, Action<ProcessStartInfo> startProcess)
    {
        _platformInfo = platformInfo ?? throw new ArgumentNullException(nameof(platformInfo));
        _startProcess = startProcess ?? throw new ArgumentNullException(nameof(startProcess));
    }

    public void OpenFile(string filePath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = true
        });
    }

    public void RevealInExplorer(string filePath)
    {
        if (_platformInfo.IsLinux)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                StartLinuxTarget(directory);
            return;
        }

        if (File.Exists(filePath))
        {
            _startProcess(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\""
            });
        }
        else
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                _startProcess(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{dir}\""
                });
        }
    }

    public void OpenFolder(string folderPath)
    {
        if (_platformInfo.IsLinux)
        {
            StartLinuxTarget(folderPath);
            return;
        }

        if (Directory.Exists(folderPath))
        {
            _startProcess(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
        }
    }

    public void OpenUrl(string url)
    {
        if (_platformInfo.IsLinux)
        {
            StartLinuxTarget(url);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private void StartLinuxTarget(string target)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "xdg-open",
            UseShellExecute = false
        };
        processStartInfo.ArgumentList.Add(target);
        _startProcess(processStartInfo);
    }
}
