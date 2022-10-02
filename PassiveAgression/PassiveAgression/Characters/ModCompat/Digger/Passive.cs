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
using EntityStates.Digger;

namespace PassiveAgression.ModCompat{
    public static class DiggerBlacksmithPassive{
     public static AssignableSkillDef def;
     public static CustomPassiveSlot slot;
     public static bool isHooked;
     internal static Hook diggerhook;

     static DiggerBlacksmithPassive(){
         slot = new CustomPassiveSlot(DiggerPlugin.DiggerPlugin.characterBodyPrefab);
         LanguageAPI.Add("PASSIVEAGRESSION_DIGGERFLAME","Heat of the Forge");
         LanguageAPI.Add("PASSIVEAGRESSION_DIGGERFLAME_DESC","Each one of your attacks deal <style=cIsHealth>BLAZING</style> damage.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_DIGGERFLAME";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_DIGGERFLAME_DESC";
         def.activationStateMachineName = "Body";
         def.onAssign = (GenericSkill slot) => {
             if(!isHooked){
                isHooked = true;
                diggerhook = new Hook(typeof(DiggerMain).GetMethod("OnEnter"),(Action<Action<DiggerMain>,DiggerMain>)UnAdrenalinize);
                IL.RoR2.GlobalEventManager.OnHitEnemy += TreatAsAspect;
                RoR2.Run.onRunDestroyGlobal += unhooker;
             }
             return null;
             void unhooker(Run run){
                if(isHooked){
                  diggerhook.Free();
                  IL.RoR2.GlobalEventManager.OnHitEnemy -= TreatAsAspect;
                  isHooked = false;
                }
                RoR2.Run.onRunDestroyGlobal -= unhooker;
             }
         };
         def.onUnassign = (GenericSkill slot) =>{
            (slot.stateMachine.state as DiggerMain)?.OnEnter();
         };
         def.icon = Util.SpriteFromFile("HForgeIcon.png"); 
         def.baseRechargeInterval = 0f;
         LoadoutAPI.AddSkillDef(def);
     }

     public static void UnAdrenalinize(Action<DiggerMain> orig,DiggerMain self){
         orig(self);
         if(def.IsAssigned(self.characterBody)){
           self.gotJunkie = true;
           self.adrenalineCap = -1;
         }
     }
     public static void TreatAsAspect(ILContext il){
        ILCursor c = new ILCursor(il);
        if(c.TryGotoNext(MoveType.After
                    ,x => x.MatchLdsfld(typeof(RoR2Content.Buffs).
                        GetField(nameof(RoR2Content.Buffs.AffixRed)))
                    ,x => x.MatchCallOrCallvirt(out _))){
          c.Emit(OpCodes.Ldarg_1);
          c.EmitDelegate<Func<bool,DamageInfo,bool>>((orig,damageinfo) => orig || (damageinfo.procChainMask.mask == default(uint) && def.IsAssigned(damageinfo.attacker.GetComponent<CharacterBody>()))); 
        }
     }

    }
}
