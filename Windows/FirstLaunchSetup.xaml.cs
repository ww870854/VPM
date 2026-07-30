using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using VPM.Language;
using static VPM.MainWindow;
namespace VPM
{
    public partial class FirstLaunchSetup : Window
    {
        private string _selectedPath = null;

        public string SelectedGamePath => _selectedPath;

        private static readonly ObservableCollection<LanguageOption> SupportedLanguages = new ObservableCollection<LanguageOption>
        {
            new LanguageOption { ResourceKey = "Chinese", CultureCode = "zh-CN" },
            new LanguageOption { ResourceKey = "English", CultureCode = "en-US" },
            new LanguageOption { ResourceKey = "Russian", CultureCode = "ru-RU" },
            new LanguageOption { ResourceKey = "French", CultureCode = "fr-FR" },
            new LanguageOption { ResourceKey = "German", CultureCode = "de-DE" },
            new LanguageOption { ResourceKey = "Spanish", CultureCode = "es-ES" },
            new LanguageOption { ResourceKey = "Italian", CultureCode = "it-IT" },
            new LanguageOption { ResourceKey = "Korean", CultureCode = "ko-KR" },
            new LanguageOption { ResourceKey = "Dutch", CultureCode = "nl-NL" },
            new LanguageOption { ResourceKey = "Polish", CultureCode = "pl-PL" },
            new LanguageOption { ResourceKey = "Portuguese", CultureCode = "pt-PT" },
            new LanguageOption { ResourceKey = "Arabic", CultureCode = "ar-SA" },
            new LanguageOption { ResourceKey = "Japanese", CultureCode = "ja-JP" }
        };
        public FirstLaunchSetup()
        {
            InitializeComponent();
            LanguageManager.Instance.InitLanguageAtAppStart();

            // Try to auto-detect game folder
            TryAutoDetectGameFolder();
            // 使用 Dispatcher 确保在 UI 线程绑定数据
            this.Loaded += (s, e) =>
            {
                if (SupportedLanguages != null && SupportedLanguages.Count > 0)
                {
                    LanguageSelectCombo.ItemsSource = SupportedLanguages;
                    // 设置默认选中项
                    var currentLang = CultureInfo.CurrentUICulture.Name;
                    var defaultItem = SupportedLanguages.FirstOrDefault(l => l.CultureCode == currentLang);
                    if (defaultItem != null)
                    {
                        LanguageSelectCombo.SelectedItem = defaultItem;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("错误：SupportedLanguages 列表为空或未初始化");
                }
            };
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
                // 通用VaM目录有效性校验，只认核心文件，完全不限制文件夹名称
                bool IsValidVaamFolder(string path)
                {
                    if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return false;
                    return File.Exists(Path.Combine(path, "VaM.exe"))
                        && Directory.Exists(Path.Combine(path, "AddonPackages"));
                }
                // 优先检测Steam默认安装路径，覆盖绝大多数常规安装场景
                string steamPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common");
                if (Directory.Exists(steamPath))
                {
                    // 遍历Steam的common目录下所有以VaM开头的文件夹，自动匹配带版本号的自定义命名
                    foreach (var vamDir in Directory.GetDirectories(steamPath, "VaM*"))
                    {
                        if (IsValidVaamFolder(vamDir))
                        {
                            _selectedPath = vamDir;
                            AutoDetectedPanel.Visibility = Visibility.Visible;
                            DetectedPathText.Text = $"📝 {_selectedPath}";
                            ManualSelectionTitle.Text = LanguageManager.Instance.GetCodeString("OrChooseDifferentFolder");
                            ContinueButton.IsEnabled = true;
                            StatusText.Text = LanguageManager.Instance.GetCodeString("ReadyToContinueWithDetectedPath");
                            return;
                        }
                    }
                }
                // 遍历所有本地固定磁盘，自动匹配根目录下所有以VaM开头的文件夹
                foreach (var drive in DriveInfo.GetDrives())
                {
                    // 跳过未就绪的光驱、网络驱动器，以及系统盘的系统目录，避免权限报错
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                        continue;
                    try
                    {
                        // 直接搜索当前盘根目录下所有以VaM开头的文件夹
                        foreach (var vamDir in Directory.GetDirectories(drive.RootDirectory.FullName, "VaM*"))
                        {
                            if (IsValidVaamFolder(vamDir))
                            {
                                _selectedPath = vamDir;
                                AutoDetectedPanel.Visibility = Visibility.Visible;
                                DetectedPathText.Text = $"📝 {_selectedPath}";
                                ManualSelectionTitle.Text = LanguageManager.Instance.GetCodeString("OrChooseDifferentFolder");
                                ContinueButton.IsEnabled = true;
                                StatusText.Text = LanguageManager.Instance.GetCodeString("ReadyToContinueWithDetectedPath");
                                return;
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // 自动跳过无访问权限的驱动器，不抛出异常中断搜索
                        continue;
                    }
                }
                // 所有搜索都未命中，引导用户手动选择路径
                AutoDetectedPanel.Visibility = Visibility.Collapsed;
                ManualSelectionTitle.Visibility = Visibility.Visible;
                StatusText.Text = LanguageManager.Instance.GetCodeString("title_13");
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
        private void LanguageSelectCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count == 0 || LanguageSelectCombo.SelectedItem is not LanguageOption opt)
                return;

            var newCulture = new CultureInfo(opt.CultureCode);
            CultureInfo.DefaultThreadCurrentCulture = newCulture;
            CultureInfo.DefaultThreadCurrentUICulture = newCulture;
            Thread.CurrentThread.CurrentCulture = newCulture;
            Thread.CurrentThread.CurrentUICulture = newCulture;

            // 倒序清理所有旧语言资源字典，彻底避免残留冲突
            for (int i = Application.Current.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                var dict = Application.Current.Resources.MergedDictionaries[i];
                if (dict.Source?.OriginalString.Contains("Resources.Language.Resources.") == true)
                {
                    Application.Current.Resources.MergedDictionaries.RemoveAt(i);
                }
            }

            // 容错加载新语言资源，避免无效编码导致崩溃
            try
            {
                var newDictUri = new Uri($"pack://application:,,,/VPM;component/Resources/Language/Resources.{opt.CultureCode}.xaml");
                if (Application.GetResourceStream(newDictUri) != null)
                {
                    Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = newDictUri });
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    string.Format(LanguageManager.Instance.GetCodeString("msg_131"), ex.Message).Replace("\\n","\n"),
                    LanguageManager.Instance.GetCodeString("Error"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            // 调用项目内置的索引器通知方法，刷新所有绑定到LanguageManager的文本
            LanguageManager.Instance.NotifyIndexerChanged();
            // 递归刷新当前FirstLaunchSetup页面的所有动态资源UI元素
            LanguageManager.Instance.UpdateAllDependencyObjects(this);
            // 持久化保存语言配置
            App.SettingsManager.Settings.SelectedLanguage = opt.CultureCode;
            App.SettingsManager.SaveSettingsImmediate();
            // ========== 在这里插入刷新逻辑 ==========
            if (!string.IsNullOrEmpty(_selectedPath))
            {
                ManualSelectionTitle.Text = LanguageManager.Instance.GetCodeString("OrChooseDifferentFolder");
                StatusText.Text = LanguageManager.Instance.GetCodeString("ReadyToContinueWithDetectedPath");
            }
            else
            {
                StatusText.Text = LanguageManager.Instance.GetCodeString("title_13");
            }
            var lastSelected = LanguageSelectCombo.SelectedItem;
            LanguageSelectCombo.ItemsSource = null;
            LanguageSelectCombo.ItemsSource = SupportedLanguages;
            LanguageSelectCombo.SelectedItem = lastSelected;
        }
    }
}

