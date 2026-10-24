using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RoR2;
using UnityEngine.Networking;

namespace HolyHolyHoly
{
    // Final IL manipulator on CharacterBody.RecalculateStats, ordered after every other IL hook and Harmony patch,
    // so it sees exactly the code that will run. Every call site becomes one of:
    //  - safe: left alone (the analyzer proved it can't reach Unity native code or shared static state),
    //  - handled: swapped for a ParallelRuntime wrapper that defers vanilla side effects to the main thread,
    //  - deferred: a foreign void call whose arguments get captured and replayed on the main thread,
    //  - bail: on a worker the body is abandoned and recalculated synchronously after the batch.
    // On the main thread every rewritten site behaves exactly like the original call.
    internal static class RecalcPatcher
    {
        public static bool Healthy { get; private set; }
        public static bool DeferForeignCalls = true;
        public static bool LogEverything;
        public static int DeferredSites { get; private set; }
        public static int BailSites { get; private set; }
        static readonly List<string> BailSiteNames = new List<string>();

        static ManualLogSource log;
        static readonly NativeAnalyzer analyzer = new NativeAnalyzer();
        static readonly Dictionary<MethodBase, List<MethodBase>> hookExtras = new Dictionary<MethodBase, List<MethodBase>>(MethodComparer.Instance);
        static readonly HashSet<Type> touchedTypes = new HashSet<Type>();
        static readonly HashSet<string> reported = new HashSet<string>();
        static readonly Dictionary<MethodBase, int> deferSiteIds = new Dictionary<MethodBase, int>(MethodComparer.Instance);
        static Dictionary<Type, List<Type>> derivedTypes;
        static Dictionary<MethodBase, MethodInfo> replacements;

        static MethodInfo RT(string name) => typeof(ParallelRuntime).GetMethod(name, BindingFlags.Public | BindingFlags.Static);

        public static void Init(ManualLogSource logger){
            log = logger;
            analyzer.Override = Override;
            analyzer.ExtraTargets = Extras;
            analyzer.OnTypeTouched = t => touchedTypes.Add(t);
            const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var body = typeof(CharacterBody);
            replacements = new Dictionary<MethodBase, MethodInfo>(MethodComparer.Instance){
                { body.GetMethod(nameof(CharacterBody.UpdateAllTemporaryVisualEffects), any), RT(nameof(ParallelRuntime.UpdateVisuals)) },
                { body.GetMethod(nameof(CharacterBody.OnCalculatedLevelChanged), any), RT(nameof(ParallelRuntime.LevelChanged)) },
                { body.GetProperty(nameof(CharacterBody.desiredScale), any).GetSetMethod(true), RT(nameof(ParallelRuntime.SetDesiredScale)) },
                { body.GetMethod(nameof(CharacterBody.HandleDisableAllSkillsDebuff), any), RT(nameof(ParallelRuntime.DisableSkills)) },
                { typeof(Trajectory).GetMethod(nameof(Trajectory.CalculateApex), new[]{ typeof(float) }), RT(nameof(ParallelRuntime.CalculateApex)) },
            };
            if(RsapiCompat.GetStatModsMethod != null) replacements[RsapiCompat.GetStatModsMethod] = RT(nameof(ParallelRuntime.GetStatMods));
        }

        // Analysis results go stale when hooks change; called before forcing a refresh at run start.
        public static void BeginSession(){
            analyzer.Clear();
            hookExtras.Clear();
            derivedTypes = null;
            HookIntrospection.BeginSession();
        }

        public static void Manipulate(ILContext il){
            Healthy = false;
            try{
                Rewrite(il);
                Healthy = true;
            }
            catch(Exception e){
                log.LogError("Couldn't prepare RecalculateStats for parallel execution, everything will stay on the main thread.\n" + e);
            }
        }

        static void Rewrite(ILContext il){
            var mutations = new List<Action>();
            var notes = new List<KeyValuePair<LogLevel, string>>();
            int deferred = 0, bails = 0;
            var instrs = il.Instrs.ToArray();

            // Prologue: if (ParallelRuntime.ShouldSkip(this)) return;
            mutations.Add(() => {
                var c = new ILCursor(il);
                var go = c.DefineLabel();
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Call, RT(nameof(ParallelRuntime.ShouldSkip)));
                c.Emit(OpCodes.Brfalse, go);
                c.Emit(OpCodes.Ret);
                c.MarkLabel(go);
            });

            var healthGate = FindHealthGate(instrs);
            if(healthGate == null) throw new InvalidOperationException("couldn't find the health adjustment block (NetworkServer.active before HealthComponent.Heal)");
            var eventInvokes = FindRecalculateEventInvokes(instrs);
            if(eventInvokes.Count == 0) throw new InvalidOperationException("couldn't find the onRecalculateStats invocation");

            foreach(var instr in instrs){
                if(instr == healthGate){
                    mutations.Add(() => {
                        // NetworkServer.active -> ParallelRuntime.HealthGate(this); keep the instruction so branches into it survive.
                        instr.OpCode = OpCodes.Ldarg_0;
                        instr.Operand = null;
                        new ILCursor(il).Goto(instr, MoveType.After).Emit(OpCodes.Call, RT(nameof(ParallelRuntime.HealthGate)));
                    });
                    continue;
                }
                if(eventInvokes.Contains(instr)){
                    var wrapper = il.Import(RT(nameof(ParallelRuntime.InvokeRecalculateEvent)));
                    mutations.Add(() => { instr.OpCode = OpCodes.Call; instr.Operand = wrapper; });
                    continue;
                }
                if(instr.OpCode == OpCodes.Calli){
                    bails++;
                    notes.Add(Note(LogLevel.Warning, "an indirect calli can't be verified, bodies reaching it will be recalculated on the main thread"));
                    mutations.Add(() => EmitBail(il, instr, "an indirect calli"));
                    continue;
                }
                if(instr.OpCode != OpCodes.Call && instr.OpCode != OpCodes.Callvirt && instr.OpCode != OpCodes.Newobj) continue;
                if(!(instr.Operand is MethodReference mref)) continue;

                MethodBase target;
                try{ target = mref.ResolveReflection(); }
                catch(Exception){ target = null; }
                if(target == null){
                    bails++;
                    notes.Add(Note(LogLevel.Warning, $"couldn't resolve {mref.FullName}, bodies reaching it will be recalculated on the main thread"));
                    mutations.Add(() => EmitBail(il, instr, mref.FullName));
                    continue;
                }
                if(target.DeclaringType?.Assembly == typeof(RecalcPatcher).Assembly) continue;

                if(replacements.TryGetValue(target, out var replacement)){
                    if(MethodComparer.Instance.Equals(target, RsapiCompat.GetStatModsMethod) && !RsapiCompat.Active) throw new InvalidOperationException("R2API.RecalculateStats is hooked in but couldn't be made thread safe");
                    var wrapper = il.Import(replacement);
                    mutations.Add(() => { instr.OpCode = OpCodes.Call; instr.Operand = wrapper; });
                    continue;
                }
                if(IsReferenceBagGetter(target)) continue;

                // Delegates emitted by ILCursor.EmitDelegate: ldc.i4 id; call InnerBag<T>.Get; call <invoker>(args..., delegate).
                Delegate bagged = ResolveBaggedDelegate(instr);
                MethodBase[] code = bagged != null ? bagged.GetInvocationList().Select(d => (MethodBase)d.Method).ToArray() : new[]{ target };
                NativeAnalyzer.Verdict verdict = NativeAnalyzer.Verdict.Safe;
                foreach(var m in code){
                    verdict = analyzer.Analyze(m);
                    if(verdict.Unsafe) break;
                }
                if(LogEverything) notes.Add(Note(LogLevel.Debug, $"{Describe(code[0])}: {(verdict.Unsafe ? verdict.Reason : "safe")}"));
                if(!verdict.Unsafe) continue;

                var owner = code[0].DeclaringType?.Assembly;
                bool foreign = IsPluginAssembly(owner);
                Type[] stack = StackTypes(target);
                bool delegateCall = bagged != null || IsDelegateInvoke(target);
                bool canDefer = DeferForeignCalls && (foreign || (delegateCall && bagged == null)) && stack != null && ReturnsVoid(target) && !AtExceptionBoundary(il, instr);
                string what = bagged != null ? $"IL hook delegate {Describe(code[0])} from {owner?.GetName().Name}" : $"{Describe(target)}{(foreign ? " from " + owner?.GetName().Name : "")}";
                var level = foreign ? LogLevel.Info : LogLevel.Debug;
                if(canDefer){
                    deferred++;
                    notes.Add(Note(level, $"{what} touches main-thread-only code and will run after the parallel part ({verdict.Reason})"));
                    if(delegateCall) mutations.Add(() => EmitDeferral(il, instr, stack, c => { c.Emit(bagged != null ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0); c.Emit(OpCodes.Call, RT(nameof(ParallelRuntime.DeferDelegate))); }));
                    else{
                        int site = DeferSiteId(target);
                        mutations.Add(() => EmitDeferral(il, instr, stack, c => { c.Emit(OpCodes.Ldc_I4, site); c.Emit(OpCodes.Call, RT(nameof(ParallelRuntime.DeferMethod))); }));
                    }
                }
                else{
                    bails++;
                    notes.Add(Note(level, $"{what} touches main-thread-only code, bodies reaching it will be recalculated on the main thread ({verdict.Reason})"));
                    mutations.Add(() => EmitBail(il, instr, what));
                }
            }

            // Nothing above touches the body, so a failure leaves the IL untouched; from here on it's plain list surgery.
            foreach(var mutation in mutations) mutation();
            DeferredSites = deferred;
            BailSites = bails;
            foreach(var note in notes){
                if(reported.Add(note.Value)) log.Log(note.Key, note.Value);
            }
        }

        static KeyValuePair<LogLevel, string> Note(LogLevel level, string text) => new KeyValuePair<LogLevel, string>(level, text);

        // The NetworkServer.active check guarding the Heal/RechargeShield block near the end.
        static Instruction FindHealthGate(Instruction[] instrs){
            var heal = typeof(HealthComponent).GetMethod(nameof(HealthComponent.Heal));
            var active = typeof(NetworkServer).GetProperty(nameof(NetworkServer.active)).GetGetMethod();
            int healIndex = Array.FindIndex(instrs, x => x.MatchCallOrCallvirt(heal));
            for(int i = healIndex - 1; i >= 0; i--){
                if(instrs[i].MatchCall(active)) return instrs[i];
            }
            return null;
        }

        static HashSet<Instruction> FindRecalculateEventInvokes(Instruction[] instrs){
            var found = new HashSet<Instruction>();
            var invoke = typeof(Action<CharacterBody>).GetMethod("Invoke");
            for(int i = 0; i < instrs.Length; i++){
                if(!instrs[i].MatchLdfld<CharacterBody>("onRecalculateStats")) continue;
                for(int j = i + 1; j < instrs.Length && j <= i + 12; j++){
                    if(instrs[j].MatchCallOrCallvirt(invoke)){ found.Add(instrs[j]); break; }
                }
            }
            return found;
        }

        static bool IsReferenceBagGetter(MethodBase m) =>
            m.Name == "Get" && m.DeclaringType != null && m.DeclaringType.IsGenericType && m.DeclaringType.Name.StartsWith("InnerBag", StringComparison.Ordinal);

        static Delegate ResolveBaggedDelegate(Instruction invoke){
            var get = invoke.Previous;
            var id = get?.Previous;
            if(get == null || id == null || !(get.Operand is MethodReference getRef) || !id.MatchLdcI4(out int index)) return null;
            try{
                var getter = getRef.ResolveReflection();
                if(getter == null || !IsReferenceBagGetter(getter)) return null;
                return ((MethodInfo)getter).Invoke(null, new object[]{ index }) as Delegate;
            }
            catch(Exception){ return null; }
        }

        static bool IsDelegateInvoke(MethodBase m) => m.Name == "Invoke" && m.DeclaringType != null && typeof(Delegate).IsAssignableFrom(m.DeclaringType);

        static bool ReturnsVoid(MethodBase m) => m is MethodInfo mi && mi.ReturnType == typeof(void);

        // What the call pops off the stack, or null if some of it can't be boxed into a closure (byrefs, pointers, struct receivers).
        static Type[] StackTypes(MethodBase m){
            if(m.IsConstructor) return null;
            var types = new List<Type>();
            if(!m.IsStatic){
                if(m.DeclaringType == null || m.DeclaringType.IsValueType) return null;
                types.Add(m.DeclaringType);
            }
            foreach(var p in m.GetParameters()){
                if(p.ParameterType.IsByRef || p.ParameterType.IsPointer) return null;
                types.Add(p.ParameterType);
            }
            if(types.Any(t => t.ContainsGenericParameters)) return null;
            return types.ToArray();
        }

        static bool AtExceptionBoundary(ILContext il, Instruction instr){
            foreach(var h in il.Body.ExceptionHandlers){
                foreach(var edge in new[]{ h.TryStart, h.TryEnd, h.HandlerStart, h.HandlerEnd, h.FilterStart }){
                    if(edge != null && (edge == instr || edge == instr.Next)) return true;
                }
            }
            return false;
        }

        static void EmitBail(ILContext il, Instruction instr, string site){
            int id = BailSiteNames.IndexOf(site);
            if(id < 0){
                id = BailSiteNames.Count;
                BailSiteNames.Add(site);
            }
            var c = new ILCursor(il).Goto(instr);
            c.MoveAfterLabels();
            c.Emit(OpCodes.Ldc_I4, id);
            c.Emit(OpCodes.Call, RT(nameof(ParallelRuntime.BailIfParallel)));
        }

        public static string BailSiteName(int id) => id == -1 ? "the Disable All Skills debuff toggling" : (id >= 0 && id < BailSiteNames.Count ? BailSiteNames[id] : "unknown");

        // Pops the call's arguments into locals, then either replays the call (main thread) or boxes them into an object[]
        // handed to `defer` (worker).
        static void EmitDeferral(ILContext il, Instruction call, Type[] stack, Action<ILCursor> defer){
            var locals = stack.Select(t => { var v = new VariableDefinition(il.Import(t)); il.Body.Variables.Add(v); return v; }).ToArray();
            var c = new ILCursor(il).Goto(call);
            c.MoveAfterLabels();
            for(int i = locals.Length - 1; i >= 0; i--) c.Emit(OpCodes.Stloc, locals[i]);
            ILLabel direct = c.DefineLabel(), done = c.DefineLabel();
            c.Emit(OpCodes.Call, typeof(ParallelRuntime).GetProperty(nameof(ParallelRuntime.InParallel)).GetGetMethod());
            c.Emit(OpCodes.Brfalse, direct);
            c.Emit(OpCodes.Ldc_I4, locals.Length);
            c.Emit(OpCodes.Newarr, il.Import(typeof(object)));
            for(int i = 0; i < locals.Length; i++){
                c.Emit(OpCodes.Dup);
                c.Emit(OpCodes.Ldc_I4, i);
                c.Emit(OpCodes.Ldloc, locals[i]);
                if(stack[i].IsValueType) c.Emit(OpCodes.Box, il.Import(stack[i]));
                c.Emit(OpCodes.Stelem_Ref);
            }
            defer(c);
            c.Emit(OpCodes.Br, done);
            c.MarkLabel(direct);
            foreach(var local in locals) c.Emit(OpCodes.Ldloc, local);
            c.Goto(call, MoveType.After);
            c.MarkLabel(done);
        }

        static int DeferSiteId(MethodBase m){
            if(!deferSiteIds.TryGetValue(m, out int id)){
                id = ParallelRuntime.deferSites.Count;
                ParallelRuntime.deferSites.Add(m);
                deferSiteIds[m] = id;
            }
            return id;
        }

        // Mod code: anything loaded from disk that isn't the BCL or one of the game's own assemblies.
        static bool IsPluginAssembly(Assembly a){
            if(a == null || a.IsDynamic || NativeAnalyzer.IsBcl(a.GetName().Name)) return false;
            try{
                string location = a.Location;
                if(string.IsNullOrEmpty(location)) return false;
                string gameDir = System.IO.Path.GetDirectoryName(typeof(CharacterBody).Assembly.Location);
                return !string.Equals(System.IO.Path.GetDirectoryName(location), gameDir, StringComparison.OrdinalIgnoreCase);
            }
            catch(Exception){ return false; }
        }

        static NativeAnalyzer.Verdict Override(MethodBase m){
            if(RsapiCompat.Patched.Contains(m)) return NativeAnalyzer.Verdict.Safe;
            // orig(self) inside an On hook: the original plus the rest of the chain, which are analyzed separately.
            if(m.Name == "Invoke" && m.DeclaringType != null && m.DeclaringType.Name.StartsWith("orig_", StringComparison.Ordinal)
                && (m.DeclaringType.Namespace?.StartsWith("On.", StringComparison.Ordinal) ?? false)) return NativeAnalyzer.Verdict.Safe;
            var extras = new List<MethodBase>();
            string issue = HookIntrospection.CheckHooks(m, extras);
            if(issue != null) return NativeAnalyzer.Verdict.No(NativeAnalyzer.Describe(m) + " is " + issue);
            if(extras.Count > 0) hookExtras[m] = extras;
            return null;
        }

        static IEnumerable<MethodBase> Extras(MethodBase m){
            if(hookExtras.TryGetValue(m, out var hooked)){
                foreach(var h in hooked) yield return h;
            }
            if(!(m is MethodInfo mi) || !mi.IsVirtual || mi.IsFinal) yield break;
            var declaring = mi.DeclaringType;
            if(declaring == null || declaring.IsSealed || declaring.IsInterface) yield break;
            string asm = declaring.Assembly.GetName().Name;
            if(NativeAnalyzer.IsBcl(asm) || NativeAnalyzer.IsUnity(asm)) yield break;
            if(!Derived().TryGetValue(declaring.IsGenericType ? declaring.GetGenericTypeDefinition() : declaring, out var subtypes)) yield break;
            var baseDef = mi.GetBaseDefinition();
            foreach(var t in subtypes){
                if(t.ContainsGenericParameters) continue;
                foreach(var cand in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)){
                    if(cand.Name != mi.Name || !cand.IsVirtual) continue;
                    var candBase = cand.GetBaseDefinition();
                    if(candBase.MetadataToken == baseDef.MetadataToken && candBase.Module == baseDef.Module) yield return cand;
                }
            }
        }

        static Dictionary<Type, List<Type>> Derived(){
            if(derivedTypes != null) return derivedTypes;
            derivedTypes = new Dictionary<Type, List<Type>>();
            foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){
                string name = a.GetName().Name;
                if(a.IsDynamic || NativeAnalyzer.IsBcl(name) || NativeAnalyzer.IsUnity(name)) continue;
                Type[] types;
                try{ types = a.GetTypes(); }
                catch(ReflectionTypeLoadException e){ types = e.Types.Where(t => t != null).ToArray(); }
                catch(Exception){ continue; }
                foreach(var t in types){
                    try{
                        for(var b = t.BaseType; b != null && b != typeof(object); b = b.BaseType){
                            var key = b.IsGenericType ? b.GetGenericTypeDefinition() : b;
                            if(!derivedTypes.TryGetValue(key, out var list)) derivedTypes[key] = list = new List<Type>();
                            list.Add(t);
                        }
                    }
                    catch(Exception){ }
                }
            }
            return derivedTypes;
        }

        // A static constructor first reached on a worker would run there; get them all out of the way on the main thread.
        // Called from the batch loop rather than the manipulator, since a constructor may itself install hooks.
        public static void RunStaticConstructors(){
            if(touchedTypes.Count == 0) return;
            foreach(var t in touchedTypes.ToArray()){
                if(t.ContainsGenericParameters) continue;
                string asm = t.Assembly.GetName().Name;
                if(NativeAnalyzer.IsBcl(asm)) continue;
                try{ RuntimeHelpers.RunClassConstructor(t.TypeHandle); }
                catch(Exception){ }
            }
            touchedTypes.Clear();
        }

        static string Describe(MethodBase m) => NativeAnalyzer.Describe(m);
    }
}
