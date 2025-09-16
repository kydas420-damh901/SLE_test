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

        /// <summary>
        /// 初回起動時にバニラスキルのみでYAMLファイル生成
        /// </summary>
        internal static void EnsureYamlExists()
        {
            Directory.CreateDirectory(ConfigDir);
            if (File.Exists(YamlPath)) return;

            var seed = new Dictionary<string, int>();
            try
            {
                foreach (global::Skills.SkillType st in Enum.GetValues(typeof(global::Skills.SkillType)))
                {
                    if (st == global::Skills.SkillType.None || st == global::Skills.SkillType.All) continue;
                    seed[st.ToString()] = SkillConfigManager.DefaultCap?.Value ?? 250;
                }
            }
            catch { /* ignore */ }

            SaveYaml(seed);
            SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] Created YAML: {YamlPath} (seed={seed.Count} vanilla skills)");
        }

        /// <summary>
        /// YAMLファイル読み込み
        /// </summary>
        internal static Dictionary<string, int> LoadYaml()
        {
            try
            {
                if (!File.Exists(YamlPath)) return new Dictionary<string, int>();
                var yaml = File.ReadAllText(YamlPath, new UTF8Encoding(false));
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();
                return deserializer.Deserialize<Dictionary<string, int>>(yaml) ?? new Dictionary<string, int>();
            }
            catch (Exception e)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] LoadYaml error: {e}");
                return new Dictionary<string, int>();
            }
        }

        /// <summary>
        /// YAMLファイル保存
        /// </summary>
        internal static void SaveYaml(Dictionary<string, int> map)
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