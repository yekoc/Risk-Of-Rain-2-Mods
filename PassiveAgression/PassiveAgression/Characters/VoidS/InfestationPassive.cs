using RoR2;
using RoR2.Skills;
using EntityStates;
using RoR2.CharacterAI;
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;
using R2API;
using BepInEx.Configuration;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using EntityStates.VoidSurvivor;
using EntityStates.VoidSurvivor.CorruptMode;
using static RoR2.ScriptedCombatEncounter;

namespace PassiveAgression.VoidSurvivor
{
    public static class InfestationPassive{
     public static AssignableSkillDef def;
     public static bool isHooked = false;
     public static ConfigEntry<bool> friendlyInfest;
     private static SpawnInfo[] spawns = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/VoidChest/VoidChest.prefab").WaitForCompletion().GetComponent<ScriptedCombatEncounter>().spawns;
     private static List<CombatSquad> squads = new List<CombatSquad>();


     static InfestationPassive(){
         friendlyInfest = PassiveAgression.PassiveAgressionPlugin.config.Bind("ViendBug","Friendly Infestors",false,"Makes the infestors spawned by Incessant Infestation friendly to their creator");
         LanguageAPI.Add("PASSIVEAGRESSION_VIENDBUG","『Incessant Infestation】");
         LanguageAPI.Add("PASSIVEAGRESSION_VIENDBUG_DESC","At full <style=cIsVoid>Corruption</style>, burst out a gaggle of Infestors.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_VIENDBUG";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_VIENDBUG_DESC";
         def.onAssign = (GenericSkill slot) => {
             var encount = slot.characterBody.gameObject.AddComponent<ScriptedCombatEncounter>();
             encount.spawns = spawns;
             if(friendlyInfest.Value){
               encount.teamIndex = slot.characterBody.teamComponent.teamIndex;
             }
             for(int i = 0;i < spawns.Length; i++){
                 encount.spawns[i].explicitSpawnPosition = slot.characterBody.gameObject.transform;
                 encount.spawns[i].cullChance /= 4f;
             }
             encount.randomizeSeed = true;
             encount.combatSquad = slot.characterBody.gameObject.AddComponent<CombatSquad>();
             encount.combatSquad.propagateMembershipToSummons = true;
             squads.Add(encount.combatSquad);
             encount.teamIndex = TeamIndex.Void;
             encount.grantUniqueBonusScaling = false;
             var machine = EntityStateMachine.FindByCustomName(slot.characterBody.gameObject,"CorruptMode");
             machine.mainStateType = new SerializableEntityStateType(typeof(InfestedMode));
             machine.initialStateType = machine.mainStateType;
            if(!isHooked){
                isHooked = true;
                IL.EntityStates.VoidInfestor.Infest.FixedUpdate += (il) =>{
                  ILCursor c = new ILCursor(il);
                  if(c.TryGotoNext(x => x.MatchLdloc(4),x => x.MatchLdcI4(4))){
                    c.Index++;
                    c.Emit(OpCodes.Ldarg_0);
                    c.EmitDelegate<Func<CharacterMaster,EntityStates.VoidInfestor.Infest,CharacterMaster>>((orig,self) =>{
                       var squad = squads.Find((s) => s.ContainsMember(self.characterBody.master));
                       if(squad && BossGroup.FindBossGroup(orig.GetBody()) == null){
                         squad.AddMember(orig);
                       }
                       return orig;
                    });
                  }
                  while(c.TryGotoNext(x => x.MatchLdcI4(4),x => x.MatchCallOrCallvirt(out _))){
                    c.Index++;
                    c.Emit(OpCodes.Ldarg_0);
                    c.Emit(OpCodes.Ldloc,4);
                    c.EmitDelegate<Func<TeamIndex,EntityStates.VoidInfestor.Infest,CharacterMaster,TeamIndex>>((orig,self,master) => friendlyInfest.Value? (self.teamComponent.teamIndex == TeamIndex.Player && BossGroup.FindBossGroup(master.GetBody()) != null)? orig : self.teamComponent.teamIndex : orig);
                  }
                };
                IL.RoR2.GlobalEventManager.OnCharacterDeath += (il) =>{
                  ILCursor c = new ILCursor(il);
                  if(c.TryGotoNext(x => x.MatchLdstr("RoR2/DLC1/EliteVoid/VoidInfestorMaster.prefab")) && c.TryGotoNext(x => x.MatchLdcI4(4),x=>x.MatchCallOrCallvirt(typeof(CharacterMaster).GetProperty("teamIndex").GetSetMethod()))){
                    c.Emit(OpCodes.Ldarg_1);
                    c.EmitDelegate<Func<CharacterMaster,DamageReport,CharacterMaster>>((orig,report) => { 
                       var squad = squads.Find((s) => s.ContainsMember(report.victimMaster));
                       if(orig && squad){
                         squad.AddMember(orig);
                       }
                       return orig;
                    });
                    c.Index++;
                    c.MoveAfterLabels();
                    c.Emit(OpCodes.Ldarg_1);
                    c.EmitDelegate<Func<TeamIndex,DamageReport,TeamIndex>>((orig,report) => friendlyInfest.Value? report.victimTeamIndex : orig);
                  }
                };
                //Run.onRunDestroyGlobal += unsub;
             }

             return null;
             /*void unsub(Run run){
                Run.onRunDestroyGlobal -= unsub;
             }*/
         };
         def.onUnassign = (GenericSkill slot) =>{
             var machine = EntityStateMachine.FindByCustomName(slot.characterBody.gameObject,"CorruptMode");
             machine.mainStateType = new SerializableEntityStateType(typeof(UncorruptedMode));
             machine.initialStateType = machine.mainStateType;
             squads.Remove(slot.characterBody.GetComponent<ScriptedCombatEncounter>().combatSquad);
         };
         def.icon = Util.SpriteFromFile("Infestation.png"); 
         def.baseRechargeInterval = 0f;
         def.activationState = new SerializableEntityStateType(typeof(InfestedMode));
         def.activationStateMachineName = "CorruptMode";

         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(InfestedMode),out _);
         ContentAddition.AddEntityState(typeof(ExpelInfestation),out _);
     }

     public class InfestedMode : CorruptModeBase{
        //public float overCorruptTimer = 12f;
        public bool overCorrupt;
        public override void OnEnter(){
           base.OnEnter();
           overCorrupt = voidSurvivorController.minimumCorruption >= voidSurvivorController.maxCorruption;
           if(friendlyInfest.Value){
             characterBody.GetComponent<ScriptedCombatEncounter>().teamIndex = teamComponent.teamIndex;
           }
        }
        public override void FixedUpdate()
        {
                base.FixedUpdate();
                if (base.isAuthority){
                    if(voidSurvivorController && !overCorrupt && voidSurvivorController.corruption >= voidSurvivorController.maxCorruption && (bool)voidSurvivorController.bodyStateMachine)
                    {
                        voidSurvivorController.bodyStateMachine.SetInterruptState(new ExpelInfestation(), InterruptPriority.Skill);
                    }
                    else if(voidSurvivorController && voidSurvivorController.minimumCorruption < voidSurvivorController.maxCorruption){
                      overCorrupt = false;
                    }
                }
                if(NetworkServer.active && overCorrupt && characterBody.gameObject.GetComponent<ScriptedCombatEncounter>().combatSquad.defeatedServer){
                        voidSurvivorController.bodyStateMachine.SetInterruptState(new ExpelInfestation(), InterruptPriority.Skill); 
                }
        }
     }
     public class ExpelInfestation : CorruptionTransitionBase{
        static (FieldInfo,object)[] fields;
        static ExpelInfestation(){
          var sFields = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<EntityStateConfiguration>("RoR2/DLC1/VoidSurvivor/EntityStates.VoidSurvivor.EnterCorruptionTransition.asset").WaitForCompletion().serializedFieldsCollection.serializedFields;
          fields = new (FieldInfo,object)[]{};
          for(int i = 0; i < sFields.Length; i++){
             if(sFields[i].fieldName == "chargeEffectPrefab"){
                continue;
             }
             var sF = typeof(ExpelInfestation).GetField(sFields[i].fieldName,BindingFlags.Static | BindingFlags.Public);
             if(sF != null)
                 sF.SetValue(null,sFields[i]);
             var f = typeof(ExpelInfestation).GetField(sFields[i].fieldName);
             if(f != null && f.GetCustomAttribute<SerializeField>() != null)
               HG.ArrayUtils.ArrayAppend(ref fields,(f,sFields[i].fieldValue.GetValue(f)));
          }
        }
        public override void OnEnter(){
            var type = typeof(ExpelInfestation);
            foreach(var field in fields){
               field.Item1.SetValue(this,field.Item2);
            }
            base.OnEnter();
        }
        public override void FixedUpdate()
        {
                base.FixedUpdate();
                if ((bool)voidSurvivorController && NetworkServer.active)
                {
                        voidSurvivorController.AddCorruption(-100f);
                }
        }

        public override void OnFinishAuthority()
        {
                base.OnFinishAuthority();
                var infests = characterBody.gameObject.GetComponent<ScriptedCombatEncounter>();
                infests.BeginEncounter();
                infests.hasSpawnedServer = false;
                infests.combatSquad.defeatedServer = false;
                if ((bool)voidSurvivorController)
                {
                        voidSurvivorController.corruptionModeStateMachine.SetNextState(new InfestedMode());
                }
        }

     } 
    }
}
