using System;
using RoR2;
using RoR2.Skills;
using EntityStates;
using System.Linq;
using EntityStates.Loader;
using RoR2.Projectile;
using UnityEngine;
using R2API;
using static RoR2.Projectile.ProjectileGrappleController;

namespace PassiveAgression.Loader{
  public static class LoaderZipline{
     public static SkillDef def;
     public static GameObject projectileprefab;

     static LoaderZipline(){
         var proj = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Loader/LoaderHook.prefab");  
         LanguageAPI.Add("PASSIVEAGRESSION_LOADERZIP","Detachable Abseil");
         LanguageAPI.Add("PASSIVEAGRESSION_LOADERZIP_DESC","Fire your gauntlet forward,let go to launch the rope,<style=cIsUtility>attaching</style> the two ends.");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_LOADERZIP";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_LOADERZIP_DESC";
         def.baseRechargeInterval = 5f;
         def.canceledFromSprinting = false;
         def.stockToConsume = 0;
         def.cancelSprintingOnActivation = true;
         def.beginSkillCooldownOnSkillEnd = true;
         def.activationStateMachineName = "Hook";
         def.activationState = new SerializableEntityStateType(typeof(ZipHookState)); 
         projectileprefab = proj.WaitForCompletion();
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(ZipHookState),out _);
         ContentAddition.AddEntityState(typeof(ZipperHookState),out _);
     }

	class ZipHookState : FireHook{

            public override void OnEnter(){
                projectilePrefab = projectileprefab;
                base.OnEnter();
            }

            public override void FixedUpdate(){
              base.FixedUpdate();
              if(!IsKeyDownAuthority()){
                outer.SetNextStateToMain();
              }
            }

            public override void OnExit(){
                base.OnExit();
                var aimRay = GetAimRay();
                if(isAuthority){
                  FireProjectileInfo fireProjectileInfo = default(FireProjectileInfo);
                  fireProjectileInfo.position = aimRay.origin;
                  fireProjectileInfo.rotation = Quaternion.LookRotation(aimRay.direction);
                  fireProjectileInfo.crit = base.characterBody.RollCrit();
                  fireProjectileInfo.damage = damageStat * damageCoefficient;
                  fireProjectileInfo.force = 0f;
                  fireProjectileInfo.damageColorIndex = DamageColorIndex.Default;
                  fireProjectileInfo.procChainMask = default(ProcChainMask);
                  fireProjectileInfo.projectilePrefab = projectilePrefab;
                  fireProjectileInfo.owner = base.gameObject;
                  FireProjectileInfo fireProjectileInfo2 = fireProjectileInfo;
                  ProjectileManager.instance.FireProjectile(fireProjectileInfo2);
                }
                EffectManager.SimpleMuzzleFlash(muzzleflashEffectPrefab, base.gameObject, "MuzzleLeft", transmit: false);
                RoR2.Util.PlaySound(fireSoundString, base.gameObject);
            }

	}
        class ZipperHookState : BaseGripState{
            GameObject otherCatch;
            //CharacterBody stuckBody;

            ZipperHookState(GameObject other){
                if(other){
                  otherCatch = other;
                }    
            }

        }
  }
}
