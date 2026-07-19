using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using static VPM.MainWindow;

namespace VPM.Language
{
    public class LanguageManager : INotifyPropertyChanged
    {
        // 全局单例保持不变
        private static readonly Lazy<LanguageManager> _lazy = new Lazy<LanguageManager>(() => new LanguageManager());
        public static LanguageManager Instance => _lazy.Value;

        public event PropertyChangedEventHandler PropertyChanged;

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

            // 优化：资源字典加载完成后，延迟1帧再发通知，避免资源还没解析完绑定就拉取值
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                NotifyIndexerChanged();
            }), System.Windows.Threading.DispatcherPriority.Input);

            AppConfig.SelectedLanguage = cultureCode;
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
            // 读取配置里保存的上次选中语言，没有就用默认中文
            var defaultLang = string.IsNullOrEmpty(AppConfig.SelectedLanguage) ? "zh-CN" : AppConfig.SelectedLanguage;
            // 执行语言切换，加载对应资源
            ChangeLanguage(defaultLang);
            // 执行全绑定刷新，覆盖所有启动阶段没加载到资源的控件
            ForceAllBindingsRefresh();
        }
    }
}