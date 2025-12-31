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

    // identifies which buffer to use: live/movie/series
    private string _contentType = "movie";
    public string ContentType
    {
        get => _contentType;
        set
        {
            _contentType = string.IsNullOrWhiteSpace(value)
                ? "movie"
                : value.ToLowerInvariant();

            if (ReadyToPlay)
                PlayVideo();
        }
    }

    private bool ReadyToPlay =>
        !string.IsNullOrWhiteSpace(_videoUrl) &&
        !string.IsNullOrWhiteSpace(_contentType);

    // Timers (explicit types to avoid ambiguity with System.Threading.Timer)
    private readonly System.Timers.Timer _hideControlsTimer;
    private readonly System.Timers.Timer _bufferingTimer;

    // Buffer UI tracking
    private double _simulatedProgress = 0.0;
    private double _realProgress = 0.0;
    private int _bufferMs = 120000;
    private bool _bufferComplete = false;

    public string VideoUrl
    {
        get => _videoUrl;
        set
        {
            _videoUrl = value ?? string.Empty;

            if (ReadyToPlay)
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
        Stop();

        _bufferMs = ResolveBufferMs();
        _bufferComplete = false;
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
        _mediaPlayer.EncounteredError += (s, e) => StopBuffering();

        _mediaPlayer.Buffering += (s, e) =>
        {
            _realProgress = e.Cache / 100.0;
            UpdateBufferingUI();
            UpdateBufferInfo();
            CheckBufferCompletion();
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
            ControlsGrid.IsVisible = false;
        });
    }

    // ---------------- Buffer UI ----------------

    private void StartBuffering()
    {
        _simulatedProgress = 0.0;
        _realProgress = 0.0;
        _bufferComplete = false;

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
        _bufferComplete = true;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            BufferingProgressBar.Progress = 1.0;
            BufferingLayout.IsVisible = false;
        });
    }

    private void OnBufferingTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_bufferComplete)
            return;

        var steps = Math.Max(1.0, _bufferMs / 100.0);
        _simulatedProgress += 1.0 / steps;
        if (_simulatedProgress > 1.0)
            _simulatedProgress = 1.0;

        UpdateBufferingUI();
        UpdateBufferInfo();
        CheckBufferCompletion();
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
            BufferInfoLabel.Text = $"Buffer: {bufferedSeconds} sec";
        });
    }

    private void CheckBufferCompletion()
    {
        if (_bufferComplete)
            return;

        double progress = Math.Max(_realProgress, _simulatedProgress);
        int bufferedSeconds = (int)((_bufferMs / 1000.0) * progress);

        if (bufferedSeconds >= (_bufferMs / 1000))
            StopBuffering();
    }

    // ---------------- Controls ----------------

    private void ResetHideTimer()
    {
        _hideControlsTimer.Stop();
        _hideControlsTimer.Start();
    }

    private void OnHideTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        HideControls();
    }

    private void ShowControls()
    {
        ControlsGrid.IsVisible = true;
        ResetHideTimer();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(50);
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

    private void OnOverlayInputClicked(object sender, EventArgs e)
    {
        if (ControlsGrid.IsVisible)
            HideControls();
        else
            ShowControls();
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

    private void OnSeekableChanged(object? sender, MediaPlayerSeekableChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            bool canSeek = e.Seekable != 0;
            RewindButton.IsVisible = canSeek;
            FastForwardButton.IsVisible = canSeek;
        });
    }

    // ---------------- Lifecycle ----------------

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
            ControlsGrid.IsVisible = false;
        });

        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _mediaPlayer = null;

        _libVLC?.Dispose();
        _libVLC = null;
    }
}
