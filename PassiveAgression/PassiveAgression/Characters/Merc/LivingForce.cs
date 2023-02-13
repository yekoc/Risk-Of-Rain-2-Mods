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
using EntityStates.Bandit2;
using static R2API.DamageAPI;

namespace PassiveAgression.Bandit
{
    public static class LivingForce{
     public static SkillDef def;
     public static ModdedDamageType damageType;

     static LivingForce(){
         LanguageAPI.Add("PASSIVEAGRESSION_MERCLIVING","Living Force");
         LanguageAPI.Add("PASSIVEAGRESSION_MERCLIVING_DESC","<style=cIsDamage>Slayer</style>. Unleash a finishing blow for <style=cIsDamage>350% damage</syle>. Hold to release your swords current aspect. Kills <style=cIsUtility>absorb the targets aspect into your blade</style>.");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_MERCLIVING";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MERCLIVING_DESC";
         def.baseRechargeInterval = 6f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
         def.keywordTokens = new string[]{"KEYWORD_SLAYER"};
         def.activationState = new SerializableEntityStateType(typeof(ForceAttackState));
         def.icon = Util.SpriteFromFile("Showdown.png");
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(ForceAttackState),out _);
         damageType = ReserveDamageType();
         }


     public class ForceAttackState : BaseState {
         public static float duration;

         static ForceAttackState(){
         }
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
     }

    } 
}
