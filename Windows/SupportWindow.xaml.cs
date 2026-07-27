using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using VPM.Language;
using VPM.Services;
using VPM.Windows;

namespace VPM
{
    public partial class SupportWindow : Window
    {
        // Windows API for dark title bar
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private SupportInfo _supportInfo;
        private SupportInfo _supportInfo1;

        public SupportWindow()
        {
            InitializeComponent();
            SourceInitialized += SupportWindow_SourceInitialized;
            LoadDataAsync();
        }

        private void SupportWindow_SourceInitialized(object sender, EventArgs e)
        {
            ApplyDarkTitleBar();
        }

        private void ApplyDarkTitleBar()
        {
            try
            {
                bool isDarkMode = false;

                if (Application.Current?.Resources != null)
                {
                    if (Application.Current.Resources.MergedDictionaries.Count > 0)
                    {
                        var themeDict = Application.Current.Resources.MergedDictionaries[0];
                        if (themeDict.Source != null && themeDict.Source.ToString().Contains("Dark"))
                        {
                            isDarkMode = true;
                        }
                    }

                    if (!isDarkMode && Application.Current.Resources.Contains(System.Windows.SystemColors.ControlBrushKey))
                    {
                        var brush = Application.Current.Resources[System.Windows.SystemColors.ControlBrushKey] as System.Windows.Media.SolidColorBrush;
                        if (brush != null)
                        {
                            isDarkMode = brush.Color.R < 128;
                        }
                    }
                }

                if (isDarkMode)
                {
                    var hwnd = new WindowInteropHelper(this).Handle;
                    if (hwnd != IntPtr.Zero)
                    {
                        int value = 1;
                        if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)) != 0)
                        {
                            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref value, sizeof(int));
                        }
                    }
                }
            }
            catch
            {
                // Silently fail if dark title bar is not supported
            }
        }

        private async void LoadDataAsync()
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                
                // Load Supporters asynchronously
                _supportInfo = await SupportService.GetSupportInfoAsync();
                
                SupportersList.ItemsSource = _supportInfo.Supporters;
                
                // Update UI if we have a valid link, otherwise keep default behavior
                // (Link text is initially collapsed)
                
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
            catch (System.Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show(string.Format(LanguageManager.Instance.GetCodeString("msg_106"), ex.Message));
            }
        }

        private void PatreonButton_Click(object sender, RoutedEventArgs e)
        {
            // Use loaded link or fallback
            string url = _supportInfo?.PatreonLink ?? "https://www.patreon.com/gicstin";
            OpenUrl(url);
        }
        private void InternationalButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. 定义图片链接（可以是硬编码，也可以从配置/输入框获取）
            string imageUrl = "https://www.imageoss.com/images/2026/07/26/_20260726054509_5_8882b735d8ca8fd38.jpg";

            // 2. 创建子窗口实例
            // 假设你的子窗口类名为 ImageWindow，构造函数接收图片URL
            var childWindow = new SupportImage(imageUrl);

            // 3. 【关键步骤】设置所有者为当前主窗口
            // 这建立了父子关系，让子窗口知道“中心”是相对于谁而言的
            childWindow.Owner = this;

            // 4. 【关键步骤】设置启动位置为“相对于所有者居中”
            childWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // 5. 显示窗口
            childWindow.ShowDialog();
        }

        private void SupporterItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is SupporterItem item)
            {
                if (!string.IsNullOrEmpty(item.Link))
                {
                    OpenUrl(item.Link);
                }
            }
        }

        private void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(string.Format(LanguageManager.Instance.GetCodeString("msg_107"), ex.Message));
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
