using BepInEx.Configuration;
using RoR2;
using RoR2.Skills;
using EntityStates;
using RoR2.CharacterAI;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using MonoMod.RuntimeDetour;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using RiskOfRuinaMod.Modules.Components;

namespace PassiveAgression.ModCompat{
    public static class ZenaLine{
     public static AssignableSkillDef def;
     public static bool isHooked;

     static ZenaLine(){
         LanguageAPI.Add("PASSIVEAGRESSION_RUINALINE","Line");
         LanguageAPI.Add("PASSIVEAGRESSION_RUINALINE_DESC","All <style=cIsDamage>Attack Speed</style> and <style=cIsUtility>Movement Speed</style> bonuses are converted to block chance for long range attacks");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_RUINALINE";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_RUINALINE_DESC";
         def.onAssign = (GenericSkill slot) => {
             if(!isHooked){
                isHooked = true;
             }
             return null;
             void unhooker(Run run){
                if(isHooked){
                   isHooked = false;
                }
                RoR2.Run.onRunDestroyGlobal -= unhooker;
             }
             
         };
         def.onUnassign = (GenericSkill slot) =>{
         };
         def.icon = PassiveAgressionPlugin.unfinishedIcon;//Util.SpriteFromFile("ZenalLineIcon.png");

         def.baseRechargeInterval = 0f;
         def.activationStateMachineName = "Weapon";
         def.cancelSprintingOnActivation = false;
         def.canceledFromSprinting = false;
         def.activationState = new SerializableEntityStateType(typeof(ZenaLineState));
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(ZenaLineState), out _);

     }

     public class ZenaLineState : GenericBulletBaseState{

     }
    }
}
