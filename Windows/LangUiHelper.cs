using System.Windows.Controls;
using VPM.Language; // 假设 LanguageManager 在此命名空间

namespace VPM.Helpers
{
    public static class LangUiHelper
    {
        /// <summary>
        /// 根据数量自动切换按钮文本和提示的资源Key，并处理字符串格式化
        /// </summary>
        public static void SetButtonContentWithCount(Button btn, string baseKey, string countKey, int count, string tooltipKey = null)
        {
            if (btn == null) return;

            var lang = LanguageManager.Instance;

            // 1. 确定 Content Key
            string contentKey = count == 1 ? baseKey : countKey;
            string contentTemplate = lang.GetCodeString(contentKey);

            // 2. 格式化 Content
            btn.Content = string.Format(contentTemplate, count);

            // 3. 处理 Tooltip
            if (!string.IsNullOrEmpty(tooltipKey))
            {
                string tooltipTemplate = lang.GetCodeString(tooltipKey);
                btn.ToolTip = string.Format(tooltipTemplate, count);
            }
        }
    }
}
