using RoR2;
using RoR2.Skills;
using RoR2.UI;
using EntityStates;
using RoR2.CharacterAI;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Networking;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using PaladinMod.States;
using R2API;

using static RoR2.UI.HealthBar;

namespace PassiveAgression.ModCompat
{
    public static class PaladinBolt{
     public static AssignableSkillDef def;
     public static SkillDef scepterdef;
     public static bool isHooked;


     static PaladinBolt(){
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINBOLT","Sunbolt Blessing");
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINBOLT_DESC","Call down a <style=cIsUtility>lightning bolt</style>, boosting your weapon and armor with lightning.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_PALADINBOLT";
         def.skillDescriptionToken = "PASSIVEAGRESSION_PALADINBOLT_DESC";
         def.baseRechargeInterval = 10f;
         def.dontAllowPastMaxStocks = false;
         def.fullRestockOnAssign = true;
         def.rechargeStock = 1;
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(PrepBoltState));
         def.cancelSprintingOnActivation = true;
         def.canceledFromSprinting = true;
         def.isCombatSkill = false;
         def.onAssign += (skillSlot) => {
             if(!isHooked){
                isHooked = true;
                Run.onRunDestroyGlobal += unsub;
             }
             return null;
         };
         (def as ScriptableObject).name = def.skillNameToken;
         def.icon = Util.SpriteFromFile("BoltIcon.png");
         

         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(PrepBoltState),out _);
         ContentAddition.AddEntityState(typeof(CastBoltState),out _);

         void unsub(Run run){ 
            Run.onRunDestroyGlobal -= unsub;
            isHooked = false;
         }
     }

     [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.NoOptimization)]
     public static void SetUpScepter(){
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINBOLT_SCEPTER","Beloved Sunbolt");
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINBOLT_SCEPTERDESC","Call down a empowering <style=cIsUtility>lightning bolt</style>,boosting your weapon,armor,and body with lightning.");
         scepterdef = ScriptableObject.CreateInstance<SkillDef>();
         scepterdef.skillNameToken = "PASSIVEAGRESSION_PALADINBOLT_SCEPTER";
         scepterdef.skillDescriptionToken = "PASSIVEAGRESSION_PALADINBOLT_SCEPTERDESC";
         scepterdef.baseRechargeInterval = 0f;
         scepterdef.dontAllowPastMaxStocks = true;
         scepterdef.fullRestockOnAssign = true;
         scepterdef.rechargeStock = 1;
         scepterdef.requiredStock = 0;
         scepterdef.activationStateMachineName = "Weapon";
         scepterdef.activationState = new SerializableEntityStateType(typeof(PrepBoltState));
         scepterdef.cancelSprintingOnActivation = false;
         scepterdef.canceledFromSprinting = false;
         scepterdef.isCombatSkill = false;
         (scepterdef as ScriptableObject).name = scepterdef.skillNameToken;
         scepterdef.icon = Util.SpriteFromFile("BoltIconScepter.png");;

         ContentAddition.AddSkillDef(scepterdef);
         AncientScepter.AncientScepterItem.instance.RegisterScepterSkill(scepterdef,"RobPaladinBody",def);
     }


     public class PrepBoltState : BaseChannelSpellState{
            public override void OnEnter(){
                    baseDuration = 2f;
                    //base. = true;
		    base.OnEnter();
                    areaIndicatorInstance = null;
	    }
	    public override InterruptPriority GetMinimumInterruptPriority(){
		    return InterruptPriority.PrioritySkill;
	    }
            public override BaseCastChanneledSpellState GetNextState(){
                 return new CastBoltState();
                
            }
     }
     public class CastBoltState : BaseCastChanneledSpellState{
           
            
            public override void OnEnter(){
                baseDuration = 1f;
                base.OnEnter();
            }
            public override void OnExit(){
               
                base.OnExit();
                if(NetworkServer.active){
                  characterBody.AddTimedBuff(PaladinMod.Modules.Buffs.overchargeBuff,4f);
                }
            }
	    public override InterruptPriority GetMinimumInterruptPriority(){
		    return InterruptPriority.PrioritySkill;
	    }
     }
    }
}
