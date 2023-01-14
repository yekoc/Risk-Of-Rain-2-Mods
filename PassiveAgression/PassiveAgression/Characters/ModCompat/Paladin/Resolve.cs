
using RoR2;
using RoR2.Skills;
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

namespace PassiveAgression.ModCompat
{
    public static class PaladinResolve{
     public static AssignableSkillDef def;
     public static SkillDef scepterdef;
     public static BuffDef  bdef;//,sbdef;
     public static BuffDef hiddenbdef,hiddensbdef;
     public static bool isHooked;
     public static float resolveMult = 0.8f;


     static PaladinResolve(){
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINRESOLVE","Steel Resolve");
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINRESOLVE_DESC","Raise your sword high and steel your Resolve. \n Your next accurate sword attack will hit for <style=cIsDamage>80% more total damage</style>.");
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
            isHooked = false;
         }
     }

     [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.NoOptimization)]
     public static void SetUpScepter(){
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINRESOLVE_SCEPTER","Royal Resolve");
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINRESOLVE_SCEPTERDESC","Raise your sword high and steel your Resolve. \n Your next accurate sword attack will hit for <color=#d299ff>3000% total damage</color>.");
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
/*       sbdef = GameObject.Instantiate(bdef);
         (sbdef as ScriptableObject).name = "PASSIVEAGRESSION_RESOLVE_BUFFSCEPTER";*/
         hiddensbdef = GameObject.Instantiate(hiddenbdef);
         (hiddensbdef as ScriptableObject).name = "PASSIVEAGRESSION_RESOLVE_BUFFSCEPTER_ACTUAL";
         hiddensbdef.buffColor = new Color32(0xd2,0x99,0xff,0xff);

         //ContentAddition.AddBuffDef(sbdef);
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
                base.OnEnter();
              /*  if(activatorSkillSlot.skillDef = scepterdef){
                  characterBody.AddBuff(sbdef);
                }
                else*/
                  characterBody.AddBuff(bdef);
            }
	    public override InterruptPriority GetMinimumInterruptPriority(){
		    return InterruptPriority.PrioritySkill;
	    }
     }
    }
}
