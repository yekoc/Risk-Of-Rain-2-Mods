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
using Deputy;
using System.Collections.Generic;
using Skillstates.Deputy;

namespace PassiveAgression.ModCompat{
    public static class DeputyPassive{
     public static AssignableSkillDef def;
     public static CustomPassiveSlot slot;
     public static bool isHooked;
     public static Hook hook;
     public static int slotIndex;
     public static ConfigEntry<bool> damage;
     public static ConfigEntry<float> proc;
     public static string animLayer = "FullBody, Override";
     public static string animName = "Kick1";
     public static string animParam = "Flip.playbackRate";
     public static Dictionary<GameObject,OverlapAttack> dictThatPreventsSpammingSpaceBarAsAViableTactic = new();

     static DeputyPassive(){
         slot = new CustomPassiveSlot(DeputyPlugin.deputyBodyPrefab);
         LanguageAPI.Add("PASSIVEAGRESSION_DEPUTYBOUNCE", (UnityEngine.Random.value > 0.5) ? "Jangling Spurs" : "Jingling Spurs" );
         LanguageAPI.Add("PASSIVEAGRESSION_DEPUTYBOUNCE_DESC","The Deputy can sprint in any direction. Press jump again while in contact with an enemy to bounce off and reset your jump count.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_DEPUTYBOUNCE";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_DEPUTYBOUNCE_DESC";
         def.activationStateMachineName = "Body";
         def.onAssign = (GenericSkill slot) => {
             if(!isHooked){
                isHooked = true;
                hook = new Hook(typeof(DeputyMainState).GetMethod("FixedUpdate"),typeof(DeputyPassive).GetMethod("EnemyStep"));
                On.RoR2.GlobalEventManager.OnHitEnemy += LostPursuit;
                RoR2.Run.onRunDestroyGlobal += unhooker;
             }
             slotIndex = Array.FindIndex(slot.characterBody.skillLocator.allSkills,(s) => s == slot);
             dictThatPreventsSpammingSpaceBarAsAViableTactic.Add(slot.gameObject,new OverlapAttack{
                 attacker = slot.characterBody.gameObject,
                 damage = damage.Value ? 1f : 0f,
                 procCoefficient = proc.Value,
                 hitBoxGroup = Array.Find<HitBoxGroup>(slot.characterBody.gameObject.GetComponent<ModelLocator>().modelTransform.GetComponents<HitBoxGroup>(),(HitBoxGroup element) => element.groupName == "Dash")
             });
             return null;
             void unhooker(Run run){
                if(isHooked){
                  isHooked = false;
                  hook.Free();
                  On.RoR2.GlobalEventManager.OnHitEnemy -= LostPursuit;
                }
                RoR2.Run.onRunDestroyGlobal -= unhooker;
             }
         };
         def.onUnassign = (GenericSkill slot) =>{
             dictThatPreventsSpammingSpaceBarAsAViableTactic.Remove(slot.gameObject);
         };
         def.icon = Util.SpriteFromFile("SpursIcon.png"); 
         def.baseRechargeInterval = 0f;
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(BounceState));
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         damage = PassiveAgressionPlugin.config.Bind("DeputyBounce","Sharper Spurs",false,"When set to true,makes it so that bouncing off of enemies deals 1 (1) damage to them.");
         proc = PassiveAgressionPlugin.config.Bind("DeputyBounce","Spur Proc Coefficient",0f,"Proc coefficient for bouncing off of enemies.");
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(BounceState),out _);
     }

     public static void LostPursuit(On.RoR2.GlobalEventManager.orig_OnHitEnemy orig,GlobalEventManager self,DamageInfo info,GameObject victim){
         var bod = info.attacker.GetComponent<CharacterBody>();
         if(bod.bodyIndex == DeputyPlugin.deputyBodyIndex && def.IsAssigned(bod.skillLocator.allSkills[slotIndex])){
             info.RemoveModdedDamageType(DeputyPlugin.grantDeputyBuff);
         }
         orig(self,info,victim);
     }
     public static void EnemyStep(Action<DeputyMainState> orig,DeputyMainState self){
        orig(self);
        if(self.isAuthority && !self.wasGrounded && self.inputBank.jump.justPressed && def.IsAssigned(self.skillLocator.allSkills[slotIndex]) && dictThatPreventsSpammingSpaceBarAsAViableTactic[self.skillLocator.gameObject].Fire()){
            EntityStateMachine.FindByCustomName(self.characterBody.gameObject,"Weapon").SetInterruptState(new BounceState(),InterruptPriority.Skill);
        }
     }
     public class BounceState : BaseState{
         public override void OnEnter(){
             base.OnEnter();
             PlayAnimation(animLayer,animName,animParam,1f);
             GenericCharacterMain.ApplyJumpVelocity(characterMotor,characterBody,1.5f,1.5f);
         }
         public override void FixedUpdate(){
             base.FixedUpdate();
             if(base.isAuthority){
                characterBody.isSprinting = true;
                if(fixedAge >= 1){
                  outer.SetNextStateToMain();
                }
             }
         }
         public override void OnExit(){
             base.OnExit();
             characterMotor.OnLanded();
             characterMotor.OnLeaveStableGround();
             dictThatPreventsSpammingSpaceBarAsAViableTactic[skillLocator.gameObject].ResetIgnoredHealthComponents();
         }
         public override InterruptPriority GetMinimumInterruptPriority(){
             return InterruptPriority.PrioritySkill;
         }
     }
    }
}
