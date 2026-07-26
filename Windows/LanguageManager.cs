using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using VPM.Services;


namespace VPM.Language
{
    public class LanguageManager : INotifyPropertyChanged
    {
        private static readonly Lazy<LanguageManager> _lazy = new Lazy<LanguageManager>(() => new LanguageManager());
        public static LanguageManager Instance => _lazy.Value;

        public event PropertyChangedEventHandler PropertyChanged;
        // 在LanguageManager类里新增状态标记字段
        public bool IsLanguageResourcesLoaded { get; private set; } = false;

        // 用于获取 SettingsManager 实例的辅助方法
        // 请根据你项目中实际获取 SettingsManager 的方式修改此方法
        private ISettingsManager GetSettingsManager()
        {
            // 方案 1: 如果 App.xaml.cs 中有 public static ISettingsManager SettingsManager { get; set; }
            if (App.Current is VPM.App && App.SettingsManager != null)
            {
                return App.SettingsManager;
            }

            // 方案 2: 如果通过 Application.Current.Resources 获取
            // if (Application.Current.Resources["SettingsManager"] is ISettingsManager sm) return sm;

            // 兜底：如果都获取不到，返回 null 或抛出异常，视项目架构而定
            // 这里假设你能通过某种全局方式获取，否则需要重构 LanguageManager 以支持依赖注入
            throw new InvalidOperationException("无法获取 SettingsManager 实例，请检查全局配置。");
        }

        // 优化：增加线程安全的通知触发，避免跨线程调用时UI死锁
        public void NotifyIndexerChanged()
        {
            // 如果不在UI线程，自动调度到UI线程执行，彻底规避跨线程更新绑定的异常
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]")));
                return;
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }

        // 优化：构造函数里移除直接调用NotifyIndexerChanged，避免绑定未挂载时无效通知
        private LanguageManager()
        {
            // 改为在App完全启动后再触发首次通知，这里只做基础初始化
        }

        public string this[string name]
        {
            get
            {
                if (name == null) throw new ArgumentNullException(nameof(name));
                return GetCodeString(name);
            }
        }

        public void ChangeLanguage(string cultureCode)
        {
            var newCulture = new CultureInfo(cultureCode);
            CultureInfo.DefaultThreadCurrentCulture = newCulture;
            CultureInfo.DefaultThreadCurrentUICulture = newCulture;
            Thread.CurrentThread.CurrentCulture = newCulture;
            Thread.CurrentThread.CurrentUICulture = newCulture;

            // 原有资源字典替换逻辑完全保留，不做改动
            var oldLangDicts = Application.Current.Resources.MergedDictionaries
                .Where(d => d.Source?.OriginalString.Contains("Resources.Language.Resources.") == true)
                .ToList();
            foreach (var dict in oldLangDicts)
            {
                Application.Current.Resources.MergedDictionaries.Remove(dict);
            }
            var newLangDict = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/VPM;component/Resources/Language/Resources.{cultureCode}.xaml")
            };
            Application.Current.Resources.MergedDictionaries.Add(newLangDict);

            // 【核心修改】使用 SettingsManager 保存语言设置
            try
            {
                var settingsManager = GetSettingsManager();
                // 1. 更新内存中的配置
                settingsManager.Settings.SelectedLanguage = cultureCode;

                // 2. 立即保存到文件，防止重启丢失
                // 注意：如果 ChangeLanguage 在 UI 线程频繁调用，建议异步保存或使用防抖，
                // 但在这里因为是用户主动切换，立即保存是安全的且符合预期。
                settingsManager.SaveSettingsImmediate();
            }
            catch (Exception ex)
            {
                // 记录日志或处理异常，避免因为配置保存失败导致语言切换整体失败
                System.Diagnostics.Debug.WriteLine($"保存语言配置失败: {ex.Message}");
            }

            // 优化：资源字典加载完成后，延迟1帧再发通知，避免资源还没解析完绑定就拉取值
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                NotifyIndexerChanged();
            }), System.Windows.Threading.DispatcherPriority.Input);
            // 在ChangeLanguage方法的最后一行设置标记，资源完全加载后就变为true
            IsLanguageResourcesLoaded = true;
        }

        public string GetCodeString(string key)
        {
            // 优化：增加空值兜底，极端情况下资源字典未初始化时直接返回Key，不会抛异常
            if (Application.Current?.Resources != null && Application.Current.Resources.Contains(key))
            {
                return Application.Current.Resources[key]?.ToString() ?? key;
            }
            return key;
        }

        public void ForceAllBindingsRefresh()
        {
            // 这个优先级比你之前用的Input更低，能保证所有UI初始化任务全部执行完才触发刷新
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                NotifyIndexerChanged();
                // 额外补一次通知，覆盖极端情况下漏监听的绑定
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    NotifyIndexerChanged();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        // 新增：专门用于程序启动完成后，一次性初始化所有语言资源的方法
        public void InitLanguageAtAppStart()
        {
            string defaultLang = "zh-CN";
            try
            {
                // 【核心修改】使用 SettingsManager 读取语言设置
                var settingsManager = GetSettingsManager();
                var savedLang = settingsManager.Settings.SelectedLanguage;

                if (!string.IsNullOrEmpty(savedLang))
                {
                    defaultLang = savedLang;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取语言配置失败，使用默认值: {ex.Message}");
            }

            // 执行语言切换，加载对应资源
            ChangeLanguage(defaultLang);
            // 执行全绑定刷新，覆盖所有启动阶段没加载到资源的控件
            ForceAllBindingsRefresh();
        }
    }
}