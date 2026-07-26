using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VPM.Language;

namespace VPM.Windows
{
    /// <summary>
    /// SupportImage.xaml 的交互逻辑
    /// </summary>
    public partial class SupportImage : Window
    {
        public SupportImage(string imageUrl)
        {
            InitializeComponent();
            LoadImageAsync(imageUrl);

        }
        // 1. 自定义关闭按钮点击事件
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        // 2. 实现窗口拖动 (模拟标题栏行为)
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 只有左键按下时才拖动
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
        private async void LoadImageAsync(string url)
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    var bytes = await client.GetByteArrayAsync(url);
                    var stream = new System.IO.MemoryStream(bytes);

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    DisplayImage.Source = bitmap;
                    StatusText.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(LanguageManager.Instance.GetCodeString("OpenImageInViewer_LoadFailed"), ex.Message);
            }
        }
    }
}
