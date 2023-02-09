
using RoR2;
using RoR2.Skills;
using RoR2.UI;
using EntityStates;
using RoR2.CharacterAI;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Networking;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using PaladinMod.States;
using R2API;

using static RoR2.UI.HealthBar;

namespace PassiveAgression.ModCompat
{
    public static class PaladinResolve{
     public static AssignableSkillDef def;
     public static SkillDef scepterdef;
     public static BuffDef  bdef;
     public static BuffDef hiddenbdef,hiddensbdef;
     public static bool isHooked;
     public static float resolveMult = 0.8f;


     static PaladinResolve(){
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINRESOLVE","Steel Resolve");
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINRESOLVE_DESC","Raise your sword high and steel your resolve. \nYour next accurate sword attack will hit for <style=cIsDamage>80% more TOTAL damage</style>.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_PALADINRESOLVE";
         def.skillDescriptionToken = "PASSIVEAGRESSION_PALADINRESOLVE_DESC";
         def.baseRechargeInterval = 10f;
         def.dontAllowPastMaxStocks = false;
         def.fullRestockOnAssign = true;
         def.rechargeStock = 1;
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(PrepResolveState));
         def.cancelSprintingOnActivation = false;
         def.canceledFromSprinting = false;
         def.isCombatSkill = false;
         def.onAssign += (skillSlot) => {
             if(!isHooked){
                isHooked = true;
                On.RoR2.OverlapAttack.ProcessHits += OverlapFire;
                Run.onRunDestroyGlobal += unsub;
               // IL.RoR2.UI.HealthBar.UpdateBarInfos += ApplyCullStyle;
               // IL.RoR2.HealthComponent.TakeDamage += ApplyCullBonus;
                RecalculateStatsAPI.GetStatCoefficients += (sender,args) =>{
                  if(sender.HasBuff(hiddenbdef)){
                    args.damageMultAdd += resolveMult;
                  }
                  else if(sender.HasBuff(hiddensbdef)){
                    args.damageMultAdd += 2;
                  }
                };
             }
             return null;
         };
         (def as ScriptableObject).name = def.skillNameToken;
         def.icon = Util.SpriteFromFile("ResolveIcon.png");
         
         bdef = ScriptableObject.CreateInstance<BuffDef>();
         (bdef as ScriptableObject).name = "PASSIVEAGRESSION_RESOLVE_BUFF";
         bdef.buffColor = Color.gray;
         bdef.canStack = false;
         bdef.iconSprite = Util.SpriteFromFile("ResolveBuffIcon.png");
         hiddenbdef = ScriptableObject.CreateInstance<BuffDef>();
         (hiddenbdef as ScriptableObject).name = "PASSIVEAGRESSION_RESOLVE_BUFF_ACTUAL";
         hiddenbdef.iconSprite = Util.SpriteFromFile("ResolveBuffIcon.png");


         ContentAddition.AddBuffDef(bdef);
         ContentAddition.AddBuffDef(hiddenbdef);
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(PrepResolveState),out _);
         ContentAddition.AddEntityState(typeof(CastResolveState),out _);

         void unsub(Run run){ 
            On.RoR2.OverlapAttack.ProcessHits -= OverlapFire;
            Run.onRunDestroyGlobal -= unsub;
           // IL.RoR2.UI.HealthBar.UpdateBarInfos -= ApplyCullStyle;
           // IL.RoR2.HealthComponent.TakeDamage -= ApplyCullBonus;
            isHooked = false;
         }
     }

     [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.NoOptimization)]
     public static void SetUpScepter(){
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINRESOLVE_SCEPTER","Royal Resolve");
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINRESOLVE_SCEPTERDESC","Raise your sword high and steel your resolve. \nYour next accurate sword attack will hit for <color=#d299ff>3000% TOTAL damage</color>.");
         scepterdef = ScriptableObject.CreateInstance<SkillDef>();
         scepterdef.skillNameToken = "PASSIVEAGRESSION_PALADINRESOLVE_SCEPTER";
         scepterdef.skillDescriptionToken = "PASSIVEAGRESSION_PALADINRESOLVE_SCEPTERDESC";
         scepterdef.baseRechargeInterval = 0f;
         scepterdef.dontAllowPastMaxStocks = true;
         scepterdef.fullRestockOnAssign = true;
         scepterdef.rechargeStock = 1;
         scepterdef.requiredStock = 0;
         scepterdef.activationStateMachineName = "Weapon";
         scepterdef.activationState = new SerializableEntityStateType(typeof(PrepResolveState));
         scepterdef.cancelSprintingOnActivation = false;
         scepterdef.canceledFromSprinting = false;
         scepterdef.isCombatSkill = false;
         (scepterdef as ScriptableObject).name = scepterdef.skillNameToken;
         scepterdef.icon = Util.SpriteFromFile("ResolveIconScepter.png");;
         hiddensbdef = GameObject.Instantiate(hiddenbdef);
         (hiddensbdef as ScriptableObject).name = "PASSIVEAGRESSION_RESOLVE_BUFFSCEPTER_ACTUAL";
         hiddensbdef.buffColor = new Color32(0xd2,0x99,0xff,0xff);

         ContentAddition.AddBuffDef(hiddensbdef);
         ContentAddition.AddSkillDef(scepterdef);
         AncientScepter.AncientScepterItem.instance.RegisterScepterSkill(scepterdef,"RobPaladinBody",def);
     }

     public static void OverlapFire(On.RoR2.OverlapAttack.orig_ProcessHits orig,OverlapAttack self,object hitlist){
         var list = (hitlist as List<OverlapAttack.OverlapInfo>);
         if(list != null && list.Count > 0 && (!self.inflictor || self.attacker == self.inflictor)){
             var body = self.attacker.GetComponent<CharacterBody>();
             if(body.HasBuff(bdef)){
                body.RemoveBuff(bdef);
                if(body.skillLocator.special.skillDef == scepterdef){
                    body.AddBuff(hiddensbdef);
                    self.damage *= 3f;
                }
                else{
                    body.AddBuff(hiddenbdef);
                    self.damage *= (1 + resolveMult);
                }
                Util.OnStateWorkFinished(EntityStateMachine.FindByCustomName(self.attacker,"Weapon"),(EntityStateMachine machine,ref EntityState state) =>{
                  if(NetworkServer.active && machine.commonComponents.characterBody.HasBuff(hiddenbdef)){
                    machine.commonComponents.characterBody.RemoveBuff(hiddenbdef);
                  }
                  if(NetworkServer.active && machine.commonComponents.characterBody.HasBuff(hiddensbdef)){
                    machine.commonComponents.characterBody.RemoveBuff(hiddensbdef);
                  }
                },new List<Type>{typeof(PaladinMod.States.Slash)});
             }
         }
         orig(self,list);
     }

     public static void ApplyCullStyle(ILContext il){
         var c = new ILCursor(il);
         var ind = -1;
         if(c.TryGotoNext(MoveType.After,x => x.MatchLdloc(out ind),x => x.MatchLdarg(0),x => x.MatchLdfld(out _),x => x.MatchCallOrCallvirt(out _),x => x.MatchCallOrCallvirt(typeof(Mathf).GetMethod("Max",new Type[]{typeof(float),typeof(float)})),x => x.MatchStloc(ind))){
            c.MoveAfterLabels();
            c.Emit(OpCodes.Ldloc,ind);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float,HealthBar,float>>((orig,self) => (self.viewerBody && (self.viewerBody.HasBuff(hiddenbdef) || self.viewerBody.HasBuff(hiddensbdef)))? orig + 0.1f  : orig);
            c.Emit(OpCodes.Stloc,ind);
         }
         else{
           PassiveAgressionPlugin.Logger.LogError("CullStyle Fail");
         }
     }
     public static void ApplyCullBonus(ILContext il){
         var c = new ILCursor(il);
         var ind = -1;
         if(c.TryGotoNext(x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetProperty("executeEliteHealthFraction").GetGetMethod())) && c.TryGotoNext(x => x.MatchLdloc(out ind),x => x.MatchLdcR4(0f),x => x.MatchBleUn(out _)) ){
            c.Emit(OpCodes.Ldloc,ind);
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate<Func<float,DamageInfo,float>>((orig,damage) => {
              if(damage.attacker){
               var body = damage.attacker.GetComponent<CharacterBody>();
               if(body && (body.HasBuff(hiddenbdef) || body.HasBuff(hiddensbdef))){
                  return orig + 0.1f;
               }
              }
              return orig;
            });
            c.Emit(OpCodes.Stloc,ind);
         }
         else{
           PassiveAgressionPlugin.Logger.LogError("CullBonus Fail");
         }
     }

     public class PrepResolveState : BaseChannelSpellState{
            public override void OnEnter(){
                    baseDuration = 1f;
                    //base. = true;
		    base.OnEnter();
                    areaIndicatorInstance = null;
	    }
	    public override InterruptPriority GetMinimumInterruptPriority(){
		    return InterruptPriority.PrioritySkill;
	    }
            public override BaseCastChanneledSpellState GetNextState(){
                 return new CastResolveState();
                
            }
     }
     public class CastResolveState : BaseCastChanneledSpellState{
           
            
            public override void OnEnter(){
                baseDuration = 0.1f;
                base.OnEnter();
                if(NetworkServer.active)
                  characterBody.AddBuff(bdef);
            }
	    public override InterruptPriority GetMinimumInterruptPriority(){
		    return InterruptPriority.PrioritySkill;
	    }
     }
    }
}
