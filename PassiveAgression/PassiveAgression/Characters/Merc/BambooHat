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
using static R2API.DamageAPI;

namespace PassiveAgression.Merc
{
    public static class YieldToClaim{
     public static SkillDef def;
     public static ModdedDamageType damageType;
     public static BuffDef bdef;

     static YieldToClaim(){
         LanguageAPI.Add("PASSIVEAGRESSION_MERCYIELD","Yield My Flesh");
         LanguageAPI.Add("PASSIVEAGRESSION_MERCYIELD_DESC","<style=cIsDamage>Slayer.</style> Unleash a finishing blow for <style=cIsDamage>350% damage</syle>.Hold to release your swords current aspect.Kills <style=cIsUtility>absorb the targets aspect into your blade</style>.");
         LanguageAPI.Add("PASSIVEAGRESSION_MERCCLAIM","Claim Their Bones");
         LanguageAPI.Add("PASSIVEAGRESSION_MERCCLAIM_DESC","<style=cIsDamage>Slayer.</style> Unleash a finishing blow for <style=cIsDamage>350% damage</syle>.Hold to release your swords current aspect.Kills <style=cIsUtility>absorb the targets aspect into your blade</style>.");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_MERCYIELD";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MERCYIELD_DESC";
         def.baseRechargeInterval = 6f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
         def.keywordTokens = new string[]{"KEYWORD_SLAYER"};
         def.activationState = new SerializableEntityStateType(typeof(YieldState));
         def.icon = Util.SpriteFromFile("Showdown.png");
         bdef = ScriptableObject.CreateInstance<BuffDef>();
         (bdef as ScriptableObject).name = "PASSIVEAGRESSION_MERCYIELD_BUFF";
         bdef.canStack = false;
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(YieldState),out _);
         damageType = ReserveDamageType();

     }


     public class YieldState : BaseState {
         public static float duration;
         public OverlapAttack overlapAttack;
         public override void OnEnter(){
             base.OnEnter();
             var anim = GetModelAnimator();
             duration = GroundLight.baseFinisherAttackDuration / attackSpeedStat;
             //overlapAttack = InitMeleeOverlap(3.5f, GroundLight.hitEffectPrefab, GetModelTransform(), "SwordLarge");
             if (anim.GetBool("isMoving") || !anim.GetBool("isGrounded"))
             {
                     PlayAnimation("Gesture, Additive", "GroundLight3", "GroundLight.playbackRate", duration);
                     PlayAnimation("Gesture, Override", "GroundLight3", "GroundLight.playbackRate", duration);
             }
             else
             {
                     PlayAnimation("FullBody, Override", "GroundLight3", "GroundLight.playbackRate", duration);
             }
             //slashChildName = "GroundLight3Slash";
             //swingEffectPrefab = finisherSwingEffectPrefab;
             //hitEffectPrefab = finisherHitEffectPrefab;
             //attackSoundString = finisherAttackSoundString;
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
     }

     public class ClaimState : BaseState{


     }

    } 
}
