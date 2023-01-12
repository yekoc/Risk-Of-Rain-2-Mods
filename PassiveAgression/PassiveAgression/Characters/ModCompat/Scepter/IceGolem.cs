
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

namespace PassiveAgression.Mage
{
    public static class IceGolemScepter{
     public static SkillDef def;
     public static GameObject golemPrefab;

     static IceGolemScepter(){
         LanguageAPI.Add("PASSIVEAGRESSION_MAGEICEARMOR_SCEPTER","Greater Snowman");
         LanguageAPI.Add("PASSIVEAGRESSION_MAGEICEARMOR_SCEPTERDESC","");
         def = ScriptableObject.Instantiate(IceGolemSpecial.def);
         def.skillNameToken = "PASSIVEAGRESSION_MAGEICEARMOR_SCEPTER";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MAGEICEARMOR_SCEPTERDESC";
         def.baseRechargeInterval = 6f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = true;
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(SnowmanState));
         def.icon = Util.SpriteFromFile("SnowmanIcon.png");


         golemPrefab = PrefabAPI.InstantiateClone(UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Titan/TitanBody.prefab").WaitForCompletion(),"MageSnowmanMech");
         GameObject.Destroy(golemPrefab.GetComponent<BaseAI>());
         golemPrefab.AddComponent<CharacterMaster>();
         golemPrefab.AddComponent<VehicleSeat>();
         golemPrefab.AddComponent<IceGolemSpecial.GolemMechBehaviour>();
         foreach(var l in golemPrefab.GetComponentsInChildren<Light>()) {l.color = new Color(0,0,1);}
         var esm = EntityStateMachine.FindByCustomName(golemPrefab,"Body");
         if(esm){
            esm.initialStateType = esm.mainStateType;
         }
         golemPrefab.GetComponent<CharacterBody>().bodyFlags |= CharacterBody.BodyFlags.Masterless;

         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(SnowmanState),out _); 
     }


     public class SnowmanState : IceGolemSpecial.SnowmanState {
         static SnowmanState(){
         }
         public override void OnEnter(){
             base.golemPrefab = IceGolemScepter.golemPrefab;
             base.OnEnter();
             characterBody.currentVehicle.transform.localScale /= 10;
         }
     }
    } 
}
