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
using RoR2.Orbs;

namespace PassiveAgression.Croc
{
    public static class PathogenSpecial{
     public static AssignableSkillDef def;
     public static AssignableSkillDef scepterDef;
     public static GameObject proPrefab;
     private static bool isHooked;

     static PathogenSpecial(){
         var epiESC = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<EntityStateConfiguration>("RoR2/Base/Croco/EntityStates.Croco.FireDiseaseProjectile.asset");
         
         LanguageAPI.Add("PASSIVEAGRESSION_CROCSPREAD","Carrier Pathogen");
         LanguageAPI.Add("PASSIVEAGRESSION_CROCSPREAD_DESC","Release a disease that deals <style=cIsDamage> 85% damage </style>. The disease spreads to up to <style=cIsDamage>20</style> targets. <style=cIsUtility>carrying a random debuff with it</style>.");
         
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_CROCSPREAD";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_CROCSPREAD_DESC";
         def.baseRechargeInterval = 10f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Mouth";
         def.activationState = new SerializableEntityStateType(typeof(PathogenState));
         def.onAssign += (skill) => {
             if(!isHooked){
                isHooked = true;
                IL.RoR2.Projectile.ProjectileProximityBeamController.UpdateServer += (il) =>{
                   ILCursor c = new ILCursor(il);
                   if(c.TryGotoNext(MoveType.After,x => x.MatchNewobj(typeof(LightningOrb).GetConstructor(new Type[]{})))){
                      c.Emit(OpCodes.Ldarg_0);
                      c.EmitDelegate<Func<LightningOrb,ProjectileProximityBeamController,LightningOrb>>((orb,self) =>{
                          var ownerbody = self.projectileController.owner.GetComponent<CharacterBody>();
                          bool flag = (scepterDef != null) && scepterDef.IsAssigned(ownerbody,SkillSlot.Special);
                          if(self.lightningType == LightningOrb.LightningType.CrocoDisease && (flag || def.IsAssigned(self.projectileController.owner.GetComponent<CharacterBody>(),SkillSlot.Special))){
                            var cdt = ownerbody.gameObject.GetComponent<CrocoDamageTypeController>();
                            if(cdt){
                              self.projectileDamage.damageType &= ~cdt.GetDamageType();
                            }
                            var pOrb = new PathogenOrb();
                            Util.GetRandomDebuffOrDot(ownerbody,out pOrb.debuff,out pOrb.dot);
                            if(flag){
                              pOrb.isScepter = true;
                            }
                            return pOrb;
                          }
                            return orb;
                      });
                   }
                };
             }
             return null;
         };
         def.icon = Util.SpriteFromFile("Pathogen.png");

         LoadoutAPI.AddSkillDef(def);
        
         proPrefab = PrefabAPI.InstantiateClone((GameObject)epiESC.WaitForCompletion().serializedFieldsCollection.GetOrCreateField("projectilePrefab").fieldValue.GetValue(typeof(FireDiseaseProjectile).GetField("projectilePrefab")),"CrocoCarrierProjectile");
         proPrefab.GetComponent<ProjectileDamage>().damageType = DamageType.Generic;
         proPrefab.GetComponent<ProjectileProximityBeamController>().damageCoefficient = 0.85f;
         ContentAddition.AddProjectile(proPrefab);
         LoadoutAPI.AddSkill(typeof(PathogenState));
     }

     public class PathogenOrb : RoR2.Orbs.LightningOrb {

         public BuffIndex debuff = BuffIndex.None;
         public DotController.DotStack dot = null;
         public bool isScepter = false;

         public override void OnArrival(){
             
             targetsToFindPerBounce = 0;
             base.OnArrival();
             CharacterBody body = target.healthComponent.body;
             DotController dootc = null;
             DotController.dotControllerLocator.TryGetValue(body.gameObject.GetInstanceID(),out dootc);
             if(debuff != BuffIndex.None){
                body.AddTimedBuff(debuff,5f);
             }
             else if(dot != null){
               DotController.InflictDot(body.gameObject,attacker,dot.dotIndex,5f);
             }
             if(bouncesRemaining > 0){
             int targets = 2;
             if(isScepter){
                 targets = body.activeBuffsListCount;
                 if(dootc)
                    targets += dootc.dotStackList?.Count??0;
             }
             for(int i = 0; i < targets ; i++){
                if(!isScepter){
                 Util.GetRandomDebuffOrDot(body,out debuff,out dot);
                }
                else if(i < body.activeBuffsListCount){
                 debuff = body.activeBuffsList[i];
                 dot = null;
                }
                else{
                 debuff = BuffIndex.None;
                 dot = dootc.dotStackList[i];
                }
                if (bouncedObjects != null)
                {
                        if (canBounceOnSameTarget)
                        {
                                bouncedObjects.Clear();
                        }
                        bouncedObjects.Add(target.healthComponent);
                }
                HurtBox hurtBox = PickNextTarget(target.transform.position);
                if ((bool)hurtBox)
                {
                        PathogenOrb lightningOrb = new PathogenOrb();
                        lightningOrb.search = search;
                        lightningOrb.origin = target.transform.position;
                        lightningOrb.target = hurtBox;
                        lightningOrb.attacker = attacker;
                        lightningOrb.inflictor = inflictor;
                        lightningOrb.teamIndex = teamIndex;
                        lightningOrb.damageValue = damageValue * damageCoefficientPerBounce;
                        lightningOrb.bouncesRemaining = bouncesRemaining - 1;
                        lightningOrb.isCrit = isCrit;
                        lightningOrb.bouncedObjects = bouncedObjects;
                        lightningOrb.lightningType = lightningType;
                        lightningOrb.procChainMask = procChainMask;
                        lightningOrb.procCoefficient = procCoefficient;
                        lightningOrb.damageColorIndex = damageColorIndex;
                        lightningOrb.damageCoefficientPerBounce = damageCoefficientPerBounce;
                        lightningOrb.speed = speed;
                        lightningOrb.range = range;
                        lightningOrb.damageType = damageType;
                        lightningOrb.failedToKill = failedToKill;
                        lightningOrb.dot = dot;
                        lightningOrb.debuff = debuff;
                        OrbManager.instance.AddOrb(lightningOrb);
                }
             }
             }
         }
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
