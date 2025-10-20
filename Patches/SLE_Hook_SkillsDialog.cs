using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace SkillLimitExtender
{
    /// <summary>
    /// Transpiler that replaces "/ 100f" with global UI denominator inside SkillsDialog.Setup.
    /// Always uses the same denominator for both levelbar and levelbar_total to ensure consistent scaling.
    /// </summary>
    [HarmonyPatch(typeof(global::SkillsDialog), nameof(global::SkillsDialog.Setup))]
    internal static class SLE_Hook_SkillsDialog_LevelBars
    {
        [HarmonyPrefix]
        private static bool Prefix(global::SkillsDialog __instance, ref Player player)
        {
            try
            {
                SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] SkillsDialog.Setup - Player: {player?.GetPlayerName() ?? "null"}");
                
                // Check SkillsDialog instance
                if (__instance == null)
                {
                    SkillLimitExtenderPlugin.Logger?.LogError("[SLE] SkillsDialog instance is null - cannot proceed");
                    return false;
                }

                // Null check for player parameter to prevent NullReferenceException
                if (player == null)
                {
                    SkillLimitExtenderPlugin.Logger?.LogWarning("[SLE] SkillsDialog.Setup called with null player - attempting to get local player");
                    
                    // Try to get local player as fallback
                    var localPlayer = SLE_SkillHelpers.GetSafeLocalPlayer();
                    if (localPlayer != null)
                    {
                        SkillLimitExtenderPlugin.Logger?.LogInfo("[SLE] Successfully recovered local player for SkillsDialog.Setup");
                        player = localPlayer;
                    }
                    else
                    {
                        SkillLimitExtenderPlugin.Logger?.LogError("[SLE] Cannot get local player - skipping SkillsDialog.Setup");
                        return false; // Skip original method
                    }
                }

                // Additional safety check for player skills
                var skills = player.GetSkills();
                if (skills == null)
                {
                    SkillLimitExtenderPlugin.Logger?.LogWarning("[SLE] Player skills are null - skipping SkillsDialog.Setup");
                    return false; // Skip original method
                }

                // Verify SkillConfigManager state before proceeding
                try
                {
                    // Test if SkillConfigManager is in a valid state
                    var testCap = SkillConfigManager.GetCap(global::Skills.SkillType.Swords);
                    if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug?.Value == true)
                    {
                        SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] SkillConfigManager test - Swords cap: {testCap}");
                    }
                }
                catch (Exception configEx)
                {
                    SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] SkillConfigManager is in invalid state: {configEx.Message}");
                    return false; // Skip original method to prevent crash
                }

                // Check for MOD skills that might cause issues
                try
                {
                    var skillData = HarmonyLib.Traverse.Create(skills).Field("m_skillData").GetValue<System.Collections.Generic.Dictionary<global::Skills.SkillType, global::Skills.Skill>>();
                    if (skillData != null)
                    {
                        int modSkillCount = 0;
                        int nullInfoCount = 0;
                        
                        foreach (var kvp in skillData)
                        {
                            var skillType = kvp.Key;
                            var skill = kvp.Value;
                            
                            // Check for MOD skills (ID > 999)
                            if ((int)skillType > 999)
                            {
                                modSkillCount++;
                                
                                if (skill == null)
                                {
                                    SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] MOD skill {skillType} ({(int)skillType}) is null in m_skillData");
                                    continue;
                                }
                                
                                var info = HarmonyLib.Traverse.Create(skill).Field("m_info").GetValue<global::Skills.SkillDef>();
                                if (info == null)
                                {
                                    nullInfoCount++;
                                    if ((int)skillType == 1337)
                                    {
                                        SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] MOD skill 1337 has null m_info (level={skill.m_level})");
                                    }
                                    else
                                    {
                                        if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug?.Value == true)
                                        {
                                            SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] MOD skill {skillType} ({(int)skillType}) has null m_info (level={skill.m_level})");
                                        }
                                    }
                                }
                            }
                        }
                        SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] Found {modSkillCount} MOD skills ({nullInfoCount} with null m_info)");
                    }
                }
                catch (Exception skillCheckEx)
                {
                    SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] Could not check MOD skills: {skillCheckEx.Message}");
                }

                if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug?.Value == true)
                {
                    SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] SkillsDialog.Setup proceeding with player: {player.GetPlayerName()}");
                }
                return true; // Continue with original method
            }
            catch (System.Exception ex)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] SkillsDialog.Setup Prefix failed: {ex}");
                return false; // Skip original method to prevent crash
            }
        }

        [HarmonyTranspiler]
        [HarmonyPriority(-9000)]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            try
            {
                var codes = new List<CodeInstruction>(instructions);
                var mi_GetUiDenominator = AccessTools.Method(typeof(SkillConfigManager), nameof(SkillConfigManager.GetUiDenominator));

                if (mi_GetUiDenominator == null)
                {
                    SkillLimitExtenderPlugin.Logger?.LogWarning("[SLE] SkillsDialog: GetUiDenominator method missing; keeping original code");
                    return codes;
                }

                // Additional safety check - verify SkillConfigManager is in valid state
                try
                {
                    var testValue = SkillConfigManager.GetUiDenominator();
                    if (testValue <= 0)
                    {
                        SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] SkillsDialog: GetUiDenominator returned invalid value {testValue}; keeping original code");
                        return codes;
                    }
                }
                catch (Exception testEx)
                {
                    SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] SkillsDialog: GetUiDenominator test failed: {testEx.Message}; keeping original code");
                    return codes;
                }

                int replacementCount = 0;

                for (int i = 0; i < codes.Count - 1; i++)
                {
                    var instr = codes[i];
                    var nextInstr = codes[i + 1];

                    if (instr.opcode == OpCodes.Ldc_R4 &&
                        instr.operand is float f &&
                        Math.Abs(f - 100f) < 0.0001f &&
                        nextInstr.opcode == OpCodes.Div)
                    {
                        // Check if this is in a SetValue context
                        bool isSetValueContext = false;
                        for (int j = i + 1; j < Math.Min(codes.Count, i + 12); j++)
                        {
                            if (codes[j].opcode == OpCodes.Callvirt &&
                                codes[j].operand?.ToString()?.Contains("SetValue") == true)
                            {
                                isSetValueContext = true;
                                break;
                            }
                        }

                        if (!isSetValueContext) continue;

                        // Replace with global UI denominator for consistent scaling
                        codes[i] = new CodeInstruction(OpCodes.Call, mi_GetUiDenominator);
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Conv_R4));
                        i++; // Skip the inserted instruction
                        replacementCount++;
                    }
                }

                SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] SkillsDialog: Replaced {replacementCount} instances of 100f with global UI denominator");
                return codes;
            }
            catch (System.Exception ex)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] SkillsDialog Transpiler failed: {ex}");
                // Return original instructions on error to prevent crashes
                return instructions;
            }
        }
    }
}