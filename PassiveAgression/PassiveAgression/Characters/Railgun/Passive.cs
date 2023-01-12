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

namespace PassiveAgression.Railgunner{
    public static class RailgunnerPassive{
     public static AssignableSkillDef def;
     public static bool isHooked;

     public class critStore : SkillDef.BaseSkillInstanceData{
       public float critChance; 
     }

     static RailgunnerPassive(){
         LanguageAPI.Add("PASSIVEAGRESSION_RAILMISSILE","Micro-Missile Foundry");
         LanguageAPI.Add("PASSIVEAGRESSION_RAILMISSILE_DESC","All <style=cIsDamage>Critical Strike Chance</style> is converted into damage for missiles that fire on critical hits.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_RAILMISSILE";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_RAILMISSILE_DESC";
         def.activationStateMachineName = "Body";
         def.onAssign = (GenericSkill slot) => {
             if(!isHooked){
                isHooked = true;
                On.RoR2.GlobalEventManager.OnCrit += OnCrit;
                On.RoR2.CharacterBody.RecalculateStats += Recalculate;
                RoR2.Run.onRunDestroyGlobal += unhooker;
             }
             return new critStore();
             void unhooker(Run run){
                if(isHooked){
                  On.RoR2.GlobalEventManager.OnCrit -= OnCrit;
                  On.RoR2.CharacterBody.RecalculateStats -= Recalculate;
                  isHooked = false;
                }
                RoR2.Run.onRunDestroyGlobal -= unhooker;
             }
         };
         def.onUnassign = (GenericSkill slot) =>{
            
         };
         def.icon = Util.SpriteFromFile("MicroMissile.png"); 
         def.baseRechargeInterval = 0f;
         def.activationStateMachineName = "Body";
         def.activationState = new SerializableEntityStateType(typeof(GenericCharacterMain));
         ContentAddition.AddSkillDef(def);
     }

     public static void OnCrit(On.RoR2.GlobalEventManager.orig_OnCrit orig,GlobalEventManager self,CharacterBody body,DamageInfo info,CharacterMaster master,float proc,ProcChainMask procChainMask){
        orig(self,body,info,master,proc,procChainMask);
        if(!NetworkServer.active)
            return;
        var skill = def.GetSkill(body);
        if(skill){
          var num = ((critStore)skill.skillInstanceData).critChance  * 0.01f;
          if(num > 0)
           MissileUtils.FireMissile(body.corePosition,body,procChainMask,null,info.damage * num,false,GlobalEventManager.CommonAssets.missilePrefab,DamageColorIndex.Default,false);
        }
     }
     public static void Recalculate(On.RoR2.CharacterBody.orig_RecalculateStats orig,CharacterBody self){
        orig(self);
        var skill = def.GetSkill(self);
        if(skill){
          ((critStore)skill.skillInstanceData).critChance = self.crit;
          self.crit = 0;
        }
     }
    }
}
