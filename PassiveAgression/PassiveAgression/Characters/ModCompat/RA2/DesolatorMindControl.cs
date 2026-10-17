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

namespace PassiveAgression.ModCompat
{
    public static class DesolatorMindControlPassive{
     public static AssignableSkillDef def;
     public static CustomPassiveSlot slot;
     public static bool isHooked = false;


     static DesolatorMindControlPassive(){
         //slot = new CustomPassiveSlot((BepInEx.Bootstrap.Chainloader.PluginInfos["com.Gnome.ChefMod"].Instance as ChefMod.ChefPlugin).chefPrefab);
         LanguageAPI.Add("PASSIVEAGRESSION_DESOHYPNO","C-Conciousness");
         LanguageAPI.Add("PASSIVEAGRESSION_DESOHYPNO_DESC","Desolator's attacks have a chance to cause infighting between <style=cIsHealing>Irradiated</style> targets.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_DESOHYPNO";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_DESOHYPNO_DESC";
         def.onAssign = (GenericSkill slot) => {
             if(!isHooked){
                Run.onRunDestroyGlobal += unsub;
                isHooked = true;
             }

             return null;
             void unsub(Run run){
                Run.onRunDestroyGlobal -= unsub;
                isHooked = false;
             }
         };
         def.onUnassign = (GenericSkill slot) =>{

         };
         def.activationStateMachineName = "Body";
         def.activationState = EntityStateMachine.FindByCustomName(slot.bodyPrefab,"Body").mainStateType;
         //def.icon = 
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.baseRechargeInterval = 0f;
         ContentAddition.AddSkillDef(def);
     }
    }
}
