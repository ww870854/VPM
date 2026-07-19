using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using VPM.Language;
using VPM.Models;
using VPM.Services;


namespace VPM
{
    public partial class ArchiveAllOldWindow : Window
    {
        public ArchiveAllOldWindow(List<VarMetadata> oldPackages, string destinationPath)
        {
            InitializeComponent();
            Loaded += (s, e) => DarkTitleBarHelper.Apply(this);
            string template = LanguageManager.Instance.GetCodeString("MessageTextBlock");
            string message = string.Format(template, oldPackages.Count, destinationPath);
            message = message.Replace("\\n", "\n");
            MessageTextBlock.Text = message;
            
            // Create display items
            var displayItems = oldPackages.Select(p => new
            {
                DisplayName = $"{p.CreatorName}.{p.PackageName}.{p.Version}",
                Version = p.Version,
                LatestVersionNumber = p.LatestVersionNumber
            }).OrderBy(p => p.DisplayName).ToList();
            
            PackageListControl.ItemsSource = displayItems;
        }

        private void ArchiveButton_Click(object sender, RoutedEventArgs e)
        {
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

