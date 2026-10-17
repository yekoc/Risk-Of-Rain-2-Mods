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
using static RoR2.DLC2Content.Items;
using static RoR2.DLC3Content.Items;
using static RoR2.DLC4Content.Items;

namespace PassiveAgression.ModCompat
{
    public static class ChefPantryPassive{
     public static AssignableSkillDef def,def2;
     public static CustomPassiveSlot moddedSlot;
     public static ItemTag ingredientTag = (ItemTag)(-1);
     public static bool isHooked = false;
     public static bool isHookedGBox = false;

     static public void TakeStock(CharacterBody body,RecalculateStatsAPI.StatHookEventArgs args){
        if(body && def.IsAssigned(body)){
         var count = Util.CountUniqueItemWithTag(body.inventory,ingredientTag);
         var equip = body.inventory.GetEquipmentIndex();
         count += (equip == RoR2Content.Equipment.Fruit.equipmentIndex || equip == DLC2Content.Equipment.HealAndRevive.equipmentIndex) ? 1 : 0;
         args.specialSkill.cooldownReductionMultAdd += 0.025f * count;
        }
     }

     static private void NothingOnAStick(ILContext il){
         ILCursor c = new ILCursor(il);
         ILLabel labrador = null;
         if(c.TryGotoNext(x =>x.MatchBleUn(out labrador), x=> x.MatchLdstr("Chef/ChefFoodPickup"))){
             c.Index++;
             c.Emit(OpCodes.Ldarg_1);
             c.EmitDelegate<Func<DamageReport,bool>>((rep) =>{
                return !def.IsAssigned(rep.attackerBody);
             });
             c.Emit(OpCodes.Brfalse,labrador);
         }
     }

     static public void HandleItemTags(){
         if(ingredientTag == (ItemTag)(-1)){
            ingredientTag = FindItemTagByName("CHEFPantryIngredient");
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
            ApplyTagToItem(ingredientTag,RechargeableJump);
            ApplyTagToItem(ingredientTag,MushShot);
            ApplyTagToItem(ingredientTag,HelpWake);
            ApplyTagToItem(ingredientTag,BoostAllStats);
            ApplyTagToItem(ingredientTag,KnockBackHitEnemies);

         }
     }

     static ChefPantryPassive(){
         AddItemTag("CHEFPantryIngredient");
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
             if(!isHookedGBox && slot.characterBody.bodyIndex == BodyCatalog.FindBodyIndex(DLC2Content.BodyPrefabs.ChefBody)){
                 IL.RoR2.GlobalEventManager.OnCharacterDeath += NothingOnAStick;
             }

             return null;
             void unsub(Run run){
                RecalculateStatsAPI.GetStatCoefficients -= TakeStock;
                Run.onRunDestroyGlobal -= unsub;
                isHooked = false;
                if(isHookedGBox){
                    IL.RoR2.GlobalEventManager.OnCharacterDeath -= NothingOnAStick;
                    isHookedGBox = false;
                }
             }
         };
         def.onUnassign = (GenericSkill slot) =>{

         };
         def.activationStateMachineName = "Body";
         def.activationState = new SerializableEntityStateType(typeof(GenericCharacterMain));
         def.icon = Util.SpriteFromFile("ChefPantryDev.png");
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.baseRechargeInterval = 0f;
         ContentAddition.AddSkillDef(def);
     }

     public static void ModChef(){
         def2 = GameObject.Instantiate(def);
         def2.icon = ChefMod.ChefPlugin.specialDef.icon;
         moddedSlot = new CustomPassiveSlot((BepInEx.Bootstrap.Chainloader.PluginInfos["com.Gnome.ChefMod"].Instance as ChefMod.ChefPlugin).chefPrefab);
         def2.activationState = EntityStateMachine.FindByCustomName(moddedSlot.bodyPrefab,"Body").mainStateType;
     }
    }
}
