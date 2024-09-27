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
    public static class PaladinBolt{
     public static AssignableSkillDef def;
     public static SkillDef scepterdef;
     public static BuffDef scepterbuff;
     public static bool isHooked;
     public static bool paladinUpdatedWhileIWasntLooking = false;
     public static GameObject lightningEffectPrefab;
     private static System.Reflection.FieldInfo updatedLightningTimer;


     static PaladinBolt(){
         paladinUpdatedWhileIWasntLooking = BepInEx.Bootstrap.Chainloader.PluginInfos[PaladinMod.PaladinPlugin.MODUID].Metadata.Version.CompareTo(new System.Version(1,6,3)) > 0;
         if(paladinUpdatedWhileIWasntLooking){
            updatedLightningTimer = typeof(PaladinMod.Misc.PaladinSwordController).GetField("lightningBuffTimer",(System.Reflection.BindingFlags)(-1));
         }
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINBOLT","Sunbolt Blessing");
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINBOLT_DESC","Call down a <style=cIsUtility>lightning bolt</style>, boosting your weapon with lightning.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_PALADINBOLT";
         def.skillDescriptionToken = "PASSIVEAGRESSION_PALADINBOLT_DESC";
         def.baseRechargeInterval = 10f;
         def.dontAllowPastMaxStocks = false;
         def.fullRestockOnAssign = true;
         def.rechargeStock = 1;
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(PrepBoltState));
         def.cancelSprintingOnActivation = true;
         def.canceledFromSprinting = true;
         def.isCombatSkill = false;
         def.onAssign += (skillSlot) => {
             if(!isHooked){
                isHooked = true;
                Run.onRunDestroyGlobal += unsub;
             }
             if(!lightningEffectPrefab){
                 var obj = skillSlot.characterBody.modelLocator?.modelTransform?.GetComponent<ChildLocator>()?.FindChild("SwordLightningEffect")?.gameObject;
                 if(obj){
                     lightningEffectPrefab = PrefabAPI.InstantiateClone(obj,"lightningEff",false);
                 }
             }
             return null;
         };
         (def as ScriptableObject).name = def.skillNameToken;
         def.icon = PassiveAgressionPlugin.unfinishedIcon;
         

         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(PrepBoltState),out _);
         ContentAddition.AddEntityState(typeof(CastBoltState),out _);

         void unsub(Run run){ 
            Run.onRunDestroyGlobal -= unsub;
            isHooked = false;
         }
     }

     [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.NoOptimization)]
     public static void SetUpScepter(){
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINBOLT_SCEPTER","Beloved's Sunbolt");
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINBOLT_SCEPTERDESC","Call down an empowering <style=cIsUtility>lightning bolt</style>, boosting yourself and allies with lightning.");
         scepterdef = ScriptableObject.CreateInstance<SkillDef>();
         scepterdef.skillNameToken = "PASSIVEAGRESSION_PALADINBOLT_SCEPTER";
         scepterdef.skillDescriptionToken = "PASSIVEAGRESSION_PALADINBOLT_SCEPTERDESC";
         scepterdef.baseRechargeInterval = 0f;
         scepterdef.dontAllowPastMaxStocks = true;
         scepterdef.fullRestockOnAssign = true;
         scepterdef.rechargeStock = 1;
         scepterdef.activationStateMachineName = "Weapon";
         scepterdef.activationState = new SerializableEntityStateType(typeof(PrepBoltState));
         scepterdef.cancelSprintingOnActivation = false;
         scepterdef.canceledFromSprinting = false;
         scepterdef.isCombatSkill = false;
         (scepterdef as ScriptableObject).name = scepterdef.skillNameToken;
         scepterdef.icon = Util.SpriteFromFile("BoltIconScepter.png");

         scepterbuff = ScriptableObject.CreateInstance<BuffDef>();
         scepterbuff.buffColor = Color.yellow;
         scepterbuff.name = "Sun's Beloved";
         (scepterbuff as ScriptableObject).name = "PASSIVEAGRESSION_PALADINBOLT_SCEPTERBUFF";

         On.RoR2.CharacterBody.OnBuffFirstStackGained += (orig,self,buff) =>{
             orig(self,buff);
             if(buff == scepterbuff && CameraRigController.instancesList[0].targetBody == self){
                 PaladinMod.Modules.Buffs.overchargeBuff.isHidden = true;
             }
         };
         On.RoR2.CharacterBody.OnBuffFinalStackLost += (orig,self,buff) =>{
             orig(self,buff);
             if(buff == scepterbuff && PaladinMod.Modules.Buffs.overchargeBuff.isHidden){
                 PaladinMod.Modules.Buffs.overchargeBuff.isHidden = false;
             }
         };

         ContentAddition.AddSkillDef(scepterdef);
         ContentAddition.AddBuffDef(scepterbuff);
         AncientScepter.AncientScepterItem.instance.RegisterScepterSkill(scepterdef,"RobPaladinBody",def);
     }


     public class PrepBoltState : BaseChannelSpellState{
            public override void OnEnter(){
                    baseDuration = 2f;
                    //base. = true;
		    base.OnEnter();
                    if(areaIndicatorInstance){
                        GameObject.Destroy(areaIndicatorInstance);
                    }
                    areaIndicatorInstance = null;
	    }
	    public override InterruptPriority GetMinimumInterruptPriority(){
		    return InterruptPriority.PrioritySkill;
	    }
            public override BaseCastChanneledSpellState GetNextState(){
                 return new CastBoltState{
                     isScepter = (scepterdef && activatorSkillSlot?.skillDef == scepterdef)
                 };
            }
     }
     public class CastBoltState : BaseCastChanneledSpellState{

         public bool isScepter = false;
         public float buffDuration = 6f;
         public static bool lmao = true;

         public override void OnEnter(){
             baseDuration = 1f;
             base.OnEnter();
             if(lmao){
                 lmao = false;
                 foreach(var skill in RoR2.UI.HUD.instancesList[0].skillIcons.Where(s => s.targetSkill.skillNameToken.Contains("Lightning"))){
                     GameObject.Instantiate(lightningEffectPrefab,skill.gameObject.transform);
                 }
             }
         }
         public override void OnExit(){
             base.OnExit();
             EffectManager.SpawnEffect(PaladinMod.Modules.Assets.altLightningImpactFX,new EffectData{
                     origin = GetModelChildLocator()?.FindChild(muzzleString)?.position ?? characterBody.corePosition
             },transmit: true);

             if(NetworkServer.active){
               var controller = characterBody.GetComponent<PaladinMod.Misc.PaladinSwordController>();
               if(controller){
                   controller.ApplyLightningBuff();
                   if(paladinUpdatedWhileIWasntLooking){
                       updatedLightningTimer.SetValue(controller,buffDuration);
                   }
                   else{
                       controller.CancelInvoke();
                       controller.Invoke("KillLightningBuff", buffDuration);
                   }
               }
               else{
                   characterBody.AddTimedBuff(PaladinMod.Modules.Buffs.overchargeBuff,buffDuration);
               }
               if(isScepter){
                   characterBody.AddTimedBuff(scepterbuff,buffDuration);
               }
             }
         }
         public override InterruptPriority GetMinimumInterruptPriority(){
                 return InterruptPriority.PrioritySkill;
         }
     }
    }
}
