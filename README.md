# Skill Limit Externder 
Latest version 1.1.0





SkillLimitExtender
Extend skill level caps beyond 100 with full server support!

Features
Skill Cap Extension: Raise any skill beyond the vanilla 100 limit
Mod Skill Support: Works with custom skills from other mods
Easy Configuration: Configuration Manager (F1) + YAML file support
Server Sync: Admins can lock configurations and sync to all players
Perfect UI: Level bars scale correctly with custom caps
Balanced: Vanilla 100 = Custom cap for same effectiveness
Lightweight: No heavy dependencies, optimized performance
How It Works
The mod maintains the original game balance by scaling effectiveness:

Vanilla: Level 100 = 100% effectiveness
Extended: Level 250 = 100% effectiveness (same as vanilla 100)
Growth: Linear scaling means longer progression, same final power
Configuration
In-Game (F1 Key)
[General]
DefaultCap = 250          # Default cap for all skills
EnableYamlOverride = true # Allow YAML customization

[Server] (Admin Only)
LockConfiguration = false # Force server settings to all clients
YAML File (Individual Skills)
Location: BepInEx/config/SkillLimitExtender/SLE_Skill_List.yaml

# Vanilla Skills
Swords: 500
Bows: 300
Jump: 250
Run: 250

# Mod Skills (manually add)
Cartography: 400
MagicSkill: 350
Server Administration
For Server Owners:
Set LockConfiguration = true in your config
Configure DefaultCap and EnableYamlOverride as desired
All connecting players will automatically use your settings
Players cannot override with local YAML when locked
For Players:
Use Configuration Manager (F1) to adjust personal settings
Edit YAML file for individual skill customization
Server settings override local config when locked
Mod Skill Support
To add skills from other mods:

Find the skill name using /raiseskill command
Add to YAML file: SkillName: DesiredCap
Restart game to apply changes
Example for Cartography skill:

Cartography: 400
Commands
All vanilla commands work with extended caps:

raiseskill Swords 200     # Works with mod skills too
resetskill Cartography   # Individual reset works
resetskill all           # Resets vanilla skills (mod limitation)
Technical Details
Harmony Transpiler: Patches skill calculation at bytecode level
RPC Sync: Lightweight server-client communication
UI Compatible: Level bars display correctly with any cap
Performance: Minimal overhead, no constant polling
Requirements
BepInEx: 5.4.2202 or newer
Jotunn: 2.22.3 or newer (Valheim modding framework)
YamlDotNet: 16.1.3 or newer (Configuration file support)
Valheim: Latest version
Server: Optional, works in single-player too
Known Limitations
resetskill all doesn't affect mod skills (use individual commands)
Mod skills must be manually added to YAML
Server restart required for server config changes
Changelog
v1.1.0
- Fix: ゲーム終了時のセーブで発生した NullReferenceException を回避するため、`Skills.Save` の直前に不正なスキルエントリ（null/未初期化）をクリーンアップする安全パッチを追加。
- Change: `SkillsDialog.Setup` のUI分母置換を安全化。`100f`→`cap`置換は `SetValue` 近傍のみに限定し、ローカル推測失敗時はグローバル分母にフォールバック。
- Add: `SkillConfigManager.GetUiDenominatorForSkillSafe(object?)` を追加し、UI分母の個別適用時に安全に `SkillType` を取得可能に。
- Improve: `SkillConfigManager.GetCap` のフォールバック分岐に明示的なnullガードを追加し、CS8602警告を解消。
- Change: `Skills.GetSkillFactor` をPrefixでオーバーライドし、cap/relative/bonusCapに基づく安全な倍率計算に統一。
- Docs: ログ出力の整備（置換件数、クリーンアップ件数）と動作確認手順の追記。

v1.0.0
Initial release
Skill cap extension beyond 100
Mod skill support (manual YAML addition)
Server configuration sync
UI level bar scaling
Configuration Manager integration
Lightweight implementation
Support
Issues: Report on GitHub or Thunderstore
Discord: Join Valheim Modding Community
Compatibility: Works with most skill-related mods
License
MIT License - Feel free to modify and redistribute!