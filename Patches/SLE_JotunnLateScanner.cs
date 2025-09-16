using System;
using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Managers;

namespace SkillLimitExtender
{
    /// <summary>
    /// 全MOD読み込み後（Gameシーン）に一回だけスキル収集→YAML反映→cap適用
    /// </summary>
    internal static class SLE_JotunnLateScanner
    {
        private static bool _initialized;

        internal static void HookOnceAtGameScene()
        {
            if (_initialized) return;
            ItemManager.OnItemsRegistered += OnItemsRegisteredOnce;
            _initialized = true;
        }

        private static void OnItemsRegisteredOnce()
        {
            ItemManager.OnItemsRegistered -= OnItemsRegisteredOnce;

            try
            {
                var all = CollectAllSkillsSafe();
                SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] Detected {all.Count} skills after all mods loaded.");

                SkillYamlExporter.AppendMissingSkills(all);
                SkillConfigManager.ReloadFromYaml();
                SkillConfigManager.ApplyCaps(all);
            }
            catch (Exception e)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] Late-scan failed: {e}");
            }
        }

        private static List<global::Skills.SkillDef> CollectAllSkillsSafe()
        {
            var result = new List<global::Skills.SkillDef>();
            const int MAX_ID = 2000;
            var seen = new HashSet<global::Skills.SkillType>();

            for (int id = 0; id <= MAX_ID; id++)
            {
                global::Skills.SkillDef? def = null;
                try
                {
                    var mi = AccessTools.Method(typeof(global::Skills), "GetSkillDef", new Type[] { typeof(global::Skills.SkillType) });
                    if (mi != null)
                        def = mi.Invoke(null, new object[] { (global::Skills.SkillType)id }) as global::Skills.SkillDef;
                }
                catch { /* ignore */ }

                if (def == null) continue;
                if (seen.Add(def.m_skill))
                    result.Add(def);
            }
            return result;
        }
    }
}
