using BepInEx.Configuration;
using RoR2;
using RoR2.Skills;
using EntityStates;
using RoR2.CharacterAI;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using MonoMod.RuntimeDetour;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using static RoR2.Skills.SkillDef;
using HouseMod.SkillStates.House;
using HouseMod.SkillStates.House.TheGame;

namespace PassiveAgression.ModCompat{
    public static class HousePassive{
     public static HouseDeckDef[] defs;
     public static bool isHooked;
     public static ILHook hook,hook2;

     static HousePassive(){
       /*  LanguageAPI.Add("PASSIVEAGRESSION_HOUSEDECKTAROT", "Arcanum Arcana" );
         LanguageAPI.Add("PASSIVEAGRESSION_HOUSEDECKTAROT_DESC","The Funny Tarot.");
         var def = ScriptableObject.CreateInstance<HouseDeckDef>();
         def.skillNameToken = "PASSIVEAGRESSION_HOUSEDECKTAROT";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_HOUSEDECKTAROT_DESC";
         def.activationStateMachineName = "Body";
         def.icon = Util.SpriteFromFile("GloomIcon.png"); 
         def.baseRechargeInterval = 0f;
         def.activationStateMachineName = "Body";
         def.activationState = new SerializableEntityStateType(typeof(HouseMod.SkillStates.House.UseCard));
         def.cards = new int[]{
             11,
             19,
             19,
             19
         };
         ContentAddition.AddSkillDef(def);
         hook2 = new ILHook(typeof(HouseMod.HousePlugin).GetMethod(nameof(HouseMod.HousePlugin.CharacterSelectController_OnEnable),(BindingFlags)(-1)),DeckShowHook);
         */defs = new HouseDeckDef[]{};
         //HG.ArrayUtils.ArrayAppend(ref defs,def);
     }

     public static void DeckHook(ILContext il){
         ILCursor c = new(il);
         if(c.TryGotoNext(x => x.MatchStfld(typeof(HousePassiveManager).GetField(nameof(HousePassiveManager.Initialised),(BindingFlags)(-1))))){
           c.GotoPrev(MoveType.AfterLabel,x => x.MatchBr(out _));
           c.Emit(OpCodes.Ldarg_0);
           c.Emit(OpCodes.Ldloc,5);
           c.EmitDelegate<Action<HousePassiveManager,GenericSkill>>((self,skill) =>{
             var def = skill.skillDef as HouseDeckDef;
             if(def){
               self.gameDeck = def.cards;
             }
           });
         }
         else{
           PassiveAgressionPlugin.Logger.LogError("House Deck addition hook failed.");
         }
     }

     public static void DeckShowHook(ILContext il){
         ILCursor c = new(il);
         if(c.TryGotoNext(MoveType.After,x => x.MatchStsfld(typeof(HouseLoadoutOverview).GetField(nameof(HouseLoadoutOverview.OverviewPanel),(BindingFlags)(-1))))){
           c.EmitDelegate<Action>(() =>{
             var gameobject = HouseLoadoutOverview.OverviewPanel;
             var FullDeckTransform = gameobject.GetComponent<ChildLocator>().FindChild("FullHouse");
             foreach(var deck in defs){
               var newObject = GameObject.Instantiate(FullDeckTransform.gameObject,FullDeckTransform.parent);
               newObject.name = (deck as ScriptableObject).name;
               foreach(var val in deck.cards.Distinct()){
                 var card = HouseCardDefs.cards[val];
                 var button = GameObject.Instantiate(HouseMod.Modules.Assets.HouseCSSIcon,newObject.transform);
                 var tooltip = button.AddComponent<HouseMod.Modules.Components.TooltipComponent>();
                 button.GetComponent<UnityEngine.UI.Image>().sprite = card.cardIcon;
                 tooltip.tooltipName = card.name;
                 tooltip.tooltipColor = card.color;
                 tooltip.tooltipDescription = card.description;
                 tooltip.Awake();
                 tooltip.UpdateCardTooltips();
               }
             }
           });
         }
         else{
           PassiveAgressionPlugin.Logger.LogError("House Custom Deck Display hook failed.");
         }
     }

     public class HouseDeckDef : SkillDef{
       public System.Collections.Generic.List<int> cards;
       public override BaseSkillInstanceData OnAssigned(GenericSkill skillSlot){
         if(!isHooked){
           isHooked = true;
           hook = new ILHook(typeof(HousePassiveManager).GetMethod(nameof(HousePassiveManager.Initialise),(BindingFlags)(-1)),DeckHook);

           RoR2.Run.onRunDestroyGlobal += unhooker;
         }
         return null;
         void unhooker(Run run){
           if(isHooked){
             isHooked = false;
             hook.Free();
           }
           RoR2.Run.onRunDestroyGlobal -= unhooker;
         }
       }
     }

    }
}
