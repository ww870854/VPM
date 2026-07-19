using System;
using System.ComponentModel;
using VPM.Language; // 引入你项目的LanguageManager命名空间，根据你的实际命名空间调整

namespace VPM.Models
{
    /// <summary>
    /// Sorting options for the main package table
    /// </summary>
    public enum PackageSortOption
    {
        [Description("Sort_Name")]
        Name,
        [Description("Sort_Date")]
        Date,
        [Description("Sort_Size")]
        Size,
        [Description("Sort_Dependencies")]
        Dependencies,
        [Description("Sort_Dependents")]
        Dependents,
        [Description("Sort_Status")]
        Status,
        [Description("Sort_Morphs")]
        Morphs,
        [Description("Sort_Hair")]
        Hair,
        [Description("Sort_Clothing")]
        Clothing,
        [Description("Sort_Scenes")]
        Scenes,
        [Description("Sort_Looks")]
        Looks,
        [Description("Sort_Poses")]
        Poses,
        [Description("Sort_Assets")]
        Assets,
        [Description("Sort_Scripts")]
        Scripts,
        [Description("Sort_Plugins")]
        Plugins,
        [Description("Sort_SubScenes")]
        SubScenes,
        [Description("Sort_Skins")]
        Skins
    }

    /// <summary>
    /// Sorting options for the scene table
    /// </summary>
    public enum SceneSortOption
    {
        [Description("Sort_Name")]
        Name,
        [Description("Sort_Date")]
        Date,
        [Description("Sort_Size")]
        Size,
        [Description("Sort_Dependencies")]
        Dependencies,
        [Description("Sort_Atoms")]
        Atoms
    }

    /// <summary>
    /// Sorting options for the presets table
    /// </summary>
    public enum PresetSortOption
    {
        [Description("Sort_Name")]
        Name,
        [Description("Sort_Date")]
        Date,
        [Description("Sort_Size")]
        Size,
        [Description("Sort_Category")]
        Category,
        [Description("Sort_Subfolder")]
        Subfolder,
        [Description("Sort_Status")]
        Status
    }

    /// <summary>
    /// Sorting options for the dependencies table
    /// </summary>
    public enum DependencySortOption
    {
        [Description("Sort_Name")]
        Name,
        [Description("Sort_Status")]
        Status
    }

    /// <summary>
    /// Sorting options for filter lists
    /// </summary>
    public enum FilterSortOption
    {
        [Description("Sort_Name")]
        Name,
        [Description("Sort_Count")]
        Count
    }

    /// <summary>
    /// Current sorting state for a table
    /// </summary>
    public class SortingState
    {
        public object CurrentSortOption { get; set; }
        public bool IsAscending { get; set; } = true;
        public DateTime LastSortTime { get; set; } = DateTime.Now;

        public SortingState()
        {
        }

        public SortingState(object sortOption, bool isAscending = true)
        {
            CurrentSortOption = sortOption;
            IsAscending = isAscending;
            LastSortTime = DateTime.Now;
        }
    }

    /// <summary>
    /// Serializable sorting state for persistence
    /// </summary>
    public class SerializableSortingState
    {
        public string SortOptionType { get; set; }
        public string SortOptionValue { get; set; }
        public bool IsAscending { get; set; } = true;

        public SerializableSortingState()
        {
        }

        public SerializableSortingState(string sortOptionType, string sortOptionValue, bool isAscending)
        {
            SortOptionType = sortOptionType;
            SortOptionValue = sortOptionValue;
            IsAscending = isAscending;
        }
    }

    /// <summary>
    /// Extension methods for sorting enums
    /// 【改造完成：完全适配LanguageManager国际化，原有调用点100%兼容，无需修改外部代码】
    /// </summary>
    public static class SortingExtensions
    {
        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = (DescriptionAttribute)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));

            // 优先走LanguageManager拉取翻译，找不到资源时兜底返回原始Description兼容旧逻辑
            var resourceKey = attribute?.Description ?? value.ToString();
            var localizedText = LanguageManager.Instance.GetCodeString(resourceKey);

            // 如果返回的文本和资源Key完全一致，说明找不到对应翻译，兜底兼容
            return localizedText != resourceKey ? localizedText : resourceKey;
        }

        public static string GetDisplayText(this Enum value, bool isAscending)
        {
            var baseDescription = value.GetDescription();
            // 方向箭头也支持国际化，兼容右对齐等特殊语种显示需求
            var directionKey = isAscending ? "Sort_Ascending_Arrow" : "Sort_Descending_Arrow";
            var direction = LanguageManager.Instance.GetCodeString(directionKey);
            // 兜底兼容旧版箭头显示，找不到资源时用默认符号
            direction = direction != directionKey ? direction : (isAscending ? " ↑" : " ↓");

            return $"{baseDescription}{direction}";
        }
    }
}
