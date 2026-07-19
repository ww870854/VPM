using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using VPM.Language;
using VPM.Services;
using static VPM.MainWindow;

namespace VPM
{
    public partial class App : Application
    {

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            //Dispatcher.BeginInvoke(new Action(() =>
            //{
            //    LanguageManager.Instance.NotifyIndexerChanged();
            //}), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 先同步执行语言全量加载，保证资源字典完全替换完成
                LanguageManager.Instance.InitLanguageAtAppStart();
                // 再发送通知，此时所有后续创建的窗口绑定都能直接读取到已加载的有效资源
                LanguageManager.Instance.NotifyIndexerChanged();
            }), System.Windows.Threading.DispatcherPriority.Normal);


            // Suppress WPF binding errors in debug output
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Critical;

            // Set shutdown mode to manual so closing setup window doesn't close app
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Global exception handlers to catch crashes
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Check if this is the first launch
            HandleFirstLaunch();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            
            Debug.WriteLine($"FATAL ERROR: {exception?.Message}");
            if (exception != null)
            {
                Debug.WriteLine(exception.StackTrace);
            }

            // MessageBox.Show(
            //     $"A fatal error occurred:\n\n{exception?.Message}\n\nStack Trace:\n{exception?.StackTrace}",
            //     "Fatal Error",
            //     MessageBoxButton.OK,
            //     MessageBoxImage.Error);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            
            Debug.WriteLine($"UNHANDLED ERROR: {e.Exception.Message}");
            Debug.WriteLine(e.Exception.StackTrace);

            // MessageBox.Show(
            //     $"An unhandled error occurred:\n\n{e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}",
            //     "Unhandled Error",
            //     MessageBoxButton.OK,
            //     MessageBoxImage.Error);
            
            // Mark as handled to prevent crash
            e.Handled = true;
        }

        /// <summary>
        /// Handles first launch setup
        /// </summary>
        private void HandleFirstLaunch()
        {
            try
            {
                // Load settings to check if this is first launch
                var settingsManager = new SettingsManager();
                
                if (settingsManager.Settings.IsFirstLaunch)
                {
                    // Show first launch setup window
                    var setupWindow = new FirstLaunchSetup();
                    var result = setupWindow.ShowDialog();
                    
                    if (result == true && !string.IsNullOrEmpty(setupWindow.SelectedGamePath))
                    {
                        // Save the selected path
                        settingsManager.Settings.SelectedFolder = setupWindow.SelectedGamePath;
                        settingsManager.Settings.IsFirstLaunch = false;
                        
                        try
                        {
                            settingsManager.SaveSettingsImmediate();
                        }
                        catch (Exception saveEx)
                        {
                            string template = LanguageManager.Instance.GetCodeString("HandleFirstLaunch_Full");
                            string message = string.Format(template, saveEx.Message);
                            message = message.Replace("\\n", "\n");
                            MessageBox.Show(
                                message,
                                LanguageManager.Instance.GetCodeString("HandleFirstLaunch_Title"),
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                        
                        // Create and show main window with the settings manager instance
                        try
                        {
                            // 新增：主窗口实例化前最后一次确认语言资源就绪
                            LanguageManager.Instance.InitLanguageAtAppStart();
                            var mainWindow = new MainWindow(settingsManager);
                            MainWindow = mainWindow;
                            
                            // Change shutdown mode to close when main window closes
                            ShutdownMode = ShutdownMode.OnMainWindowClose;
                            
                            mainWindow.Show();
                        }
                        catch (Exception mainWindowEx)
                        {
                            string template = LanguageManager.Instance.GetCodeString("HandleFirstLaunch_Create");
                            string message = string.Format(template, mainWindowEx.Message);
                            message = message.Replace("\\n", "\n");
                            MessageBox.Show(
                                message,
                                LanguageManager.Instance.GetCodeString("HandleFirstLaunch_Create_Title"),
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            Shutdown();
                        }
                    }
                    else
                    {
                        // User cancelled setup, exit application
                        string message = LanguageManager.Instance.GetCodeString("HandleFirstLaunch_Cancelled");
                        message = message.Replace("\\n", "\n");
                        MessageBox.Show(
                            message,
                            LanguageManager.Instance.GetCodeString("HandleFirstLaunch_Cancelled_Title"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        Shutdown();
                        return;
                    }
                }
                else
                {
                    // Not first launch, show main window normally with loaded settings
                    try
                    {
                        // 新增：主窗口实例化前最后一次确认语言资源就绪
                        LanguageManager.Instance.InitLanguageAtAppStart();
                        var mainWindow = new MainWindow(settingsManager);
                        MainWindow = mainWindow;
                        
                        // Change shutdown mode to close when main window closes
                        ShutdownMode = ShutdownMode.OnMainWindowClose;
                        
                        mainWindow.Show();
                    }
                    catch (Exception mainWindowEx)
                    {
                        string template = LanguageManager.Instance.GetCodeString("HandleFirstLaunch_Create");
                        string message = string.Format(template, mainWindowEx.Message);
                        message = message.Replace("\\n", "\n");
                        MessageBox.Show(
                            message,
                            LanguageManager.Instance.GetCodeString("HandleFirstLaunch_Create_Title"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        Shutdown();
                    }
                }
            }
            catch (Exception ex)
            {
                string template = LanguageManager.Instance.GetCodeString("HandleFirstLaunch_error");
                string message = string.Format(template, ex.Message);
                MessageBox.Show(
                    message,
                    LanguageManager.Instance.GetCodeString("HandleFirstLaunch_Error_Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}

