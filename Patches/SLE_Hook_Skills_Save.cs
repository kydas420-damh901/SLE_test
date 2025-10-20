using System;
using System.Collections.Generic;
using HarmonyLib;

namespace SkillLimitExtender
{
    /// <summary>
    /// Cleans up invalid Skill entries (null/uninitialized) before Skills.Save runs.
    /// Safely avoids rare cases where other mods or inconsistencies insert nulls into m_skillData.
    /// </summary>
    [HarmonyPatch(typeof(global::Skills), nameof(global::Skills.Save))]
    internal static class SLE_Hook_Skills_Save_Cleanup
    {
        [HarmonyPrefix]
        private static bool Prefix(global::Skills __instance, ZPackage pkg)
        {
            try
            {
                var fiSkillData = AccessTools.Field(typeof(global::Skills), "m_skillData");
                var dict = fiSkillData?.GetValue(__instance) as IDictionary<global::Skills.SkillType, global::Skills.Skill>;
                if (dict == null) 
                {
                    SkillLimitExtenderPlugin.Logger?.LogError("[SLE] Skills.Save: m_skillData is null!");
                    // Write minimal valid data to prevent corruption
                    pkg.Write(2); // version
                    pkg.Write(0); // skill count
                    return false; // Skip original method
                }

                var validSkills = new List<KeyValuePair<global::Skills.SkillType, global::Skills.Skill>>();
                var invalidCount = 0;

                foreach (var kv in dict)
                {
                    var skillType = kv.Key;
                    var skill = kv.Value;
                    
                    if (skill == null)
                    {
                        SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] Skills.Save: Skipping null skill {skillType}");
                        invalidCount++;
                        continue;
                    }

                    // Check m_info field (referenced during Save)
                    try
                    {
                        var info = HarmonyLib.Traverse.Create(skill).Field("m_info").GetValue<global::Skills.SkillDef>();
                        
                        // For MOD skills, m_info might be null but the skill is still valid
                        if (info == null)
                        {
                            // Check if this is a MOD skill (ID > 999) OR defined in YAML
                            int skillId = (int)skillType;
                            string skillKey = skillType.ToString();
                            
                            // Allow MOD skills (ID > 999) or skills defined in YAML
                            if (skillId > 999 || IsSkillDefinedInYaml(skillKey))
                            {
                                if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug?.Value == true)
                                {
                                    SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] Skills.Save: Including custom skill {skillType} (ID: {skillId}) with null m_info");
                                }
                                validSkills.Add(kv);
                                continue;
                            }
                            else
                            {
                                // Skip SkillType.None silently (it's expected to have null m_info)
                                if (skillType != global::Skills.SkillType.None)
                                {
                                    SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] Skills.Save: Skipping vanilla skill {skillType} with null m_info");
                                    invalidCount++;
                                }
                                continue;
                            }
                        }

                        // Check m_info.m_skill field (directly accessed in Save)
                        var infoSkillType = HarmonyLib.Traverse.Create(info).Field("m_skill").GetValue<global::Skills.SkillType>();
                        
                        // For MOD skills, m_info.m_skill might not match the dictionary key or be undefined in enum
                        int currentSkillId = (int)skillType;
                        
                        // MOD skills (ID > 999) are always allowed, regardless of m_info.m_skill validation
                        if (currentSkillId > 999)
                        {
                            if (!Enum.IsDefined(typeof(global::Skills.SkillType), infoSkillType))
                            {
                                if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug?.Value == true)
                                {
                                    SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] Skills.Save: Including MOD skill {skillType} (ID: {currentSkillId}) with undefined m_info.m_skill value {infoSkillType}");
                                }
                            }
                            else
                            {
                                if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug?.Value == true)
                                {
                                    SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] Skills.Save: Including MOD skill {skillType} (ID: {currentSkillId})");
                                }
                            }
                            validSkills.Add(kv);
                            continue;
                        }
                        
                        // For vanilla skills, check if m_info.m_skill is defined in enum
                        if (!Enum.IsDefined(typeof(global::Skills.SkillType), infoSkillType))
                        {
                            SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] Skills.Save: Skipping vanilla skill '{skillType}' (ID: {currentSkillId}) with invalid m_info.m_skill value {infoSkillType}");
                            invalidCount++;
                            continue;
                        }
                        
                        // For vanilla skills, ensure m_info.m_skill matches the dictionary key
                        if (currentSkillId <= 999 && infoSkillType != skillType)
                        {
                            SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] Skills.Save: Skipping skill '{skillType}' (ID: {currentSkillId}) with mismatched m_info.m_skill value {infoSkillType}");
                            invalidCount++;
                            continue;
                        }

                        // Skill is valid, add to save list
                        validSkills.Add(kv);
                    }
                    catch (Exception ex)
                    {
                        SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] Skills.Save: Skipping skill {skillType} due to error: {ex.Message}");
                        invalidCount++;
                    }
                }

                // Write the safe version of Skills.Save
                pkg.Write(2); // version
                pkg.Write(validSkills.Count);
                
                foreach (var kv in validSkills)
                {
                    try
                    {
                        var skill = kv.Value;
                        var skillType = kv.Key;
                        var info = HarmonyLib.Traverse.Create(skill).Field("m_info").GetValue<global::Skills.SkillDef>();
                        
                        // For MOD skills with null m_info, use the dictionary key as skill type
                        global::Skills.SkillType writeSkillType;
                        if (info != null)
                        {
                            writeSkillType = HarmonyLib.Traverse.Create(info).Field("m_skill").GetValue<global::Skills.SkillType>();
                        }
                        else
                        {
                            // MOD skill with null m_info - use the key directly
                            writeSkillType = skillType;
                        }
                        
                        pkg.Write((int)writeSkillType);
                        pkg.Write(skill.m_level);
                        pkg.Write(skill.m_accumulator);
                    }
                    catch (Exception ex)
                    {
                        SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] Skills.Save: Failed to write skill {kv.Key}: {ex.Message}");
                        // Continue with other skills
                    }
                }

                if (invalidCount > 0)
                {
                    SkillLimitExtenderPlugin.Logger?.LogWarning($"[SLE] Skills.Save: Saved {validSkills.Count} valid skills, skipped {invalidCount} invalid skills");
                }
                else
                {
                    if (SkillLimitExtenderPlugin.EnableGrowthCurveDebug?.Value == true)
                {
                    SkillLimitExtenderPlugin.Logger?.LogDebug($"[SLE] Skills.Save: Successfully saved {validSkills.Count} skills");
                }
                }

                return false; // Skip original method
            }
            catch (Exception e)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] Skills.Save replacement failed: {e}");
                // Fall back to original method as last resort
                return true;
            }
        }

        /// <summary>
        /// Check if a skill is defined in YAML configuration
        /// </summary>
        private static bool IsSkillDefinedInYaml(string skillKey)
        {
            try
            {
                // Access SkillConfigManager's internal state via reflection
                var entriesField = typeof(SkillConfigManager).GetField("_entriesByName", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                
                if (entriesField?.GetValue(null) is Dictionary<string, YamlExporter.SkillYamlEntry> entries)
                {
                    return entries.ContainsKey(skillKey);
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}