using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Shinobu.Dialogs
{
    public sealed partial class PageOptionsDialog : UserControl
    {
        private Shinobu.Pages.ReaderPage _readerPage;
        private bool _isLoaded = false;

        public PageOptionsDialog(Shinobu.Pages.ReaderPage readerPage)
        {
            _readerPage = readerPage;
            InitializeComponent();
            Loaded += PageOptionsDialog_Loaded;
        }

        private void PageOptionsDialog_Loaded(object sender, RoutedEventArgs e)
        {
            InitControls();
            LoadSettings();
            _isLoaded = true;
        }

        private void InitControls()
        {
            var fonts = new[] { "Segoe UI", "Meiryo", "Yu Gothic", "Noto Sans JP", "Roboto", "Arial" };
            foreach (var font in fonts)
                FontFamilyComboBox.Items.Add(font);

            var sizes = new[] { 12, 14, 16, 18, 20, 22, 24, 28, 32, 36, 42, 48 };
            foreach (var size in sizes)
                FontSizeComboBox.Items.Add(size);

            var lineSpacings = new[] { 1.0, 1.15, 1.25, 1.4, 1.5, 1.8, 2.0, 2.25, 2.4, 2.5, 3.0 };
            foreach (var spacing in lineSpacings)
                LineSpacingComboBox.Items.Add($"{spacing:F2}×");

            var margins = new[] { "None", "Small", "Medium", "Large", "Very Large" };
            foreach (var m in margins)
                PageMarginComboBox.Items.Add(m);
        }

        private void LoadSettings()
        {
            HorizontalRadio.IsChecked = !_readerPage.IsVerticalText;
            VerticalRadio.IsChecked = _readerPage.IsVerticalText;

            FontSizeComboBox.SelectedItem = (int)Math.Round(_readerPage.FontSize);
            LineSpacingComboBox.SelectedItem = $"{_readerPage.LineHeight:F2}×";

            // FontFamilyComboBox.SelectedItem = _readerPage.FontFamily?.Source ?? "Segoe UI";

            // TODO
            PageMarginComboBox.SelectedIndex = 2;
        }


        private void OrientationRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            _readerPage.IsVerticalText = VerticalRadio.IsChecked == true;
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || FontSizeComboBox.SelectedItem is not int size) return;
            _readerPage.FontSize = size;
        }

        private void LineSpacingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || LineSpacingComboBox.SelectedItem is not string str) return;

            if (double.TryParse(str.Replace("×", ""), out double value))
                _readerPage.LineHeight = value;
        }

        private void PageMarginComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            // int index = PageMarginComboBox.SelectedIndex;
        }

        private void StyleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                btn.Opacity = btn.Opacity < 0.7 ? 1.0 : 0.5;
        }

        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TODO
        }
    }
}