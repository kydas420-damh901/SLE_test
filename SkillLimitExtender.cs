
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using Jotunn;                 // Jötunn
using Jotunn.Managers;       // ItemManager

namespace SkillLimitExtender
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Main.ModGuid, BepInDependency.DependencyFlags.HardDependency)] // Jötunn必須
    public class SkillLimitExtenderPlugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "com.author.SkillLimitExtender";
        internal const string PluginName = "SkillLimitExtender";
        internal const string PluginVersion = "1.0.0";

        internal static ManualLogSource Logger { get; private set; }
        private readonly Harmony _harmony = new(PluginGuid);

        private void Awake()
        {
            Logger = base.Logger;

            try
            {
                // 設定の初期化＆YAML初期生成
                SkillConfigManager.Initialize(Config);
                SkillYamlExporter.EnsureYamlExists();

                // Harmony適用（本プラグインの全パッチ）
                _harmony.PatchAll(typeof(SkillLimitExtenderPlugin).Assembly);

                // 全MOD読み込み後（Gameシーン）に一回だけスキャン
                SLE_JotunnLateScanner.HookOnceAtGameScene();

                Logger.LogInfo("[SLE] Awake OK");
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
