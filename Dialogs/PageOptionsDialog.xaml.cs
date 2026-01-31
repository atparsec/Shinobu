using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Shinobu.Helpers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Shinobu.Dialogs
{
    public sealed partial class PageOptionsDialog : UserControl
    {
        private Pages.ReaderPage _readerPage;
        private bool _isLoaded = false;
        public ReaderThemeManager ThemeManager { get; } = new ReaderThemeManager();

        public ObservableCollection<ThemeViewModel> ThemeViewModels { get; } = [];

        public event EventHandler? CustomThemeRequested;

        public PageOptionsDialog(Pages.ReaderPage readerPage)
        {
            _readerPage = readerPage;
            InitializeComponent();
            Loaded += PageOptionsDialog_Loaded;
        }

        private async void PageOptionsDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await ThemeManager.LoadAsync();
            InitControls();
            LoadSettings();
            InitThemes();
            _isLoaded = true;
        }

        private void InitControls()
        {
            string[] fonts = ["Segoe UI", "Meiryo", "Yu Gothic"];
            foreach (string? font in fonts)
            {
                FontFamilyComboBox.Items.Add(font);
            }

            int[] sizes = [12, 14, 16, 18, 20, 22, 24, 28, 32, 36, 42, 48];
            foreach (int size in sizes)
            {
                FontSizeComboBox.Items.Add(size);
            }

            double[] lineSpacings = [1.0, 1.15, 1.25, 1.4, 1.5, 1.8, 2.0, 2.25, 2.4, 2.5, 3.0];
            foreach (double spacing in lineSpacings)
            {
                LineSpacingComboBox.Items.Add($"{spacing:F2}×");
            }

            string[] margins = ["Small", "Medium", "Large"];
            foreach (string? m in margins)
            {
                PageMarginComboBox.Items.Add(m);
            }
        }

        private void LoadSettings()
        {
            HorizontalRadio.IsChecked = !_readerPage.IsVerticalText;
            VerticalRadio.IsChecked = _readerPage.IsVerticalText;

            FontSizeComboBox.SelectedItem = (int)Math.Round(_readerPage.ReaderFontSize);
            LineSpacingComboBox.SelectedItem = $"{_readerPage.LineHeight:F2}×";

            FontFamilyComboBox.SelectedItem = _readerPage.ReaderFont?.Source ?? "Segoe UI";

            PageMarginComboBox.SelectedIndex = _readerPage.ReaderMargin switch
            {
                20 => 0,
                30 => 1,
                60 => 2,
                _ => 1
            };
        }

        public void InitThemes()
        {
            ThemeViewModels.Clear();
            int index = 0;
            foreach (var theme in ThemeManager.Themes)
            {
                string bg = theme.Background;
                string fg = theme.Foreground;
                if (theme.Name == "Default")
                {
                    bool isDark = Application.Current.RequestedTheme == ApplicationTheme.Dark;
                    bg = isDark ? "#000" : "#FFF";
                    fg = isDark ? "#FFF" : "#000";
                }
                ThemeViewModels.Add(new ThemeViewModel { Theme = theme, DisplayBackground = bg, DisplayForeground = fg, IsSelected = theme.Name == _readerPage.ReaderThemeName, CanDelete = index++ >= 4 });
            }
        }

        private void OrientationRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded)
            {
                return;
            }

            _readerPage.IsVerticalText = VerticalRadio.IsChecked == true;
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || FontSizeComboBox.SelectedItem is not int size)
            {
                return;
            }

            _readerPage.ReaderFontSize = size;
        }

        private void LineSpacingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || LineSpacingComboBox.SelectedItem is not string str)
            {
                return;
            }

            if (double.TryParse(str.Replace("×", ""), out double value))
            {
                _readerPage.LineHeight = value;
            }
        }

        private void PageMarginComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded)
            {
                return;
            }

            _readerPage.ReaderMargin = PageMarginComboBox.SelectedIndex switch
            {
                0 => 20,
                1 => 30,
                2 => 60,
                _ => 30
            };
        }

        private void StyleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                btn.Opacity = btn.Opacity < 0.7 ? 1.0 : 0.5;
            }
        }

        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || FontFamilyComboBox.SelectedItem is not string font)
            {
                return;
            }

            _readerPage.ReaderFont = new FontFamily(font);
        }

        private void ThemeBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border && border.Tag is string themeName)
            {
                _readerPage.ReaderThemeName = themeName;
                foreach (var vm in ThemeViewModels)
                {
                    vm.IsSelected = vm.Theme.Name == themeName;
                }
            }
        }

        private async void CustomThemeButton_Click(object sender, RoutedEventArgs e)
        {
            CustomThemeRequested?.Invoke(this, EventArgs.Empty);
        }

        private async void DeleteThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string ThemeName)
            {
                ThemeManager.RemoveTheme(ThemeName);
                await ThemeManager.SaveAsync();
                InitThemes();
                if (_readerPage.ReaderThemeName == ThemeName)
                {
                    _readerPage.ReaderThemeName = "Default";
                    foreach (var vm in ThemeViewModels)
                    {
                        vm.IsSelected = vm.Theme.Name == "Default";
                    }
                }
            }
        }

        public partial class ThemeViewModel : INotifyPropertyChanged
        {
            private bool _isSelected;
            public BookTheme Theme { get; set; } = new();
            public string DisplayBackground { get; set; } = "#FFF";
            public string DisplayForeground { get; set; } = "#000";
            public bool CanDelete { get; set; }

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected != value)
                    {
                        _isSelected = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                    }
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }
    }
}