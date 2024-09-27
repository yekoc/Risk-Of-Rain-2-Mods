#What?
---
Mod to house experimental changes intended to optimize certain parts of the game,currently there is only a single change;\
* Parallelize calls to RecalculateStats.Preliminary testing shows this to be highly beneficial when enemies change stats en masse(spawns,level ups).

When/If more changes become part of this they will be togglable by config,though one isn't included for now since it would be equivalent to uninstalling the mod.\
THIS MOD CAN BREAK COMPATIBILITY WITH OTHER MODS,DONT COMPLAIN TO OTHER MOD AUTHORS IF SOMETHING BREAKS WITH THIS.\
I have left it on in my personal profile since the first release and haven't had any crashes based on it yet,your milleage may vary depending on your mod loadout.

#I'm a mod dev,how does this affect my work?
---
Any hooks on CharacterBody.RecalculateStats not done through RecalculateStatsAPI.GetStatCoefficents WILL break,__unless__ your hook doesn't touch any of unity's code (this means no interaction with GameObjects,vanilla csharp only).\
While you can still put through synchronous calls to RecalculateStats() when necessary,it is greatly recommended that devs prefer marking the body's stats dirty instead,as this will allow the calls to be batched together.The statsDirty value can be set directly or through the relevant CharacterBody methods.

Thanks to the eldritch power of reflection and heretical inter-mod manipulation the above now only applies to IL hooks.On hooks should work,but will always behave as if they start with a call to orig(self),Consider using RecalculateStatsAPI if you need to run logic before RecalculateStats is called.

#Whats with the icon
---
That's the file icon for linkable libraries(shared objects) that my system theme uses,I attempted to make it look more 'holy',it's sitting on a pedestal made of fuel arrays to reinforce how janky I thought it was.\
DLLs don't actually get that icon in system though,they get a generic one that is not as stylish so I am using this instead. 

#Whats with the Name?
---
I named the project files haphazardly due to not expecting this to go anywhere and ended up with output named holy.dll. It's kinda funny so it stays.\
The ingame name is HolyHolyHOLY because I was reminded of that one Shin Megami Tensei schizopost,also it's easy to notice in Logs,which means it's easier for people to point out that you shouldn't be expecting this to have good mod compatibility.I mean,it works with some stuff,see above section,but you waive the right to complain about mods breaking by installing this.


Credit to Rob,as always,since I used his csproj file.\
Credit to IDeath and Twiner,for reminding me that Unity's csharp multi-threading support is non-existant and providing information on how to navigate the minefield it represents.\
Credit to HIFU and Phreel for providing much needed help debugging.\
Credit to the GNU image manipulation program for helping me make the icon.\
Credit to cute anime girls for giving me the will to continue living.

#Changelog
---
1.0.10 - Fixed SOTS specific crash.\
1.0.9 - Changed R2API dependency to be finer grained,added max-hp fix dependency,fixed incompat with latest SS2\
1.0.7 - Removed Crash.\
1.0.6 - Added a bunch of robustness logic.\
1.0.5 - _Fixed_ special handling for ExtraSkillSlots.\
1.0.4 - Gained Immunity to On hook related crashes through dark arts. Implemented special handling for ExtraSkillSlots.\
1.0.3 - Fixed the Mysterious Incompat Bug.(Hopefully)\
1.0.2 - Fixed RecalculateStatsAPI IL hook failiure.\
1.0.1 - Fixed visual effect related crash due to SOTV changing how often they get updated.\
1.0.0 - SOTV update,also the point wherein I am overconfident enough in it's stability to declare 1.0 :^) ,mentions of jank in Readme toned down\
0.0.4 - Implemented some additional logic to make sure synchronous calls to determine stats don't ignore RecalculateStatsAPI,Note:Research if this is actually necessary,it is a slight reduction in the performance gain to leave it as is since the very initial call can not be safely parallelized.\
0.0.3 - Moved OnLevelUp dispatch back into the main thread.\
0.0.2 - RecalculateStatsAPI compatibility.\
0.0.1 - Initial Shitpost
