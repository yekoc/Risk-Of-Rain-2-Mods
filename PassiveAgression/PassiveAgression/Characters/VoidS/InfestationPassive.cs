using RoR2;
using RoR2.Skills;
using EntityStates;
using RoR2.CharacterAI;
using System;
using System.Reflection;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;
using R2API;
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
     private static SpawnInfo[] spawns = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/VoidChest/VoidChest.prefab").WaitForCompletion().GetComponent<ScriptedCombatEncounter>().spawns; 

     static InfestationPassive(){
         LanguageAPI.Add("PASSIVEAGRESSION_VIENDBUG","『Incessant Infestation】");
         LanguageAPI.Add("PASSIVEAGRESSION_VIENDBUG_DESC","At full <style=cIsVoid>Corruption</style>, burst out a gaggle of Infestors.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_VIENDBUG";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_VIENDBUG_DESC";
         def.onAssign = (GenericSkill slot) => {
             var encount = slot.characterBody.gameObject.AddComponent<ScriptedCombatEncounter>();
             encount.spawns = spawns;
             for(int i = 0;i < spawns.Length; i++){
                 encount.spawns[i].explicitSpawnPosition = slot.characterBody.gameObject.transform;
                 encount.spawns[i].cullChance /= 4f;
             }
             encount.randomizeSeed = true;
             encount.combatSquad = slot.characterBody.gameObject.AddComponent<CombatSquad>();
             encount.teamIndex = TeamIndex.Void;
             encount.grantUniqueBonusScaling = false;
             var machine = EntityStateMachine.FindByCustomName(slot.characterBody.gameObject,"CorruptMode");
             machine.mainStateType = new SerializableEntityStateType(typeof(InfestedMode));
             machine.initialStateType = machine.mainStateType;
            /* if(!isHooked){
                isHooked = true;
                Run.onRunDestroyGlobal += unsub;
             }*/

             return null;
             /*void unsub(Run run){
                Run.onRunDestroyGlobal -= unsub;
             }*/
         };
         def.onUnassign = (GenericSkill slot) =>{
             var machine = EntityStateMachine.FindByCustomName(slot.characterBody.gameObject,"CorruptMode");
             machine.mainStateType = new SerializableEntityStateType(typeof(UncorruptedMode));
             machine.initialStateType = machine.mainStateType;

         };
         def.icon = Util.SpriteFromFile("Infestation.png"); 
         def.baseRechargeInterval = 0f;

         LoadoutAPI.AddSkillDef(def);
     }

     public class InfestedMode : CorruptModeBase{
        public float overCorruptTimer = 12f;
        public override void FixedUpdate()
        {
                base.FixedUpdate();
                if (base.isAuthority){ 
                    if(voidSurvivorController && voidSurvivorController.minimumCorruption >= voidSurvivorController.maxCorruption){
                        if(overCorruptTimer <= 0){
                         var infests = characterBody.gameObject.GetComponent<ScriptedCombatEncounter>();
                         infests.BeginEncounter();
                         infests.hasSpawnedServer = false;
                         overCorruptTimer = 12f;
                        }
                        overCorruptTimer -= 0.01f;
                    }
                    else if((bool)voidSurvivorController && voidSurvivorController.corruption >= voidSurvivorController.maxCorruption && (bool)voidSurvivorController.bodyStateMachine)
                    {
                        voidSurvivorController.bodyStateMachine.SetInterruptState(new ExpelInfestation(), InterruptPriority.Skill);
                    }
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
                if ((bool)voidSurvivorController)
                {
                        voidSurvivorController.corruptionModeStateMachine.SetNextState(new InfestedMode());
                }
        }

     } 
    }
}
