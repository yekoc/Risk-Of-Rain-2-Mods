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

namespace PassiveAgression.Bandit
{
    public static class StandoffPassive{
     public static AssignableSkillDef def;
     public static CustomPassiveSlot slot;
     public static bool isHooked = false;
     internal class attackerTracker {internal uint val;}
     internal static ConditionalWeakTable<CharacterBody,attackerTracker> attacking = new ConditionalWeakTable<CharacterBody,attackerTracker>();

     private static void takeDamageHook(ILContext il){
         ILCursor c = new ILCursor(il);
         if(c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(typeof(BackstabManager).GetMethod(nameof(BackstabManager.IsBackstab))))){
             c.Index++;
             ILLabel label = c.MarkLabel();
             c.GotoPrev(x => x.MatchLdloc(out _),x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetProperty(nameof(CharacterBody.canPerformBackstab)).GetGetMethod()));
             c.Emit(OpCodes.Ldarg_0);
             c.Emit(OpCodes.Ldarg_1);
             c.EmitDelegate<Func<HealthComponent,DamageInfo,bool>>((self,info) => attacking.TryGetValue(self.body,out var result) && result.val > 0 && def.IsAssigned(info.attacker.GetComponent<CharacterBody>()));
             c.Emit(OpCodes.Brtrue,label);
         }
     }

     private static void BodyTracker(CharacterBody body){
         if(body.canReceiveBackstab){
            body.onSkillActivatedServer += (slot) =>{
                if(slot.isCombatSkill){
                    Util.OnStateWorkFinished(slot.stateMachine,(EntityStateMachine machine,ref EntityState state) =>{
                            attacking.GetOrCreateValue(machine.commonComponents.characterBody).val--;
                    });
                    attacking.GetOrCreateValue(slot.characterBody).val++;
                }
            };
         }
     }

     static StandoffPassive(){
         slot = new CustomPassiveSlot("RoR2/Base/Bandit2/Bandit2Body.prefab");
         LanguageAPI.Add("PASSIVEAGRESSION_BANDITSHOWDOWN","Standoff");
         LanguageAPI.Add("PASSIVEAGRESSION_BANDITSHOWDOWN_DESC","All attacks on enemies that are <style=cIsDamage>attacking</style> are <style=cIsDamage>Critical Strikes</style>.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_BANDITSHOWDOWN";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_BANDITSHOWDOWN_DESC";
         def.onAssign = (GenericSkill slot) => {
             slot.characterBody.bodyFlags &= ~CharacterBody.BodyFlags.HasBackstabPassive;
             if(!isHooked){
                isHooked = true;
                IL.RoR2.HealthComponent.TakeDamage += takeDamageHook;
                CharacterBody.onBodyStartGlobal += BodyTracker;
                Run.onRunDestroyGlobal += unsub;
             }

             return null;
             void unsub(Run run){
                IL.RoR2.HealthComponent.TakeDamage -= takeDamageHook;
                CharacterBody.onBodyStartGlobal -= BodyTracker;
                Run.onRunDestroyGlobal -= unsub;
                isHooked = false;
             }
         };
         def.onUnassign = (GenericSkill slot) =>{
             slot.characterBody.bodyFlags |= CharacterBody.BodyFlags.HasBackstabPassive;
         };
         def.activationStateMachineName = "Body";
         def.activationState = EntityStateMachine.FindByCustomName(slot.bodyPrefab,"Body").mainStateType;
         def.icon = Util.SpriteFromFile("Standoff.png");
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.baseRechargeInterval = 0f;
         ContentAddition.AddSkillDef(def);
     }
    }
}
