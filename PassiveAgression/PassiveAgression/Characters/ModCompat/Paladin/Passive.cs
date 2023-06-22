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
using PaladinMod.States;

namespace PassiveAgression.ModCompat{
    public static class PaladinDesignPassive{
     public static AssignableSkillDef def;
     public static CustomPassiveSlot slot;
     public static bool isHooked;
     internal static ILHook pallyhookEnter;
     internal static ILHook pallyhookFUpdate;

     static PaladinDesignPassive(){
         slot = new CustomPassiveSlot(PaladinMod.PaladinPlugin.characterPrefab);
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINDESIGN","Sovereign's Design");
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINDESIGN_DESC","Gain <style=cIsHealing>adaptive armor</style>. While having full <style=cIsHealth>shields</style>, the Paladin is <style=cIsHealing>blessed</style>, empowering all sword skills.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_PALADINDESIGN";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_PALADINDESIGN_DESC";
         def.activationStateMachineName = "Body";
         def.onAssign = (GenericSkill slot) => {
             if(!isHooked){
                isHooked = true;
                pallyhookEnter = new ILHook(typeof(PaladinMain).GetMethod("OnEnter"),(ILContext.Manipulator)Edify);
                pallyhookFUpdate = new ILHook(typeof(PaladinMain).GetMethod("FixedUpdate"),(ILContext.Manipulator)Edify);
                RoR2.Run.onRunDestroyGlobal += unhooker;
             }
             slot.characterBody.levelArmor = 0;
             slot.characterBody.master.inventory.GiveItem(RoR2Content.Items.AdaptiveArmor);
             return null;
             void unhooker(Run run){
                if(isHooked){
                  pallyhookEnter.Free();
                  pallyhookFUpdate.Free();
                  isHooked = false;
                }
                RoR2.Run.onRunDestroyGlobal -= unhooker;
             }
         };
         def.onUnassign = (GenericSkill slot) =>{
            slot.characterBody.levelArmor = PaladinDesignPassive.slot.bodyPrefab.GetComponent<CharacterBody>().levelArmor;
            slot.characterBody.master?.inventory?.RemoveItem(RoR2Content.Items.AdaptiveArmor);
            (slot.stateMachine.state as PaladinMain)?.OnEnter();
         };
         def.icon = Util.SpriteFromFile("HForgeIcon.png"); 
         def.baseRechargeInterval = 0f;
         def.activationStateMachineName = "Body";
         def.cancelSprintingOnActivation = false;
         def.canceledFromSprinting = false;
         def.activationState = EntityStateMachine.FindByCustomName(slot.bodyPrefab,"Body").mainStateType;
         ContentAddition.AddSkillDef(def);
     }

     public static void Edify(ILContext il){
        ILCursor c = new ILCursor(il);
        int index = -1;
        if(c.TryGotoNext(MoveType.After,
                    x => x.MatchStloc(out index),
                    x => x.MatchLdloc(index),
                    x => x.MatchBrfalse(out _))){
            c.Index--;
            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldfld,typeof(EntityState).GetField("outer"));
            c.Emit(OpCodes.Ldfld,typeof(EntityStateMachine).GetField("commonComponents"));
            c.Emit(OpCodes.Ldfld,typeof(EntityStateMachine.CommonComponentCache).GetField("characterBody"));
            c.EmitDelegate<Func<bool,CharacterBody,bool>>((orig,bod) => def.IsAssigned(bod)? (bod.healthComponent.shield >= bod.healthComponent.fullShield) : orig);
        }
     }
    }
}
