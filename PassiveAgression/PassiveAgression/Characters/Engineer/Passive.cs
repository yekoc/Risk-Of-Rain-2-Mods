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
using static RoR2.CharacterBody;

#pragma warning disable CS0618
namespace PassiveAgression.Engineer
{
    public static class ScrapPassive{
     public static AssignableSkillDef def;
     public static CustomPassiveSlot slot;
     public static float[] scrapPower = null;
     public static bool isHooked = false;

     //Unused
     public static void ScrapBuffMinion(CharacterBody sender,RecalculateStatsAPI.StatHookEventArgs args){
         if(sender.bodyFlags.HasFlag(BodyFlags.Mechanical) && sender.master && sender.master.minionOwnership){
            float scrapPercent = 0;
            var inv = sender.master.minionOwnership.ownerMaster.inventory;
            foreach(var index in inv.itemAcquisitionOrder){
               var item = ItemCatalog.GetItemDef(index);
               if(item.tags.Contains(ItemTag.PriorityScrap) || item.tags.Contains(ItemTag.Scrap)){
                 scrapPercent += inv.GetItemCount(index) * item.deprecatedTier switch{
                    ItemTier.Tier2 => 0.03f,
                    ItemTier.Tier3 => 0.09f,
                    ItemTier.Boss => 0.10f,
                    _ => 0.02f
                 };
               }
            }
            Debug.Log(sender +" - " + scrapPercent);
            args.healthMultAdd += scrapPercent;
            args.regenMultAdd += scrapPercent;
            args.moveSpeedMultAdd += scrapPercent;
            args.damageMultAdd += scrapPercent;
            args.attackSpeedMultAdd += scrapPercent;
         }
     }
     public static void ScrapBuffTeam(CharacterBody sender,RecalculateStatsAPI.StatHookEventArgs args){
        if(sender.bodyFlags.HasFlag(BodyFlags.Mechanical)){
         var scrapPercent = scrapPower[(int)sender.teamComponent.teamIndex];
         if(scrapPercent < 0){
             scrapPower[(int)sender.teamComponent.teamIndex] = 0;
             scrapPercent = 0;
         }
         args.healthMultAdd += scrapPercent;
         args.regenMultAdd += scrapPercent;
         args.moveSpeedMultAdd += scrapPercent;
         args.damageMultAdd += scrapPercent;
         args.attackSpeedMultAdd += scrapPercent;
        }
     }

     public class ScrapTracker : SkillDef.BaseSkillInstanceData{
         public float contribution = 0f;
     }

     static ScrapPassive(){
         slot = new CustomPassiveSlot("RoR2/Base/Engi/EngiBody.prefab");
         LanguageAPI.Add("PASSIVEAGRESSION_ENGISCRAP","Field Tinkering");
         LanguageAPI.Add("PASSIVEAGRESSION_ENGISCRAP_DESC","Scrap in inventory provides <style=cIsUtility>mechanical</style> allies with increased stats.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_ENGISCRAP";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_ENGISCRAP_DESC";
         def.onAssign = (GenericSkill slot) => {
             if(scrapPower == null){
                scrapPower = new float[TeamCatalog.teamDefs.Length];
             }
             if(!isHooked){
                isHooked = true;
                RecalculateStatsAPI.GetStatCoefficients += ScrapBuffTeam;
                var inv = slot.characterBody.master.inventory;
                inv.onInventoryChanged += () =>{
                    var scrapTracker = slot.skillInstanceData as ScrapTracker;
                    scrapPower[(int)slot.characterBody.teamComponent.teamIndex] -= scrapTracker.contribution; 
                    var scrapPercent = 0f;
                    foreach(var index in inv.itemAcquisitionOrder){
                       var item = ItemCatalog.GetItemDef(index);
                       if(item.tags.Contains(ItemTag.PriorityScrap) || item.tags.Contains(ItemTag.Scrap)){
                         scrapPercent += inv.GetItemCount(index) * item.deprecatedTier switch{
                            ItemTier.Tier2 => 0.03f,
                            ItemTier.Tier3 => 0.09f,
                            ItemTier.Boss => 0.10f,
                            _ => 0.01f
                         };
                       }
                    }
                    scrapTracker.contribution = scrapPercent;
                    scrapPower[(int)slot.characterBody.teamComponent.teamIndex] += scrapTracker.contribution; 
                 };
                slot.characterBody.master.onBodyDestroyed += (body) =>{ 
                    var scrapTracker = slot.skillInstanceData as ScrapTracker;
                    scrapPower[(int)slot.characterBody.teamComponent.teamIndex] -= scrapTracker.contribution; 
                };
                Run.onRunDestroyGlobal += unsub;
             }

             return new ScrapTracker();
             void unsub(Run run){
                Run.onRunDestroyGlobal -= unsub;
                RecalculateStatsAPI.GetStatCoefficients -= ScrapBuffTeam;
                isHooked = false;
             }
         };
         def.onUnassign = (GenericSkill slot) =>{
         };
         def.icon = Util.SpriteFromFile("StandoffIcon.png"); 
         def.activationStateMachineName = "Body";
         def.activationState = EntityStateMachine.FindByCustomName(slot.bodyPrefab,"Body").mainStateType;
         def.baseRechargeInterval = 0f;
         ContentAddition.AddSkillDef(def);
     }
    }
}
#pragma warning restore CS0618
