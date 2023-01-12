using RoR2;
using RoR2.Skills;
using EntityStates;
using RoR2.CharacterAI;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using EntityStates.VoidSurvivor.Weapon;
using static RoR2.MapZone;

namespace PassiveAgression.VoidSurvivor
{
    public static class TearUtil{
     public static AssignableSkillDef def;
     public static SkillDef cdef;
     public static bool isHooked;

     static TearUtil(){
         LanguageAPI.Add("PASSIVEAGRESSION_VIENDTEAR","「T?ea?r】");
         LanguageAPI.Add("PASSIVEAGRESSION_VIENDTEAR_DESC","Tear open a portal through the void,leading to the target location.");
         LanguageAPI.Add("PASSIVEAGRESSION_VIENDTEARCORRUPT_DESC","Haphazardly open a tear to the void,exposing the surrounding area to it's atmosphere");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_VIENDTEAR";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_VIENDTEAR_DESC";
         def.baseRechargeInterval = 6f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Body";
         def.keywordTokens = new string[]{};
         def.activationState = new SerializableEntityStateType(typeof(TearState));
         def.icon = Util.SpriteFromFile("TEAR.png");
         cdef = ScriptableObject.CreateInstance<SkillDef>();
         cdef.skillNameToken = "PASSIVEAGRESSION_VIENDTEAR";
         (cdef as ScriptableObject).name = def.skillNameToken + "CORRUPT";
         cdef.skillDescriptionToken = "PASSIVEAGRESSION_VIENDTEARCORRUPT_DESC";
         cdef.baseRechargeInterval = 10f;
         cdef.canceledFromSprinting = false;
         cdef.cancelSprintingOnActivation = false;
         cdef.activationStateMachineName = "Weapon";
         cdef.keywordTokens = new string[]{};
         cdef.activationState = new SerializableEntityStateType(typeof(TearCorruptState));
         cdef.icon = Util.SpriteFromFile("TEARC.png");

         def.onAssign = (skill) => {
            if(!isHooked){
            On.EntityStates.VoidSurvivor.CorruptMode.CorruptMode.OnEnter += (orig,self) =>{
                if(def.IsAssigned(self.characterBody,SkillSlot.Utility)){
                   self.utilityOverrideSkillDef = cdef;
                } 
                orig(self);
            };
            isHooked = true;
            }
            return null;
         };

         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(TearState),out _);
     }


     public class TearState : GenericCharacterMain,ISkillState {

         public static GameObject prefab;
         public static SpawnCard card;
         public GameObject instance;
         public GameObject instance2;
         public float movetoggle = 0.5f;
         public float duration = 1f;
         public Vector3 initialPos;

         public GenericSkill activatorSkillSlot {get;set;}
         

         static TearState(){
             var obj = PrefabAPI.InstantiateClone(Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/gauntlets/GauntletEntranceOrb.prefab").WaitForCompletion(),"TearPrefab");
             Destroy(obj.GetComponent<VoidRaidGauntletEntranceController>());
             Destroy(obj.GetComponent<VoidRaidGauntletExitController>());
             var col = obj.GetComponentInChildren<CapsuleCollider>();
             col.radius /= 10;
             col.height /= 10;
             obj.AddComponent<DestroyOnTimer>().duration = 4f;
             prefab = obj;
             card = ScriptableObject.CreateInstance<SpawnCard>();
             card.prefab = prefab;
             card.nodeGraphType = RoR2.Navigation.MapNodeGroup.GraphType.Ground;
             card.sendOverNetwork = true;
         }
         public override void OnEnter(){
             var origState = new SwingMelee2();
             StartAimMode();
             PlayAnimation(origState.animationLayerName,origState.animationStateName,origState.animationPlaybackRateParameter,1f);
             initialPos = transform.position;
             base.OnEnter();
         }
         public override void FixedUpdate(){
             base.FixedUpdate();
             if(fixedAge >= movetoggle && !instance){
                 instance = GameObject.Instantiate(prefab,initialPos,base.transform.rotation);
                 instance.transform.localScale /= 10;
                 var zone1 = instance.GetComponentInChildren<MapZone>();
                 if(Physics.Raycast(GetAimRay(),out var hit,Mathf.Infinity,LayerIndex.world.mask)){
                    instance2 = GameObject.Instantiate(prefab,hit.point,base.transform.rotation);
                 }
                 if(!(zone1 && instance2 && instance2.transform)){
                    Destroy(instance);
                    outer.SetNextStateToMain();
                    activatorSkillSlot.AddOneStock();
                    return;
                 }
                 instance2.transform.localScale /= 10;
                 //instance2.GetComponentInChildren<MapZone>().explicitDestination = instance.transform;
                 zone1.explicitDestination = instance2.transform;
                 if(NetworkServer.active){
                     NetworkServer.Spawn(instance);
                     NetworkServer.Spawn(instance2);
                 }
             }
             if(fixedAge > duration || (instance && instance2 && (transform.position - instance.transform.position).sqrMagnitude > (transform.position - instance2.transform.position).sqrMagnitude)){
                 outer.SetNextStateToMain();
             }
         }
        public override void HandleMovements(){
             if(fixedAge < movetoggle){
                 characterMotor.moveDirection = (-0.75f) * characterDirection.forward;
             }
         }
         public override void OnExit(){
             base.OnExit();
         }
         public override void UpdateAnimationParameters(){
             base.UpdateAnimationParameters();
             modelAnimator.SetBool(AnimationParameters.isSprinting,true);
         }

         public override InterruptPriority GetMinimumInterruptPriority(){
             return InterruptPriority.PrioritySkill;
         }
     }

     public class TearCorruptState : BaseState {

         public static GameObject prefab;
         public static SpawnCard card;
         internal static GameObject indicator;
         public GameObject instance;
         public float movetoggle = 0.5f;
         public float duration = 1f;
         public Vector3 initialPos;

         static TearCorruptState(){
             var obj = PrefabAPI.InstantiateClone(Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/gauntlets/GauntletEntranceOrb.prefab").WaitForCompletion(),"TearCPrefab");
             var obj2 = PrefabAPI.InstantiateClone(Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/VoidCamp/VoidCamp.prefab").WaitForCompletion(),"TearCAreaattempt",false);
             indicator = obj2.transform.Find("mdlVoidFogEmitter/RangeIndicator").gameObject;
             Destroy(obj.GetComponent<VoidRaidGauntletEntranceController>());
             Destroy(obj.GetComponent<VoidRaidGauntletExitController>());
             Destroy(obj.GetComponentInChildren<MapZone>());
             Destroy(obj.GetComponentInChildren<Collider>());
             var fog = obj.AddComponent<FogDamageController>();
             fog.dangerBuffDuration = 0.6f;
             fog.tickPeriodSeconds = 0.5f;
             fog.healthFractionPerSecond = 0.025f;
             fog.healthFractionRampCoefficientPerSecond = 0.15f;
             fog.dangerBuffDef = Addressables.LoadAssetAsync<BuffDef>("RoR2/Base/Common/bdVoidFogMild.asset").WaitForCompletion();
             var zone = obj.AddComponent<SphereZone>();
             indicator.transform.SetParent(obj.transform);
             zone.radius = 20f;
             zone.isInverted = true;
             zone.rangeIndicator = indicator.transform;
             indicator.transform.localPosition = new Vector3(0,0,0);
             indicator.transform.localScale *= 0.5f;
             
             fog.initialSafeZones = new BaseZoneBehavior[]{zone};
             obj.AddComponent<DestroyOnTimer>().duration = 20f;
             prefab = obj;

         }

         public override void OnEnter(){
             var origState = new SwingMelee2();
             StartAimMode();
             PlayAnimation(origState.animationLayerName,origState.animationStateName,origState.animationPlaybackRateParameter,1f);
             initialPos = transform.position;
             base.OnEnter();
         }
         public override void FixedUpdate(){
             base.FixedUpdate();
             if(fixedAge >= movetoggle && !instance){
                 instance = GameObject.Instantiate(prefab,initialPos,base.transform.rotation);
                 instance.GetComponent<SphereZone>().rangeIndicator.localPosition = new Vector3(0,0,0);
                 if(NetworkServer.active){
                    NetworkServer.Spawn(instance);
                 }
             }
             if(fixedAge > duration){
                 outer.SetNextStateToMain();
             }
         }
     }

    } 
}
