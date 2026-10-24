using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using BepInEx.Logging;
using MonoMod.Cil;
using MonoMod.Utils;
using MonoMod.Utils.Cil;

// Offline check of the RecalculateStats rewrite: stacks R2API.RecalculateStats' and ExtraSkillSlots' real IL hooks,
// runs HolyHolyHOLY's final manipulator on top, generates the method and forces the JIT to compile it.
// Nothing Unity-side can execute here, so the run is expected to die inside game code; what matters is that it
// isn't an InvalidProgramException (bad IL).
var nuget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
var searchDirs = new List<string>{
    @"E:\SteamLibrary\steamapps\common\Risk of Rain 2\Risk of Rain 2_Data\Managed",
    Path.Combine(nuget, @"r2api.recalculatestats\1.7.0\lib\netstandard2.1"),
    Path.Combine(nuget, @"r2api.core\5.3.0\lib\netstandard2.1"),
    Path.Combine(nuget, @"mmhook.ror2\2026.10.8\lib"),
    Path.Combine(nuget, @"harmonyx\2.7.0\lib\netstandard2.0"),
    Path.Combine(Path.GetTempPath(), @"holy_dl\ess\x\plugins\ExtraSkillSlots"),
};
AssemblyLoadContext.Default.Resolving += (ctx, name) => {
    foreach(var dir in searchDirs){
        var p = Path.Combine(dir, name.Name + ".dll");
        if(File.Exists(p)) return ctx.LoadFromAssemblyPath(p);
    }
    return null;
};
Logger.Listeners.Add(new ConsoleListener());
return Run(args);

// Separate method so nothing game-related gets resolved before the resolver above is in place.
[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
static int Run(string[] args){
var holy = typeof(HolyHolyHoly.ParallelRuntime).Assembly;
var rsapi = Assembly.Load("R2API.RecalculateStats");
var ess = Assembly.Load("ExtraSkillSlots");
const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

var introspection = holy.GetType("HolyHolyHoly.HookIntrospection");
Console.WriteLine("HookIntrospection.Available = " + introspection.GetProperty("Available").GetValue(null));
introspection.GetField("InspectHarmony", any).SetValue(null, false);

// Pretend RsapiCompat initialized (its own ILHooks need a real detour runtime).
var compat = holy.GetType("HolyHolyHoly.RsapiCompat");
var getStatMods = rsapi.GetType("R2API.RecalculateStatsAPI").GetMethod("GetStatMods", any);
compat.GetMethod("Init").Invoke(null, new object[]{ new ManualLogSource("RsapiCompat"), false });


var patcher = holy.GetType("HolyHolyHoly.RecalcPatcher");
var source = new ManualLogSource("Holy"); Logger.Sources.Add(source);
patcher.GetMethod("Init").Invoke(null, new object[]{ source });
patcher.GetField("LogEverything").SetValue(null, args.Contains("-v"));

var recalc = typeof(RoR2.CharacterBody).GetMethod("RecalculateStats");
var manipulators = new List<(string, ILContext.Manipulator)>{
    ("R2API.RecalculateStats", (ILContext.Manipulator)Delegate.CreateDelegate(typeof(ILContext.Manipulator), rsapi.GetType("R2API.RecalculateStatsAPI").GetMethod("HookRecalculateStats", any))),
    ("ExtraSkillSlots", (ILContext.Manipulator)Delegate.CreateDelegate(typeof(ILContext.Manipulator), ess.GetType("ExtraSkillSlots.ExtraCharacterBody").GetMethod("RecalculateStatsILHook", any))),
    ("FakeMod", FakeMod.Hook),
    ("HolyHolyHOLY",(ILContext.Manipulator)Delegate.CreateDelegate(typeof(ILContext.Manipulator), patcher.GetMethod("Manipulate"))),
};
if(args.Contains("--holy-only")) manipulators.RemoveRange(0, 2);

using var dmd = new DynamicMethodDefinition(recalc);
foreach(var (name, manipulator) in manipulators){
    var ctx = new ILContext(dmd.Definition){ ReferenceBag = RuntimeILReferenceBag.Instance };
    ctx.Invoke(manipulator);
    Console.WriteLine($"applied {name}: {dmd.Definition.Body.Instructions.Count} instructions");
}
Console.WriteLine("Healthy = " + patcher.GetProperty("Healthy").GetValue(null) + ", deferred = " + patcher.GetProperty("DeferredSites").GetValue(null) + ", bails = " + patcher.GetProperty("BailSites").GetValue(null));
File.WriteAllLines("RecalculateStats.patched.il", dmd.Definition.Body.Instructions.Select(i => i.ToString()));

var generated = DMDCecilGenerator.Generate(dmd);
try{
    generated.Invoke(null, new object[]{ null });
    Console.WriteLine("RESULT: ran to completion (unexpected but fine)");
}
catch(TargetInvocationException e) when (e.InnerException is InvalidProgramException){
    Console.WriteLine("RESULT: FAIL, invalid IL: " + e.InnerException);
    return 1;
}
catch(TargetInvocationException e){
    Console.WriteLine($"RESULT: OK, IL compiled; execution stopped in game code with {e.InnerException?.GetType().Name}: {e.InnerException?.Message?.Split('\n')[0]}");
    Console.WriteLine(string.Join("\n", (e.InnerException?.StackTrace ?? "").Split('\n').Take(4)));
}
return 0;
}

// Stand-in for a typical mod IL hook: lambdas through EmitDelegate, one with a side effect on a Unity object
// (should be deferred), one feeding a value back into the stat math (can't be deferred, should bail), one pure (safe).
static class FakeMod
{
    public static void Hook(ILContext il){
        var c = new ILCursor(il);
        c.GotoNext(MoveType.After, x => x.MatchCallOrCallvirt<RoR2.CharacterBody>("set_maxJumpCount"));
        c.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
        c.EmitDelegate<Action<RoR2.CharacterBody>>(body => body.transform.localScale = UnityEngine.Vector3.one);
        c.Emit(Mono.Cecil.Cil.OpCodes.Ldc_R4, 2f);
        c.EmitDelegate<Func<float, float>>(x => x * UnityEngine.Time.deltaTime);
        c.Emit(Mono.Cecil.Cil.OpCodes.Pop);
        c.Emit(Mono.Cecil.Cil.OpCodes.Ldc_R4, 3f);
        c.EmitDelegate<Func<float, float>>(x => x * 2f);
        c.Emit(Mono.Cecil.Cil.OpCodes.Pop);
    }
}

sealed class ConsoleListener : ILogListener
{
    public void LogEvent(object sender, LogEventArgs e) => Console.WriteLine($"[{e.Level}] {e.Data}");
    public void Dispose(){ }
}
