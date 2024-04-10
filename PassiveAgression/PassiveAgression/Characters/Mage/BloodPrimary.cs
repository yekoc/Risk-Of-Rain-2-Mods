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
         def.keywordTokens = new string[]{"PASSIVEAGRESSION_MAGEBLOODBOLT_KEYWORD",};
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(BloodPrimaryState));
         def.stepGraceDuration = 3f;
         def.stepResetTimer = 0f;
         def.icon = Util.SpriteFromFile("ExciseIcon.png");
         ContentAddition.AddSkillDef(def);
        
         proPrefab = PrefabAPI.InstantiateClone((GameObject)epiESC.WaitForCompletion().serializedFieldsCollection.GetOrCreateField("projectilePrefab").fieldValue.GetValue(typeof(FireFireBolt).GetField("projectilePrefab")),"MageBloodBoltProjectile");
         proPrefab.GetComponent<ProjectileDamage>().damageType = DamageType.BleedOnHit;
         var ghost = PrefabAPI.InstantiateClone(UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/ImpBoss/ImpVoidspikeProjectileGhost.prefab").WaitForCompletion(),"MageBloodBoltProjectileGhost",false);
         //ghost.GetComponentInChildren<Light>().color = Color.red;
         ghost.GetComponentInChildren<MeshRenderer>().material = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Material>("RoR2/Base/moon2/matBloodSiphon.mat").WaitForCompletion();
         ghost.GetComponentInChildren<TrailRenderer>().material = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Material>("RoR2/Base/moon2/matBloodSiphon.mat").WaitForCompletion();
         proPrefab.GetComponent<ProjectileController>().ghostPrefab = ghost;
         var exp = proPrefab.GetComponent<ProjectileImpactExplosion>();
         exp.explosionEffect = null;
         exp.impactEffect = null;
         /*
         muzzleFlash = PrefabAPI.InstantiateClone(UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Common/VFX/BleedEffect.prefab").WaitForCompletion(),"MuzzleFlashMageBlood",false);
         muzzleFlash.AddComponent<EffectComponent>();
         var vfx = muzzleFlash.AddComponent<VFXAttributes>();
         vfx.vfxPriority = VFXAttributes.VFXPriority.Medium;
         vfx.vfxIntensity = VFXAttributes.VFXIntensity.Medium;
         muzzleFlash.AddComponent<DestroyOnParticleEnd>();
         muzzleFlash.AddComponent<DestroyOnTimer>().duration = 0.2f;
         GameObject.Destroy(muzzleFlash.transform.GetChild(0).gameObject);
         */
         muzzleFlash = PrefabAPI.InstantiateClone(UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Mage/MuzzleflashMageFire.prefab").WaitForCompletion(),"MuzzleFlashMageBlood",false);
         foreach(var rend in muzzleFlash.GetComponentsInChildren<Renderer>()){rend.material = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Material>("RoR2/Base/moon2/matBloodSiphon.mat").WaitForCompletion();}

         ContentAddition.AddEffect(muzzleFlash);
         ContentAddition.AddProjectile(proPrefab);
         ContentAddition.AddEntityState(typeof(BloodPrimaryState),out _);
     }

     public static float regenBlock(On.RoR2.HealthComponent.orig_Heal orig,HealthComponent self,float amount,ProcChainMask proc,bool nonRegen){
          if(!nonRegen && self.body.HasBuff(BloodSiphonSkillDef.bdef)){
              amount = 0f;
          }
          return orig(self,amount,proc,nonRegen);
     }

     public class BloodPrimaryState : FireFireBolt {

         public override void OnEnter(){
             var firebolt = new FireFireBolt();
             baseDuration = firebolt.baseDuration * 1.5f;
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

         public static BuffDef bdef; 

         static BloodSiphonSkillDef(){
            bdef = ScriptableObject.CreateInstance<BuffDef>();
            bdef.buffColor = Color.red;
            bdef.canStack = false;
            bdef.isCooldown = true;
            bdef.iconSprite = Util.SpriteFromFile("SiphonedBuff.png");
            (bdef as ScriptableObject).name = "PASSIVEAGRESSION_MAGEBLOODBOLT_DEBUFF";
            ContentAddition.AddBuffDef(bdef);
         }
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
             instanceData.currentcooldown = Mathf.Min(99f,instanceData.currentcooldown + baseRechargeInterval);
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
              if(NetworkServer.active && skillSlot.characterBody.HasBuff(bdef)){
                skillSlot.characterBody.RemoveBuff(bdef);
              }
             }
             else{
               if(NetworkServer.active && !skillSlot.characterBody.HasBuff(bdef)){
                skillSlot.characterBody.AddBuff(bdef);
               }
             }
         }
     }
    } 
}
