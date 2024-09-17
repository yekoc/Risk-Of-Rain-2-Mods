using RoR2;
using RoR2.Skills;
using RoR2.Projectile;
using EntityStates;
using EntityStates.Mage.Weapon;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using RoR2.Orbs;
using static RoR2.DotController;

namespace PassiveAgression.Mage
{
    public static class BloodDotSkill{
     public static HuntressTrackingSkillDef def;
     public static GameObject muzzleFlash;
     public static System.Collections.Generic.Dictionary<DotController,bool[]> r2apiDotActive;
     public static GameObject effectPrefab;

     static BloodDotSkill(){
         var epiESC = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<EntityStateConfiguration>("RoR2/Base/Mage/EntityStates.Mage.Weapon.FireFireBolt.asset");
         var blood = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Material>("RoR2/Base/moon2/matBloodSiphon.mat");
         r2apiDotActive = typeof(DotAPI).GetField("ActiveCustomDots",(System.Reflection.BindingFlags)(-1)).GetValue(null) as System.Collections.Generic.Dictionary<DotController,bool[]>;

         LanguageAPI.Add("PASSIVEAGRESSION_MAGEBLOODRIPDOT","Conclude");
         LanguageAPI.Add("PASSIVEAGRESSION_MAGEBLOODRIPDOT_DESC","Inflict <style=cDeath>Pain</style> on an enemy, triggering all of their remaining DoTs at once.");
         
         def = ScriptableObject.CreateInstance<ConcludeTrackingSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_MAGEBLOODRIPDOT";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MAGEBLOODRIPDOT_DESC";
         def.baseRechargeInterval = 15f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = true;
         def.stockToConsume = 1;
         def.isCombatSkill = true;
         def.keywordTokens = new string[]{};
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(BloodDotSkillState));
         def.icon = Util.SpriteFromFile("ConcludeIcon.png");
         ContentAddition.AddSkillDef(def);
        
         effectPrefab = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("Prefabs/TemporaryVisualEffects/DoppelgangerEffect"),"PASSIVEAGRESSION_MAGEBLOODRIPDOT_VFX");
         GameObject.Destroy(effectPrefab.GetComponent<PostProcessDuration>());
         GameObject.Destroy(effectPrefab.transform.Find("PP").gameObject);
         GameObject.Destroy(effectPrefab.GetComponentInChildren<AkEvent>());
         effectPrefab.GetComponentInChildren<TemporaryVisualEffect>().exitComponents = new MonoBehaviour[]{effectPrefab.GetComponent<DestroyOnTimer>()};
         foreach(var rend in effectPrefab.GetComponentsInChildren<Renderer>()){
             rend.material = blood.WaitForCompletion();
         }

         ContentAddition.AddEntityState(typeof(BloodDotSkillState),out _);
     }

     public class ConclusionTracker : HuntressTracker {

         public new void Awake(){
             base.Awake();
             maxTrackingDistance = 70f;
             maxTrackingAngle = 30f;
         }
         
         public new void Start(){
             base.Start();
         }

         public new void OnEnable(){
             base.OnEnable();
         }

         public new void OnDisable(){
             base.OnDisable();
         }

         public new void MyFixedUpdate(float deltaTime)
            {
             base.MyFixedUpdate(deltaTime);
             if(!trackingTarget || (!DotController.dotControllerLocator.ContainsKey(trackingTarget.healthComponent.body.GetInstanceID()))){
                 search.sortMode = BullseyeSearch.SortMode.Angle;
                 search.RefreshCandidates();
                 search.FilterOutGameObject(base.gameObject);
                 trackingTarget = search.GetResults().FirstOrDefault((t) => DotController.FindDotController(t.healthComponent.body.gameObject));
                 indicator.targetTransform = trackingTarget ? trackingTarget.transform : null;
             }
         }
     }


     public class BloodDotSkillState : BaseState {
         public HuntressTracker tracker;
         private HurtBox targer;
         public float baseDuration = 0.5f;
         public float duration;
         public TemporaryVisualEffect effectInstance;

         public override void OnEnter(){
             base.OnEnter();
             tracker = GetComponent<HuntressTracker>();
             if(tracker && isAuthority){
                 targer = tracker.GetTrackingTarget();
             }
             duration = baseDuration / attackSpeedStat;
             characterBody?.SetAimTimer(duration+1f);
             PlayAnimation("Gesture Left, Additive", "FireGauntletLeft", "FireGauntlet.playbackRate", duration);
             PlayAnimation("Gesture Right, Additive", "FireGauntletRight", "FireGauntlet.playbackRate", duration);
             PlayAnimation("Gesture, Additive", "HoldGauntletsUp", "FireGauntlet.playbackRate", duration);
             targer?.healthComponent.body.UpdateSingleTemporaryVisualEffect(ref effectInstance,effectPrefab,targer.healthComponent.body.radius,true,"Head");
         }

         public override void FixedUpdate(){
             base.FixedUpdate();
             if(fixedAge >= duration){
                 outer.SetNextStateToMain();
             }
         }

         public override void OnExit(){
            base.OnExit();
            targer?.healthComponent.body.UpdateSingleTemporaryVisualEffect(ref effectInstance,effectPrefab,targer.healthComponent.body.radius,false,"Head");
            if(!NetworkServer.active){
                return;
            }
            DotController dotc;
            var hurt = targer.healthComponent.body.GetComponent<SetStateOnHurt>();
            if(hurt){
                hurt.SetPain();
            }
            if(DotController.dotControllerLocator.TryGetValue(targer.healthComponent.body.gameObject.GetInstanceID(),out dotc)){
                bool extraAttention = dotc.dotStackList.Any(d => float.IsPositiveInfinity(d.timer));
                for(DotIndex i = 0; (int)i < DotAPI.VanillaDotCount + DotAPI.CustomDotCount; i++){
                while(dotc.HasDotActive(i) && dotDefs[(int)i].interval != 0){
                    int remain = 0;
                    dotc.EvaluateDotStacksForType((DotIndex)i,0f,out remain);
                    dotc.EvaluateDotStacksForType((DotIndex)i,DotController.GetDotDef((DotIndex)i).interval,out remain);
                    if(extraAttention){
                        //Double If instead of && to (hopefully) ensure this check doesn't get 'optimized' into running everytime
                        if(dotc.dotStackList.Any(d => d.dotIndex == i && float.IsPositiveInfinity(d.timer))){
                          continue;
                        }
                    }
                    if(remain <= 0){
                        if( (int)i < DotAPI.VanillaDotCount){
                            dotc.NetworkactiveDotFlags &= ~(uint)(1 << (int)i);
                        }
                        else{
                            r2apiDotActive[dotc][(int)i - DotAPI.VanillaDotCount] = false;
                        }
                        break;
                    }
                }
                }
            }
         }

         public override InterruptPriority GetMinimumInterruptPriority(){
             return InterruptPriority.PrioritySkill;
         }

         public override void OnSerialize(NetworkWriter writer){
             writer.Write(HurtBoxReference.FromHurtBox(targer));
         }

         public override void OnDeserialize(NetworkReader reader){
             targer = reader.ReadHurtBoxReference().ResolveHurtBox();
         }
     }

     public class ConcludeTrackingSkillDef : HuntressTrackingSkillDef{
         public override BaseSkillInstanceData OnAssigned(GenericSkill skillSlot){
             var concTracker = skillSlot.characterBody.GetComponent<ConclusionTracker>();
             if(!concTracker){
                 concTracker = skillSlot.characterBody.gameObject.AddComponent<ConclusionTracker>();
             }
             else if(!concTracker.enabled){
                 concTracker.enabled = true;
             }
             return new InstanceData{
                 huntressTracker = concTracker
             };
         }

         public override void OnUnassigned(GenericSkill skillSlot){
             base.OnAssigned(skillSlot);
             var ins = skillSlot.skillInstanceData as InstanceData;
             if(ins == null || !ins.huntressTracker){
                 return;
             }
             ins.huntressTracker.enabled = false;
         }
     }

    } 
}
