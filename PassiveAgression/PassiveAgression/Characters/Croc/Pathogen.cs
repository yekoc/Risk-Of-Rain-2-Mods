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
         LanguageAPI.Add("PASSIVEAGRESSION_CROCSPREAD_DESC","Release a pathogen that deals <style=cIsDamage> 85% damage </style> and <style=cIsUtility>spawns a disease per every debuff on the target</style>. The disease spreads to up to <style=cIsDamage>20</style> targets, carrying carrying a random debuff from its last host.");
         
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
                              self.projectileDamage.damageType.damageType &= ~cdt.GetDamageType().damageType;
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

         ContentAddition.AddSkillDef(def);
        
         proPrefab = PrefabAPI.InstantiateClone((GameObject)epiESC.WaitForCompletion().serializedFieldsCollection.GetOrCreateField("projectilePrefab").fieldValue.GetValue(typeof(FireDiseaseProjectile).GetField("projectilePrefab")),"CrocoCarrierProjectile");
         /*var procont = proPrefab.GetComponent<ProjectileController>();
         procont.ghostPrefab = PrefabAPI.InstantiateClone(procont.ghostPrefab,"CrocoCarrierProjectileGhost");
         var ghostPart = procont.ghostPrefab.transform.GetChild(0).GetComponent<ParticleSystemRenderer>();
         var ghostMat = GameObject.Instantiate<Material>(ghostPart.materials[0]);
         ghostMat.name += "spread";
         ghostMat.SetColor("_TintColor", new Color(3.5f, 1.9f, 9.3f, 1f));*/
         proPrefab.GetComponent<ProjectileDamage>().damageType.damageType = DamageType.Generic;
         proPrefab.GetComponent<ProjectileProximityBeamController>().damageCoefficient = 0.85f;
         ContentAddition.AddProjectile(proPrefab);
         ContentAddition.AddEntityState(typeof(PathogenState),out _);
     }

     public class PathogenOrb : RoR2.Orbs.LightningOrb {

         public BuffIndex debuff = BuffIndex.None;
         public DotController.DotStack dot = null;
         public bool isScepter = false;

         public override void OnArrival(){
             
             targetsToFindPerBounce = 0;
             base.OnArrival();
             CharacterBody body = target?.healthComponent?.body;
             if(!body){
                return;
             }
             DotController dootc = null;
             DotController.dotControllerLocator.TryGetValue(body.gameObject.GetInstanceID(),out dootc);
             if(debuff != BuffIndex.None){
                body.AddTimedBuff(debuff,5f);
             }
             else if(dot != null){
               DotController.InflictDot(body.gameObject,attacker,dot.dotIndex,5f);
             }
             var buffList = body.activeBuffsList.Select(b => BuffCatalog.GetBuffDef(b)).Where( b => b.isDebuff).Except(DotController.dotDefs.Select(d => d.associatedBuff)).ToList();
             if(bouncesRemaining > 0){
             int targets = 1;
             if(isScepter || !bouncedObjects.Any()){
                 targets = buffList.Count;
                 if(dootc){
                        targets += dootc.dotStackList?.Count??0;
                 }
             }
             for(int i = 0; i < targets ; i++){
               if(targets == 1){
                 Util.GetRandomDebuffOrDot(body,out debuff,out dot);
               }
               else{
               if(i < buffList.Count){
                 debuff = buffList[i].buffIndex;
                 dot = null;
               }
               else if(dootc){
                 debuff = BuffIndex.None;
                 dot = dootc.dotStackList[Math.Max(i - buffList.Count,0)];
                }
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
                        lightningOrb.isScepter = isScepter;
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
