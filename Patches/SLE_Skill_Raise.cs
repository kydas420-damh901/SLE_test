using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SkillLimitExtender
{
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
                return codes; // Safety: return original IL if nothing can be replaced

            // Replacement logic:
            //   ldc.r4 100.0 → (ldarg.0 → ldfld m_info → ldfld m_skill → call GetCap → conv.r4)
            for (int i = 0; i < codes.Count; i++)
            {
                var instr = codes[i];

                if (instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f && System.Math.Abs(f - 100f) < 0.0001f)
                {
                    // Replace 100f with cap
                    // ldarg.0                         // this (Skills.Skill)
                    // ldfld     Skills.Skill::m_info
                    // ldfld     Skills.SkillDef::m_skill
                    // call      int SkillConfigManager.GetCap(Skills.SkillType)
                    // conv.r4
                    var newSeq = new List<CodeInstruction>
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, fi_m_info),
                        new CodeInstruction(OpCodes.Ldfld, fi_m_skill),
                        new CodeInstruction(OpCodes.Call, mi_GetCap),
                        new CodeInstruction(OpCodes.Conv_R4)
                    };

                    // Replace current slot with the first instruction and insert the rest after
                    codes[i] = newSeq[0];
                    codes.InsertRange(i + 1, newSeq.GetRange(1, newSeq.Count - 1));

                    // Advance i to avoid jumping over the inserted sequence
                    i += newSeq.Count - 1;
                }
            }

            return codes;
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
