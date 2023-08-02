
#Changelog
---
1.2.0
  - Added new Field Tinkering Icon
  - Added Cascading Resonance,an alt primary for Engineer.
  - Added ScrollableLobbyUI dependency,fixing potential issue with passives breaking the loadout panel.

1.1.0
  - Added Jingling Spurs,an alt passive for Deputy.
  - Moved Power Pack Discharge behind unfinished config,as it was reported to feel incomplete.(The skill it was meant to synergise with _is_,so it counts).
  - Tear: Added Corrupted version tooltip.
  - Misc Unfinished Content (a lot)
  - Bug Fixes:
    - Glass Shadow: Fixed interaction with Ancient Scepter
    - Fixed ExtraSkillSlot compatibility.
    - "Fixed" HuntressBuffUltimate compatibility.(It was actually working as intended on HBU's side,just that the intention was not foreseen/isn't consistent with the vanilla game.)
    - Misc Fixes.

1.0.4
  - Networking fixes.
  - Fixed potential compatibility issues preventing corrupted tear from working.
  - Overhauled Excise Visuals
  - Initial Balance Pass:
    - Excise : Increased duration to hopefully reduce the effect of stacking attack speed.
    - Starch : Actually increased radius (oops), Increased amount of time enemies are stunned **1** -> **2.5**.
    - Infestation : Added config option for max amount of spawns active,corruption will wait if over the setting,default is still infinite.
    - Tear : No longer locked into movement if skill is used while moving, uncorrupted cooldown **6** -> **10**, corrupted cooldown **6** -> **12**
    - Pack Discharge : Changed barrier to damage scaling from additive to multiplicative,greatly increasing potential damage. **400-1400** -> **400-4000**

1.0.3
  - Heat of the Forge : Added a config option to enable an alternate implementation,since the normal one is sensitive to potential mod conflicts.
  - Snowsculpt : Networking Fix,Exiting the skill early now triggers a freezing blast.
  - Starch : Increased Radius
  - Misc Changes (Networking,rng)

1.0.2
  - Fixed Engineer Passive breaking pickups + scrap contribution not being redacted correctly on body change

1.0.1
  - Fixed the friendlyInfest config option for Viend's Infestation Passive.
  - Fixed lysate cell interaction with Paladin Glass Shadow
  - Added new Starch Bomb Icon
  - Added config option to enable old icons for skills that get their icons replaced

1.0.0 - Initial Release
