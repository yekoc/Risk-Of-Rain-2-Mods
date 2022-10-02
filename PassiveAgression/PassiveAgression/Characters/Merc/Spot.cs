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

namespace PassiveAgression.Bandit
{
    public static class SpotDodge{
     public static SkillDef def;

     static SpotDodge(){
         LanguageAPI.Add("PASSIVEAGRESSION_MERCDODGE","Shadow Spot");
         LanguageAPI.Add("PASSIVEAGRESSION_MERCDODGE_DESC",".");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_MERCDODGE";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MERCDODGE_DESC";
         def.baseRechargeInterval = 6f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Body";
         def.interruptPriority = InterruptPriority.PrioritySkill;
         def.activationState = new SerializableEntityStateType(typeof(SpotState));
         LoadoutAPI.AddSkillDef(def);
         LoadoutAPI.AddSkill(typeof(SpotState));
         }


     public class SpotState : BaseState {

         public override void OnEnter(){
            base.OnEnter();
            SmallHop(characterMotor,0.5f);
            if(NetworkServer.active){
              characterBody.AddBuff(RoR2Content.Buffs.HiddenInvincibility);
            }
         }
         public override void FixedUpdate(){
             base.FixedUpdate();
	     PlayCrossfade("FullBody, Override", "AssaulterLoop", 0.1f);
         }
         public override void OnExit(){
             base.OnExit();
             if(NetworkServer.active){
               characterBody.RemoveBuff(RoR2Content.Buffs.HiddenInvincibility);
             }
         }
         public override InterruptPriority GetMinimumInterruptPriority(){
             return InterruptPriority.Frozen;
         }
     }

    } 
}
