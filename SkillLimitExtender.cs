using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;

namespace SkillLimitExtender
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class SkillLimitExtenderPlugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "SkillLimitExtender";
        internal const string PluginName = "SkillLimitExtender";
        internal const string PluginVersion = "1.0.0";

        internal new static ManualLogSource Logger { get; private set; } = null!;
        private readonly Harmony _harmony = new(PluginGuid);

        private void Awake()
        {
            Logger = base.Logger;

            try
            {
                // 設定と初回YAML生成
                SkillConfigManager.Initialize(Config);
                YamlExporter.EnsureYamlExists();

                // Harmony パッチ適用
                _harmony.PatchAll(typeof(SkillLimitExtenderPlugin).Assembly);

                Logger.LogInfo("[SLE] Plugin loaded successfully");
            }
            catch (Exception e)
            {
                Logger.LogError($"[SLE] Awake failed: {e}");
            }
        }

        private void OnDestroy()
        {
            try { _harmony?.UnpatchSelf(); }
            catch (Exception e) { Logger.LogError($"[SLE] UnpatchSelf failed: {e}"); }
        }
    }
}