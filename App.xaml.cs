using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System.ComponentModel;
using Windows.Storage;
using Shinobu.Helpers;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Shinobu
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        public static Window? MainWindowInstance { get; private set; }
        public static IJapaneseDictionary? Dictionary { get; set; }
        private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            MainWindowInstance = _window;
            _window.Activate();
            UpdateTheme();
            _ = LoadDictionaryAsync();
        }

        private void UpdateTheme()
        {
            if (_window != null)
            {
                var theme = _localSettings.Values.TryGetValue("Theme", out var t) ? t as string : "";
                ElementTheme requestedTheme = theme switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };
                if (App.MainWindowInstance is Window w && w.Content is FrameworkElement fe)
                {
                    fe.RequestedTheme = requestedTheme;
                }
            }
        }

        public static async Task LoadDictionaryAsync()
        {
            var settings = ApplicationData.Current.LocalSettings;
            var dictType = settings.Values.TryGetValue("Dictionary", out var d) && d is string s ? s : "Local";
            if (dictType == "Local")
            {
                Dictionary = new LocalDictionary();
            }
            else
            {
                // Defaulting to local for now
                Dictionary = new LocalDictionary();
            }
        }
    }

    public class BoolToStringConverter : IValueConverter
    {
        public string TrueValue { get; set; } = "★";
        public string FalseValue { get; set; } = "☆";

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b && b) ? TrueValue : FalseValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is long bytes)
            {
                const long KB = 1024;
                const long MB = KB * 1024;
                const long GB = MB * 1024;

                if (bytes >= GB)
                    return $"{bytes / (double)GB:F1} GB";
                if (bytes >= MB)
                    return $"{bytes / (double)MB:F1} MB";
                if (bytes >= KB)
                    return $"{bytes / (double)KB:F1} KB";
                return $"{bytes} B";
            }
            return "0 B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class BookItem : INotifyPropertyChanged
    {
        private bool _isFavorite;
        private string _previewText = "Loading preview...";
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string DateModified { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged(nameof(IsFavorite));
                }
            }
        }

        public string PreviewText
        {
            get => _previewText;
            set
            {
                if (_previewText != value)
                {
                    _previewText = value;
                    OnPropertyChanged(nameof(PreviewText));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
