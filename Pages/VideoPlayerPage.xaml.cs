using LibVLCSharp.Shared;
using LibVLCSharp.MAUI;
using System.Timers;
using MAXTV.Services;

namespace MAXTV.Pages;

[QueryProperty(nameof(VideoUrl), "VideoUrl")]
[QueryProperty(nameof(ContentType), "ContentType")]
public partial class VideoPlayerPage : ContentPage
{
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private string _videoUrl = string.Empty;

    private string _contentType = "movie";
    public string ContentType
    {
        get => _contentType;
        set => _contentType = string.IsNullOrWhiteSpace(value) ? "movie" : value.ToLowerInvariant();
    }

    private readonly System.Timers.Timer _hideControlsTimer;

    // Buffering simulation
    private readonly System.Timers.Timer _bufferingTimer;
    private double _simulatedProgress = 0.0;
    private double _realProgress = 0.0;

    private int _bufferMs = 120000;

    public string VideoUrl
    {
        get => _videoUrl;
        set
        {
            _videoUrl = value ?? string.Empty;
            PlayVideo();
        }
    }

    public VideoPlayerPage()
    {
        InitializeComponent();

        _hideControlsTimer = new System.Timers.Timer(3000);
        _hideControlsTimer.AutoReset = false;
        _hideControlsTimer.Elapsed += OnHideTimerElapsed;

        _bufferingTimer = new System.Timers.Timer(100);
        _bufferingTimer.AutoReset = true;
        _bufferingTimer.Elapsed += OnBufferingTimerElapsed;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        DeviceDisplay.Current.KeepScreenOn = true;

        ResetHideTimer();
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(500);
            PlayPauseButton.Focus();
        });
    }

    private int ResolveBufferMs() =>
        ContentType switch
        {
            "live" => AppSettings.LiveBufferMs,
            "series" => AppSettings.SeriesBufferMs,
            _ => AppSettings.MovieBufferMs
        };

    private void PlayVideo()
    {
        if (string.IsNullOrWhiteSpace(_videoUrl))
            return;

        Stop();

        _bufferMs = ResolveBufferMs();
        StartBuffering();

        _libVLC = new LibVLC(
            enableDebugLogs: true,
            $"--network-caching={_bufferMs}",
            "--clock-jitter=0",
            "--clock-synchro=0"
        );

        _mediaPlayer = new MediaPlayer(_libVLC);
        videoView.MediaPlayer = _mediaPlayer;

        _mediaPlayer.SeekableChanged += OnSeekableChanged;
        _mediaPlayer.Opening += (s, e) => StartBuffering();
        _mediaPlayer.Playing += (s, e) => StopBuffering();
        _mediaPlayer.EncounteredError += (s, e) => StopBuffering();

        _mediaPlayer.Buffering += (s, e) =>
        {
            _realProgress = e.Cache / 100.0;
            UpdateBufferingUI();
            UpdateBufferInfo();
        };

        var media = new Media(_libVLC, new Uri(_videoUrl));
        media.AddOption($":network-caching={_bufferMs}");
        media.AddOption($":live-caching={_bufferMs}");
        media.AddOption($":file-caching={_bufferMs}");
        media.AddOption($":disc-caching={_bufferMs}");

        _mediaPlayer.Play(media);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            BufferInfoLabel.IsVisible = AppSettings.ShowBufferDebug;
        });
    }

    private void StartBuffering()
    {
        _simulatedProgress = 0.0;
        _realProgress = 0.0;
        _bufferingTimer.Start();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            BufferingLayout.IsVisible = true;
            BufferingProgressBar.Progress = 0.0;
        });
    }

    private void StopBuffering()
    {
        _bufferingTimer.Stop();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            BufferingProgressBar.Progress = 1.0;
            BufferingLayout.IsVisible = false;
        });
    }

    private void OnBufferingTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_simulatedProgress < 0.95)
        {
            var steps = Math.Max(1.0, _bufferMs / 100.0);
            _simulatedProgress += 0.95 / steps;
            if (_simulatedProgress > 0.95)
                _simulatedProgress = 0.95;
        }

        UpdateBufferingUI();
        UpdateBufferInfo();
    }

    private void UpdateBufferingUI()
    {
        double displayProgress = Math.Max(_realProgress, _simulatedProgress);
        if (displayProgress > 1.0) displayProgress = 1.0;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (BufferingLayout.IsVisible)
                BufferingProgressBar.Progress = displayProgress;
        });
    }

    private void UpdateBufferInfo()
    {
        if (!AppSettings.ShowBufferDebug)
            return;

        double progress = Math.Max(_realProgress, _simulatedProgress);
        int bufferedSeconds = (int)((_bufferMs / 1000.0) * progress);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            BufferInfoLabel.Text = $"Buffer: ~{bufferedSeconds} sec";
        });
    }

    private void OnSeekableChanged(object? sender, MediaPlayerSeekableChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            bool canSeek = e.Seekable != 0;
            RewindButton.IsVisible = canSeek;
            FastForwardButton.IsVisible = canSeek;
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        DeviceDisplay.Current.KeepScreenOn = false;
        Stop();
    }

    private void Stop()
    {
        _hideControlsTimer?.Stop();
        _bufferingTimer?.Stop();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            BufferInfoLabel.IsVisible = false;
        });

        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _mediaPlayer = null;

        _libVLC?.Dispose();
        _libVLC = null;
    }

    // --- Controls (unchanged) ---

    private void OnOverlayInputClicked(object sender, EventArgs e)
    {
        if (ControlsGrid.IsVisible)
            HideControls();
        else
            ShowControls();
    }

    private void ShowControls()
    {
        ControlsGrid.IsVisible = true;
        ResetHideTimer();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100);
            PlayPauseButton.Focus();
        });
    }

    private void HideControls()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ControlsGrid.IsVisible = false;
            OverlayInputButton.Focus();
        });
        _hideControlsTimer.Stop();
    }

    private void ResetHideTimer()
    {
        _hideControlsTimer.Stop();
        _hideControlsTimer.Start();
    }

    private void OnHideTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        HideControls();
    }

    private void OnPlayPauseClicked(object sender, EventArgs e)
    {
        ResetHideTimer();

        if (_mediaPlayer == null) return;

        if (_mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Pause();
            PlayPauseButton.Text = "Play";
        }
        else
        {
            _mediaPlayer.Play();
            PlayPauseButton.Text = "Pause";
        }
    }

    private async void OnStopClicked(object sender, EventArgs e)
    {
        Stop();
        await Shell.Current.GoToAsync("..");
    }

    private void OnRewindClicked(object sender, EventArgs e)
    {
        ResetHideTimer();
        if (_mediaPlayer?.IsSeekable == true)
            _mediaPlayer.Time = Math.Max(0, _mediaPlayer.Time - 10000);
    }

    private void OnFastForwardClicked(object sender, EventArgs e)
    {
        ResetHideTimer();
        if (_mediaPlayer?.IsSeekable == true)
            _mediaPlayer.Time = Math.Min(_mediaPlayer.Length, _mediaPlayer.Time + 30000);
    }
}
