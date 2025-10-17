using System;

namespace SkillLimitExtender
{
    /// <summary>
    /// バージョン管理と互換性チェック
    /// </summary>
    internal static class VersionInfo
    {
        // セマンティックバージョン（ユーザー指定）
        public const string Version = "1.1.0";
        public const string Prerelease = ""; // リリース版
        public const string Build = "20251017";

        // 開発版フラグ（ログで使用）
        public const bool IsDevelopmentVersion = false;

        // 完全なバージョン文字列（定数）
        public const string FullVersion = "1.1.0";
        public const string FullVersionWithBuild = "1.1.0.0";

        // 設定・RPCの互換性判定用（整数で維持。RPCでは int を送受信しています）
        public const int ProtocolVersion = 2;
        public const int ConfigSchemaVersion = 2;

        // 表示用文字列（Prerelease や Build を含めた文字列）
        public static string DisplayVersion => string.IsNullOrEmpty(Prerelease) ? FullVersion : $"{FullVersion}-{Prerelease}";
        public static string VersionString => $"v{DisplayVersion} (build {Build}, proto={ProtocolVersion}, cfg={ConfigSchemaVersion})";

        public static bool IsCompatible(int remoteProtocolVersion) => remoteProtocolVersion == ProtocolVersion;
    }

    /// <summary>
    /// ConfigurationManager プラグイン用のメタ属性スタブ。
    /// 参照が無くてもコンパイルできるようにここで定義します（存在すればプラグイン側の型が優先されます）。
    /// </summary>
    internal sealed class ConfigurationManagerAttributes
    {
        public bool? ShowRangeAsPercent { get; set; }
        public bool? IsAdvanced { get; set; }
        public int? Order { get; set; }
        public string? Category { get; set; }
        public bool? Browsable { get; set; }
        public object? DefaultValue { get; set; }
        public string? ReadOnly { get; set; }
        public bool? HideDefaultButton { get; set; }
        public bool? HideSettingName { get; set; }
        public bool? IsAdminOnly { get; set; }
    }
}