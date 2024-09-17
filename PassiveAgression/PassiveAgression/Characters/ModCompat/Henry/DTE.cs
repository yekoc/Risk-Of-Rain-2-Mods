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
using HenryMod.SkillStates.BaseStates;

namespace PassiveAgression.ModCompat
{
    public static class HenryDTExplosion{
     public static SkillDef def;

     static HenryDTExplosion(){
         LanguageAPI.Add("PASSIVEAGRESSION_HENRYDTE","Furious Burst");
         LanguageAPI.Add("PASSIVEAGRESSION_HENRYDTE_DESC","Hold to charge your Fury into a large explosion, dealing <style=cIsDamage>immense damage</style>.");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_HENRYDTE";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_HENRYDTE_DESC";
         def.baseRechargeInterval = 6f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(ChargeState));
         def.icon = Util.SpriteFromFile("StarchIcon.png");
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(ChargeState),out _);

         
     }

     public class ChargeState : BaseSkillState {

     }
     public class ExplodeState : HenryMod.SkillStates.Henry.Frenzy.EnterFrenzy{

     }
    } 
}
