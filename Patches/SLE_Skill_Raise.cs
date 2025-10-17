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
}
