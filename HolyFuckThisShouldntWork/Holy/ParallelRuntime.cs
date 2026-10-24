using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace HolyHolyHoly
{
    internal sealed class HolyData
    {
        public enum Kind { Health, Level, Scale, Visuals, Event, Method, Delegate }
        public struct Deferred
        {
            public Kind kind;
            public float a, b;
            public object o;
            public object[] args;
        }

        public bool queued;
        // Snapshot taken on the main thread right before the batch.
        public float prevLevel, prevMaxHealth, prevMaxShield;
        public object statMods, customStats;
        // Filled by the worker, drained on the main thread in order.
        public readonly List<Deferred> deferred = new List<Deferred>();
        public bool needsSync;
        public int bailSite;
        public Exception error;

        public void Add(Kind kind, float a = 0f, float b = 0f, object o = null, object[] args = null){
            deferred.Add(new Deferred{ kind = kind, a = a, b = b, o = o, args = args });
        }
    }

    // Thrown on a worker to abandon a body whose recalculation needs the main thread; it gets redone synchronously.
    internal sealed class BailException : Exception
    {
        public static readonly BailException Instance = new BailException();
        BailException() : base("HolyHolyHOLY: recalculation moved to the main thread"){ }
    }

    // Everything the rewritten RecalculateStats IL calls into. Each wrapper behaves exactly like the call it replaced
    // unless the current thread is a worker, in which case it defers the work to the main thread or bails.
    public static class ParallelRuntime
    {
        [ThreadStatic] static HolyData current;
        internal static CharacterBody skipTarget;
        internal static float gravity = -30f;
        internal static readonly List<MethodBase> deferSites = new List<MethodBase>();

        public static bool InParallel => current != null;

        internal static void Enter(HolyData data){ current = data; }
        internal static void Exit(){ current = null; }

        // Prologue: lets the main thread run a body's On hooks/Detours around an already computed result.
        public static bool ShouldSkip(CharacterBody body){
            if(skipTarget == null || !ReferenceEquals(skipTarget, body)) return false;
            skipTarget = null;
            return true;
        }

        // Site ids index RecalcPatcher.BailSites; -1 is DisableSkills below.
        public static void BailIfParallel(int site){
            var d = current;
            if(d == null) return;
            d.needsSync = true;
            d.bailSite = site;
            throw BailException.Instance;
        }

        public static void UpdateVisuals(CharacterBody body){
            var d = current;
            if(d != null) d.Add(HolyData.Kind.Visuals);
            else body.UpdateAllTemporaryVisualEffects();
        }

        public static void LevelChanged(CharacterBody body, float oldLevel, float newLevel){
            var d = current;
            if(d != null) d.Add(HolyData.Kind.Level, oldLevel, newLevel);
            else body.OnCalculatedLevelChanged(oldLevel, newLevel);
        }

        public static void SetDesiredScale(CharacterBody body, float value){
            var d = current;
            if(d == null) body.desiredScale = value;
            // The setter kicks off a transition in ScaleTransitionManager's shared arrays, only do that if something changed.
            else if(value != body.desiredScale) d.Add(HolyData.Kind.Scale, value);
        }

        public static void DisableSkills(CharacterBody body){
            if(current == null){
                body.HandleDisableAllSkillsDebuff();
                return;
            }
            // Pure bookkeeping unless the debuff state flips, which swaps skills and plays sounds.
            if(body.HasBuff(DLC2Content.Buffs.DisableAllSkills) != body.allSkillsDisabled) BailIfParallel(-1);
        }

        public static float CalculateApex(float initialSpeed){
            // Trajectory.defaultGravity reads Physics.gravity, the batch caches it beforehand.
            return current != null ? Trajectory.CalculateApex(initialSpeed, gravity) : Trajectory.CalculateApex(initialSpeed);
        }

        public static void InvokeRecalculateEvent(Action<CharacterBody> handlers, CharacterBody body){
            var d = current;
            if(d != null) d.Add(HolyData.Kind.Event, o: handlers);
            else handlers(body);
        }

        public static bool HealthGate(CharacterBody body){
            var d = current;
            if(d == null) return NetworkServer.active;
            d.Add(HolyData.Kind.Health);
            return false;
        }

        public static void GetStatMods(CharacterBody body){
            // On a worker the modifiers were already gathered on the main thread and are served from HolyData.
            if(current == null) RsapiCompat.GetStatMods(body);
        }

        public static object GetThreadStatMods() => current.statMods;
        public static object GetThreadCustomStats() => current.customStats;
        public static void SetThreadCustomStats(object value){ current.customStats = value; }

        public static void DeferMethod(object[] stack, int site){
            current.Add(HolyData.Kind.Method, o: deferSites[site], args: stack);
        }

        public static void DeferDelegate(object[] stack, bool delegateLast){
            current.Add(HolyData.Kind.Delegate, delegateLast ? 1f : 0f, args: stack);
        }

        // Main thread: replay what the worker deferred, in the order the original code would have done it.
        internal static void RunDeferred(CharacterBody body, HolyData data){
            foreach(var entry in data.deferred){
                switch(entry.kind){
                    case HolyData.Kind.Health: ApplyHealthDelta(body, data); break;
                    case HolyData.Kind.Level: body.OnCalculatedLevelChanged(entry.a, entry.b); break;
                    case HolyData.Kind.Scale: body.desiredScale = entry.a; break;
                    case HolyData.Kind.Visuals: body.UpdateAllTemporaryVisualEffects(); break;
                    case HolyData.Kind.Event: ((Action<CharacterBody>)entry.o)(body); break;
                    case HolyData.Kind.Method: InvokeDeferredMethod((MethodBase)entry.o, entry.args); break;
                    case HolyData.Kind.Delegate: InvokeDeferredDelegate(entry.args, entry.a != 0f); break;
                }
            }
            data.deferred.Clear();
        }

        static void InvokeDeferredMethod(MethodBase m, object[] stack){
            try{
                if(m.IsStatic || m.IsConstructor) m.Invoke(null, stack);
                else{
                    var args = new object[stack.Length - 1];
                    Array.Copy(stack, 1, args, 0, args.Length);
                    m.Invoke(stack[0], args);
                }
            }
            catch(TargetInvocationException e) when (e.InnerException != null){
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            }
        }

        static void InvokeDeferredDelegate(object[] stack, bool delegateLast){
            // Two layouts: MonoMod's fast invokers take the delegate last, a plain Invoke has it as the instance.
            Delegate target;
            object[] args = new object[stack.Length - 1];
            if(delegateLast){
                target = (Delegate)stack[stack.Length - 1];
                Array.Copy(stack, 0, args, 0, args.Length);
            }
            else{
                target = (Delegate)stack[0];
                Array.Copy(stack, 1, args, 0, args.Length);
            }
            try{ target.DynamicInvoke(args); }
            catch(TargetInvocationException e) when (e.InnerException != null){
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            }
        }

        // Mirror of the NetworkServer.active block at the end of RecalculateStats.
        static void ApplyHealthDelta(CharacterBody body, HolyData data){
            if(!NetworkServer.active) return;
            var hc = body.healthComponent;
            if(!hc) return;
            float health = body.maxHealth - data.prevMaxHealth;
            float shield = body.maxShield - data.prevMaxShield;
            if(health > 0f) hc.Heal(health, default(ProcChainMask), false);
            else if(hc.health > body.maxHealth) hc.Networkhealth = Mathf.Max(hc.health + health, body.maxHealth);
            if(shield > 0f) hc.RechargeShield(shield);
            else if(hc.shield > body.maxShield) hc.Networkshield = Mathf.Max(hc.shield + shield, body.maxShield);
            if(hc.regainableHealth > hc.missingHealth) hc.NetworkregainableHealth = hc.missingHealth;
        }
    }
}
