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
using static R2API.ItemAPI;
using static RoR2.RoR2Content.Items;
using static RoR2.DLC1Content.Items;

namespace PassiveAgression.ModCompat
{
    public static class ChefPantryPassive{
     public static AssignableSkillDef def;
     public static CustomPassiveSlot slot;
     public static ItemTag ingredientTag = (ItemTag)(-1);
     public static bool isHooked = false;

     static public void TakeStock(CharacterBody body,RecalculateStatsAPI.StatHookEventArgs args){
        if(def.IsAssigned(body)){
         var count = Util.CountUniqueItemWithTag(body.inventory,ingredientTag);
         count += (body.inventory.GetEquipmentIndex() == RoR2Content.Equipment.Fruit.equipmentIndex) ? 1 : 0;
         args.specialCooldownMultAdd -= 0.025f * count;
        }
     }

     static public void HandleItemTags(){
         if(ingredientTag == (ItemTag)(-1)){
            ingredientTag = FindItemTagByName("CHEFIngredient");
            ApplyTagToItem(ingredientTag,FlatHealth);
            ApplyTagToItem(ingredientTag,Mushroom);
            ApplyTagToItem(ingredientTag,HealWhileSafe);
            ApplyTagToItem(ingredientTag,Squid);
            ApplyTagToItem(ingredientTag,NovaOnLowHealth);
            ApplyTagToItem(ingredientTag,Seed);
            ApplyTagToItem(ingredientTag,TPHealingNova);
            ApplyTagToItem(ingredientTag,Clover);
            ApplyTagToItem(ingredientTag,Plant);
            ApplyTagToItem(ingredientTag,IncreaseHealing);
            ApplyTagToItem(ingredientTag,Hoof);
            ApplyTagToItem(ingredientTag,SprintBonus);
            ApplyTagToItem(ingredientTag,ParentEgg);
            ApplyTagToItem(ingredientTag,BeetleGland);
            ApplyTagToItem(ingredientTag,LunarPrimaryReplacement);
            ApplyTagToItem(ingredientTag,LunarSecondaryReplacement);
            ApplyTagToItem(ingredientTag,LunarUtilityReplacement);
            ApplyTagToItem(ingredientTag,LunarSpecialReplacement);
            ApplyTagToItem(ingredientTag,AttackSpeedAndMoveSpeed);
            ApplyTagToItem(ingredientTag,HealingPotion);
            ApplyTagToItem(ingredientTag,HealingPotionConsumed);
            ApplyTagToItem(ingredientTag,PermanentDebuffOnHit);
            ApplyTagToItem(ingredientTag,RandomEquipmentTrigger);
            ApplyTagToItem(ingredientTag,MushroomVoid);
            ApplyTagToItem(ingredientTag,BearVoid);
            ApplyTagToItem(ingredientTag,SlowOnHitVoid);
            ApplyTagToItem(ingredientTag,MissileVoid);
            ApplyTagToItem(ingredientTag,ExtraLifeVoid);
            ApplyTagToItem(ingredientTag,BleedOnHitVoid);
            ApplyTagToItem(ingredientTag,CloverVoid);
            ApplyTagToItem(ingredientTag,VoidMegaCrabItem);   
         }
     }

     static ChefPantryPassive(){
         AddItemTag("CHEFIngredient");
         slot = new CustomPassiveSlot((BepInEx.Bootstrap.Chainloader.PluginInfos["com.Gnome.ChefMod"].Instance as ChefMod.ChefPlugin).chefPrefab);
         LanguageAPI.Add("PASSIVEAGRESSION_CHEFPANTRY","Well Stocked Pantry");
         LanguageAPI.Add("PASSIVEAGRESSION_CHEFPANTRY_DESC","Having <style=cIsUtility>varied</style> and <style=cIsUtility>unique</style> ingredients allows <style=cIsDamage>specials</style> to be served more often.");
         LanguageAPI.Add("PASSIVEAGRESSION_CHEFPANTRY_KEYWORD","The first stack of any <style=cIsUtility>ingredient</style> item reduces the cooldown of CHEF's special skill by <style=cIsDamage>2.5%</style>.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_CHEFPANTRY";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_CHEFPANTRY_DESC";
         def.keywordTokens = new string[]{"PASSIVEAGRESSION_CHEFPANTRY_KEYWORD"};
         def.onAssign = (GenericSkill slot) => {
             if(!isHooked){
                HandleItemTags();
                RecalculateStatsAPI.GetStatCoefficients += TakeStock;
                Run.onRunDestroyGlobal += unsub;
                isHooked = true;
             }

             return null;
             void unsub(Run run){
                RecalculateStatsAPI.GetStatCoefficients -= TakeStock;
                Run.onRunDestroyGlobal -= unsub;
                isHooked = false;
             }
         };
         def.onUnassign = (GenericSkill slot) =>{

         };
         def.activationStateMachineName = "Body";
         def.activationState = EntityStateMachine.FindByCustomName(slot.bodyPrefab,"Body").mainStateType;
         def.icon = ChefMod.ChefPlugin.specialDef.icon;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.baseRechargeInterval = 0f;
         ContentAddition.AddSkillDef(def);
     }
    }
}
