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
- Fix: Add a safety patch that cleans invalid skill entries (null/uninitialized) right before `Skills.Save` to avoid a NullReferenceException on game exit.
- Change: Harden UI denominator replacement in `SkillsDialog.Setup`. Limit `100f` → `cap` replacement to the vicinity of `GuiBar.SetValue`, and fall back to the global UI denominator when local inference fails.
- Add: Introduce `SkillConfigManager.GetUiDenominatorForSkillSafe(object?)` to safely obtain `SkillType` when applying per-skill UI denominators.
- Improve: Add explicit null guard to the fallback branch of `SkillConfigManager.GetCap`, resolving CS8602 warnings.
- Change: Override `Skills.GetSkillFactor` via Prefix; unify factor calculation based on cap/relative/bonusCap with safe clamping.
- Docs: Improve logs (replacement counts, cleanup counts) and document verification steps.

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