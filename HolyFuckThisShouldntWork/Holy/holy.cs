using BepInEx;
using RoR2;
using System;
using System.Security;
using System.Security.Permissions;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.HookGen;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.Collections.Concurrent;
using R2API;
using static R2API.RecalculateStatsAPI;
using ExtraSkillSlots;

#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace HolyHolyHoly
{
    [BepInPlugin("xyz.yekoc.Holy", "HolyHolyHOLY","1.0.9" )]
    [BepInDependency(RecalculateStatsAPI.PluginGUID,BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.KingEnderBrine.ExtraSkillSlots", BepInDependency.DependencyFlags.SoftDependency)]
    public class HolyPlugin : BaseUnityPlugin
    {
	public class HolyData{
	  public StatHookEventArgs args;
	  public bool OnLevel;
	  public bool statsDirty;
          public float prevMaxHealth;
          public float prevMaxShield;
	}
        static bool extraSkillSlots;
        static float currentGrav;
	ILHook sprint,ih;
	Hook h,trajGrav;
	public static event On.RoR2.CharacterBody.hook_RecalculateStats hookStealer;
	static ConcurrentDictionary<CharacterBody,HolyData> queue = new ConcurrentDictionary<CharacterBody,HolyData>(Environment.ProcessorCount * 2,100);
	ILContext.Manipulator Taskifier = ((il) => {
		ILCursor c = new ILCursor(il);
		while(c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetMethod("RecalculateStats")))){
                 ILLabel label = c.MarkLabel();
                 c.Index--;
		 c.EmitDelegate<Action<CharacterBody>>((cb) => {if(!queue.ContainsKey(cb)) queue.TryAdd(cb,new HolyData()); queue[cb].statsDirty = true; cb.statsDirty = false;});
                 c.Emit(OpCodes.Br,label);
                 c.Emit(OpCodes.Ldarg_0);
                 c.Index++;
		}
	  });

	private void Awake(){
          Run.onRunStartGlobal += (run) =>{
           var orig = typeof(CharacterBody).GetMethod("RecalculateStats"); 
           var endpoint = typeof(HookEndpointManager).GetMethod("GetEndpoint",(BindingFlags)(-1)).Invoke(null,new Object[]{typeof(CharacterBody).GetMethod("RecalculateStats")});
           var hookDict = (endpoint.GetType().GetField("HookMap",(BindingFlags)(-1)).GetValue(endpoint) as Dictionary<Delegate, Stack<IDetour>>);
           if(hookDict.Count > 1){
               Logger.LogInfo("Stealing RecalculateStats hooks to prevent potential crashes...");
           
            foreach(Delegate hook in hookDict.Keys.Where((del) => del.GetType() == typeof(On.RoR2.CharacterBody.hook_RecalculateStats)).ToList()){
               while(hookDict.ContainsKey(hook)){
                hookStealer += hook as On.RoR2.CharacterBody.hook_RecalculateStats;
                HookEndpointManager.Remove(orig,hook);
               }
            }
            if(!extraSkillSlots && BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.KingEnderBrine.ExtraSkillSlots")){
              ExtraSkillSlots();
              extraSkillSlots = true;
            }
         
            Logger.LogDebug("Remaining IL Hooks:");
            foreach(var hook in (endpoint.GetType().GetField("HookList",(BindingFlags)(-1)).GetValue(endpoint) as List<IDetour>)){
               if(hook != null && hook.GetType() == typeof(ILHook)){
                Logger.LogDebug(HookEndpointManager.GetOwner((hook as ILHook).Manipulator));
               }
            }
           }
          };
          Stage.onStageStartGlobal += (stage) =>{
              queue.Clear();
          };
          IL.RoR2.CharacterBody.FixedUpdate += Taskifier;
	  //IL.RoR2.CharacterBody.Start += Taskifier; //Causes Instant Death on spawn.
	  IL.RoR2.CharacterMaster.OnBodyStart += Taskifier;
	  IL.RoR2.CharacterBody.OnCalculatedLevelChanged += OnLevelUp;
	  sprint = new ILHook(typeof(CharacterBody).GetProperty("isSprinting").GetSetMethod(),Taskifier);
          trajGrav = new Hook(typeof(Trajectory).GetProperty("defaultGravity",(BindingFlags)(-1)).GetGetMethod(true),new Func<Func<float>,float>((orig) => currentGrav));
          currentGrav = UnityEngine.Physics.gravity.y;
          ih = new ILHook(typeof(RecalculateStatsAPI).GetMethod("HookRecalculateStats",(System.Reflection.BindingFlags)(-1)),RecalculateStatsAPIRuiner);
          h = new Hook(typeof(RecalculateStatsAPI).GetMethod("GetStatMods",(System.Reflection.BindingFlags)(-1)),typeof(HolyPlugin).GetMethod("RecalculateStatsAPIRuiner2",(System.Reflection.BindingFlags)(-1)));
	  IL.RoR2.CharacterBody.RecalculateStats += UnVisualize;
	}

	private void OnDestroy(){
	  IL.RoR2.CharacterBody.FixedUpdate -= Taskifier;
	  IL.RoR2.CharacterMaster.OnBodyStart -= Taskifier;
	  IL.RoR2.CharacterBody.OnCalculatedLevelChanged -= OnLevelUp;
	  IL.RoR2.CharacterBody.RecalculateStats -= UnVisualize;
	  trajGrav.Undo();
          sprint.Undo();
	  h.Undo();
	  ih.Undo();
	}

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ExtraSkillSlots(){
           Logger.LogDebug("Stealing ExtraSkillSlots RecalculateStats IL Hook");
           var endpoint = typeof(HookEndpointManager).GetMethod("GetEndpoint",(BindingFlags)(-1)).Invoke(null,new Object[]{typeof(CharacterBody).GetMethod("RecalculateStats")});
           var hook = (endpoint.GetType().GetField("HookMap",(BindingFlags)(-1)).GetValue(endpoint) as Dictionary<Delegate, Stack<IDetour>>).Where((pair) =>pair.Key.GetType() == typeof(ILContext.Manipulator) && pair.Key.Method.DeclaringType == typeof(ExtraCharacterBody));
           if(hook.Count() != 0){
            HookEndpointManager.Remove(typeof(CharacterBody).GetMethod("RecalculateStats"),hook.First().Key);
           }
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ExtraSkillStats(CharacterBody body){
            //Replication of default ExtraSkillSlots behaviour.
            //Does this even work when the character doesn't have a primary set?
            if(body?.skillLocator?.primary){
                ExtraCharacterBody.RecalculateCooldowns(body,body.skillLocator.primary.cooldownScale,body.skillLocator.primary.flatCooldownReduction);
        
            }
        }
	private void OnLevelUp(ILContext il){
		ILCursor c = new ILCursor(il);
		if(c.TryGotoNext(x => x.MatchRet())){
                 var label = c.MarkLabel();
		 c.Index -= 1;
		 c.EmitDelegate<Action<CharacterBody>>((CharacterBody cb) => {queue.GetOrAdd(cb,new HolyData()).OnLevel = true;});
                 c.Emit(OpCodes.Br,label);
                 c.Emit(OpCodes.Ldarg_0);
                }
	}

	private void UnVisualize(ILContext il){
		ILCursor c = new ILCursor(il);
                if(c.TryGotoNext(x => x.MatchRet())){
                    c.Emit(OpCodes.Ldarg_0);
                    c.EmitDelegate<Action<CharacterBody>>((cb) => {
                        if(!queue.GetOrAdd(cb,(body) => {return new HolyData();}).statsDirty){
                          //cb.UpdateAllTemporaryVisualEffects();
                          hookStealer?.Invoke((body)=>{},cb);
                          if(extraSkillSlots){
                            ExtraSkillStats(cb);
                          }
                        }
                    });
                }
		if(c.TryGotoPrev(MoveType.Before,x => x.MatchLdarg(0),x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetMethod("UpdateAllTemporaryVisualEffects",(System.Reflection.BindingFlags)(-1))))){
                  c.MoveAfterLabels();
                  c.RemoveRange(2);
                  /*c.MoveAfterLabels();
                  c.Index += 2;
		  var label = c.MarkLabel();
                  c.Index -=2;
                  c.Emit(OpCodes.Br,label);*/
		}
                if(c.TryGotoPrev(MoveType.Before,x => x.MatchCallOrCallvirt(typeof(UnityEngine.Networking.NetworkServer).GetProperty("active").GetGetMethod()),x => x.MatchBrfalse(out _))){
                    c.Index++;
                    c.Emit(OpCodes.Ldarg_0);
                    c.EmitDelegate<Func<bool,CharacterBody,bool>>((active,cb) => (!queue.GetOrAdd(cb,new HolyData()).statsDirty)? active : false);
                }

	}

	private void RecalculateStatsAPIRuiner(ILContext il){
				ILCursor c = new ILCursor(il);
				ILLabel label = c.DefineLabel();
                                if(c.TryGotoNext(MoveType.After,x=> x.MatchCallOrCallvirt(typeof(ILCursor).GetMethod("EmitDelegate").MakeGenericMethod(typeof(Action<CharacterBody>))),x=>x.MatchPop())){
                                 label = c.MarkLabel();
                                 if(c.TryGotoPrev(MoveType.After,x => x.MatchLdloc(0),x => x.MatchLdfld(out _))){
				   c.EmitDelegate<Action<ILCursor>>((cursor) => {cursor.EmitDelegate<Action<CharacterBody>>((body) => {if(!queue.GetOrAdd(body,new HolyData()).statsDirty) RecalculateStatsAPI.GetStatMods(body);});});
                                   c.Emit(OpCodes.Br,label);
                                   c.Emit(OpCodes.Ldloc_0);
                                 }
                                }
	}
	static private void RecalculateStatsAPIRuiner2(Action<CharacterBody> orig,CharacterBody cb){
				orig(cb);
				queue.GetOrAdd(cb,new HolyData()).args = RecalculateStatsAPI.StatMods;
	}
	private void FixedUpdate(){
		if(Run.instance){
                currentGrav = UnityEngine.Physics.gravity.y;
		 foreach(CharacterBody body in queue.Keys.ToList()){
		   if(body && body.healthComponent && body.healthComponent.alive){
                    if(queue[body].statsDirty){
                     queue[body].prevMaxHealth = body.maxHealth;
                     queue[body].prevMaxShield = body.maxShield;
		     RecalculateStatsAPI.GetStatMods(body);
                    }
		   }
		   else{
		     if(!queue.TryRemove(body,out _)){
                         queue[body].statsDirty = false;
                     }
		   }
		 }
                var parallelqueue = queue.Where((pair) => pair.Value.statsDirty).ToList();
		Parallel.ForEach(queue.Keys.Where((b) => queue[b].statsDirty), body =>{
			  body.RecalculateStats();
		});
		foreach(var pair in queue){
			if(pair.Key.healthComponent){
			 if(pair.Value.OnLevel)
			   pair.Key.OnLevelUp();
			 if(pair.Value.statsDirty){
			   pair.Key.UpdateAllTemporaryVisualEffects();
                           hookStealer?.Invoke((body) => {},pair.Key);
                          if(extraSkillSlots){
                            ExtraSkillStats(pair.Key);
                          }
                          if(UnityEngine.Networking.NetworkServer.active){
                            HealthComponent hc = pair.Key.healthComponent;
                            float health = pair.Key.maxHealth - pair.Value.prevMaxHealth;
                            if(health > 0){
                                hc.Heal(health,default(ProcChainMask),false);
                            }
                            else if (hc.health > pair.Key.maxHealth){
                                hc.Networkhealth = UnityEngine.Mathf.Max(hc.health + health, pair.Key.maxHealth); 
                            }
                            float shield = pair.Key.maxShield - pair.Value.prevMaxShield;
                            if(shield > 0){
                                hc.RechargeShield(shield);
                            }
                            else if(hc.shield > pair.Key.maxShield){
                                hc.Networkshield = UnityEngine.Mathf.Max(hc.shield + shield, pair.Key.maxShield);
                            }
                          }
                         }
			}
			pair.Value.statsDirty = false;
			pair.Value.OnLevel = false;
		}
	 }
	}
    }
}
