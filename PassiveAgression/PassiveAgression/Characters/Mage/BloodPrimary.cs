using RoR2;
using RoR2.Skills;
using RoR2.Projectile;
using EntityStates;
using EntityStates.Mage.Weapon;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using RoR2.Orbs;

namespace PassiveAgression.Mage
{
    public static class BloodPrimary{
     public static BloodSiphonSkillDef def;
     public static GameObject proPrefab;
     public static GameObject muzzleFlash;
     private static bool isHooked;

     static BloodPrimary(){
         var epiESC = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<EntityStateConfiguration>("RoR2/Base/Mage/EntityStates.Mage.Weapon.FireFireBolt.asset");
         
         LanguageAPI.Add("PASSIVEAGRESSION_MAGEBLOODBOLT","Excise");
         LanguageAPI.Add("PASSIVEAGRESSION_MAGEBLOODBOLT_DESC","<style=cDeath>Blood Siphon</style>. Fire a bolt for <style=cIsDamage>100%</style> damage that <style=cIsDamage>bleeds</style> enemies.");
         LanguageAPI.Add("PASSIVEAGRESSION_MAGEBLOODBOLT_KEYWORD","<style=cKeywordName>Blood Siphon</style><style=cSub>This skill can be used at all times,however, <style=cIsHealth> You cannot regenerate health </style> while it is on cooldown.</style>");
         
         def = ScriptableObject.CreateInstance<BloodSiphonSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_MAGEBLOODBOLT";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MAGEBLOODBOLT_DESC";
         def.baseRechargeInterval = 1.5f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.stockToConsume = 1;
         def.keywordTokens = new string[]{"PASSIVEAGRESSION_MAGEBLOODBOLT_KEYWORD"};
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(BloodPrimaryState));
         def.stepGraceDuration = 3f;
         def.stepResetTimer = 0f;
         LoadoutAPI.AddSkillDef(def);
        
         proPrefab = PrefabAPI.InstantiateClone((GameObject)epiESC.WaitForCompletion().serializedFieldsCollection.GetOrCreateField("projectilePrefab").fieldValue.GetValue(typeof(FireFireBolt).GetField("projectilePrefab")),"MageBloodBoltProjectile");
         proPrefab.GetComponent<ProjectileDamage>().damageType = DamageType.BleedOnHit;

         muzzleFlash = PrefabAPI.InstantiateClone((GameObject)epiESC.WaitForCompletion().serializedFieldsCollection.GetOrCreateField("muzzleflashEffectPrefab").fieldValue.GetValue(typeof(FireFireBolt).GetField("muzzleflashEffectPrefab")),"MageBloodMuzzle",false);

         ContentAddition.AddEffect(muzzleFlash);
         ContentAddition.AddProjectile(proPrefab);
         LoadoutAPI.AddSkill(typeof(BloodPrimaryState));
     }

     public static float regenBlock(On.RoR2.HealthComponent.orig_Heal orig,HealthComponent self,float amount,ProcChainMask proc,bool nonRegen){
          if(!nonRegen && !((self.body.skillLocator.FindSkillByDef(def)?.IsReady()).GetValueOrDefault())){
            amount = 0;
          }
          return orig(self,amount,proc,nonRegen);
     }

     public class BloodPrimaryState : FireFireBolt {

         public override void OnEnter(){
             var firebolt = new FireFireBolt();
             baseDuration = firebolt.baseDuration;
             projectilePrefab = proPrefab;
             muzzleflashEffectPrefab = muzzleFlash;
             damageCoefficient = 1f;
             base.OnEnter();
         }

         public override InterruptPriority GetMinimumInterruptPriority(){
             return InterruptPriority.PrioritySkill;
         }
     }

     public class BloodSiphonSkillDef : SteppedSkillDef {

         public class BInstanceData : SteppedSkillDef.InstanceData {
             public float currentcooldown;
         }
         public override BaseSkillInstanceData OnAssigned(GenericSkill skillSlot){
             if(!isHooked){
                isHooked = true;
                On.RoR2.HealthComponent.Heal += regenBlock;
                RoR2.Run.onRunDestroyGlobal += unsetHook;
             }
             return new BInstanceData();
             void unsetHook(Run run){
                if(isHooked){
                  On.RoR2.HealthComponent.Heal -= regenBlock;
                  isHooked = false;
                }
                RoR2.Run.onRunDestroyGlobal -= unsetHook;
             }
         }
         public override bool CanExecute(GenericSkill skillSlot){
             return skillSlot.stateMachine && !skillSlot.stateMachine.HasPendingState() && skillSlot.stateMachine.CanInterruptState(interruptPriority);
         }
         public override bool IsReady(GenericSkill skillSlot){
             return HasRequiredStockAndDelay(skillSlot);
         }

         public override void OnExecute(GenericSkill skillSlot){
             base.OnExecute(skillSlot);
             BInstanceData instanceData = (BInstanceData)skillSlot.skillInstanceData;
             instanceData.currentcooldown += baseRechargeInterval;
             skillSlot.RecalculateFinalRechargeInterval();
             if(skillSlot.stock < 0){
               skillSlot.stock = 0;
             }
         }

         public override float GetRechargeInterval(GenericSkill skillSlot){
             return ((BInstanceData)skillSlot.skillInstanceData).currentcooldown;
         }

         public override void OnFixedUpdate(GenericSkill skillSlot){
             base.OnFixedUpdate(skillSlot);
             if(HasRequiredStockAndDelay(skillSlot)){
              BInstanceData instanceData = (BInstanceData)skillSlot.skillInstanceData;
              instanceData.currentcooldown = 0;
             }
         }
     }
    } 
}
