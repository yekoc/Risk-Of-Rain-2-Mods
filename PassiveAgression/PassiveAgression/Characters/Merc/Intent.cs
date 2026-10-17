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
using EntityStates.Merc;

namespace PassiveAgression.Merc
{
    public static class MassExpose{
     public static SkillDef def;
     public static GameObject vfxPrefab;
     public static BepInEx.Configuration.ConfigEntry<bool> coolMode;

     static MassExpose(){
         coolMode = PassiveAgression.PassiveAgressionPlugin.config.Bind("MercGlare","Cool Mode",false,"Makes the skill cooler. (No Gameplay Effect, Sillymaxxing)");
         LanguageAPI.Add("PASSIVEAGRESSION_MERCGLARE","Sharp Intent");
         LanguageAPI.Add("PASSIVEAGRESSION_MERCGLARE_DESC",$"{(coolMode.Value ? "Do a cool flip" : "Manifest your hostile intent")}, <style=cIsUtility>Exposing</style> enemies in a cone infront of you.");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_MERCGLARE";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MERCGLARE_DESC";
         def.baseRechargeInterval = 6f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Body";
         def.interruptPriority = InterruptPriority.PrioritySkill;
         def.mustKeyPress = true;
         def.activationState = new SerializableEntityStateType(typeof(GlareState));
         def.keywordTokens = new string[]{"KEYWORD_EXPOSE"};
         def.icon = Util.SpriteFromFile("MercIntentIcon.png");
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(GlareState),out _);
         
     }


     public class GlareState : BaseState {


         public override void OnEnter(){
            base.OnEnter();
            if(!vfxPrefab){
                vfxPrefab = GameObject.Instantiate(HealthComponent.AssetReferences.mercExposeConsumeEffectPrefab);
                vfxPrefab.transform.localScale *= 0.1f;
                var main = vfxPrefab.GetComponent<ParticleSystem>();
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                var s = main.sizeOverLifetime;
                s.sizeMultiplier *= 0.1f;
            }
         }
         public override void FixedUpdate(){
             base.FixedUpdate();
             if(fixedAge > 0.2f){
                 outer.SetNextStateToMain();
                 if(coolMode.Value){
                     PlayAnimation("FullBody, Override","EvisLoopExit");
                 }
                 var childLoc = modelLocator.GetModelComponent<ChildLocator>()?.FindChild("Head");
                 EffectManager.SimpleImpactEffect(vfxPrefab,(childLoc)? childLoc.position : transform.position,GetAimRay().direction,false);
                 RoR2.Util.PlaySound(EntityStates.Merc.Assaulter.endSoundString,gameObject); 
             }
         }
         public override void OnExit(){
             base.OnExit();
             var damage = new DamageInfo{
                 attacker = characterBody.gameObject,
                 damage = 0f,
                 damageType = new DamageTypeCombo(DamageType.ApplyMercExpose | DamageType.BypassArmor | DamageType.BypassBlock | DamageType.NonLethal,DamageTypeExtended.Generic,DamageSource.Secondary),
                 procCoefficient = 0f
             };
             var search = new BullseyeSearch{
                 filterByDistinctEntity = true,
                 viewer = characterBody,
                 searchOrigin = characterBody.transform.position,
                 searchDirection = GetAimRay().direction,
                 teamMaskFilter = TeamMask.GetUnprotectedTeams(teamComponent.teamIndex),
                 maxAngleFilter = 45f,
                 maxDistanceFilter = 40f,
                 
             };
             search.RefreshCandidates();
             var targets = search.GetResults();
             foreach(var t in targets){
                 damage.position = t.transform.position;
                 t.healthComponent.TakeDamage(damage);
                 GlobalEventManager.instance.OnHitAll(damage,t.healthComponent.gameObject);
                 GlobalEventManager.instance.OnHitEnemy(damage,t.healthComponent.gameObject);
                 damage.rejected = false;
             }
         }
         public override InterruptPriority GetMinimumInterruptPriority(){
             return InterruptPriority.Skill;
         }
     }

    } 
}
