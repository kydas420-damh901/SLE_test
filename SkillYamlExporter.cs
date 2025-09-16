using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx; // Paths
using HarmonyLib; // ★ 追加（private呼び出し用）
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SkillLimitExtender
{
    internal static class SkillYamlExporter
    {
        private static readonly string ConfigDir =
            Path.Combine(Paths.ConfigPath, "SkillLimitExtender");
        private static readonly string YamlPath =
            Path.Combine(ConfigDir, "skilllimitconfig.yaml");

        internal static void EnsureYamlExists()
        {
            Directory.CreateDirectory(ConfigDir);
            if (File.Exists(YamlPath)) return;

            var seed = new Dictionary<string, int>();
            foreach (global::Skills.SkillType st in Enum.GetValues(typeof(global::Skills.SkillType)))
            {
                global::Skills.SkillDef? def = null;
                try
                {
                    var mi = AccessTools.Method(typeof(global::Skills), "GetSkillDef", new Type[] { typeof(global::Skills.SkillType) });
                    if (mi != null)
                        def = mi.Invoke(null, new object[] { st }) as global::Skills.SkillDef;
                }
                catch { /* ignore */ }

                if (def == null) continue;
                seed[st.ToString()] = SkillConfigManager.DefaultCap?.Value ?? 250;
            }
            SaveYaml(seed);
        }

        internal static void AppendMissingSkills(IEnumerable<global::Skills.SkillDef> allDefs)
        {
            var current = LoadYaml();
            bool changed = false;

            foreach (var def in allDefs)
            {
                var key = def.m_skill.ToString();
                if (!current.ContainsKey(key))
                {
                    current[key] = SkillConfigManager.DefaultCap?.Value ?? 250;
                    changed = true;
                }
            }

            if (changed)
            {
                SaveYaml(current);
                SkillLimitExtenderPlugin.Logger?.LogInfo("[SLE] YAML updated with missing mod skills.");
            }
            else
            {
                SkillLimitExtenderPlugin.Logger?.LogInfo("[SLE] YAML has no missing skills.");
            }
        }

        internal static Dictionary<string, int> LoadYaml()
        {
            try
            {
                if (!File.Exists(YamlPath)) return new Dictionary<string, int>();
                var yaml = File.ReadAllText(YamlPath);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();
                var map = deserializer.Deserialize<Dictionary<string, int>>(yaml);
                return map ?? new Dictionary<string, int>();
            }
            catch (Exception e)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] LoadYaml error: {e}");
                return new Dictionary<string, int>();
            }
        }

        private static void SaveYaml(Dictionary<string, int> map)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                var yaml = serializer.Serialize(map
                    .OrderBy(kv => kv.Key)
                    .ToDictionary(kv => kv.Key, kv => kv.Value));
                File.WriteAllText(YamlPath, yaml);
            }
            catch (Exception e)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] SaveYaml error: {e}");
            }
        }
    }
}
