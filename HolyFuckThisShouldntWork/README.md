#What?
---
Mod to house experimental changes intended to optimize certain parts of the game,currently there is only a single change;\
* Parallelize calls to RecalculateStats.Preliminary testing shows this to be highly beneficial when enemies change stats en masse(spawns,level ups).

Dirty bodies are collected during each physics step and recalculated together at the end of it; small batches stay on the main thread where threads wouldn't pay off.\
THIS MOD CAN BREAK COMPATIBILITY WITH OTHER MODS,DONT COMPLAIN TO OTHER MOD AUTHORS IF SOMETHING BREAKS WITH THIS.\
That said, as of 1.1.0 it actively checks every other mod's hooks before letting anything run off the main thread, see below.

#Config
---
* `Enabled` - turn it off and the mod does nothing.
* `Minimum Batch Size` - batches smaller than this run on the main thread (default 4).
* `Max Threads` - 0 uses every core.
* `Defer Foreign Calls` - see below.
* `Log Analysis` - log the verdict for every call inside RecalculateStats.

#I'm a mod dev,how does this affect my work?
---
The rule is still that no Unity native code may run on a worker thread. What changed in 1.1.0 is that the mod enforces it instead of hoping:

* After every IL hook and Harmony patch is applied, the final RecalculateStats is scanned call by call, and each call's whole call graph (including virtual overrides and hooks on the callees) is checked for Unity native calls, opaque delegate calls and writes to static fields.
* Calls that are clean run in parallel as is.
* Calls that touch Unity and return nothing (side effects, e.g. ExtraSkillSlots' cooldown sync, `UpdateAllTemporaryVisualEffects`, `onRecalculateStats` listeners) get their arguments captured and run on the main thread right after the parallel part, in their original order. Turn off `Defer Foreign Calls` to treat other mods' calls like the next point instead.
* Calls that touch Unity and feed a value back into the calculation make the body abandon the worker attempt and get recalculated normally on the main thread.
* If most bodies keep ending up on the main thread, the mod gives up on parallelism until the next run and logs what caused it.

On hooks and any other Detours on RecalculateStats still behave as if they start with a call to orig(self): workers skip them, and they run on the main thread around the finished result. Consider using RecalculateStatsAPI if you need to run logic before RecalculateStats is called. GetStatCoefficients handlers always run on the main thread.\
Synchronous calls to RecalculateStats() are untouched and behave exactly like vanilla, but marking the body's stats dirty is still preferred since that lets the calls be batched together.The statsDirty value can be set directly or through the relevant CharacterBody methods.

The log says which of your calls got deferred or forced back onto the main thread and why.

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
1.1.0 - Updated for the current game version. Rewrote the parallel part around a static analyzer that checks every call in RecalculateStats (vanilla and modded) for Unity native code and defers it, or falls back to the main thread, instead of patching known spots by hand. Fixed RecalculateStatsAPI modifiers being shared between threads. Harmony patches and any kind of Detour are now handled, ExtraSkillSlots no longer needs special casing. Batches run at the end of the physics step they were queued in. Added config.\
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
