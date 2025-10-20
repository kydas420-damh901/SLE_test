using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace SkillLimitExtender
{
    /// <summary>
    /// Lightweight server configuration manager (cap/bonus managed via YAML).
    /// Server syncs full YAML; clients consume it.
    /// </summary>
    internal static class SkillConfigManager
    {
        // Keep only toggle settings (caps/bonus managed via YAML)
        internal static ConfigEntry<bool> ServerConfigLocked = null!;
        internal static ConfigEntry<bool> EnableYamlOverride = null!;

        // Internal state (YAML map)
        private static Dictionary<string, YamlExporter.SkillYamlEntry> _entriesByName = new(StringComparer.Ordinal);
        private static bool _initialized;
        private static bool _isServerConfig;
        private static string _lastYamlHash = string.Empty;

        // Fallback defaults when YAML is not set
        private const int DefaultCapFallback = 250;
        private const int DefaultBonusCapFallback = 100; // 100 = 1.0x

        internal static void Initialize(ConfigFile config)
        {
            if (_initialized) return;

            // Server-only settings (admin only)
            ServerConfigLocked = config.Bind("Server", "LockConfiguration", false,
                new ConfigDescription(
                    "If true, server forces its configuration to all clients. (Admin Only)",
                    null,
                    new object[] { new ConfigurationManagerAttributes { Category = "Server", Order = -1000, IsAdminOnly = true } }
                )
            );

            EnableYamlOverride = config.Bind("General", "EnableYamlOverride", true,
                new ConfigDescription(
                    "Allow YAML file to override individual skill caps/bonus/relative",
                    null,
                    new object[] { new ConfigurationManagerAttributes { Category = "Skill Level", Order = -85 } }
                )
            );

            ReloadFromYaml();
            _initialized = true;
            SkillLimitExtenderPlugin.Logger?.LogInfo("[SLE] Lightweight server config initialized (YAML-based)");
        }

        // Receive YAML distributed by server (via RPC)
        internal static void OnYamlReceivedStatic(long sender, string yamlContent, int protocolVersion)
        {
            OnYamlReceived(sender, yamlContent, protocolVersion);
        }

        internal static void ReloadFromYaml()
        {
            // Ignore local YAML if server configuration is locked
            if (_isServerConfig || (EnableYamlOverride != null && !EnableYamlOverride.Value))
            {
                if (_isServerConfig)
                {
                    SkillLimitExtenderPlugin.Logger?.LogInfo("[SLE] Using server configuration (YAML disabled locally)");
                }
                return;
            }

            _entriesByName = YamlExporter.LoadYamlEntries() ?? new Dictionary<string, YamlExporter.SkillYamlEntry>(StringComparer.Ordinal);
        }

        // Accessors
        internal static int GetCap(global::Skills.SkillType st)
        {
            string skillKey = st.ToString();
    
            // Client-side: local YAML is enabled
            if (!_isServerConfig && EnableYamlOverride?.Value == true &&
                _entriesByName != null &&
                _entriesByName.TryGetValue(skillKey, out var entry) &&
                entry != null && entry.Cap > 0)
            {
                return entry.Cap;
            }
    
            // Fallback to numeric keys (for extensions; names are preferred)
            if (!_isServerConfig && EnableYamlOverride?.Value == true &&
                _entriesByName != null &&
                int.TryParse(skillKey, out int skillId) && skillId > 999)
            {
                foreach (var kv in _entriesByName)
                {
                    if (!int.TryParse(kv.Key, out _) && kv.Value != null && kv.Value.Cap > 0)
                    {
                        return kv.Value.Cap;
                    }
                }
            }
    
            // Applying server-distributed YAML
            if (_isServerConfig &&
                _entriesByName != null &&
                _entriesByName.TryGetValue(skillKey, out var serverEntry) &&
                serverEntry != null && serverEntry.Cap > 0)
            {
                return serverEntry.Cap;
            }
    
            return DefaultCapFallback;
        }

        internal static int GetBonusCap(global::Skills.SkillType st)
        {
            string skillKey = st.ToString();

            if (!_isServerConfig && EnableYamlOverride?.Value == true)
            {
                if (_entriesByName != null && _entriesByName.TryGetValue(skillKey, out var entry) && entry != null && entry.BonusCap > 0)
                    return entry.BonusCap;
            }

            if (_isServerConfig)
            {
                if (_entriesByName != null && _entriesByName.TryGetValue(skillKey, out var serverEntry) && serverEntry != null && serverEntry.BonusCap > 0)
                    return serverEntry.BonusCap;
            }

            return DefaultBonusCapFallback;
        }

        internal static bool IsRelative(global::Skills.SkillType st)
        {
            string skillKey = st.ToString();
            if (_entriesByName != null && _entriesByName.TryGetValue(skillKey, out var entry) && entry != null)
                return entry.Relative;
            return true; // Default to relative scaling
        }

        // Growth curve parameters
        internal static float GetGrowthExponent(global::Skills.SkillType st)
        {
            string skillKey = st.ToString();
            if (_entriesByName != null && _entriesByName.TryGetValue(skillKey, out var entry) && entry != null)
                return entry.GrowthExponent;
            return 1.5f; // Vanilla default
        }

        internal static float GetGrowthMultiplier(global::Skills.SkillType st)
        {
            string skillKey = st.ToString();
            if (_entriesByName != null && _entriesByName.TryGetValue(skillKey, out var entry) && entry != null)
                return entry.GrowthMultiplier;
            return 0.5f; // Vanilla default
        }

        internal static float GetGrowthConstant(global::Skills.SkillType st)
        {
            string skillKey = st.ToString();
            if (_entriesByName != null && _entriesByName.TryGetValue(skillKey, out var entry) && entry != null)
                return entry.GrowthConstant;
            return 0.5f; // Vanilla default
        }

        internal static bool UseCustomGrowthCurve(global::Skills.SkillType st)
        {
            string skillKey = st.ToString();
            if (_entriesByName != null && _entriesByName.TryGetValue(skillKey, out var entry) && entry != null)
                return entry.UseCustomGrowthCurve;
            return false; // Default: use vanilla curve
        }

        // UI denominator (global/per-skill)
        internal static float GetFactorDenominator(global::Skills.SkillType st) => 100f; // Vanilla behavior
        internal static float GetUiDenominator() => Math.Max(1, DefaultCapFallback);
        internal static float GetUiDenominatorForSkill(global::Skills.SkillType st) => Math.Max(1, GetCap(st));

        // Added: safe helper to obtain per-skill UI denominator
        // Special handling: always use the same denominator for both levelbar and levelbar_total
        internal static float GetUiDenominatorForSkillSafe(object? skillMaybe)
        {
            try
            {
                var s = skillMaybe as global::Skills.Skill;
                if (s != null)
                {
                    var info = HarmonyLib.Traverse.Create(s).Field("m_info").GetValue<global::Skills.SkillDef>();
                    if (info != null)
                    {
                        var st = HarmonyLib.Traverse.Create(info).Field("m_skill").GetValue<global::Skills.SkillType>();
                        float result = GetUiDenominatorForSkill(st);
                        SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] UI denominator for {st}: {result} (level={s.m_level})");
                        return result;
                    }
                }
            }
            catch (System.Exception e)
            {
                SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] GetUiDenominatorForSkillSafe failed: {e.Message}");
            }
            float fallback = GetUiDenominator();
            SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] UI denominator fallback: {fallback}");
            return fallback;
        }

        // Server → client: send full YAML
        internal static void SendConfigToClients()
        {
            if (ZNet.instance?.IsServer() != true || !(ServerConfigLocked?.Value == true)) return;
            try
            {
                var yamlPath = YamlExporter.GetYamlPath();
                string yamlContent = System.IO.File.Exists(yamlPath)
                    ? System.IO.File.ReadAllText(yamlPath)
                    : string.Empty;
                int proto = VersionInfo.ProtocolVersion;
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SLE_YamlSync", yamlContent, proto);
                _lastYamlHash = ComputeHash(yamlContent ?? string.Empty);
                SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] Server YAML sent to clients (length={yamlContent?.Length ?? 0}, proto={proto})");
            }
            catch (System.Exception e)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] Failed to send YAML to clients: {e}");
            }
        }

        internal static void SendConfigToClientsIfChanged()
        {
            if (ZNet.instance?.IsServer() != true || !(ServerConfigLocked?.Value == true)) return;
            try
            {
                var yamlPath = YamlExporter.GetYamlPath();
                string yamlContent = System.IO.File.Exists(yamlPath)
                    ? System.IO.File.ReadAllText(yamlPath)
                    : string.Empty;

                string currentHash = ComputeHash(yamlContent ?? string.Empty);
                if (string.Equals(_lastYamlHash, currentHash, System.StringComparison.Ordinal))
                {
                    SkillLimitExtenderPlugin.Logger?.LogDebug("[SLE] YAML unchanged; broadcast skipped.");
                    return;
                }

                int proto = VersionInfo.ProtocolVersion;
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SLE_YamlSync", yamlContent, proto);
                _lastYamlHash = currentHash;
                SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] Server YAML broadcasted to everybody (length={yamlContent?.Length ?? 0}, proto={proto})");
            }
            catch (System.Exception e)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] Failed to broadcast YAML: {e}");
            }
        }

        // Client: receive server YAML
        private static void OnYamlReceived(long sender, string yamlContent, int protocolVersion)
        {
            if (ZNet.instance?.IsServer() == true) return; // Ignore on server
            try
            {
                _isServerConfig = true;

                if (!VersionInfo.IsCompatible(protocolVersion))
                {
                    SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] Protocol version mismatch: remote={protocolVersion}, local={VersionInfo.ProtocolVersion}");
                    return; // Mismatch: do not apply YAML
                }

                if (!string.IsNullOrEmpty(yamlContent))
                {
                    var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                        .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                        .IgnoreUnmatchedProperties()
                        .Build();
                    try
                    {
                        var map = deserializer.Deserialize<Dictionary<string, YamlExporter.SkillYamlEntry>>(yamlContent);
                        _entriesByName = map ?? new Dictionary<string, YamlExporter.SkillYamlEntry>(StringComparer.Ordinal);
                    }
                    catch (YamlDotNet.Core.YamlException yamlEx)
                    {
                        SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] YAML parsing failed, trying legacy format: {yamlEx.Message}");
                        // Fallback to legacy format (int)
                        var mapOld = deserializer.Deserialize<Dictionary<string, int>>(yamlContent) ?? new Dictionary<string, int>();
                        var converted = new Dictionary<string, YamlExporter.SkillYamlEntry>(StringComparer.Ordinal);
                        foreach (var kv in mapOld)
                        {
                            converted[kv.Key] = new YamlExporter.SkillYamlEntry { 
                    Cap = kv.Value, 
                    BonusCap = DefaultBonusCapFallback, 
                    Relative = true,
                    UseCustomGrowthCurve = false,
                    GrowthExponent = 1.5f,
                    GrowthMultiplier = 0.5f,
                    GrowthConstant = 0.5f
                };
                        }
                        _entriesByName = converted;
                    }
                    catch (System.Exception parseEx)
                    {
                        SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] Failed to parse server YAML: {parseEx}");
                        _entriesByName = new Dictionary<string, YamlExporter.SkillYamlEntry>(StringComparer.Ordinal);
                    }
                }
                else
                {
                    _entriesByName = new Dictionary<string, YamlExporter.SkillYamlEntry>(StringComparer.Ordinal);
                }

                SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] Received server YAML (entries={_entriesByName.Count})");
            }
            catch (System.Exception e)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] Failed to apply server YAML: {e}");
                // Ensure we have a valid state even on error
                _entriesByName = new Dictionary<string, YamlExporter.SkillYamlEntry>(StringComparer.Ordinal);
            }
        }

        // Send server configuration when player connects
        internal static void OnPlayerConnected()
        {
            SendConfigToClients();
        }

        // Compatibility API
        internal static int GetCapByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return DefaultCapFallback;
            if (Enum.TryParse<global::Skills.SkillType>(name, true, out var st) &&
                st != global::Skills.SkillType.None &&
                st != global::Skills.SkillType.All)
            {
                return GetCap(st);
            }
            return DefaultCapFallback;
        }

        // Compatibility API
        internal static int GetSkillLimit(global::Skills.SkillType st) => GetCap(st);

        // Moved here: hash computation method (inside class)
        private static string ComputeHash(string text)
        {
            try
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty);
                    var hash = sha.ComputeHash(bytes);
                    return System.BitConverter.ToString(hash).Replace("-", "");
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}