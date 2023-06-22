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
using GanondorfMod.Modules;
using GanondorfMod.Modules.Survivors;

namespace PassiveAgression.ModCompat{
    public static class GanonPassive{
     public static AssignableSkillDef def;
     public static CustomPassiveSlot slot;
     public static bool isHooked;
     public static Hook hook,hook2;

     static GanonPassive(){
         slot = new CustomPassiveSlot(Ganondorf.instance.bodyPrefab);
         LanguageAPI.Add("PASSIVEAGRESSION_GANONGLOOM", "Secret Stone of Darkness" );
         LanguageAPI.Add("PASSIVEAGRESSION_GANONGLOOM_DESC","Your steps will spread <style=cDeath>Gloom</style>,draining away the max health of enemies in contact with it.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_GANONGLOOM";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_GANONGLOOM_DESC";
         def.activationStateMachineName = "Body";
         def.onAssign = (GenericSkill slot) => {
             if(!isHooked){
                isHooked = true;
                hook = new Hook(typeof(TriforceBuffComponent).GetMethod("GetBuffCount"),typeof(GanonPassive).GetMethod("ZeroIfEquipped"));
                hook2 = new Hook(typeof(TriforceBuffComponent).GetMethod("GetTrueBuffCount"),typeof(GanonPassive).GetMethod("ZeroIfEquipped"));
                RoR2.Run.onRunDestroyGlobal += unhooker;
             }
             return null;
             void unhooker(Run run){
                if(isHooked){
                  isHooked = false;
                  hook.Free();
                  hook2.Free();
                }
                RoR2.Run.onRunDestroyGlobal -= unhooker;
             }
         };
         def.onUnassign = (GenericSkill slot) =>{
         };
         def.icon = Util.SpriteFromFile("GloomIcon.png"); 
         def.baseRechargeInterval = 0f;
         def.activationStateMachineName = "Body";
         def.activationState = EntityStateMachine.FindByCustomName(slot.bodyPrefab,"Body").mainStateType;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         ContentAddition.AddSkillDef(def);
     }

     public static int ZeroIfEquipped(Func<TriforceBuffComponent,int> orig,TriforceBuffComponent self){
        return (def.IsAssigned(self.ganondorfController.characterBody))? 0 : orig(self);
     }

    }
}
