using System;
using System.IO;
using System.Windows;
using VPM.Language;
using VPM.Models;

namespace VPM
{
    public partial class FirstLaunchSetup : Window
    {
        private string _selectedPath = null;

        public string SelectedGamePath => _selectedPath;

        public FirstLaunchSetup()
        {
            InitializeComponent();
            this.ContentRendered += (s, e) =>
            {
                // 程序完全渲染完成后，一次性执行语言资源全量加载刷新
                LanguageManager.Instance.InitLanguageAtAppStart();
            };

            // Try to auto-detect game folder
            TryAutoDetectGameFolder();
        }

        /// <summary>
        /// Attempts to auto-detect if the application is inside a VaM game folder
        /// </summary>
        private void TryAutoDetectGameFolder()
        {
            try
            {
                // Get the directory where the application is running
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                
                // Check if we're in the game folder by looking for VaM.exe and AddonPackages folder
                string vamExePath = Path.Combine(appDirectory, "VaM.exe");
                string addonPackagesPath = Path.Combine(appDirectory, "AddonPackages");
                
                if (File.Exists(vamExePath) && Directory.Exists(addonPackagesPath))
                {
                    // We're inside the game folder!
                    _selectedPath = appDirectory;
                    
                    // Show the auto-detected panel
                    AutoDetectedPanel.Visibility = Visibility.Visible;
                    DetectedPathText.Text = $"📝 {_selectedPath}";
                    
                    // Update manual selection title
                    ManualSelectionTitle.Text = LanguageManager.Instance.GetCodeString("OrChooseDifferentFolder");
                    
                    // Enable continue button
                    ContinueButton.IsEnabled = true;
                    StatusText.Text = LanguageManager.Instance.GetCodeString("ReadyToContinueWithDetectedPath");
                }
                else
                {
                    // Not in game folder, check parent directory as well
                    DirectoryInfo parentDir = Directory.GetParent(appDirectory);
                    if (parentDir != null)
                    {
                        string parentVamExe = Path.Combine(parentDir.FullName, "VaM.exe");
                        string parentAddonPackages = Path.Combine(parentDir.FullName, "AddonPackages");
                        
                        if (File.Exists(parentVamExe) && Directory.Exists(parentAddonPackages))
                        {
                            // Parent directory is the game folder
                            _selectedPath = parentDir.FullName;
                            
                            AutoDetectedPanel.Visibility = Visibility.Visible;
                            DetectedPathText.Text = $"📝 {_selectedPath}";
                            ManualSelectionTitle.Text = LanguageManager.Instance.GetCodeString("OrChooseDifferentFolder");
                            
                            ContinueButton.IsEnabled = true;
                            StatusText.Text = LanguageManager.Instance.GetCodeString("ReadyToContinueWithDetectedPath");
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Auto-detection failed, user will need to select manually
            }
        }

        /// <summary>
        /// Validates if the selected path is a valid VaM game folder
        /// </summary>
        private bool ValidateGameFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return false;

            string vamExePath = Path.Combine(path, "VaM.exe");
            string addonPackagesPath = Path.Combine(path, "AddonPackages");

            return File.Exists(vamExePath) && Directory.Exists(addonPackagesPath);
        }

        private void UseDetectedPath_Click(object sender, RoutedEventArgs e)
        {
            // User confirmed the auto-detected path
            DialogResult = true;
            Close();
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            // Use FolderBrowserDialog (Windows Forms) for better folder selection
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = LanguageManager.Instance.GetCodeString("BrowsedialogDescription");
                dialog.ShowNewFolderButton = false;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;

                    if (ValidateGameFolder(selectedPath))
                    {
                        _selectedPath = selectedPath;

                        // Show selected path
                        SelectedPathBorder.Visibility = Visibility.Visible;
                        SelectedPathText.Text = selectedPath;

                        // Enable continue button
                        ContinueButton.IsEnabled = true;
                        StatusText.Text = LanguageManager.Instance.GetCodeString("BrowsedialogStatusText");
                    }
                    else
                    {
                        string message = LanguageManager.Instance.GetCodeString("Browse_Folder_Full");
                        MessageBox.Show(
                            message,
                            LanguageManager.Instance.GetCodeString("Browse_Folder_Title"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        StatusText.Text = LanguageManager.Instance.GetCodeString("BrowsedialogStatusText1");
                    }
                }
            }
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedPath) && ValidateGameFolder(_selectedPath))
            {
                LanguageManager.Instance.NotifyIndexerChanged();
                DialogResult = true;
                Close();
            }
            else
            {
                string message = LanguageManager.Instance.GetCodeString("Continue_Full");
                MessageBox.Show(
                    message,
                    LanguageManager.Instance.GetCodeString("Continue_Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }
}

