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
         LanguageAPI.Add("PASSIVEAGRESSION_VIENDTEARCORRUPT_DESC","Haphazardly open a tear to the void,filling the surrounding area with fog.");
         LanguageAPI.Add("PASSIVEAGRESSION_VIENDTEARCORRUPT_KEYWORD","<style=cKeywordName>【Corruption Upgrade】</style><style=cSub>Transform to fill an area with void fog instead.</style>");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_VIENDTEAR";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_VIENDTEAR_DESC";
         def.baseRechargeInterval = 12f;
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
         cdef.keywordTokens = new string[]{"PASSIVEAGRESSION_VIENDTEARCORRUPT_KEYWORD"};
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
            IL.RoR2.FogDamageController.EvaluateTeam += (il) => {
                ILCursor c = new ILCursor(il);
                ILLabel lab = c.DefineLabel();
                if(c.TryGotoNext(x => x.MatchBr(out lab)) && c.TryGotoNext(MoveType.After,x => x.MatchStloc(2))){
                    c.Emit(OpCodes.Ldloc,2);
                    c.EmitDelegate<Func<CharacterBody,bool>>((cb) => cb);
                    c.Emit(OpCodes.Brfalse,lab);
                    return;
                }
                PassiveAgressionPlugin.Logger.LogError("VoidFog fix hook failed,viend corrupted tear might not work depending on other mods");
            };
            isHooked = true;
            }
            return null;
         };

         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(TearState),out _);
         
         var obj = PrefabAPI.InstantiateClone(Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/gauntlets/GauntletEntranceOrb.prefab").WaitForCompletion(),"TearPrefab");
         GameObject.Destroy(obj.GetComponent<VoidRaidGauntletEntranceController>());
         GameObject.Destroy(obj.GetComponent<VoidRaidGauntletExitController>());
         obj.AddComponent<TriggerStayToTriggerEnter>();
         var col = obj.GetComponentInChildren<CapsuleCollider>();
         col.radius /= 10;
         col.height /= 10;
         col.height += 2;
         obj.AddComponent<DestroyOnTimer>().duration = 4f;
         TearState.prefab = obj;
         var card = ScriptableObject.CreateInstance<SpawnCard>();
         card.prefab = obj;
         card.nodeGraphType = RoR2.Navigation.MapNodeGroup.GraphType.Ground;
         card.sendOverNetwork = true;
         TearState.card = card;
         
     }

     public class TriggerStayToTriggerEnter : MonoBehaviour {
         public void OnTriggerStay(Collider col){
            gameObject.SendMessage("OnTriggerEnter",col);
         }
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

         public override void OnEnter(){
             var origState = new SwingMelee2();
             StartAimMode();
             PlayAnimation(origState.animationLayerName,origState.animationStateName,origState.animationPlaybackRateParameter,1f);
             initialPos = transform.position;
             base.OnEnter();
             if(isAuthority && characterMotor.velocity.sqrMagnitude > 0){
                movetoggle = 0f;
             }
         }
         public override void FixedUpdate(){
             base.FixedUpdate();
             if(fixedAge >= 0.5f && !instance){
                 instance = GameObject.Instantiate(prefab,initialPos,base.transform.rotation);
                 instance.transform.localScale /= 10;
                 var zone1 = instance.GetComponentInChildren<MapZone>();
                 if(Physics.Raycast(GetAimRay(),out var hit,Mathf.Infinity,LayerIndex.world.mask)){
                    instance2 = GameObject.Instantiate(prefab,hit.point,base.transform.rotation);
                 }
                 if(!(zone1 && instance2 && instance2.transform)){
                    Destroy(instance);
                    outer.SetNextStateToMain();
                    if(isAuthority){
                     activatorSkillSlot.AddOneStock();
                    }
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
             else if(fixedAge >= movetoggle && !instance && Vector3.Distance(initialPos,transform.position) > (characterBody.radius * 3)){
                initialPos = transform.position;
             }
             if(fixedAge > duration || (instance && instance2 && (transform.position - instance.transform.position).sqrMagnitude > (transform.position - instance2.transform.position).sqrMagnitude)){
                 outer.SetNextStateToMain();
             }
         }
        public override void HandleMovements(){
             if(isAuthority && fixedAge < movetoggle){
                 characterMotor.moveDirection = (-0.75f) * characterDirection.forward;
             }
             else{
                base.HandleMovements();
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
             indicator = PrefabAPI.InstantiateClone(Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/VoidCamp/VoidCamp.prefab").WaitForCompletion().transform.Find("mdlVoidFogEmitter/RangeIndicator").gameObject,"TearCAreaattempt");
             indicator.AddComponent<NetworkIdentity>();
             PrefabAPI.RegisterNetworkPrefab(indicator);
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
             //indicator.transform.SetParent(obj.transform);
             zone.radius = 20f;
             zone.isInverted = true;
             //zone.rangeIndicator = indicator.transform;

             fog.initialSafeZones = new BaseZoneBehavior[]{zone};
             obj.AddComponent<DestroyOnTimer>().duration = 10f;
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
                 var radiusInstance = GameObject.Instantiate(indicator,initialPos,base.transform.rotation);
                 if(NetworkServer.active){
                    NetworkServer.Spawn(instance);
                    NetworkServer.Spawn(radiusInstance);
                 }
                 instance.transform.localScale /= 10;
                 instance.GetComponent<SphereZone>().rangeIndicator = radiusInstance.transform;
                 if(Captain.RadiusPassive.supportPower != null && Captain.RadiusPassive.supportPower.Length > 0){
                   instance.GetComponent<SphereZone>().radius *= Captain.RadiusPassive.supportPower[(int)teamComponent.teamIndex];
                 }
                 //instance.GetComponent<SphereZone>().rangeIndicator.localPosition = new Vector3(0,0,0);
             }
             if(fixedAge > duration){
                 outer.SetNextStateToMain();
             }
         }
     }

    }
}
