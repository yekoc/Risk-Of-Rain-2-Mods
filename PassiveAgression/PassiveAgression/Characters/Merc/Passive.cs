using RoR2;
using RoR2.Skills;
using EntityStates;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;

namespace PassiveAgression.Merc
{
    public static class FlickerPassive{
     public static AssignableSkillDef def;
     public static CustomPassiveSlot slot;
     public static bool isHooked = false;

     private static void OrbInflictorHook(ILContext il){
         ILCursor c = new ILCursor(il);
     }


     static FlickerPassive(){
         slot = new CustomPassiveSlot("RoR2/Base/Merc/MercBody.prefab");
         LanguageAPI.Add("PASSIVEAGRESSION_MERCFLICKER","Flickering Blade");
         LanguageAPI.Add("PASSIVEAGRESSION_MERCFLICKER_DESC","All attacks are delayed by 1s, <style=cIsUtility>attacks on exposed enemies</style>.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_MERCFLICKER";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MERCFLICKER_DESC";
         def.onAssign = (GenericSkill slot) => {
             slot.characterBody.baseJumpCount--;
             if(!isHooked){
                isHooked = true;
             }
             return null;
         };
         def.onUnassign = (GenericSkill slot) =>{
             slot.characterBody.baseJumpCount++;
         };
         def.icon = slot.family.variants[0].skillDef.icon;
         def.baseRechargeInterval = 0f;
         def.activationStateMachineName = "Body";
         def.activationState = new SerializableEntityStateType(typeof(GenericCharacterMain));
         ContentAddition.AddSkillDef(def);
     }
    }
}
