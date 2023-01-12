using RoR2;
using RoR2.Skills;
using EntityStates;
using RoR2.CharacterAI;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using EntityStates.Bandit2.Weapon;
using static R2API.DamageAPI;

namespace PassiveAgression.Bandit
{
    public static class ChainSpecial{
     public static SkillDef def;
     public static SingleUseSkillDef fireDef;
     public static ModdedDamageType sixShoot = DamageAPI.ReserveDamageType();

     static ChainSpecial(){
         LanguageAPI.Add("PASSIVEAGRESSION_BANDITREFRESH","Six Soulshooter");
         LanguageAPI.Add("PASSIVEAGRESSION_BANDITREFRESH_DESC","<style=cIsDamage>Slayer</style>,replace your primary with 6 revolver shots for <style=cIsDamage>400% damage</style>. Kills <style=cIsUtility>grant more shots</style>.");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_BANDITREFRESH";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_BANDITREFRESH_DESC";
         def.baseRechargeInterval = 15f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
         def.keywordTokens = new string[]{"KEYWORD_SLAYER"};
         def.activationState = new SerializableEntityStateType(typeof(PullUpState));
         def.icon = Util.SpriteFromFile("ShowdownIcon.png");
         fireDef = ScriptableObject.CreateInstance<SingleUseSkillDef>();
         fireDef.skillNameToken = def.skillNameToken;
         (fireDef as ScriptableObject).name = def.skillNameToken + "_PRIMARY";
         fireDef.activationStateMachineName = "Weapon";
         fireDef.activationState = new SerializableEntityStateType(typeof(HopOutState));
         fireDef.rechargeStock = 0;
         fireDef.baseMaxStock = 6;
         fireDef.onUnassign += (GenericSkill skillSlot) => {
            skillSlot.stateMachine.SetNextState(new ExitSidearmRevolver());
            //skillSlot.characterBody.skillLocator.special.UnsetSkillOverride();
         };
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddSkillDef(fireDef);
         ContentAddition.AddEntityState(typeof(PullUpState),out _);
         ContentAddition.AddEntityState(typeof(HopOutState),out _);

         var conf = GameObject.Instantiate(UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<EntityStateConfiguration>("RoR2/Base/Bandit2/EntityStates.Bandit2.Weapon.PrepSidearmResetRevolver.asset").WaitForCompletion());
         conf.targetType = (HG.SerializableSystemType)typeof(PullUpState);
         conf.SetNameFromTargetType();
         ContentAddition.AddEntityStateConfiguration(conf);
         conf = GameObject.Instantiate(UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<EntityStateConfiguration>("RoR2/Base/Bandit2/EntityStates.Bandit2.Weapon.FireSidearmResetRevolver.asset").WaitForCompletion());
         conf.targetType = (HG.SerializableSystemType)typeof(HopOutState);
         conf.SetNameFromTargetType();
         ContentAddition.AddEntityStateConfiguration(conf);
        

         On.RoR2.GlobalEventManager.OnCharacterDeath += (orig,self,damageReport) => {
             orig(self,damageReport);
             if(damageReport.attackerBody && DamageAPI.HasModdedDamageType(damageReport.damageInfo,sixShoot)){ 
                EffectManager.SpawnEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/Bandit2ResetEffect"), new EffectData
                {
                        origin = damageReport.damageInfo.position
                }, transmit: true);
                damageReport.attackerBody.skillLocator.primary.AddOneStock();
             }
         };
     }

     public class HopOutState : BaseFireSidearmRevolverState {
         public override void OnEnter(){
             damageCoefficient = 4f;
             base.OnEnter();
         }
         public override void FixedUpdate(){
            ((BaseSidearmState)this).FixedUpdate();
	    if (base.fixedAge >= duration && base.isAuthority){
               outer.SetNextStateToMain();
            }
         }
         public override void ModifyBullet(BulletAttack bulletAttack){
             base.ModifyBullet(bulletAttack);
             DamageAPI.AddModdedDamageType(bulletAttack,sixShoot);
         }
     }

     public class PullUpState : BasePrepSidearmRevolverState {

         public override void OnEnter(){
             var primary = characterBody.skillLocator.primary;
             if(primary){
              primary.SetSkillOverride(primary.gameObject,fireDef,GenericSkill.SkillOverridePriority.Contextual);
             }
             base.OnEnter();
         }
         public override EntityState GetNextState(){
            return EntityStateCatalog.InstantiateState(outer.mainStateType);    
         }
         public override InterruptPriority GetMinimumInterruptPriority(){
             return InterruptPriority.PrioritySkill;
         }
     }

    } 
}
