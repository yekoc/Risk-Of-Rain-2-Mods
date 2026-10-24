using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.HookGen;

namespace HolyHolyHoly
{
    // Reflection can hand out distinct MethodInfo instances for one runtime method (e.g. different ReflectedType).
    internal sealed class MethodComparer : IEqualityComparer<MethodBase>
    {
        public static readonly MethodComparer Instance = new MethodComparer();
        public bool Equals(MethodBase a, MethodBase b){
            if(ReferenceEquals(a, b)) return true;
            if(a == null || b == null) return false;
            try{ return a.MethodHandle == b.MethodHandle; }
            catch(Exception){ return false; }
        }
        public int GetHashCode(MethodBase m){
            try{ return m.MethodHandle.GetHashCode(); }
            catch(Exception){ return m.GetHashCode(); }
        }
    }

    // Reads MonoMod/Harmony bookkeeping so the analyzer knows what else runs when a method gets called.
    // BepInEx 5 is frozen on legacy MonoMod, so these private fields are stable; if they ever vanish we refuse to parallelize.
    internal static class HookIntrospection
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        static readonly FieldInfo detourMap = typeof(Detour).GetField("_DetourMap", Any);
        static readonly FieldInfo ilHookMap = typeof(ILHook).GetField("_Map", Any);
        static readonly FieldInfo endpointMap = typeof(HookEndpointManager).GetField("HookEndpointMap", Any);
        static readonly PropertyInfo ilHookCtx = typeof(ILHook).GetProperty("_Ctx", Any);

        // Only turned off by the offline test harness, where Harmony can't initialize.
        internal static bool InspectHarmony = true;

        public static bool Available =>detourMap != null && ilHookMap != null && endpointMap != null && ilHookCtx != null;

        // The method MonoMod generated from all IL manipulators: calling it directly skips every On hook/Detour stacked on top.
        public static MethodBase GetILTarget(ILHook hook){
            object ctx = ilHookCtx.GetValue(hook);
            if(ctx == null) return null;
            var detour = ctx.GetType().GetField("Detour", Any)?.GetValue(ctx) as Detour;
            return detour?.Target;
        }

        public static int DetourCount(MethodBase m){
            var list = Lookup(detourMap, m) as ICollection;
            return list?.Count ?? 0;
        }

        // Non-null verdict when hooks on m can't be accounted for. extras receives code that runs when m is called.
        public static string CheckHooks(MethodBase m, List<MethodBase> extras){
            int detours = DetourCount(m);
            int expectedDetours = 0;

            if(Lookup(ilHookMap, m) is object ctx && ctx.GetType().GetField("Chain", Any)?.GetValue(ctx) is IEnumerable chain){
                bool anyApplied = false;
                foreach(ILHook hook in chain){
                    if(!hook.IsApplied) continue;
                    anyApplied = true;
                    var manipulatorType = hook.Manipulator?.Method?.DeclaringType;
                    // Harmony goes through an ILHook too; its patches are read from the patch info below instead.
                    if(manipulatorType?.FullName == "HarmonyLib.Public.Patching.ManagedMethodPatcher") continue;
                    if(manipulatorType?.Assembly == typeof(HookIntrospection).Assembly) continue;
                    return "IL-hooked by " + Owner(hook.Manipulator);
                }
                if(anyApplied) expectedDetours++;
            }

            if(Lookup(endpointMap, m) is object endpoint && endpoint.GetType().GetField("HookMap", Any)?.GetValue(endpoint) is IDictionary hookMap){
                foreach(DictionaryEntry entry in hookMap){
                    if(!(entry.Key is Delegate hookDelegate) || !(entry.Value is IEnumerable stack)) continue;
                    foreach(var detour in stack){
                        if(detour is Hook){
                            expectedDetours++;
                            foreach(var d in hookDelegate.GetInvocationList()) extras.Add(d.Method);
                        }
                    }
                }
            }
            if(detours > expectedDetours) return "detoured by a hook that can't be inspected";

            var info = InspectHarmony ? Harmony.GetPatchInfo(m) : null;
            if(info != null){
                if(info.Transpilers.Count > 0) return "transpiled by " + string.Join(", ", info.Transpilers.Select(p => p.owner));
                extras.AddRange(info.Prefixes.Select(p => (MethodBase)p.PatchMethod));
                extras.AddRange(info.Postfixes.Select(p => (MethodBase)p.PatchMethod));
                extras.AddRange(info.Finalizers.Select(p => (MethodBase)p.PatchMethod));
            }
            return null;
        }

        public static string Owner(Delegate d){
            var t = d?.Method?.DeclaringType;
            return t == null ? "unknown" : t.Assembly.GetName().Name + " (" + t.FullName + ")";
        }

        // Keys can be different MethodInfo instances for the same runtime method, so fall back to a handle index
        // that gets rebuilt whenever the map changes size.
        sealed class HandleIndex
        {
            public int count = -1;
            public readonly Dictionary<RuntimeMethodHandle, object> keys = new Dictionary<RuntimeMethodHandle, object>();
        }
        static readonly Dictionary<FieldInfo, HandleIndex> indices = new Dictionary<FieldInfo, HandleIndex>();

        public static void BeginSession(){ indices.Clear(); }

        static object Lookup(FieldInfo mapField, MethodBase m){
            if(!(mapField.GetValue(null) is IDictionary map)) return null;
            lock(map){
                if(map.Contains(m)) return map[m];
                if(!indices.TryGetValue(mapField, out var index)) indices[mapField] = index = new HandleIndex();
                if(index.count != map.Count){
                    index.keys.Clear();
                    foreach(DictionaryEntry entry in map){
                        try{
                            if(entry.Key is MethodBase k) index.keys[k.MethodHandle] = k;
                        }
                        catch(Exception){ } // dynamic methods have no handle
                    }
                    index.count = map.Count;
                }
                try{
                    if(index.keys.TryGetValue(m.MethodHandle, out var key)) return map[key];
                }
                catch(Exception){ }
            }
            return null;
        }
    }
}
