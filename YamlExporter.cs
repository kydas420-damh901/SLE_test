using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx; // Paths
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SkillLimitExtender
{
    internal static class YamlExporter
    {
        private static readonly string ConfigDir =
            Path.Combine(Paths.ConfigPath, "SkillLimitExtender");

        private static readonly string YamlPath =
            Path.Combine(ConfigDir, "SLE_Skill_List.yaml");

        internal static string GetYamlPath() => YamlPath;

        internal sealed class SkillYamlEntry
        {
            public int Cap { get; set; } = 250;
            public int BonusCap { get; set; } = 100; // 100 = 1.0x
            public bool Relative { get; set; } = false; // false = vanilla (level/100), true = relative (level/cap)
        }

        /// <summary>
        /// 初回起動時にバニラスキルのみでYAMLファイル生成
        /// </summary>
        internal static void EnsureYamlExists()
        {
            Directory.CreateDirectory(ConfigDir);
            if (File.Exists(YamlPath)) return;
            var seed = new Dictionary<string, SkillYamlEntry>();
            try
            {
                foreach (global::Skills.SkillType st in Enum.GetValues(typeof(global::Skills.SkillType)))
                {
                    if (st == global::Skills.SkillType.None || st == global::Skills.SkillType.All) continue;
                    seed[st.ToString()] = new SkillYamlEntry { Cap = 250, BonusCap = 100, Relative = false };
                }
            }
            catch { /* ignore */ }

            SaveYamlEntries(seed);
            SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] Created YAML: {YamlPath} (seed={seed.Count} vanilla skills, fields: cap/bonusCap/relative)");
        }

        /// <summary>
        /// YAMLファイル読み込み
        /// </summary>
        internal static Dictionary<string, SkillYamlEntry> LoadYamlEntries()
        {
            try
            {
                if (!File.Exists(YamlPath)) return new Dictionary<string, SkillYamlEntry>();
                var yaml = File.ReadAllText(YamlPath, new UTF8Encoding(false));
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();
                // 新形式（Entryオブジェクト）の読み込みを試し、失敗したら旧形式（int）にフォールバック
                try
                {
                    var mapNew = deserializer.Deserialize<Dictionary<string, SkillYamlEntry>>(yaml);
                    if (mapNew != null) return mapNew;
                }
                catch { /* fallback to old */ }

                var mapOld = deserializer.Deserialize<Dictionary<string, int>>(yaml) ?? new Dictionary<string, int>();
                var converted = new Dictionary<string, SkillYamlEntry>(StringComparer.Ordinal);
                foreach (var kv in mapOld)
                {
                    converted[kv.Key] = new SkillYamlEntry { Cap = kv.Value, BonusCap = 100, Relative = false };
                }
                return converted;
            }
            catch (Exception e)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] LoadYaml error: {e}");
                return new Dictionary<string, SkillYamlEntry>();
            }
        }

        /// <summary>
        /// YAMLファイル保存
        /// </summary>
        internal static void SaveYamlEntries(Dictionary<string, SkillYamlEntry> map)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                var yaml = serializer.Serialize(
                    map.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                       .ToDictionary(kv => kv.Key, kv => kv.Value)
                );
                File.WriteAllText(YamlPath, yaml, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] SaveYaml error: {e}");
            }
        }
    }
}