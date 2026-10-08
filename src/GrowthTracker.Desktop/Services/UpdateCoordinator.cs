using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GrowthTracker.Application.Abstractions;
using Serilog;

namespace GrowthTracker.Desktop.Services;

public enum UpdateState { Unsupported, Idle, Checking, UpToDate, Downloading, ReadyToRestart, Failed }

/// <summary>
/// Auto-update flow (TDD §8.3): shortly after start-up and then twice a day it checks GitHub Releases in the
/// background, downloads the update (a delta when possible), and offers "Khởi động lại" in the window banner,
/// the Settings page and an OS notification. Nothing is installed until the user restarts.
/// </summary>
public sealed partial class UpdateCoordinator(IUpdateService updates, AppPreferences preferences, INotifier notifier)
    : ObservableObject
{
    public static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);

    private CancellationTokenSource? _loop;

    /// <summary>Raised after the updater is told to apply the update; the shell then quits the app.</summary>
    public event EventHandler? RestartRequested;

    public string CurrentVersion => updates.CurrentVersion;
    public string VersionText => $"Phiên bản {CurrentVersion}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(IsReady), nameof(IsBusy), nameof(IsChecking), nameof(CanCheck))]
    public partial UpdateState State { get; private set; } = updates.IsSupported ? UpdateState.Idle : UpdateState.Unsupported;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(BannerText))]
    public partial string? AvailableVersion { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial int DownloadPercent { get; private set; }

    public bool IsReady => State == UpdateState.ReadyToRestart;
    public bool IsBusy => State is UpdateState.Checking or UpdateState.Downloading;
    public bool IsChecking => State == UpdateState.Checking;
    public bool CanCheck => State is not (UpdateState.Unsupported or UpdateState.Checking or UpdateState.Downloading or UpdateState.ReadyToRestart);

    public string BannerText => $"Đã tải xong bản cập nhật {AvailableVersion}. Khởi động lại để cài đặt — dữ liệu của bạn được giữ nguyên.";

    public string StatusText => State switch
    {
        UpdateState.Unsupported => "Bản chạy thử (không cài bằng bộ cài) nên không tự cập nhật.",
        UpdateState.Checking => "Đang kiểm tra cập nhật…",
        UpdateState.UpToDate => "Bạn đang dùng bản mới nhất.",
        UpdateState.Downloading => $"Đang tải bản {AvailableVersion}… {DownloadPercent}%",
        UpdateState.ReadyToRestart => $"Đã tải bản {AvailableVersion}. Khởi động lại để cài đặt.",
        UpdateState.Failed => "Không kiểm tra được cập nhật. Kiểm tra kết nối mạng rồi thử lại.",
        _ => "Ứng dụng tự kiểm tra cập nhật khi mở và mỗi 12 giờ.",
    };

    /// <summary>Starts the periodic background check (skipped when the user turned it off or the build is not installed).</summary>
    public void StartBackgroundChecks()
    {
        if (!updates.IsSupported || _loop is not null)
            return;
        _loop = new CancellationTokenSource();
        _ = RunLoopAsync(_loop.Token);
    }

    public void StopBackgroundChecks()
    {
        _loop?.Cancel();
        _loop = null;
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(FirstCheckDelay, ct);
            while (!ct.IsCancellationRequested && !IsReady)
            {
                if (preferences.Current.AutoCheckUpdates)
                    await CheckAndDownloadAsync(manual: false, ct);
                await Task.Delay(CheckInterval, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand]
    private Task CheckNow() => CheckAndDownloadAsync(manual: true);

    /// <summary>Checks the feed and, when there is a newer version, downloads it so a restart can install it.</summary>
    public async Task CheckAndDownloadAsync(bool manual, CancellationToken ct = default)
    {
        if (!CanCheck)
            return;
        State = UpdateState.Checking;
        try
        {
            string? version = await updates.CheckAsync(preferences.Current.IncludePrerelease, ct);
            if (version is null)
            {
                State = UpdateState.UpToDate;
                return;
            }

            AvailableVersion = version;
            DownloadPercent = 0;
            State = UpdateState.Downloading;
            await updates.DownloadAsync(p => Dispatcher.UIThread.Post(() => DownloadPercent = p), ct);
            DownloadPercent = 100;
            State = UpdateState.ReadyToRestart;
            if (!manual)
                await notifier.NotifyAsync($"Có bản cập nhật {version}", "Khởi động lại Growth Tracker để cài đặt.", ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            State = UpdateState.Idle;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Update check failed");
            State = UpdateState.Failed;
        }
    }

    [RelayCommand]
    private void Restart()
    {
        if (!IsReady)
            return;
        try
        {
            updates.ApplyOnExitAndRestart();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not hand the update to the updater");
            State = UpdateState.Failed;
            return;
        }
        RestartRequested?.Invoke(this, EventArgs.Empty);
    }
}
