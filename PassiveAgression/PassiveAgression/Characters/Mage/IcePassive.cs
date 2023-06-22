using RoR2;
using RoR2.Skills;
using EntityStates;
using RoR2.CharacterAI;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using EntityStates.Mage;
using static RoR2.DotController;

namespace PassiveAgression.Mage
{
    public static class IcePassive{
     public static AssignableSkillDef def;
     public static CustomPassiveSlot slot;
     public static bool isHooked = false;

     private static void unJetHook(ILContext il){
         ILCursor c = new ILCursor(il);
         if(c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(typeof(CharacterMotor).GetProperty(nameof(CharacterMotor.isGrounded)).GetGetMethod()))){
           c.Emit(OpCodes.Ldarg_0);
           c.EmitDelegate<Func<bool,MageCharacterMain,bool>>((orig,self) => orig || def.IsAssigned(self.characterBody));
         }
     }


     static IcePassive(){
         slot = new CustomPassiveSlot("RoR2/Base/Mage/MageBody.prefab");
         LanguageAPI.Add("PASSIVEAGRESSION_MAGEFROSTRITE","Hoarfrost");
         LanguageAPI.Add("PASSIVEAGRESSION_MAGEFROSTRITE_DESC","<style=cIsUtility>Frozen</style> enemies emit an aura which slows and damages enemies.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_MAGEFROSTRITE";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MAGEFROSTRITE_DESC";
         def.onAssign = (GenericSkill slot) => {
             if(!isHooked){
                isHooked = true;
                IL.EntityStates.Mage.MageCharacterMain.ProcessJump += unJetHook;
             }
             var jetMachine = EntityStateMachine.FindByCustomName(slot.characterBody.gameObject,"Jet");
             if(jetMachine){
              jetMachine.SetNextState(new IceRitualState());
              jetMachine.nextStateModifier += unJetLocal;
             }
             return null;
         };
         def.onUnassign = (GenericSkill slot) =>{
            var jetMachine = EntityStateMachine.FindByCustomName(slot.characterBody.gameObject,"Jet");
            if(jetMachine){
              jetMachine.SetNextState(new Idle());
              jetMachine.nextStateModifier -= unJetLocal;
            }
         };
         def.icon = Util.SpriteFromFile("UnorthodoxIcon.png"); 
         def.baseRechargeInterval = 0f;
         def.activationStateMachineName = "Jet";
         def.activationState = new SerializableEntityStateType(typeof(IceRitualState));
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(IceRitualState),out _);

         void unJetLocal(EntityStateMachine esm,ref EntityState s){
             if(s.GetType() == typeof(Idle)){
                s = new IceRitualState();
             }
         }
     }

     public class IceRitualState : BaseState{
         public static List<IceRitualState> instances = new List<IceRitualState>();

         public override void OnEnter(){
            base.OnEnter();
            instances.Add(this);
         }
         public override void OnExit(){
            base.OnExit();
            instances.Remove(this);
         }
         public override InterruptPriority GetMinimumInterruptPriority(){
             return InterruptPriority.Death;
         }
     }

     public class FrozenTotemState : FrozenState{
         public BuffWard ward;
     }
    }
}
