using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace SkillLimitExtender
{
    /// <summary>
    /// 軽量なサーバー設定管理
    /// 最小限のRPCでサーバー設定をクライアントに同期
    /// </summary>
    internal static class SkillConfigManager
    {
        // 設定エントリ
        internal static ConfigEntry<bool> ServerConfigLocked = null!;
        internal static ConfigEntry<int> DefaultCap = null!;
        internal static ConfigEntry<bool> EnableYamlOverride = null!;

        // 内部状態
        private static Dictionary<string, int> _capsByName = new();
        private static bool _initialized;
        private static bool _isServerConfig;

        internal static void Initialize(ConfigFile config)
        {
            if (_initialized) return;

            // サーバー専用設定（管理者のみ）
            ServerConfigLocked = config.Bind("Server", "LockConfiguration", false,
                "If true, server forces its configuration to all clients. (Admin Only)");

            // メイン設定
            DefaultCap = config.Bind("General", "DefaultCap", 250,
                new ConfigDescription("Default skill cap for skills not listed in YAML file",
                new AcceptableValueRange<int>(100, 10000)));

            EnableYamlOverride = config.Bind("General", "EnableYamlOverride", true,
                "Allow YAML file to override individual skill caps");

            // 初期化
            ReloadFromYaml();
            RegisterRPC();
            _initialized = true;

            SkillLimitExtenderPlugin.Logger?.LogInfo("[SLE] Lightweight server config initialized");
        }

        private static void RegisterRPC()
        {
            // RPC登録はPlugin.Start()で行う
        }

        // 静的メソッドとしてRPC受信処理を公開
        internal static void OnConfigReceivedStatic(long sender, int defaultCap, bool enableYaml)
        {
            OnConfigReceived(sender, defaultCap, enableYaml);
        }

        internal static void ReloadFromYaml()
        {
            // サーバー設定がロックされている場合、クライアントのYAMLは無視
            if (_isServerConfig || !EnableYamlOverride?.Value == true)
            {
                if (_isServerConfig)
                {
                    SkillLimitExtenderPlugin.Logger?.LogInfo("[SLE] Using server configuration (YAML disabled)");
                }
                return;
            }

            _capsByName = YamlExporter.LoadYaml();
        }

        internal static int GetCap(global::Skills.SkillType st)
        {
            string skillKey = st.ToString();

            // 1. YAML設定が有効で、サーバー設定でない場合
            if (!_isServerConfig &&
                EnableYamlOverride?.Value == true &&
                _capsByName != null &&
                _capsByName.TryGetValue(skillKey, out var cap) &&
                cap > 0)
            {
                return cap;
            }

            // 2. 数値IDの場合、対応する表示名エントリも検索
            if (!_isServerConfig &&
                EnableYamlOverride?.Value == true &&
                int.TryParse(skillKey, out int skillId) &&
                skillId > 999)
            {
                var friendlyEntry = _capsByName?.FirstOrDefault(kv =>
                    !int.TryParse(kv.Key, out _) &&
                    kv.Value > 0);

                if (friendlyEntry.HasValue && friendlyEntry.Value.Value > 0)
                {
                    return friendlyEntry.Value.Value;
                }
            }

            // 3. デフォルト値（サーバー設定優先）
            return DefaultCap?.Value ?? 250;
        }

        // サーバー→クライアント設定送信
        internal static void SendConfigToClients()
        {
            if (ZNet.instance?.IsServer() != true || !ServerConfigLocked?.Value == true) return;

            try
            {
                int defaultCap = DefaultCap?.Value ?? 250;
                bool enableYaml = EnableYamlOverride?.Value ?? true;

                // 全クライアントに設定を送信
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SLE_ConfigSync", defaultCap, enableYaml);

                SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] Server config sent to clients: DefaultCap={defaultCap}, EnableYaml={enableYaml}");
            }
            catch (System.Exception e)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] Failed to send config to clients: {e}");
            }
        }

        // クライアント：サーバー設定受信
        private static void OnConfigReceived(long sender, int defaultCap, bool enableYaml)
        {
            if (ZNet.instance?.IsServer() == true) return; // サーバーは無視

            try
            {
                // サーバー設定を適用
                _isServerConfig = true;
                DefaultCap.Value = defaultCap;
                EnableYamlOverride.Value = enableYaml;

                // YAML再読み込み（無効化される可能性あり）
                ReloadFromYaml();

                SkillLimitExtenderPlugin.Logger?.LogInfo($"[SLE] Received server config: DefaultCap={defaultCap}, EnableYaml={enableYaml}");
            }
            catch (System.Exception e)
            {
                SkillLimitExtenderPlugin.Logger?.LogError($"[SLE] Failed to apply server config: {e}");
            }
        }

        // プレイヤー接続時にサーバー設定を送信
        internal static void OnPlayerConnected()
        {
            SendConfigToClients();
        }

        // 互換API
        internal static int GetSkillLimit(global::Skills.SkillType st) => GetCap(st);
    }
}