using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MAXTV.Models;

public class DownloadItem : INotifyPropertyChanged
{
    private string _status = "Pending";
    private double _speedMbps;

    public string Title { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    public double SpeedMbps
    {
        get => _speedMbps;
        set
        {
            if (Math.Abs(_speedMbps - value) > 0.01)
            {
                _speedMbps = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
