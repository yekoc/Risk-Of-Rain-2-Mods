using RoR2;
using RoR2.Skills;
using RoR2.Projectile;
using EntityStates;
using RoR2.CharacterAI;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.AddressableAssets;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using EntityStates.Mage.Weapon;

namespace PassiveAgression.Mage
{
    public static class FloatingChaos{
     public static SkillDef def;
     public static GameObject projPrefab;
     public static SkillDef projdef;

     static FloatingChaos(){
         
         LanguageAPI.Add("PASSIVEAGRESSION_MAGEFLAMETURRET","Lifespark");
         LanguageAPI.Add("PASSIVEAGRESSION_MAGEFLAMETURRET_DESC","Charge up a living flame that automatically shoots at enemies, <style=cIsDamage>igniting</style> them.");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_MAGEFLAMETURRET";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MAGEFLAMETURRET_DESC";
         def.baseRechargeInterval = 6f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(PrepFloatingChaosState));
         def.icon = Util.SpriteFromFile((UnityEngine.Random.value > 0.9)? "LifesparkFunny.png" : "Lifespark.png");


         projPrefab = PrefabAPI.InstantiateClone(Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Vagrant/VagrantTrackingBomb.prefab").WaitForCompletion(),"FloatingFlameyBoy");
         GameObject.Destroy(projPrefab.GetComponent<ProjectileImpactExplosion>());
         GameObject.Destroy(projPrefab.GetComponent<ProjectileSimple>());
         var esm = projPrefab.AddComponent<EntityStateMachine>();
         var nsm = projPrefab.AddComponent<NetworkStateMachine>();
         HG.ArrayUtils.ArrayAppend(ref nsm.stateMachines,esm);
         esm.initialStateType = new SerializableEntityStateType(typeof(ChaosFloatState));
         esm.mainStateType = esm.initialStateType;
         

         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(PrepFloatingChaosState),out _);
         ContentAddition.AddEntityState(typeof(FloatingChaosState),out _);
         ContentAddition.AddEntityState(typeof(ChaosFloatState),out _);
         }


     public class PrepFloatingChaosState : BaseChargeBombState {

         public override void OnEnter(){
             base.OnEnter();
         }
         public override void FixedUpdate(){
             base.FixedUpdate();
             if(base.fixedAge >= duration){
                 outer.SetNextStateToMain();
             }
         }
         public override void OnExit(){
             base.OnExit();
         }
         public override InterruptPriority GetMinimumInterruptPriority(){
             return InterruptPriority.PrioritySkill;
         }
         public override BaseThrowBombState GetNextState(){
             return new FloatingChaosState();
         }
     }
     public class FloatingChaosState : BaseThrowBombState{

         public override void OnEnter(){
            muzzleflashEffectPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Mage/MuzzleflashMageFire.prefab").WaitForCompletion();
            projectilePrefab = projPrefab;
            force = 0f;
            selfForce = 0f;
            base.OnEnter();
         }

     }

     public class ChaosFloatState : BaseState{
         BullseyeSearch search;
         GameObject owner;
         CharacterBody ownerBody;
         int delay = 0;
         static GameObject projectilePrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Mage/MageFireboltBasic.prefab").WaitForCompletion();

         public override void OnEnter(){
             base.OnEnter();
             characterBody.AddBuff(RoR2Content.Buffs.AffixRed);
             search = new BullseyeSearch{
                 sortMode = BullseyeSearch.SortMode.Distance,
                 viewer = characterBody,
                 searchOrigin = characterBody.transform.position,
                 filterByDistinctEntity = true,
                 filterByLoS = true,
                 teamMaskFilter = TeamMask.GetEnemyTeams(teamComponent.teamIndex)
             };
             owner = GetComponent<ProjectileController>().owner;
             ownerBody = owner?.GetComponent<CharacterBody>();
         }

         public override void FixedUpdate(){
            base.FixedUpdate();
            if(!ownerBody){
              outer.SetNextState(new Idle());
            }
            if(isAuthority && rigidbody){
             var velocity = rigidbody.velocity;
             if((int)fixedAge % 10 <= 5){
               velocity.y = 0.25f;
             }
             else{
               velocity.y = (-0.25f);
             }
             rigidbody.velocity = velocity;
            }
            if(NetworkServer.active && delay <= 0){
              //search.searchOrigin = characterBody.transform.position;
              //search.RefreshCandidates();
              var result = GetComponent<ProjectileTargetComponent>().target;
              if(result){
               ProjectileManager.instance.FireProjectile(projectilePrefab,transform.position,transform.rotation,owner,ownerBody.damage * 4f, 0f,RoR2.Util.CheckRoll(ownerBody.crit,ownerBody.master));
              }
              delay = 20;
            }
            delay--;
            
         }

         public override void OnExit(){
            base.OnExit();
            healthComponent.Suicide();
         }

     }

    } 
}
