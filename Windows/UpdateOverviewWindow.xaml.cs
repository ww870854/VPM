using System;
using System.Diagnostics;
using System.Windows;
using VPM.Services;
using VPM.Language;

namespace VPM.Windows
{
    public partial class UpdateOverviewWindow : Window
    {
        public string VpmReleaseUrl { get; set; }
        public string VpmInternationalReleaseUrl { get; set; }
        public string VpbDownloadUrl { get; set; }

        public UpdateOverviewWindow()
        {
            InitializeComponent();
            DarkTitleBarHelper.Apply(this);
        }

        public void SetVpmStatus(AppUpdateChecker.AppUpdateInfo info)
        {
            if (info.IsUpdateAvailable)
            {
                VpmStatusText.Text = LanguageManager.Instance.GetCodeString("text_16");
                VpmStatusText.Foreground = System.Windows.Media.Brushes.Green;
                VpmVersionText.Text = $"{info.CurrentVersion} ➜ {info.LatestVersion}";
                VpmVersionText.Visibility = Visibility.Visible;
                VpmUpdateButton.Visibility = Visibility.Visible;
                VpmReleaseUrl = info.ReleaseUrl;
            }
            else
            {
                VpmStatusText.Text = LanguageManager.Instance.GetCodeString("text_17");
                VpmStatusText.Foreground = System.Windows.Media.Brushes.Gray;
                VpmVersionText.Text = string.Format(LanguageManager.Instance.GetCodeString("text_18"), info.CurrentVersion);
                VpmVersionText.Visibility = Visibility.Visible;
                VpmUpdateButton.Visibility = Visibility.Collapsed;
            }
        }
        public void SetVpmInternationalStatus(AppInternationalUpdateChecker.AppInternationalUpdateInfo info)
        {
            if (info.IsUpdateAvailable)
            {
                VpmInternationalStatusText.Text = LanguageManager.Instance.GetCodeString("text_16");
                VpmInternationalStatusText.Foreground = System.Windows.Media.Brushes.Green;
                VpmInternationalVersionText.Text = $"{info.CurrentVersion} ➜ {info.LatestVersion}";
                VpmInternationalVersionText.Visibility = Visibility.Visible;
                VpmInternationalUpdateButton.Visibility = Visibility.Visible;
                VpmInternationalReleaseUrl = info.ReleaseUrl;
            }
            else
            {
                VpmInternationalStatusText.Text = LanguageManager.Instance.GetCodeString("text_17");
                VpmInternationalStatusText.Foreground = System.Windows.Media.Brushes.Gray;
                VpmInternationalVersionText.Text = string.Format(LanguageManager.Instance.GetCodeString("text_18"), info.CurrentVersion);
                VpmInternationalVersionText.Visibility = Visibility.Visible;
                VpmInternationalUpdateButton.Visibility = Visibility.Collapsed;
            }
        }
        public void SetVpbStatus(VpbPluginCheckResult info)
        {
            VpbDetailsText.Text = ""; // Reset details
            
            if (info.IsUpdateAvailable)
            {
                VpbStatusText.Text = LanguageManager.Instance.GetCodeString("text_19");
                VpbStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                
                string details = "";
                if (!string.IsNullOrEmpty(info.LocalVersion))
                    details += string.Format(LanguageManager.Instance.GetCodeString("text_20"), info.LocalVersion);
                
                if (info.RemoteLastModified.HasValue)
                    details += $"GitHub: {info.RemoteLastModified.Value.ToLocalTime():yyyy-MM-dd}";
                
                VpbDetailsText.Text = details.Trim();
                VpbDetailsText.Visibility = Visibility.Visible;
                
                VpbUpdateButton.Visibility = Visibility.Visible;
                VpbUpdateButton.Content = LanguageManager.Instance.GetCodeString("text_21"); // "Sync" implies matching remote state, up or down
                VpbDownloadUrl = info.DownloadUrl;
            }
            else if (!info.IsInstalled)
            {
                VpbStatusText.Text = LanguageManager.Instance.GetCodeString("VPB_Patch_Status_Not_installed");
                VpbStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                VpbUpdateButton.Content = LanguageManager.Instance.GetCodeString("text_22");
                VpbUpdateButton.Visibility = Visibility.Visible;
                VpbDownloadUrl = info.DownloadUrl;
                VpbDetailsText.Visibility = Visibility.Collapsed;
            }
            else
            {
                VpbStatusText.Text = LanguageManager.Instance.GetCodeString("text_17");
                VpbStatusText.Foreground = System.Windows.Media.Brushes.Gray;
                VpbUpdateButton.Visibility = Visibility.Collapsed;
                
                string details = "";
                if (!string.IsNullOrEmpty(info.LocalVersion))
                    details += string.Format(LanguageManager.Instance.GetCodeString("text_20"), info.LocalVersion);
                VpbDetailsText.Text = details;
                VpbDetailsText.Visibility = string.IsNullOrEmpty(details) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void VpmUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(VpmReleaseUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = VpmReleaseUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(string.Format(LanguageManager.Instance.GetCodeString("msg_109"),ex.Message), LanguageManager.Instance.GetCodeString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void VpmInternationalUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(VpmInternationalReleaseUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = VpmInternationalReleaseUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(string.Format(LanguageManager.Instance.GetCodeString("msg_109"), ex.Message), LanguageManager.Instance.GetCodeString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void VpbUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MainWindow mainWindow)
            {
                // Close the overview window first so we don't have stacked update windows
                Close();
                mainWindow.OpenVpbPatcher();
            }
            else if (!string.IsNullOrEmpty(VpbDownloadUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = VpbDownloadUrl, 
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(string.Format(LanguageManager.Instance.GetCodeString("msg_109"), ex.Message), LanguageManager.Instance.GetCodeString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
