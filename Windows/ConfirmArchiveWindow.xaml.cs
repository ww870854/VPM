using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using VPM.Services;
using VPM.Language;

namespace VPM
{
    public partial class ConfirmArchiveWindow : Window
    {
        public bool ArchiveAll { get; private set; } = false;
        private int _selectedCount;
        private int _totalOldCount;

        public ConfirmArchiveWindow(int selectedCount, string destinationPath, int totalOldCount)
        {
            InitializeComponent();
            Loaded += (s, e) => DarkTitleBarHelper.Apply(this);
            
            _selectedCount = selectedCount;
            _totalOldCount = totalOldCount;
            
            var message = LanguageManager.Instance.GetCodeString("ArchiveSelectedOld");
            message = string.Format(message, selectedCount, destinationPath);
            message = message.Replace("\\n", "\n");

            MessageTextBlock.Text = message;
            
            // Update Archive All button text
            if (totalOldCount > selectedCount)
            {
                string template = LanguageManager.Instance.GetCodeString("ArchiveAllButton_Content");
                string template1 = LanguageManager.Instance.GetCodeString("ArchiveAllButton_ToolTip");
                string message1 = string.Format(template, totalOldCount);
                string message2 = string.Format(template, totalOldCount);
                ArchiveAllButton.Content = message1;
                ArchiveAllButton.ToolTip = message2;
            }
            else
            {
                ArchiveAllButton.Visibility = Visibility.Collapsed;
            }
        }

        private void ArchiveButton_Click(object sender, RoutedEventArgs e)
        {
            ArchiveAll = false;
            DialogResult = true;
            Close();
        }

        private void ArchiveAllButton_Click(object sender, RoutedEventArgs e)
        {
            ArchiveAll = true;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }
    }
}