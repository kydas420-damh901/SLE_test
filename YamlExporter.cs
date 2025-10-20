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

        private static string YamlPath =
            Path.Combine(ConfigDir, "SLE_Skill_List.yaml");

        internal static string GetYamlPath() => YamlPath;

        internal sealed class SkillYamlEntry
        {
            public int Cap { get; set; } = 250;
            public int BonusCap { get; set; } = 100; // 100 = 1.0x
            public bool Relative { get; set; } = true; // true = relative (level/cap), false = vanilla (level/100)
            public bool UseCustomGrowthCurve { get; set; } = false; // true = custom curve, false = vanilla curve
            public float GrowthExponent { get; set; } = 1.5f; // Growth curve exponent (vanilla: 1.5)
            public float GrowthMultiplier { get; set; } = 0.5f; // Growth curve multiplier (vanilla: 0.5)
            public float GrowthConstant { get; set; } = 0.5f; // Growth curve constant (vanilla: 0.5)
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
                    seed[st.ToString()] = new SkillYamlEntry { 
                        Cap = 250, 
                        BonusCap = 100, 
                        Relative = true,
                        UseCustomGrowthCurve = false,
                        GrowthExponent = 1.5f,
                        GrowthMultiplier = 0.5f,
                        GrowthConstant = 0.5f
                    };
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
                    converted[kv.Key] = new SkillYamlEntry { 
                        Cap = kv.Value, 
                        BonusCap = 100, 
                        Relative = true,
                        UseCustomGrowthCurve = false,
                        GrowthExponent = 1.5f,
                        GrowthMultiplier = 0.5f,
                        GrowthConstant = 0.5f
                    };
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

                // 原子書き込み（失敗時はフォールバックへ）
                if (!TryWriteAllText(YamlPath, yaml, new UTF8Encoding(false), out var err))
                {
                    var fallbackDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SkillLimitExtender");
                    var fallbackPath = Path.Combine(fallbackDir, "SLE_Skill_List.yaml");
                    Directory.CreateDirectory(fallbackDir);
                    File.WriteAllText(fallbackPath, yaml, new UTF8Encoding(false));
                    // 読み込み先もフォールバックへ切替
                    YamlPath = fallbackPath;
                    SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] SaveYaml primary failed: {err?.GetType().Name} {err?.Message}; switched to fallback: {fallbackPath}");
                }
            }
            catch (Exception e)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] SaveYaml error: {e}");
            }
        }

        // 原子書き込み＋クリーンアップ（内部ユーティリティ）
        private static bool TryWriteAllText(string path, string contents, Encoding encoding, out Exception? error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, contents, encoding);
                if (File.Exists(path))
                {
                    File.Replace(tmp, path, null);
                }
                else
                {
                    File.Move(tmp, path);
                }
                return true;
            }
            catch (Exception e)
            {
                error = e;
                try
                {
                    var tmp = path + ".tmp";
                    if (File.Exists(tmp)) File.Delete(tmp);
                }
                catch { /* ignore */ }
                return false;
            }
        }
    }
}