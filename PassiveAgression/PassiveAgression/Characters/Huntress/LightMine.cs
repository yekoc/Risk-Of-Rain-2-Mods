using RoR2;
using RoR2.Skills;
using EntityStates;
using EntityStates.Huntress.HuntressWeapon;
using R2API;

namespace PassiveAgression.Huntress{

    public static class LightMine{

        public static SkillDef def;

        static LightMine(){
         var body = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<CharacterBody>("RoR2/Base/Huntress/HuntressBody.prefab");  
         LanguageAPI.Add("PASSIVEAGRESSION_HUNTMINE","Light Mine");
         LanguageAPI.Add("PASSIVEAGRESSION_HUNTMINE_DESC","Emit a floating mine that deals 300% damage when picked up.");
         def = UnityEngine.ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_HUNTMINE";
         (def as UnityEngine.ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_HUNTMINE_DESC";
         def.baseRechargeInterval = 4f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
         def.baseMaxStock = 4;
         def.activationState = new SerializableEntityStateType(typeof(LightMineState));
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(LightMineState),out _);
        }


        public class LightMineState : BaseState {
        
            static UnityEngine.GameObject minePrefab;
            static float duration = 1f;

            static LightMineState(){
               minePrefab = PrefabAPI.InstantiateClone(UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<UnityEngine.GameObject>("RoR2/Base/Tooth/HealPack.prefab").WaitForCompletion(),"DamagePack");
               var comp = minePrefab.GetComponentInChildren<HealthPickup>();
               if(comp){
                   var comp2 = comp.gameObject.AddComponent<DamagePickup>();
                   comp2.pickupEffect = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<UnityEngine.GameObject>("RoR2/Base/Huntress/HuntressGlaiveOrbEffect.prefab").WaitForCompletion();
                   comp2.baseObject = comp.baseObject;
               }
               var comp3 = minePrefab.GetComponentInChildren<VelocityRandomOnStart>();
               if(comp3){
                   comp3.minSpeed = 0;
                   comp3.maxSpeed = 5;
               }
               minePrefab.GetComponentInChildren<GravitatePickup>().acceleration /= 4f;
               minePrefab.GetComponentInChildren<UnityEngine.Rigidbody>().useGravity = false;
               minePrefab.GetComponentInChildren<DestroyOnTimer>().duration *= 10f;
               minePrefab.GetComponentInChildren<BeginRapidlyActivatingAndDeactivating>().delayBeforeBeginningBlinking *= 10f;
               Destroy(minePrefab.GetComponentInChildren<HealthPickup>());
            }
            public override void OnEnter(){
                base.OnEnter();
		PlayAnimation("Gesture", "FireGlaive", "FireGlaive.playbackRate", 2f);
                if(UnityEngine.Networking.NetworkServer.active){
                var obj = UnityEngine.GameObject.Instantiate(minePrefab,transform.position, transform.rotation);
                var comp = obj.GetComponent<TeamFilter>();
                if(comp){
                   comp.teamIndex = TeamIndex.Monster;
                }
                var comp2 = obj.GetComponentInChildren<DamagePickup>();
                if(comp2){
                  comp2.teamFilter = TeamMask.AllExcept(characterBody.teamComponent.teamIndex);
                  comp2.damage = damageStat * 3f;
                  comp2.owner = characterBody.gameObject;
                }
                UnityEngine.Networking.NetworkServer.Spawn(obj);
                }
            }

            public override void FixedUpdate(){
             base.FixedUpdate();
             if(base.fixedAge >= duration){
                 outer.SetNextStateToMain();
             }
            }

            public override InterruptPriority GetMinimumInterruptPriority(){
                return InterruptPriority.PrioritySkill;
            }
        }

    }
}
