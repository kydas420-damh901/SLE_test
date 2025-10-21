using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SkillLimitExtender
{
    /// <summary>
    /// Shared helper methods for skill-related patches
    /// </summary>
    internal static class SLE_SkillHelpers
    {
        // Helper method to safely get the local player
        internal static Player? GetSafeLocalPlayer()
        {
            try
            {
                // Try multiple ways to get the local player safely
                if (Player.m_localPlayer != null)
                    return Player.m_localPlayer;
                
                // Alternative: try to find local player through other means
                if (Game.instance?.GetPlayerProfile()?.GetPlayerID() != 0)
                {
                    var players = Player.GetAllPlayers();
                    if (players != null && players.Count > 0)
                    {
                        foreach (var player in players)
                        {
                            if (player != null && player.IsOwner())
                            {
                                return player;
                            }
                        }
                    }
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Transpiler that replaces the 100f hardcode in Skills.Skill.Raise with the skill's cap.
    /// This allows normal growth up to the cap (YAML/config).
    /// </summary>
    [HarmonyPatch(typeof(global::Skills.Skill), nameof(global::Skills.Skill.Raise))]
    internal static class SLE_Skill_Raise_Transpiler
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            // Acquire field and method references
            var fi_m_info = AccessTools.Field(typeof(global::Skills.Skill), "m_info");
            var fi_m_skill = AccessTools.Field(typeof(global::Skills.SkillDef), "m_skill");
            var mi_GetCap = AccessTools.Method(typeof(SkillConfigManager), nameof(SkillConfigManager.GetCap));

            if (fi_m_info == null || fi_m_skill == null || mi_GetCap == null)
            {
                SkillLimitExtenderPlugin.Logger?.LogError("[SLE] Transpiler failed: Cannot find required fields/methods");
                return codes; // Safety: return original IL if nothing can be replaced
            }

            bool replacementMade = false;
            int replacementCount = 0;
            // Replacement logic:
            //   ldc.r4 100.0 → (ldarg.0 → ldfld m_info → ldfld m_skill → call GetCap → conv.r4)
            for (int i = 0; i < codes.Count; i++)
            {
                var instr = codes[i];

                if (instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f && System.Math.Abs(f - 100f) < 0.0001f)
                {
                    // Replace 100f with cap - enhanced for MOD skills
                    // ldarg.0                         // this (Skills.Skill)
                    // call      GetSkillTypeFromInstance  // Skills.SkillType (handles MOD skills)
                    // call      int SkillConfigManager.GetCap(Skills.SkillType)
                    // conv.r4
                    var newSeq = new List<CodeInstruction>
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SLE_Skill_Raise_Transpiler), nameof(GetSkillTypeFromInstance))),
                        new CodeInstruction(OpCodes.Call, mi_GetCap),
                        new CodeInstruction(OpCodes.Conv_R4)
                    };

                    // Replace current slot with the first instruction and insert the rest after
                    codes[i] = newSeq[0];
                    codes.InsertRange(i + 1, newSeq.GetRange(1, newSeq.Count - 1));

                    // Advance i to avoid jumping over the inserted sequence
                    i += newSeq.Count - 1;
                    
                    replacementCount++;
                    SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] Skill.Raise: Replaced 100f #{replacementCount} with GetCap(GetSkillTypeFromInstance()) at position {i}");
                    replacementMade = true;
                    // Continue to replace ALL occurrences, not just the first one
                }
            }

            if (!replacementMade)
            {
                SkillLimitExtenderPlugin.Logger?.LogWarning("[SLE] Transpiler: No 100f constant found to replace in Skills.Skill.Raise");
            }

            return codes;
        }
        
        // Helper method for Transpiler to get skill type from Skills.Skill instance
        internal static global::Skills.SkillType GetSkillTypeFromInstance(global::Skills.Skill skillInstance)
        {
            try
            {
                // First try the normal way
                var info = HarmonyLib.Traverse.Create(skillInstance).Field("m_info").GetValue<global::Skills.SkillDef>();
                if (info != null)
                {
                    var skillType = HarmonyLib.Traverse.Create(info).Field("m_skill").GetValue<global::Skills.SkillType>();
                    if (int.TryParse(skillType.ToString(), out int skillId) && skillId == 1337)
                    {
                        SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE DEBUG Transpiler] Skill 1337 found via m_info path, returning: {skillType}");
                    }
                    return skillType;
                }

                // For MOD skills with null m_info, search in Skills.m_skillData
                var localPlayer = SLE_SkillHelpers.GetSafeLocalPlayer();
                var skillsInstance = localPlayer?.GetSkills();
                if (skillsInstance != null)
                {
                    var skillData = HarmonyLib.Traverse.Create(skillsInstance).Field("m_skillData").GetValue<System.Collections.Generic.Dictionary<global::Skills.SkillType, global::Skills.Skill>>();
                    if (skillData != null)
                    {
                        foreach (var kv in skillData)
                        {
                            if (ReferenceEquals(kv.Value, skillInstance))
                            {
                                if (int.TryParse(kv.Key.ToString(), out int skillId) && skillId == 1337)
                                {
                                    SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE DEBUG Transpiler] Skill 1337 found via skillData search, returning: {kv.Key}");
                                }
                                return kv.Key;
                            }
                        }
                    }
                }

                // Fallback: return None (will use default cap)
                SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE DEBUG Transpiler] Could not find skill type for instance, returning None");
                return global::Skills.SkillType.None;
            }
            catch (Exception ex)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE DEBUG Transpiler] Exception in GetSkillTypeFromInstance: {ex.Message}");
                return global::Skills.SkillType.None;
            }
        }
    }

    /// <summary>
    /// Debug patches for Skills.Skill.Raise to track level changes
    /// </summary>
    [HarmonyPatch(typeof(global::Skills.Skill), nameof(global::Skills.Skill.Raise))]
    internal static class SLE_Skill_Raise_Debug
    {
        [HarmonyPrefix]
        private static void Prefix(global::Skills.Skill __instance, float factor, out float __state)
        {
            // Store the current level for comparison in Postfix
            __state = __instance.m_level;
            
            // Log for skill 1337 specifically
            var skillType = SLE_Skill_Raise_Transpiler.GetSkillTypeFromInstance(__instance);
            if (int.TryParse(skillType.ToString(), out int skillId) && skillId == 1337)
            {
                SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE DEBUG Raise] BEFORE - Skill 1337: Level={__instance.m_level:F2}, Accumulator={__instance.m_accumulator:F2}, Factor={factor:F4}");
            }
        }

        [HarmonyPostfix]
        private static void Postfix(global::Skills.Skill __instance, float factor, float __state)
        {
            // Log for skill 1337 specifically
            var skillType = SLE_Skill_Raise_Transpiler.GetSkillTypeFromInstance(__instance);
            if (int.TryParse(skillType.ToString(), out int skillId) && skillId == 1337)
            {
                float levelChange = __instance.m_level - __state;
                SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE DEBUG Raise] AFTER - Skill 1337: Level={__instance.m_level:F2} (change: {levelChange:F2}), Accumulator={__instance.m_accumulator:F2}");
                
                if (levelChange > 0)
                {
                    SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE DEBUG Raise] *** LEVEL UP DETECTED *** Skill 1337 leveled up by {levelChange:F2}!");
                }
                if (__instance.m_level >= 100f)
                {
                    SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] Skill 1337 reached level {__instance.m_level:F2} (cap: 250)");
                }
            }
        }
    }



    /// <summary>
    /// Patch for Skills.Skill.GetLevelPercentage to customize growth curves
    /// We patch GetLevelPercentage instead of GetNextLevelRequirement because GetNextLevelRequirement is private
    /// </summary>
    [HarmonyPatch(typeof(global::Skills.Skill), nameof(global::Skills.Skill.GetLevelPercentage))]
    internal static class SLE_Skill_GetLevelPercentage_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::Skills.Skill __instance, ref float __result)
        {
            try
            {
                // Get skill type first to check actual cap
                var info = HarmonyLib.Traverse.Create(__instance).Field("m_info").GetValue<global::Skills.SkillDef>();
                global::Skills.SkillType skillType;
                
                if (info == null)
                {
                    // For MOD skills with invalid m_info, try to get skill type from Skills.m_skillData
                    var localPlayer = SLE_SkillHelpers.GetSafeLocalPlayer();
                    var skillsInstance = localPlayer?.GetSkills();
                    if (skillsInstance != null)
                    {
                        var skillData = HarmonyLib.Traverse.Create(skillsInstance).Field("m_skillData").GetValue<System.Collections.Generic.Dictionary<global::Skills.SkillType, global::Skills.Skill>>();
                        if (skillData != null)
                        {
                            foreach (var kv in skillData)
                            {
                                if (ReferenceEquals(kv.Value, __instance))
                                {
                                    skillType = kv.Key;
                                    if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug?.Value == true)
                    {
                        SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] Found MOD skill type {skillType} for skill with null m_info");
                    }
                                    goto SkillTypeFound;
                                }
                            }
                        }
                    }
                    return true; // Fall back to vanilla if we can't find the skill type
                }
                else
                {
                    skillType = HarmonyLib.Traverse.Create(info).Field("m_skill").GetValue<global::Skills.SkillType>();
                }

                SkillTypeFound:
                int skillCap = SkillConfigManager.GetCap(skillType);
                
                // Check if level is at actual cap (not hardcoded 100)
                if (__instance.m_level >= skillCap)
                {
                    __result = 0f;
                    return false;
                }

                // Calculate next level requirement using our custom logic
                float nextLevelRequirement = CalculateNextLevelRequirement(__instance, skillType);
                
                // Calculate percentage
                __result = UnityEngine.Mathf.Clamp01(__instance.m_accumulator / nextLevelRequirement);
                
                return false; // Skip vanilla implementation
            }
            catch (System.Exception ex)
            {
                if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug.Value)
                {
                    SkillLimitExtenderPlugin.Logger?.LogError($"[SLE Growth Curve] Error in GetLevelPercentage patch: {ex.Message}");
                }
                return true; // Fall back to vanilla on error
            }
        }

        private static float CalculateNextLevelRequirement(global::Skills.Skill skill, global::Skills.SkillType skillType)
        {
            try
            {
                // Check if custom growth curve is enabled for this skill
                bool useCustomCurve = SkillConfigManager.UseCustomGrowthCurve(skillType);
                
                float level = skill.m_level;
                float result;
                
                if (!useCustomCurve)
                {
                    // Use vanilla calculation: Mathf.Pow(Mathf.Floor(level + 1f), 1.5f) * 0.5f + 0.5f
                    result = UnityEngine.Mathf.Pow(UnityEngine.Mathf.Floor(level + 1f), 1.5f) * 0.5f + 0.5f;
                    
                    // Debug logging if enabled
                    if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug.Value)
                    {
                        SkillLimitExtenderPlugin.Logger?.LogInfo(
                            $"[SLE Growth Curve] Skill: {skillType}, Level: {level:F1}, " +
                            $"Mode: Vanilla, Result: {result:F2}"
                        );
                    }
                }
                else
                {
                    // Get custom growth curve parameters
                    float exponent = SkillConfigManager.GetGrowthExponent(skillType);
                    float multiplier = SkillConfigManager.GetGrowthMultiplier(skillType);
                    float constant = SkillConfigManager.GetGrowthConstant(skillType);
                    
                    // Calculate custom growth curve: multiplier * (level + constant) ^ exponent
                    result = multiplier * UnityEngine.Mathf.Pow(level + constant, exponent);
                    
                    // Debug logging if enabled
                    if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug.Value)
                    {
                        float vanillaResult = UnityEngine.Mathf.Pow(UnityEngine.Mathf.Floor(level + 1f), 1.5f) * 0.5f + 0.5f;
                        SkillLimitExtenderPlugin.Logger?.LogInfo(
                            $"[SLE Growth Curve] Skill: {skillType}, Level: {level:F1}, " +
                            $"Mode: Custom, Params: exp={exponent:F2}, mult={multiplier:F2}, const={constant:F2}, " +
                            $"Vanilla: {vanillaResult:F2}, Custom: {result:F2}"
                        );
                    }
                }
                
                return result;
            }
            catch (System.Exception ex)
            {
                if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug.Value)
                {
                    SkillLimitExtenderPlugin.Logger?.LogError($"[SLE Growth Curve] Error in CalculateNextLevelRequirement: {ex.Message}");
                }
                // Fall back to vanilla calculation on error
                float level = skill.m_level;
                return UnityEngine.Mathf.Pow(UnityEngine.Mathf.Floor(level + 1f), 1.5f) * 0.5f + 0.5f;
            }
        }
    }

    /// <summary>
    /// Patch for Skills.Skill.GetLevelPercentage to customize growth curves
    /// We patch GetLevelPercentage instead of GetNextLevelRequirement because GetNextLevelRequirement is private
    /// </summary>
    [HarmonyPatch(typeof(global::Skills.Skill), nameof(global::Skills.Skill.GetLevelPercentage))]
    internal static class SLE_Skill_GetLevelPercentage_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::Skills.Skill __instance, ref float __result)
        {
            try
            {
                // Get skill type first to check actual cap
                var info = HarmonyLib.Traverse.Create(__instance).Field("m_info").GetValue<global::Skills.SkillDef>();
                if (info == null) return true; // Fall back to vanilla

                var skillType = HarmonyLib.Traverse.Create(info).Field("m_skill").GetValue<global::Skills.SkillType>();
                int skillCap = SkillConfigManager.GetCap(skillType);
                
                // Check if level is at actual cap (not hardcoded 100)
                if (__instance.m_level >= skillCap)
                {
                    __result = 0f;
                    return false;
                }

                // Calculate next level requirement using our custom logic
                float nextLevelRequirement = CalculateNextLevelRequirement(__instance, skillType);
                
                // Calculate percentage
                __result = UnityEngine.Mathf.Clamp01(__instance.m_accumulator / nextLevelRequirement);
                
                return false; // Skip vanilla implementation
            }
            catch (System.Exception ex)
            {
                if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug.Value)
                {
                    SkillLimitExtenderPlugin.Logger?.LogError($"[SLE Growth Curve] Error in GetLevelPercentage patch: {ex.Message}");
                }
                return true; // Fall back to vanilla on error
            }
        }

        private static float CalculateNextLevelRequirement(global::Skills.Skill skill, global::Skills.SkillType skillType)
        {
            try
            {
                // Check if custom growth curve is enabled for this skill
                bool useCustomCurve = SkillConfigManager.UseCustomGrowthCurve(skillType);
                
                float level = skill.m_level;
                float result;
                
                if (!useCustomCurve)
                {
                    // Use vanilla calculation: Mathf.Pow(Mathf.Floor(level + 1f), 1.5f) * 0.5f + 0.5f
                    result = UnityEngine.Mathf.Pow(UnityEngine.Mathf.Floor(level + 1f), 1.5f) * 0.5f + 0.5f;
                    
                    // Debug logging if enabled
                    if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug.Value)
                    {
                        SkillLimitExtenderPlugin.Logger?.LogInfo(
                            $"[SLE Growth Curve] Skill: {skillType}, Level: {level:F1}, " +
                            $"Mode: Vanilla, Result: {result:F2}"
                        );
                    }
                }
                else
                {
                    // Get custom growth curve parameters
                    float exponent = SkillConfigManager.GetGrowthExponent(skillType);
                    float multiplier = SkillConfigManager.GetGrowthMultiplier(skillType);
                    float constant = SkillConfigManager.GetGrowthConstant(skillType);
                    
                    // Calculate custom growth curve: multiplier * (level + constant) ^ exponent
                    result = multiplier * UnityEngine.Mathf.Pow(level + constant, exponent);
                    
                    // Debug logging if enabled
                    if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug.Value)
                    {
                        float vanillaResult = UnityEngine.Mathf.Pow(UnityEngine.Mathf.Floor(level + 1f), 1.5f) * 0.5f + 0.5f;
                        SkillLimitExtenderPlugin.Logger?.LogInfo(
                            $"[SLE Growth Curve] Skill: {skillType}, Level: {level:F1}, " +
                            $"Mode: Custom, Params: exp={exponent:F2}, mult={multiplier:F2}, const={constant:F2}, " +
                            $"Vanilla: {vanillaResult:F2}, Custom: {result:F2}"
                        );
                    }
                }
                
                return result;
            }
            catch (System.Exception ex)
            {
                if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug.Value)
                {
                    SkillLimitExtenderPlugin.Logger?.LogError($"[SLE Growth Curve] Error in CalculateNextLevelRequirement: {ex.Message}");
                }
                // Fall back to vanilla calculation on error
                float level = skill.m_level;
                return UnityEngine.Mathf.Pow(UnityEngine.Mathf.Floor(level + 1f), 1.5f) * 0.5f + 0.5f;
            }
        }
    }
}
