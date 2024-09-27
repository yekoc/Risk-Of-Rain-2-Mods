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
    public static class BleedPassive{
     public static AssignableSkillDef def;
     public static CustomPassiveSlot slot;
     public static bool isHooked = false;

     private static void dotDamageHook(ILContext il){
         ILCursor c = new ILCursor(il);
         if(c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(typeof(HealthComponent).GetMethod(nameof(HealthComponent.TakeDamage))))){
             c.MoveBeforeLabels();
             c.Emit(OpCodes.Ldarg_0);
             c.Emit(OpCodes.Ldarg_1);
             c.EmitDelegate<Action<DotController,DotIndex>>((self,index) =>{
                if((index != DotIndex.Bleed && index != DotIndex.SuperBleed) || (self.victimHealthComponent && !self.victimHealthComponent.alive)){
                  return;
                }
                var pos = self.victimBody.transform.position;
                foreach(var state in BleedRitualState.instances.Where((state) => state.characterBody != self.victimBody && inRange(state.characterBody.transform.position,pos))){
                  RoR2.Orbs.OrbManager.instance.AddOrb(new RoR2.Orbs.HealOrb{
                    origin = pos,
                    healValue = 1f,
                    target = state.characterBody.mainHurtBox
                  });
                } 
             });
         }
         bool inRange(Vector3 origin,Vector3 target){
             var vec = target - origin;
             return vec.sqrMagnitude <= 1600;
         }
     }
     private static void unJetHook(ILContext il){
         ILCursor c = new ILCursor(il);
         if(c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(typeof(CharacterMotor).GetProperty(nameof(CharacterMotor.isGrounded)).GetGetMethod()))){
           c.Emit(OpCodes.Ldarg_0);
           c.EmitDelegate<Func<bool,MageCharacterMain,bool>>((orig,self) => orig || def.IsAssigned(self.characterBody));
         }
     }


     static BleedPassive(){
         slot = new CustomPassiveSlot("RoR2/Base/Mage/MageBody.prefab");
         LanguageAPI.Add("PASSIVEAGRESSION_MAGEBLOODRITE","Unorthodox Rituals");
         LanguageAPI.Add("PASSIVEAGRESSION_MAGEBLOODRITE_DESC","Whenever a tick of <style=cDeath>bleed</style> occurs nearby, heal <style=cIsHealing>1 health</style>.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_MAGEBLOODRITE";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MAGEBLOODRITE_DESC";
         def.onAssign = (GenericSkill slot) => {
             if(!isHooked){
                isHooked = true;
                IL.RoR2.DotController.EvaluateDotStacksForType += dotDamageHook;
                IL.EntityStates.Mage.MageCharacterMain.ProcessJump += unJetHook;
             }
             var jetMachine = EntityStateMachine.FindByCustomName(slot.characterBody.gameObject,"Jet");
             if(jetMachine){
              jetMachine.SetNextState(new BleedRitualState());
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
         def.activationState = new SerializableEntityStateType(typeof(BleedRitualState));
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(BleedRitualState),out _);

         void unJetLocal(EntityStateMachine esm,ref EntityState s){
             if(s.GetType() == typeof(Idle)){
                s = new BleedRitualState();
             }
         }
     }

     public class BleedRitualState : BaseState{
         public static List<BleedRitualState> instances = new List<BleedRitualState>();

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
    }
}
