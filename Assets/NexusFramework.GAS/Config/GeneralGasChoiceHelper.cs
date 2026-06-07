using System;
using System.Collections.Generic;

namespace NexusFramework.GAS.Config
{
    public class DropdownItem
    {
        public string Text { get; }
        public object Value { get; }

        public DropdownItem(string text, object value)
        {
            Text = text;
            Value = value;
        }
    }

    /// <summary>编辑器下拉选项工具，由 ConfigModel 在加载时填充数据</summary>
    public static class GeneralGasChoiceHelper
    {
        private static List<DropdownItem> _tags = new();
        private static List<DropdownItem> _effects = new();
        private static List<DropdownItem> _cues = new();
        private static List<DropdownItem> _mmcs = new();
        private static readonly Dictionary<int, List<DropdownItem>> _attrsBySet = new();

        public static List<DropdownItem> Tags() => _tags;
        public static List<DropdownItem> GameplayEffects() => _effects;
        public static List<DropdownItem> GameplayCues() => _cues;
        public static List<DropdownItem> MmcTypes() => _mmcs;
        public static List<DropdownItem> AttrSets() => new();
        public static List<DropdownItem> Attrs(int attrSetCode) =>
            _attrsBySet.TryGetValue(attrSetCode, out var list) ? list : new();

        internal static void SetTags(List<DropdownItem> tags) => _tags = tags;
        internal static void SetEffects(List<DropdownItem> effects) => _effects = effects;
        internal static void SetCues(List<DropdownItem> cues) => _cues = cues;
        internal static void SetMmcs(List<DropdownItem> mmcs) => _mmcs = mmcs;

        private static readonly List<Type> _mmcTypes = new();
        internal static void RegisterMmcType(Type mmcType)
        {
            if (!_mmcTypes.Contains(mmcType))
            {
                _mmcTypes.Add(mmcType);
                _mmcs.Add(new DropdownItem(mmcType.Name, mmcType.Name));
            }
        }
    }
}
