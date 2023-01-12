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
using EntityStates.Huntress;

namespace PassiveAgression.Huntress
{
    public static class RivetShot{
     public static SkillDef def;

     static RivetShot(){
         LanguageAPI.Add("PASSIVEAGRESSION_HUNTRESSRIVET","Explosive Rivet");
         LanguageAPI.Add("PASSIVEAGRESSION_HUNTRESSRIVET_DESC",".");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_HUNTRESSRIVET";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_HUNTRESSRIVET_DESC";
         def.baseRechargeInterval = 6f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(RivetState));
         def.icon = Util.SpriteFromFile("StandoffIcon.png");
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(RivetState),out _);
         }


     public class RivetState : GenericProjectileBaseState {

         public override InterruptPriority GetMinimumInterruptPriority(){
             return InterruptPriority.PrioritySkill;
         }
     }

    } 
}
