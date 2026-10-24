using BepInEx;
using BepInEx.Configuration;
using RoR2;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Permissions;
using System.Threading.Tasks;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using R2API;
using UnityEngine;

#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace HolyHolyHoly
{
    [BepInPlugin(GUID, "HolyHolyHOLY", "1.1.0")]
    [BepInDependency(RecalculateStatsAPI.PluginGUID, BepInDependency.DependencyFlags.HardDependency)]
    public class HolyPlugin : BaseUnityPlugin
    {
        public const string GUID = "xyz.yekoc.Holy";

        sealed class ReferenceComparer : IEqualityComparer<CharacterBody>
        {
            // UnityEngine.Object equality treats every destroyed object as equal to null and to each other.
            public bool Equals(CharacterBody a, CharacterBody b) => ReferenceEquals(a, b);
            public int GetHashCode(CharacterBody b) => RuntimeHelpers.GetHashCode(b);
        }

        static HolyPlugin instance;
        static MethodInfo recalculateStats;
        static readonly Dictionary<CharacterBody, HolyData> bodies = new Dictionary<CharacterBody, HolyData>(new ReferenceComparer());
        static readonly List<CharacterBody> dirty = new List<CharacterBody>();

        ConfigEntry<bool> enabledConfig;
        ConfigEntry<int> minimumBatch, maxThreads;
        ILHook recalcHook, sprintHook;
        MethodBase workerTarget;
        Action<CharacterBody> worker;
        bool ready, serialFallback;
        int windowBatches, windowBodies, windowBails;
        readonly Dictionary<int, int> windowBailSites = new Dictionary<int, int>();
        readonly HashSet<string> loggedErrors = new HashSet<string>();

        void Awake(){
            instance = this;
            enabledConfig = Config.Bind("General", "Enabled", true, "Recalculate character stats on worker threads. Turning this off makes the mod do nothing.");
            minimumBatch = Config.Bind("General", "Minimum Batch Size", 4, "Batches smaller than this are recalculated on the main thread, where thread overhead would outweigh the gain.");
            maxThreads = Config.Bind("General", "Max Threads", 0, "Upper limit on threads used per batch, 0 uses every core.");
            var deferForeign = Config.Bind("Compatibility", "Defer Foreign Calls", true, "Let other mods' RecalculateStats IL hooks/Harmony patches that touch main-thread-only code run right after the parallel part. When off, bodies reaching such code are recalculated on the main thread instead (exact, but slower).");
            var verbose = Config.Bind("Debug", "Log Analysis", false, "Log the verdict for every call inside RecalculateStats.");
            if(!enabledConfig.Value) return;
            if(!HookIntrospection.Available){
                Logger.LogError("Unrecognized MonoMod version, can't verify other mods' hooks. Staying disabled.");
                return;
            }

            recalculateStats = typeof(CharacterBody).GetMethod(nameof(CharacterBody.RecalculateStats), BindingFlags.Public | BindingFlags.Instance);
            RsapiCompat.Init(Logger);
            RecalcPatcher.Init(Logger);
            RecalcPatcher.DeferForeignCalls = deferForeign.Value;
            RecalcPatcher.LogEverything = verbose.Value;

            IL.RoR2.CharacterBody.FixedUpdate += Batchify;
            IL.RoR2.CharacterMaster.OnBodyStart += Batchify;
            sprintHook = new ILHook(typeof(CharacterBody).GetProperty(nameof(CharacterBody.isSprinting)).GetSetMethod(), Batchify);
            // Installed once every plugin has loaded so the first analysis sees their hooks; refreshed on every run start.
            RoR2Application.onLoad += InstallRecalculateHook;
            Run.onRunStartGlobal += _ => Refresh();
            Stage.onStageStartGlobal += _ => { bodies.Clear(); dirty.Clear(); };
            StartCoroutine(BatchLoop());
        }

        void OnDestroy(){
            IL.RoR2.CharacterBody.FixedUpdate -= Batchify;
            IL.RoR2.CharacterMaster.OnBodyStart -= Batchify;
            sprintHook?.Dispose();
            recalcHook?.Dispose();
            RsapiCompat.Unload();
            ready = false;
        }

        void InstallRecalculateHook(){
            var config = new ILHookConfig{ ID = GUID, After = new[]{ "*" } };
            recalcHook = new ILHook(recalculateStats, RecalcPatcher.Manipulate, ref config);
            UpdateWorker();
        }

        void Refresh(){
            if(recalcHook == null) return;
            serialFallback = false;
            windowBatches = windowBodies = windowBails = 0;
            windowBailSites.Clear();
            RecalcPatcher.BeginSession();
            recalcHook.Undo();
            recalcHook.Apply();
            UpdateWorker();
            Logger.LogInfo(ready
                ? $"RecalculateStats ready for parallel execution ({RecalcPatcher.DeferredSites} deferred calls, {RecalcPatcher.BailSites} main-thread fallbacks)."
                : "RecalculateStats will stay on the main thread this run, see earlier messages.");
        }

        // Workers call the method MonoMod generated from all IL manipulators directly, skipping On hooks/Detours;
        // those run afterwards on the main thread around the computed result.
        void UpdateWorker(){
            var target = HookIntrospection.GetILTarget(recalcHook);
            if(target != workerTarget){
                workerTarget = target;
                worker = null;
                try{
                    if(target is DynamicMethod dm) worker = (Action<CharacterBody>)dm.CreateDelegate(typeof(Action<CharacterBody>));
                    else if(target is MethodInfo mi) worker = (Action<CharacterBody>)Delegate.CreateDelegate(typeof(Action<CharacterBody>), mi);
                }
                catch(Exception e){
                    Logger.LogError("Couldn't bind the patched RecalculateStats: " + e);
                }
            }
            ready = worker != null && RecalcPatcher.Healthy;
        }

        static void Batchify(ILContext il){
            var c = new ILCursor(il);
            var enqueue = typeof(HolyPlugin).GetMethod(nameof(Enqueue));
            while(c.TryGotoNext(x => x.MatchCallOrCallvirt(recalculateStats))){
                c.Next.OpCode = Mono.Cecil.Cil.OpCodes.Call;
                c.Next.Operand = il.Import(enqueue);
                c.Index++;
            }
        }

        // Replaces RecalculateStats() at the batched call sites; the body gets recalculated at the end of this physics step.
        public static void Enqueue(CharacterBody body){
            if(instance == null || !instance.ready || ParallelRuntime.InParallel || !body || !Run.instance){
                body.RecalculateStats();
                return;
            }
            if(!bodies.TryGetValue(body, out var data)) bodies[body] = data = new HolyData();
            if(!data.queued){
                data.queued = true;
                dirty.Add(body);
            }
            body.statsDirty = false;
        }

        IEnumerator BatchLoop(){
            // Resumes after every FixedUpdate and the physics step, so everything queued this step gets processed this step.
            var wait = new WaitForFixedUpdate();
            while(true){
                yield return wait;
                try{ ProcessBatch(); }
                catch(Exception e){ Logger.LogError(e); }
            }
        }

        void ProcessBatch(){
            if(dirty.Count == 0) return;
            var batch = new List<CharacterBody>(dirty.Count);
            foreach(var body in dirty){
                if(bodies.TryGetValue(body, out var d)) d.queued = false;
                if(body) batch.Add(body);
                else bodies.Remove(body);
            }
            dirty.Clear();
            if(batch.Count == 0) return;

            UpdateWorker();
            RecalcPatcher.RunStaticConstructors();
            if(!ready || serialFallback || !Run.instance || batch.Count < minimumBatch.Value){
                foreach(var body in batch) RecalculateOnMainThread(body, null);
                return;
            }

            var datas = new HolyData[batch.Count];
            ParallelRuntime.gravity = Physics.gravity.y;
            for(int i = 0; i < batch.Count; i++){
                var body = batch[i];
                var d = datas[i] = bodies[body];
                d.needsSync = false;
                d.bailSite = 0;
                d.error = null;
                d.deferred.Clear();
                d.prevLevel = body.level;
                d.prevMaxHealth = body.maxHealth;
                d.prevMaxShield = body.maxShield;
                try{
                    _ = body.master; // warms the cached master lookup, which uses GetComponent the first time
                    RsapiCompat.Prepare(body, d);
                }
                catch(Exception e){
                    d.needsSync = true;
                    d.error = e;
                }
            }

            var run = worker;
            var options = new ParallelOptions{ MaxDegreeOfParallelism = maxThreads.Value > 0 ? maxThreads.Value : -1 };
            Parallel.For(0, batch.Count, options, i => {
                var d = datas[i];
                if(d.needsSync) return;
                ParallelRuntime.Enter(d);
                try{ run(batch[i]); }
                catch(BailException){ d.needsSync = true; }
                catch(Exception e){
                    d.needsSync = true;
                    d.error = e;
                }
                finally{ ParallelRuntime.Exit(); }
            });

            // On hooks/Detours on RecalculateStats were skipped by the workers, run them around the results now.
            bool runDetours = HookIntrospection.DetourCount(recalculateStats) > 1;
            int bails = 0;
            for(int i = 0; i < batch.Count; i++){
                var body = batch[i];
                var d = datas[i];
                if(d.needsSync){
                    bails++;
                    windowBailSites.TryGetValue(d.bailSite, out int n);
                    windowBailSites[d.bailSite] = n + 1;
                    RecalculateOnMainThread(body, d);
                    continue;
                }
                try{
                    ParallelRuntime.RunDeferred(body, d);
                    if(runDetours){
                        ParallelRuntime.skipTarget = body;
                        try{ body.RecalculateStats(); }
                        finally{ ParallelRuntime.skipTarget = null; }
                    }
                }
                catch(Exception e){ LogOnce("Error while finishing a parallel stat recalculation", e); }
            }
            TrackFallbacks(batch.Count, bails);
        }

        void RecalculateOnMainThread(CharacterBody body, HolyData abandoned){
            if(abandoned != null){
                // Undo what the abandoned worker run changed that the full run uses to detect changes (level ups, heal deltas).
                abandoned.deferred.Clear();
                body.level = abandoned.prevLevel;
                body.maxHealth = abandoned.prevMaxHealth;
                body.maxShield = abandoned.prevMaxShield;
            }
            try{
                body.RecalculateStats();
                if(abandoned?.error != null) LogOnce("RecalculateStats failed on a worker thread but worked on the main thread", abandoned.error);
            }
            catch(Exception e){ LogOnce("RecalculateStats threw", e); }
        }

        // If most bodies end up redone on the main thread anyway, the parallel attempt is pure overhead.
        void TrackFallbacks(int count, int bails){
            windowBatches++;
            windowBodies += count;
            windowBails += bails;
            if(windowBatches < 32) return;
            if(windowBails * 2 > windowBodies){
                serialFallback = true;
                var top = windowBailSites.OrderByDescending(p => p.Value).Take(3).Select(p => $"{RecalcPatcher.BailSiteName(p.Key)} ({p.Value})");
                Logger.LogWarning($"{windowBails} of {windowBodies} recent stat recalculations needed the main thread, running them serially until the next run. Most common causes: {string.Join("; ", top)}");
            }
            windowBatches = windowBodies = windowBails = 0;
            windowBailSites.Clear();
        }

        void LogOnce(string context, Exception e){
            string key = context + e.GetType().FullName + e.Message;
            if(loggedErrors.Count < 64 && loggedErrors.Add(key)) Logger.LogError(context + ":\n" + e);
        }
    }
}
