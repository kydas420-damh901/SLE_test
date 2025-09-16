using System.Collections.Generic;
using BepInEx.Configuration;

namespace SkillLimitExtender
{
    /// <summary>
    /// YAMLロード/保存および cap の参照窓口
    /// </summary>
    internal static class SkillConfigManager
    {
        internal static ConfigEntry<int> DefaultCap = null!; // ★ NRT抑止（Initializeで必ず設定）

        private static Dictionary<string, int> _capsByName = new();
        private static bool _initialized;

        internal static void Initialize(ConfigFile config)
        {
            if (_initialized) return;
            DefaultCap = config.Bind("General", "DefaultCap", 250,
                "YAMLに無いスキルへ適用される既定の上限値");
            ReloadFromYaml();
            _initialized = true;
        }

        internal static void ReloadFromYaml()
        {
            _capsByName = SkillYamlExporter.LoadYaml();
        }

        internal static int GetCap(global::Skills.SkillType st)
        {
            if (_capsByName != null && _capsByName.TryGetValue(st.ToString(), out var cap) && cap > 0)
                return cap;
            return DefaultCap?.Value ?? 250;
        }

        // 互換API（既存呼び出し対策）
        internal static int GetSkillLimit(global::Skills.SkillType st) => GetCap(st);

        // 将来拡張用（今はno-op）
        internal static void ApplyCaps(System.Collections.Generic.IEnumerable<global::Skills.SkillDef> _all) { /* no-op */ }
    }
}
