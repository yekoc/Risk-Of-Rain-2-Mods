using RoR2;
using RoR2.Skills;
using RoR2.Projectile;
using EntityStates;
using RoR2.CharacterAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;

namespace PassiveAgression
{
    public static class Coin{
     public static AssignableSkillDef def;
     public static CoinProjectileComponent[] activeCoins; 
     //public static Dictionary<BodyIndex,string> anims; 
     public static bool isHooked;

     static Coin(){
         LanguageAPI.Add("PASSIVEAGRESSION_MARKSMANCOIN","Coin Toss");
         LanguageAPI.Add("PASSIVEAGRESSION_MARKSMANCOIN_DESC","Toss a coin.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_MARKSMANCOIN";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MARKSMANCOIN_DESC";
         def.baseRechargeInterval = 6f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
         def.mustKeyPress = true;
         def.activationState = new SerializableEntityStateType(typeof(CoinState));
         def.onAssign = (GenericSkill slot) => {
             if(!isHooked){
                isHooked = true;
                Run.onRunDestroyGlobal += unsub;
             }

             return null;
             void unsub(Run run){
                Run.onRunDestroyGlobal -= unsub;
                isHooked = false;
             }
         };
         def.onUnassign += (skill) =>{

         };
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(CoinState),out _);
         //SurvivorCatalog..CallWhenAvailable(() => anims = new string[SurvivorCatalog.survivorCount]);
         
         var coinPref = PrefabAPI.InstantiateClone(UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Commando/CommandoGrenadeProjectile.prefab").WaitForCompletion(),"CoinProjectile");
         coinPref.GetComponent<ProjectileController>().ghostPrefab = PrefabAPI.InstantiateClone(coinPref.GetComponent<ProjectileController>().ghostPrefab,"CoinProjectileGhost",false);
         Component.Destroy(coinPref.GetComponent<ProjectileImpactExplosion>());
         Component.Destroy(coinPref.GetComponent<ProjectileSimple>());
         //var subobj = coinPref.transform.GetChild(1).gameObject;
         //subobj.AddComponent<SphereCollider>().radius = 0.3f;
         var hurtbox = coinPref.AddComponent<HurtBox>();
         //subobj.layer = LayerIndex.entityPrecise.intVal;
         var hboxGroup = hurtbox.gameObject.AddComponent<HurtBoxGroup>();
         hboxGroup.mainHurtBox = hurtbox;
         hboxGroup.hurtBoxes = new HurtBox[]{hurtbox};
         hboxGroup.bullseyeCount = 1;
         hurtbox.hurtBoxGroup = hboxGroup;
         hurtbox.isBullseye = true;
         hurtbox.isSniperTarget = true;
         hurtbox.indexInGroup = 0;
         hurtbox.damageModifier = HurtBox.DamageModifier.Normal;
         coinPref.layer = LayerIndex.projectile.intVal;
         coinPref.AddComponent<CoinProjectileComponent>();
         coinPref.GetComponent<ProjectileController>().startSound = "";
         coinPref.GetComponent<Rigidbody>().isKinematic = false;
         ContentAddition.AddProjectile(coinPref);
         CoinState.coinPref = coinPref;

         On.RoR2.BulletAttack.DefaultHitCallbackImplementation += (On.RoR2.BulletAttack.orig_DefaultHitCallbackImplementation orig,BulletAttack bulletAttack,ref BulletAttack.BulletHit hitInfo) =>{
            if(hitInfo.entityObject && hitInfo.entityObject.GetComponent<CoinProjectileComponent>()){
              hitInfo.entityObject.GetComponent<CoinProjectileComponent>().WasHit(bulletAttack.owner.GetComponent<CharacterBody>().isPlayerControlled,bulletAttack);
            }
            return orig(bulletAttack,ref hitInfo);
         };
         
     }

     public static void SetMarksman(GameObject bodyPrefab,SkillFamily family,UnlockableDef unlock = null){
         if(PassiveAgressionPlugin.unfinishedContent.Value)
         HG.ArrayUtils.ArrayAppend(ref family.variants,new SkillFamily.Variant{
            skillDef = def,
            viewableNode = new ViewablesCatalog.Node(def.skillNameToken,false,null),
            unlockableDef = unlock
         });
     }
     public static void SetMarksman(GameObject bodyPrefab,SkillSlot slot,UnlockableDef unlock = null){
         SetMarksman(bodyPrefab,bodyPrefab.GetComponent<SkillLocator>().GetSkill(slot).skillFamily,unlock);
     }

     public class CoinState : GenericProjectileBaseState  {
         internal static GameObject coinPref;
         public override void OnEnter(){
             projectilePrefab = coinPref;
             baseDuration = 0.1f;
             base.OnEnter();
         }
       /*  public override void PlayAnimation(float duration){
             var anim = String.Empty; //anims[SurvivorCatalog.bodyIndexToSurvivorIndex[characterBody.bodyIndex]];
             var animLayer = "Base";
             if(anim != null && anim != String.Empty){
                PlayAnimation(animLayer,anim,anim + ".playbackRate",duration);
             }
         }*/
         public override InterruptPriority GetMinimumInterruptPriority(){
             return InterruptPriority.PrioritySkill;
         }
     }

     public class CoinProjectileComponent : MonoBehaviour,RoR2.Projectile.IProjectileImpactBehavior {
         public static GameObject coinTracerPrefab = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Toolbot/TracerToolbotRebar.prefab").WaitForCompletion();
         public static List<CoinProjectileComponent> instances = new List<CoinProjectileComponent>();
         bool redState = false;
         bool glintState = false;
         BulletAttack redAttack;

         public void Awake(){
             var rb =  GetComponent<Rigidbody>();
             rb.isKinematic = false;

         }
         public void Start(){
           var owner = GetComponent<ProjectileController>().owner;
           var ownerBody = owner?.GetComponent<CharacterBody>();
           if(NetworkServer.active){
             var rb =  GetComponent<Rigidbody>();
             rb.isKinematic = false;
             rb.AddForce(ownerBody.inputBank.aimDirection * 25f + Vector3.up * 20f + ownerBody.characterMotor.velocity,ForceMode.VelocityChange);
           }
           instances.Add(this);
         }
         public void Update(){
            if(redState){
              //vfx enemy coin
            }
            if(glintState){
              //vfx power coin
            }
         }

         public void OnDestroy(){
           instances.Remove(this);
         }

         public void OnProjectileImpact(ProjectileImpactInfo info){
             if(info.collider && !info.collider.GetComponent<HurtBox>()){
                Destroy(this.gameObject);
             }
         }
         public void WasHit(bool playerHit,BulletAttack attack){
           for(int i = instances.Count -1 ; i >= 0; i--){
             var other = instances[i];
             if(other == this) continue;
             var dir = transform.position - other.transform.position;
             if(!Physics.Raycast(transform.position,dir,out var hitInfo,dir.magnitude,LayerIndex.world.mask,0)){
               attack.origin = transform.position;
               attack.aimVector = dir;
               attack.damage *= 2f;
               attack.bulletCount = 1;
               attack.stopperMask = LayerIndex.world.mask;
               attack.tracerEffectPrefab ??= coinTracerPrefab;
               attack.weapon = this.gameObject;
               attack.Fire();
               Destroy(this.gameObject);
               return;
             }
           }
           if((redAttack == attack) || playerHit){

            Destroy(this.gameObject);
           }
           else{
             redState = true;
             redAttack = attack;
           }
         }
     }
    }
}
