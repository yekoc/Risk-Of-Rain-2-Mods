
#What?
---
Lets you mix and match different parts of available skins,multiplayer compatible.\
this can be used both to let commando hold two different pistols (cool!) and to create never before seen abominations (incomprehensible!).\
If a choice looks really broken switch the base skin used to the one it comes from and it'll probably fix itself.\
There is a config option to seperate out textures and meshes for advanced customization but it turns some characters partially invisble so it's not enabled by default.This config option DOES NOT need to be the same in mp for players to play together,the syncing logic will take care of it.

The R2API dep is there purely to set network requirements.

Credit to souvlakispacestation for starting the line of thought that led to this.\
Credit to Dotflare for the skins used for the demonstration in the icon.\
Credit to the GNU image manipulation program for helping me make the icon.\
Credit to cute anime girls for giving me the will to continue living.

#Changelog
---
2.0.0 - Rewrote most of the logic, fixing multiplayer compatibility. Added Risk of Options support. Added Custom Compatibility for the following characters:
   * Ravager (Sword related effects based on sword,Body effects on body, animation set on ImpWrap)
   * Paladin (Removed Extraneous Entries ("Crystal" repeated a bunch))
   * Pathfinder (Squall)
   * Red Mist (attack vfx based on weapon material, EGO based on body mesh)
   * Hunk (disabled customization due to skin specific gameplay.)

1.0.1 - Fixed some renderers getting permenantly enabled when an alternate is choosen. (Bandit's knife etc.)\
1.0.0 - Initial Release
