using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using R2API;
using RoR2;

namespace HolyHolyHoly
{
    // RecalculateStatsAPI keeps the modifiers of "the body currently being recalculated" in static fields,
    // which every worker would trample. Each read/write of those fields gets redirected to per-body storage
    // while on a worker; GetStatCoefficients itself (arbitrary mod code) still runs on the main thread beforehand.
    internal static class RsapiCompat
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        public static bool Active { get; private set; }
        public static MethodInfo GetStatModsMethod { get; private set; }
        public static readonly HashSet<MethodBase> Patched = new HashSet<MethodBase>(MethodComparer.Instance);

        static FieldInfo statModsField, customStatsField;
        static Action<CharacterBody> getStatMods;
        static Func<CharacterBody, object> getCustomStats;
        static readonly List<ILHook> hooks = new List<ILHook>();

        public static void Init(ManualLogSource log, bool installHooks = true){
            var api = typeof(RecalculateStatsAPI);
            statModsField = api.GetField("StatMods", Any);
            customStatsField = api.GetField("BodyCustomStats", Any);
            GetStatModsMethod = api.GetMethod("GetStatMods", Any, null, new[]{ typeof(CharacterBody) }, null);
            var custom = api.GetMethod("GetCustomStatsFromBody", Any, null, new[]{ typeof(CharacterBody) }, null);
            if(statModsField == null || customStatsField == null || GetStatModsMethod == null || custom == null){
                log.LogWarning("R2API.RecalculateStats internals changed, can't make it thread safe.");
                return;
            }
            getStatMods = (Action<CharacterBody>)Delegate.CreateDelegate(typeof(Action<CharacterBody>), GetStatModsMethod);
            getCustomStats = (Func<CharacterBody, object>)Delegate.CreateDelegate(typeof(Func<CharacterBody, object>), custom);

            Type[] types;
            try{ types = api.Assembly.GetTypes(); }
            catch(ReflectionTypeLoadException e){ types = e.Types.Where(t => t != null).ToArray(); }
            foreach(var type in types){
                foreach(var method in type.GetMethods(Any)){
                    if(method.IsAbstract || method.ContainsGenericParameters || !TouchesScratch(method)) continue;
                    // Main thread only, and it is the writer of StatMods.
                    if(method == GetStatModsMethod) continue;
                    if(installHooks) hooks.Add(new ILHook(method, RedirectScratch));
                    Patched.Add(method);
                }
            }
            Active = true;
        }

        public static void Unload(){
            foreach(var h in hooks) h.Dispose();
            hooks.Clear();
            Patched.Clear();
            Active = false;
        }

        public static void GetStatMods(CharacterBody body) => getStatMods(body);

        // Main thread, right before the batch: gather this body's modifiers exactly like RSAPI would at the top of RecalculateStats.
        public static void Prepare(CharacterBody body, HolyData data){
            if(!Active) return;
            getStatMods(body);
            data.statMods = statModsField.GetValue(null);
            data.customStats = getCustomStats(body);
        }

        static bool TouchesScratch(MethodBase m){
            byte[] il;
            try{ il = m.GetMethodBody()?.GetILAsByteArray(); }
            catch(Exception){ return false; }
            if(il == null) return false;
            int a = statModsField.MetadataToken, b = customStatsField.MetadataToken;
            // Cheap scan for the field tokens after ldsfld/stsfld; a false positive only costs a no-op hook.
            for(int i = 0; i + 4 < il.Length; i++){
                if(il[i] != 0x7E && il[i] != 0x80) continue;
                int token = BitConverter.ToInt32(il, i + 1);
                if(token == a || token == b) return true;
            }
            return false;
        }

        static void RedirectScratch(ILContext il){
            var inParallel = typeof(ParallelRuntime).GetProperty(nameof(ParallelRuntime.InParallel)).GetGetMethod();
            foreach(var instr in il.Instrs.ToArray()){
                if(instr.OpCode != OpCodes.Ldsfld && instr.OpCode != OpCodes.Stsfld) continue;
                if(!(instr.Operand is FieldReference fieldRef)) continue;
                FieldInfo field;
                try{ field = fieldRef.ResolveReflection() as FieldInfo; }
                catch(Exception){ continue; }
                bool isMods = field == statModsField, isCustom = field == customStatsField;
                if(!isMods && !isCustom) continue;
                bool load = instr.OpCode == OpCodes.Ldsfld;
                if(!load && isMods) continue;

                // Reuse the original instruction as the branch head so incoming jumps stay valid.
                instr.OpCode = OpCodes.Call;
                instr.Operand = il.Import(inParallel);
                var c = new ILCursor(il).Goto(instr, MoveType.After);
                ILLabel onWorker = c.DefineLabel(), done = c.DefineLabel();
                c.Emit(OpCodes.Brtrue, onWorker);
                c.Emit(load ? OpCodes.Ldsfld : OpCodes.Stsfld, fieldRef);
                c.Emit(OpCodes.Br, done);
                c.MarkLabel(onWorker);
                if(load){
                    c.Emit(OpCodes.Call, typeof(ParallelRuntime).GetMethod(isMods ? nameof(ParallelRuntime.GetThreadStatMods) : nameof(ParallelRuntime.GetThreadCustomStats)));
                    c.Emit(OpCodes.Castclass, il.Import(field.FieldType));
                }
                else c.Emit(OpCodes.Call, typeof(ParallelRuntime).GetMethod(nameof(ParallelRuntime.SetThreadCustomStats)));
                c.MarkLabel(done);
            }
        }
    }
}
