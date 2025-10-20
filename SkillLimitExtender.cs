using BepInEx;
using BepInEx.Configuration;
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
        internal const string PluginVersion = VersionInfo.Version;

        internal new static ManualLogSource Logger { get; private set; } = null!;
        private readonly Harmony _harmony = new(PluginGuid);

        // Configuration Manager settings
        internal static ConfigEntry<bool> EnableGrowthCurveDebug { get; private set; } = null!;

        private void Awake()
        {
            Logger = base.Logger;

            // Configuration Manager settings
            EnableGrowthCurveDebug = Config.Bind("Debug", "Enable Growth Curve Debug", false, 
                "Enable debug logging for skill growth curve calculations");

            try
            {
                Logger.LogInfo($"[SLE] {VersionInfo.VersionString}");
                // 設定とYAML初期化
                SkillConfigManager.Initialize(Config);
                YamlExporter.EnsureYamlExists();

                // Harmonyパッチ適用
                _harmony.PatchAll(typeof(SkillLimitExtenderPlugin).Assembly);
                
                // MODスキル汎用パッチの初期化
                SLE_Hook_ModSkills.Initialize(_harmony);
                
                // コンソールコマンド登録（バージョン差異に依存しない安全な方式）
                SLE_TerminalCommands.Register();

                Logger.LogInfo($"[SLE] Plugin loaded successfully (v{PluginVersion})");
            }
            catch (Exception e)
            {
                Logger.LogError($"[SLE] Awake failed: {e}");
            }
        }

        private void Start()
        {
            // ゲーム開始後にRPC登録
            if (ZNet.instance != null)
            {
                try
                {
                    // サーバーYAML全文を同期
                    ZRoutedRpc.instance.Register<string, int>("SLE_YamlSync", SkillConfigManager.OnYamlReceivedStatic);
                    SkillLimitExtenderPlugin.Logger?.LogInfo("[SLE] RPC registered successfully");
                }
                catch (Exception e)
                {
                    SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] RPC registration failed: {e}");
                }
            }
        }

        private void OnDestroy()
        {
            try
            {
                _harmony?.UnpatchSelf();
            }
            catch (Exception e)
            {
                Logger.LogError($"[SLE] UnpatchSelf failed: {e}");
            }
        }
    }

    /// <summary>
    /// プレイヤーがワールドにスポーンした時にサーバー設定を送信
    /// </summary>
    // SLE_Hook_PlayerSpawned.Postfix
    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    internal static class SLE_Hook_PlayerSpawned
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance)
        {
            if (ZNet.instance?.IsServer() == true && __instance != null)
            {
                // プレイヤーがスポーンした時：内容が変わったときのみブロードキャスト
                SkillConfigManager.SendConfigToClientsIfChanged();
            }
        }
    }
}