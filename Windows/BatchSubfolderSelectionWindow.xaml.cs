using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using VPM.Language;

namespace VPM
{
    public partial class BatchSubfolderSelectionWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private readonly ObservableCollection<PackageSelectionGroup> _packageGroups = new ObservableCollection<PackageSelectionGroup>();

        public Dictionary<string, string> SelectedFilesToKeep { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public BatchSubfolderSelectionWindow(Dictionary<string, List<string>> packageInstances)
        {
            InitializeComponent();
            ApplyDarkTitleBar();
            LoadPackageGroups(packageInstances ?? new Dictionary<string, List<string>>());
        }

        private void ApplyDarkTitleBar()
        {
            Loaded += (s, e) =>
            {
                try
                {
                    var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    if (handle == IntPtr.Zero)
                        return;

                    int darkMode = 1;
                    if (DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int)) != 0)
                        DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref darkMode, sizeof(int));
                }
                catch { }
            };
        }

        private void LoadPackageGroups(Dictionary<string, List<string>> packageInstances)
        {
            _packageGroups.Clear();

            foreach (var entry in packageInstances.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
            {
                var instances = new ObservableCollection<PackageVersionItem>();
                foreach (var filePath in entry.Value.OrderByDescending(GetLastWriteTimeSafe))
                {
                    var item = CreateVersionItem(filePath);
                    if (item != null)
                        instances.Add(item);
                }

                if (instances.Count <= 1)
                    continue;

                _packageGroups.Add(new PackageSelectionGroup
                {
                    PackageName = entry.Key,
                    Instances = instances,
                    SelectedInstance = instances[0]
                });
            }

            PackageGroupsItemsControl.ItemsSource = _packageGroups;
            UpdateStatusText();
        }

        private static PackageVersionItem CreateVersionItem(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                    return null;

                return new PackageVersionItem
                {
                    FullPath = filePath,
                    RelativeLocation = GetRelativePath(filePath),
                    FileSize = fileInfo.Length,
                    FileSizeFormatted = FormatFileSize(fileInfo.Length),
                    ModifiedDate = fileInfo.LastWriteTime,
                    ModifiedDateFormatted = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                };
            }
            catch
            {
                return null;
            }
        }

        private static DateTime GetLastWriteTimeSafe(string filePath)
        {
            try
            {
                return new FileInfo(filePath).LastWriteTime;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private static string GetRelativePath(string fullPath)
        {
            try
            {
                var pathParts = fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                for (int i = 0; i < pathParts.Length; i++)
                {
                    if (pathParts[i].Equals("AddonPackages", StringComparison.OrdinalIgnoreCase) ||
                        pathParts[i].Equals("AllPackages", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i < pathParts.Length - 1)
                            return string.Join(Path.DirectorySeparatorChar.ToString(), pathParts.Skip(i));
                        break;
                    }
                }

                return Path.GetFileName(fullPath);
            }
            catch
            {
                return Path.GetFileName(fullPath);
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n1} {suffixes[counter]}";
        }

        private void UpdateStatusText()
        {
            string template = LanguageManager.Instance.GetCodeString("UpdateStatusText");
            string message = string.Format(template, _packageGroups.Count);
            StatusText.Text = message;
        }

        private void KeepSelected_Click(object sender, RoutedEventArgs e)
        {
            SelectedFilesToKeep.Clear();

            var missingSelections = new List<string>();
            foreach (var group in _packageGroups)
            {
                if (group.SelectedInstance == null || string.IsNullOrEmpty(group.SelectedInstance.FullPath))
                {
                    missingSelections.Add(group.PackageName);
                    continue;
                }

                SelectedFilesToKeep[group.PackageName] = group.SelectedInstance.FullPath;
            }

            //if (missingSelections.Count > 0)
            //{
            //    DarkMessageBox.Show(
            //        $"Select a copy to keep for:\n{string.Join("\n", missingSelections.Take(10))}" +
            //        (missingSelections.Count > 10 ? $"\n... and {missingSelections.Count - 10} more" : string.Empty),
            //        "Selection Required",
            //        MessageBoxButton.OK,
            //        MessageBoxImage.Warning);
            //    return;
            //}
            if (missingSelections.Count > 0)
            {
                // 1. 获取标题
                string title = LanguageManager.Instance.GetCodeString("SelectionRequiredTitle");

                // 2. 构建主要消息内容
                // 注意：这里我们不再依赖单一的 template 格式化所有复杂逻辑，而是分步构建以确保换行和列表格式正确

                // 获取前10项用于显示
                var displayItems = missingSelections.Take(10);
                string itemsList = string.Join("\n", displayItems);

                // 构建基础提示信息 (可选：如果你想在弹窗正文中显示完整列表摘要)
                // 如果列表很长，通常建议在弹窗内只显示部分，或者使用之前的 DarkMessageBox 支持长文本滚动的特性
                string mainMessage = string.Format(
                    LanguageManager.Instance.GetCodeString("SelectionRequiredMessage"),
                    missingSelections.Count,
                    string.Join(", ", missingSelections) // 这里用逗号分隔作为简要摘要，或者根据UI需求调整
                );

                // 3. 构建弹窗显示的详细列表字符串 (带换行)
                string detailedList = itemsList;

                // 处理超过10项的情况
                if (missingSelections.Count > 10)
                {
                    int remainingCount = missingSelections.Count - 10;
                    // 获取 "... and X more" 的国际化文本
                    string moreTextTemplate = LanguageManager.Instance.GetCodeString("AndMoreItems");
                    string moreText = string.Format(moreTextTemplate, remainingCount);

                    detailedList += $"\n{moreText}";
                }

                // 4. 组合最终弹窗内容
                // 建议：将 "Select a copy to keep for:" 也提取为资源 Key，例如 "SelectCopyPrompt"
                string promptTitle = LanguageManager.Instance.GetCodeString("SelectCopyPrompt"); // 需新增此Key: "Select a copy to keep for:"

                string finalMessage = $"{promptTitle}\n{detailedList}";

                // 5. 调用弹窗
                DarkMessageBox.Show(
                    finalMessage,
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning,
                    customBtn1Text: LanguageManager.Instance.GetCodeString("Btn_Confirm") // 确保按钮也国际化
                );

                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class PackageSelectionGroup : INotifyPropertyChanged
    {
        private PackageVersionItem _selectedInstance;

        public string PackageName { get; set; } = string.Empty;
        public ObservableCollection<PackageVersionItem> Instances { get; set; } = new ObservableCollection<PackageVersionItem>();

        public PackageVersionItem SelectedInstance
        {
            get => _selectedInstance;
            set
            {
                if (_selectedInstance != value)
                {
                    _selectedInstance = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
