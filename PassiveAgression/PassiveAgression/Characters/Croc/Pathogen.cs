using RoR2;
using RoR2.Skills;
using RoR2.Projectile;
using EntityStates;
using EntityStates.Croco;
using UnityEngine;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;

namespace PassiveAgression.Croc
{
    public static class PathogenSpecial{
     public static SkillDef def;
     public static GameObject proPrefab;
     public static DamageAPI.ModdedDamageType carryDType = DamageAPI.ReserveDamageType();

     static PathogenSpecial(){
         var epiESC = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<EntityStateConfiguration>("RoR2/Base/Croco/EntityStates.Croco.FireDiseaseProjectile.asset");
         
         LanguageAPI.Add("PASSIVEAGRESSION_CROCSPREAD","Carrier Pathogen");
         LanguageAPI.Add("PASSIVEAGRESSION_CROCSPREAD_DESC","Release a deadly disease that deals <style=cIsDamage> 85% damage </style>. The disease spreads to up to <style=cIsDamage>20</style> targets. <style=cIsUtility>carrying a random debuff with it</style>.");
         
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_CROCSPREAD";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_CROCSPREAD_DESC";
         def.baseRechargeInterval = 10f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Mouth";
         def.activationState = new SerializableEntityStateType(typeof(PathogenState));
         LoadoutAPI.AddSkillDef(def);
        
         proPrefab = PrefabAPI.InstantiateClone((GameObject)epiESC.WaitForCompletion().serializedFieldsCollection.GetOrCreateField("projectilePrefab").fieldValue.GetValue(typeof(FireDiseaseProjectile).GetField("projectilePrefab")),"CrocoCarrierProjectile");
         proPrefab.GetComponent<ProjectileDamage>().damageType = DamageType.Generic;
         proPrefab.AddComponent<DamageAPI.ModdedDamageTypeHolderComponent>().Add(carryDType);
         proPrefab.GetComponent<ProjectileProximityBeamController>().damageCoefficient = 0.85f;
         On.RoR2.Orbs.LightningOrb.OnArrival += (orig,self) =>{
             if(DamageAPI.HasModdedDamageType(self,carryDType) && self.target && self.target.healthComponent){
                self.damageType &= ~(DamageType.PoisonOnHit | DamageType.BlightOnHit);
                BuffIndex debuff;
                DotController.DotStack dot;
                var body = self.target.healthComponent.body; 
                if(body && self.bouncedObjects.Count > 0 && Util.GetRandomDebuffOrDot(self.bouncedObjects.Last((hc) => hc).body,out debuff,out dot)){
                    if(debuff != BuffIndex.None){
                       body.AddTimedBuff(debuff,5f);
                    }
                    else{
                      DotController.InflictDot(body.gameObject,self.attacker,dot.dotIndex,5f);
                    }
                }
             }
             orig(self);
         };
         ContentAddition.AddProjectile(proPrefab);
         LoadoutAPI.AddSkill(typeof(PathogenState));
     }


     public class PathogenState : FireDiseaseProjectile {

         public override void OnEnter(){
             projectilePrefab = proPrefab;
             damageCoefficient = 0.85f;
             base.OnEnter();
         }
     }

    } 
}
