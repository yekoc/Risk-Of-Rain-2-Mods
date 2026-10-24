using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HolyHolyHoly;

// Offline check of NativeAnalyzer against the real game: lists every call RecalculateStats makes and what the analyzer thinks.
// usage: AnalyzerHarness "<Risk of Rain 2_Data\Managed>" [extra dll dirs...]
var dirs = args.Length > 0 ? args : new[]{ @"E:\SteamLibrary\steamapps\common\Risk of Rain 2\Risk of Rain 2_Data\Managed" };
var paths = dirs.SelectMany(d => Directory.GetFiles(d, "*.dll")).GroupBy(Path.GetFileName).Select(g => g.First())
    .ToDictionary(p => Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase);
// Real (inspect-only) load: MetadataLoadContext can't resolve tokens. Nothing here gets executed, so Unity being absent is fine.
var alc = new System.Runtime.Loader.AssemblyLoadContext("game", isCollectible: false);
alc.Resolving += (ctx, name) => {
    if(NativeAnalyzer.IsBcl(name.Name)) return null;
    return paths.TryGetValue(name.Name, out var p) ? ctx.LoadFromAssemblyPath(p) : null;
};
var ror2 = alc.LoadFromAssemblyPath(paths["RoR2"]);
var body = ror2.GetType("RoR2.CharacterBody", true);
var recalc = body.GetMethod("RecalculateStats", BindingFlags.Public | BindingFlags.Instance);

Type[] ror2Types;
try{ ror2Types = ror2.GetTypes(); } catch(ReflectionTypeLoadException e){ ror2Types = e.Types.Where(t => t != null).ToArray(); }

var handled = new HashSet<string>{
    "CharacterBody.UpdateAllTemporaryVisualEffects", "CharacterBody.OnCalculatedLevelChanged", "CharacterBody.set_desiredScale",
    "CharacterBody.HandleDisableAllSkillsDebuff", "Trajectory.CalculateApex", "NetworkServer.get_active", "Action`1.Invoke",
    "CharacterBody.get_master",
};
var analyzer = new NativeAnalyzer();
analyzer.Override = m => NativeAnalyzer.Describe(m) == "CharacterBody.get_master" ? NativeAnalyzer.Verdict.Safe : null;
analyzer.ExtraTargets = m => Overrides(m);

IEnumerable<MethodBase> Overrides(MethodBase m){
    if(!(m is MethodInfo mi) || !mi.IsVirtual || mi.IsFinal || mi.DeclaringType == null) yield break;
    if(mi.DeclaringType.Assembly != ror2) yield break;
    foreach(var t in ror2Types){
        if(t == mi.DeclaringType || !SafeIsSubclass(t, mi.DeclaringType)) continue;
        foreach(var cand in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)){
            if(cand.Name == mi.Name && cand.IsVirtual && cand.GetBaseDefinition().MetadataToken == mi.GetBaseDefinition().MetadataToken && cand.GetBaseDefinition().Module == mi.GetBaseDefinition().Module)
                yield return cand;
        }
    }
}
static bool SafeIsSubclass(Type t, Type b){ try{ return t.IsSubclassOf(b); } catch{ return false; } }

// Walk RecalculateStats's own call sites (not the method itself: its call sites are what the patcher rewrites one by one).
var il = recalc.GetMethodBody().GetILAsByteArray();
var seen = new HashSet<string>();
int unsafeCount = 0;
foreach(var target in CallTargets(recalc, il)){
    string name = NativeAnalyzer.Describe(target);
    if(!seen.Add(name + target.MetadataToken)) continue;
    if(handled.Contains(name)){ Console.WriteLine($"HANDLED  {name}"); continue; }
    var v = analyzer.Analyze(target);
    if(v.Unsafe) unsafeCount++;
    Console.WriteLine(v.Unsafe ? $"UNSAFE   {name}\n         {v.Reason}" : $"safe     {name}");
}
Console.WriteLine($"\n{unsafeCount} unsafe call targets");

static IEnumerable<MethodBase> CallTargets(MethodBase m, byte[] il){
    var ops = typeof(System.Reflection.Emit.OpCodes).GetFields().Select(f => (System.Reflection.Emit.OpCode)f.GetValue(null)).ToList();
    var one = ops.Where(o => o.Size == 1).ToDictionary(o => (byte)o.Value);
    var two = ops.Where(o => o.Size == 2).ToDictionary(o => (byte)(o.Value & 0xFF));
    int i = 0;
    while(i < il.Length){
        var op = il[i] == 0xFE ? two[il[++i]] : one[il[i]];
        i++;
        int size = op.OperandType switch {
            System.Reflection.Emit.OperandType.InlineNone => 0,
            System.Reflection.Emit.OperandType.ShortInlineBrTarget or System.Reflection.Emit.OperandType.ShortInlineI or System.Reflection.Emit.OperandType.ShortInlineVar => 1,
            System.Reflection.Emit.OperandType.InlineVar => 2,
            System.Reflection.Emit.OperandType.InlineI8 or System.Reflection.Emit.OperandType.InlineR => 8,
            System.Reflection.Emit.OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
            _ => 4 };
        if(op.OperandType == System.Reflection.Emit.OperandType.InlineMethod)
            yield return m.Module.ResolveMethod(BitConverter.ToInt32(il, i));
        i += size;
    }
}
