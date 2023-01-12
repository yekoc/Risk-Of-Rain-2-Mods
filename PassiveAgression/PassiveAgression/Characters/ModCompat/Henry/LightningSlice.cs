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
using HenryMod.SkillStates.BaseStates;

namespace PassiveAgression.ModCompat
{
    public static class HenryYomiJC{
     public static SkillDef def;

     static HenryYomiJC(){
         LanguageAPI.Add("PASSIVEAGRESSION_HENRYYOMIJC","Lightning Slice");
         LanguageAPI.Add("PASSIVEAGRESSION_HENRYYOMIJC_DESC","Strike through space itself,dealing <style=cIsDamage>400% damage</style>.");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_HENRYYOMIJC";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_HENRYYOMIJC_DESC";
         def.baseRechargeInterval = 6f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(LightningEnter));
         def.icon = Util.SpriteFromFile("StarchIcon.png");
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(LightningEnter),out _);
         var hitboxObj = new GameObject("JCHitBoxHolder");
         var henryBody = HenryMod.Modules.Survivors.Henry.instance.bodyPrefab;
         hitboxObj.transform.SetParent(henryBody.transform);
         HG.ArrayUtils.ArrayAppend(ref henryBody.GetComponent<ChildLocator>().transformPairs,new ChildLocator.NameTransformPair{name = "LightningBox",transform = hitboxObj.transform});
         HenryMod.Modules.Prefabs.SetupHitbox(henryBody.GetComponent<CharacterBody>().modelLocator.modelTransform.gameObject,hitboxObj.transform,"Lightning");
     }

     public class LightningEnter : BaseMeleeAttack {

     }
    } 
}
