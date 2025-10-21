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
        
        // Track warned skill IDs to prevent duplicate warnings
        private static HashSet<int> _warnedSkillIds = new HashSet<int>();

        // Fallback defaults when YAML is not set
        internal const int DefaultCapFallback = 250;
        internal const int DefaultBonusCapFallback = 100; // 100 = 1.0x

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

            try
            {
                var newEntries = YamlExporter.LoadYamlEntries();
                if (newEntries != null)
                {
                    _entriesByName = newEntries;
                    SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] YAML reloaded successfully ({_entriesByName.Count} entries)");
                }
                else
                {
                    SkillLimitExtenderPlugin.Logger?.LogWarning("[SLE] YAML reload returned null, keeping existing configuration");
                    _entriesByName = _entriesByName ?? new Dictionary<string, YamlExporter.SkillYamlEntry>(StringComparer.Ordinal);
                }
            }
            catch (Exception ex)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] YAML reload failed: {ex.Message}");
                // Keep existing configuration on error to prevent null reference
                _entriesByName = _entriesByName ?? new Dictionary<string, YamlExporter.SkillYamlEntry>(StringComparer.Ordinal);
            }
        }

        // Accessors
        // Helper method to map MOD skill IDs to readable names from YAML
        private static string GetSkillKeyForLookup(global::Skills.SkillType st)
        {
            string skillKey = st.ToString();
            
            // For MOD skills (numeric IDs > 999), try to find corresponding name in YAML
            if (int.TryParse(skillKey, out int skillId) && skillId > 999)
            {
                if (_entriesByName != null)
                {
                    // First, check if the numeric ID itself is defined in YAML
                    if (_entriesByName.ContainsKey(skillKey))
                    {
                        if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug?.Value == true)
                        {
                            SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] Using numeric key '{skillKey}' for MOD skill");
                        }
                        return skillKey;
                    }
                    
                    // Try to get the actual skill name from the game
                    string actualSkillName = GetActualModSkillName(st);
                    if (!string.IsNullOrEmpty(actualSkillName) && !string.Equals(actualSkillName, skillKey, StringComparison.Ordinal))
                    {
                        // Check if the actual skill name exists in YAML
                        if (_entriesByName.ContainsKey(actualSkillName))
                        {
                            if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug?.Value == true)
                            {
                                SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] Mapping MOD skill ID {skillId} to actual name '{actualSkillName}'");
                            }
                            return actualSkillName;
                        }
                        
                        // Also try common variations of the skill name
                        string[] nameVariations = GenerateSkillNameVariations(actualSkillName);
                        foreach (string variation in nameVariations)
                        {
                            if (_entriesByName.ContainsKey(variation))
                            {
                                if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug?.Value == true)
                                {
                                    SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] Mapping MOD skill ID {skillId} to variation '{variation}' (from '{actualSkillName}')");
                                }
                                return variation;
                            }
                        }
                    }
                    
                    // Log warning only once per skill ID
                    if (!_warnedSkillIds.Contains(skillId))
                    {
                        _warnedSkillIds.Add(skillId);
                        var availableKeys = string.Join(", ", _entriesByName.Keys.Take(10));
                        string suggestedKey = !string.IsNullOrEmpty(actualSkillName) && !actualSkillName.StartsWith("$") ? actualSkillName : skillKey;
                        SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] MOD skill ID {skillId} (name: '{actualSkillName ?? "Unknown"}') not found in YAML. Available keys: {availableKeys}... Please add '{suggestedKey}' to your YAML file.");
                    }
                }
            }
            
            return skillKey;
        }
        
        // Generate common variations of skill names for YAML lookup
        private static string[] GenerateSkillNameVariations(string skillName)
        {
            if (string.IsNullOrEmpty(skillName)) return new string[0];
            
            var variations = new List<string>();
            
            // Original name
            variations.Add(skillName);
            
            // Remove localization prefix if present
            if (skillName.StartsWith("$"))
            {
                string withoutPrefix = skillName.Substring(1);
                variations.Add(withoutPrefix);
                
                // Try common patterns for localization keys
                if (withoutPrefix.StartsWith("skilldesc_"))
                {
                    variations.Add(withoutPrefix.Substring(10)); // Remove "skilldesc_"
                }
                if (withoutPrefix.EndsWith("Skill"))
                {
                    variations.Add(withoutPrefix.Substring(0, withoutPrefix.Length - 5)); // Remove "Skill"
                }
            }
            
            // Clean version (alphanumeric only)
            string cleaned = CleanSkillNameForYaml(skillName);
            if (!variations.Contains(cleaned))
            {
                variations.Add(cleaned);
            }
            
            return variations.ToArray();
        }
        
        // Helper method to get the actual skill name from the game
        private static string GetActualModSkillName(global::Skills.SkillType skillType)
        {
            try
            {
                // Try to get skill name from localization or skill definition
                var localPlayer = SLE_SkillHelpers.GetSafeLocalPlayer();
                var skillsInstance = localPlayer?.GetSkills();
                if (skillsInstance != null)
                {
                    var skillData = HarmonyLib.Traverse.Create(skillsInstance).Field("m_skillData").GetValue<System.Collections.Generic.Dictionary<global::Skills.SkillType, global::Skills.Skill>>();
                    if (skillData != null && skillData.TryGetValue(skillType, out var skill))
                    {
                        var info = HarmonyLib.Traverse.Create(skill).Field("m_info").GetValue<global::Skills.SkillDef>();
                        if (info != null)
                        {
                            // Try to get the skill identifier (m_skill field) first
                            var skillIdentifier = HarmonyLib.Traverse.Create(info).Field("m_skill").GetValue<global::Skills.SkillType>();
                            if (skillIdentifier != global::Skills.SkillType.None)
                            {
                                string identifierName = skillIdentifier.ToString();
                                // If it's not a numeric ID, use the identifier name
                                if (!int.TryParse(identifierName, out _))
                                {
                                    return identifierName;
                                }
                            }
                            
                            // Try to get localized name from Localization system
                            var descriptionKey = HarmonyLib.Traverse.Create(info).Field("m_description").GetValue<string>();
                            if (!string.IsNullOrEmpty(descriptionKey) && !descriptionKey.StartsWith("$"))
                            {
                                return descriptionKey;
                            }
                            
                            // Try to resolve localization key to actual name
                            if (!string.IsNullOrEmpty(descriptionKey) && descriptionKey.StartsWith("$"))
                            {
                                try
                                {
                                    string localizedName = Localization.instance?.Localize(descriptionKey);
                                    if (!string.IsNullOrEmpty(localizedName) && localizedName != descriptionKey)
                                    {
                                        // Clean up the localized name for YAML compatibility
                                        return CleanSkillNameForYaml(localizedName);
                                    }
                                }
                                catch { /* ignore localization errors */ }
                            }
                        }
                    }
                }
                
                // Fallback: use enum name
                return skillType.ToString();
            }
            catch (System.Exception ex)
            {
                SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] Failed to get actual skill name for {skillType}: {ex.Message}");
                return skillType.ToString();
            }
        }
        
        // Helper method to clean skill names for YAML compatibility
        private static string CleanSkillNameForYaml(string skillName)
        {
            if (string.IsNullOrEmpty(skillName)) return skillName;
            
            // Remove special characters and spaces, keep only alphanumeric and underscores
            var cleaned = System.Text.RegularExpressions.Regex.Replace(skillName, @"[^\w]", "");
            
            // Ensure it starts with a letter or underscore (YAML key requirement)
            if (cleaned.Length > 0 && char.IsDigit(cleaned[0]))
            {
                cleaned = "_" + cleaned;
            }
            
            return string.IsNullOrEmpty(cleaned) ? skillName : cleaned;
        }

        internal static int GetCap(global::Skills.SkillType st)
        {
            string skillKey = GetSkillKeyForLookup(st);
            
            // Parse skill ID once for logging
            bool isSkill1337 = int.TryParse(st.ToString(), out int skillId) && skillId == 1337;
            
            // Skill 1337 specific handling
            if (isSkill1337)
            {
                var debugEntry = _entriesByName?.TryGetValue(skillKey, out var tempEntry) == true ? tempEntry : null;
                SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] Skill 1337 - Key: '{skillKey}', Cap: {debugEntry?.Cap ?? DefaultCapFallback}");
            }
    
            // Client-side: local YAML is enabled
            if (!_isServerConfig && EnableYamlOverride?.Value == true &&
                _entriesByName != null &&
                _entriesByName.TryGetValue(skillKey, out var entry) &&
                entry != null && entry.Cap > 0)
            {
                if (isSkill1337)
                {
                    SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] Skill 1337 using client cap: {entry.Cap}");
                }
                return entry.Cap;
            }
    
            // Applying server-distributed YAML
            if (_isServerConfig &&
                _entriesByName != null &&
                _entriesByName.TryGetValue(skillKey, out var serverEntry) &&
                serverEntry != null && serverEntry.Cap > 0)
            {
                if (isSkill1337)
                {
                    SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] Skill 1337 using server cap: {serverEntry.Cap}");
                }
                return serverEntry.Cap;
            }
    
            if (isSkill1337)
            {
                SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] Skill 1337 using fallback cap: {DefaultCapFallback}");
            }
            return DefaultCapFallback;
        }

        /// <summary>
        /// CartographySkill専用の上限取得メソッド
        /// </summary>
        internal static int GetCartographySkillCap()
        {
            // CartographySkillのスキルタイプを取得（通常は数値ID）
            try
            {
                // YAMLでCartographySkillが定義されているかチェック
                if (_entriesByName != null)
                {
                    // 一般的なCartographySkillの名前でチェック
                    string[] possibleKeys = { "Cartography", "CartographySkill", "1337" };
                    
                    foreach (var key in possibleKeys)
                    {
                        if (_entriesByName.TryGetValue(key, out var entry) && entry != null && entry.Cap > 0)
                        {
                            if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug?.Value == true)
                            {
                                SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] CartographySkill cap found in YAML with key '{key}': {entry.Cap}");
                            }
                            return entry.Cap;
                        }
                    }
                }
                
                if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug?.Value == true)
                {
                    SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] CartographySkill using default cap: {DefaultCapFallback}");
                }
                return DefaultCapFallback;
            }
            catch (Exception ex)
            {
                SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] Error getting CartographySkill cap: {ex.Message}");
                return DefaultCapFallback;
            }
        }

        internal static int GetBonusCap(global::Skills.SkillType st)
        {
            string skillKey = GetSkillKeyForLookup(st);

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
            string skillKey = GetSkillKeyForLookup(st);
            if (_entriesByName != null && _entriesByName.TryGetValue(skillKey, out var entry) && entry != null)
                return entry.Relative;
            return true; // Default to relative scaling
        }

        // Growth curve parameters
        internal static float GetGrowthExponent(global::Skills.SkillType st)
        {
            string skillKey = GetSkillKeyForLookup(st);
            if (_entriesByName != null && _entriesByName.TryGetValue(skillKey, out var entry) && entry != null)
                return entry.GrowthExponent;
            return 1.5f; // Vanilla default
        }

        internal static float GetGrowthMultiplier(global::Skills.SkillType st)
        {
            string skillKey = GetSkillKeyForLookup(st);
            if (_entriesByName != null && _entriesByName.TryGetValue(skillKey, out var entry) && entry != null)
                return entry.GrowthMultiplier;
            return 0.5f; // Vanilla default
        }

        internal static float GetGrowthConstant(global::Skills.SkillType st)
        {
            string skillKey = GetSkillKeyForLookup(st);
            if (_entriesByName != null && _entriesByName.TryGetValue(skillKey, out var entry) && entry != null)
                return entry.GrowthConstant;
            return 0.5f; // Vanilla default
        }

        internal static bool UseCustomGrowthCurve(global::Skills.SkillType st)
        {
            string skillKey = GetSkillKeyForLookup(st);
            if (_entriesByName != null && _entriesByName.TryGetValue(skillKey, out var entry) && entry != null)
                return entry.UseCustomGrowthCurve;
            return false; // Default: use vanilla curve
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
        internal static float GetUiDenominator() 
        {
            try
            {
                // Always return a safe value, even if configuration is corrupted
                var result = Math.Max(1, DefaultCapFallback);
                if (result <= 0 || float.IsNaN(result) || float.IsInfinity(result))
                {
                    SkillLimitExtenderPlugin.Logger?.LogWarning("[SLE] GetUiDenominator: Invalid result, using fallback 100f");
                    return 100f;
                }
                return result;
            }
            catch (Exception ex)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] GetUiDenominator failed: {ex.Message}, using fallback 100f");
                return 100f; // Safe fallback
            }
        }
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
                        if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug?.Value == true)
                        {
                            SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] UI denominator for {st}: {result} (level={s.m_level})");
                        }
                        return result;
                    }
                }
            }
            catch (System.Exception e)
            {
                SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] GetUiDenominatorForSkillSafe failed: {e.Message}");
            }
            float fallback = GetUiDenominator();
            if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug?.Value == true)
            {
                SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] UI denominator fallback: {fallback}");
            }
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
                    if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug?.Value == true)
                    {
                        SkillLimitExtenderPlugin.Logger?.LogDebug("[SLE] YAML unchanged; broadcast skipped.");
                    }
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