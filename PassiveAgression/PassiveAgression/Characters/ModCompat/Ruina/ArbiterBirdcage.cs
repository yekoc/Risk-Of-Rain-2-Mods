using RoR2;
using RoR2.Projectile;
using RoR2.Skills;
using EntityStates;
using R2API;
using System;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;
using System.Collections.Generic;

namespace PassiveAgression.ModCompat
{
    public static class ArbiterBirdcage{
     public static SkillDef def;
     public static BuffDef bdef;
     public static GameObject projPrefab;

     static ArbiterBirdcage(){
         LanguageAPI.Add("PASSIVEAGRESSION_RUINABIRDCAGE","Birdcage");
         LanguageAPI.Add("PASSIVEAGRESSION_RUINABIRDCAGE_DESC","");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_RUINABIRDCAGE";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_RUINABIRDCAGE_DESC";
         def.baseRechargeInterval = 20f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
         //def.keywordTokens = new string[]{"PASSIVEAGRESSION_RUINABIRDCAGE_KEYWORD"};
         def.activationState = new SerializableEntityStateType(typeof(AimBirdcageState));
         def.icon = Util.SpriteFromFile("StimShot.png");
         bdef = ScriptableObject.CreateInstance<BuffDef>();
         bdef.buffColor = Color.yellow;
         var sprite = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Texture2D>("RoR2/Base/Croco/texBuffRegenBoostIcon.tif").WaitForCompletion();
         bdef.iconSprite = Sprite.Create(sprite, new Rect(0.0f, 0.0f, sprite.width, sprite.height), new Vector2(0.5f, 0.5f), 100.0f);
         bdef.name = "PASSIVEAGRESSION_RUINABIRDCAGE_BUFF";
         bdef.canStack = false;
         bdef.isDebuff = true;

         projPrefab = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("Prefabs/Projectiles/SporeGrenadeProjectileDotZone"),"PASSIVEAGRESSION_RUINABIRDCAGE_PROJECTILE",true);
         GameObject.Destroy(projPrefab.GetComponent<ProjectileDotZone>());
         GameObject.Destroy(projPrefab.transform.GetChild(0).gameObject);
         projPrefab.AddComponent<SphereZone>();
         var cageSphere = new GameObject("sphere",new Type[]{typeof(BirdcageComponent)});
         cageSphere.AddComponent<SphereCollider>().isTrigger = true;
         cageSphere.transform.SetParent(projPrefab.transform);

         BirdcageComponent.tetherPrefab = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/TarTether"),"PASSIVEAGRESSION_RUINABIRDCAGE_TETHER",true);
         BirdcageComponent.tetherPrefab.GetComponent<LineRenderer>().material = RiskOfRuinaMod.Modules.Asset.mainAssetBundle.LoadAsset<Material>("matChains");
         
         
         R2API.ContentAddition.AddBuffDef(bdef);
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(AimBirdcageState),out _);
         ContentAddition.AddProjectile(projPrefab);

     }

        public class AimBirdcageState : AimThrowableBase{
                public override void OnEnter(){
                    projectilePrefab = projPrefab;
                    base.OnEnter();
                }
                public override InterruptPriority GetMinimumInterruptPriority(){
                        return InterruptPriority.PrioritySkill;
                }
        }

        public class BirdcageComponent : MonoBehaviour{
            public ProjectileController proj;
            public SphereCollider zoneCollider;
            public SphereZone parentZone;
            public static GameObject tetherPrefab;
            public Dictionary<CharacterBody,BezierCurveLine> draggedBodies = new();

            public void Start(){
                proj = transform.parent.GetComponentInParent<ProjectileController>();
                zoneCollider = transform.parent.GetComponent<SphereCollider>();
                parentZone = transform.parent.GetComponentInParent<SphereZone>();
            }
            public void OnTriggerEnter(Collider col){
                var body = col.GetComponentInParent<CharacterBody>();
                if(body && !body.HasBuff(bdef) && FriendlyFireManager.ShouldSplashHitProceed(body.healthComponent,proj.teamFilter.teamIndex)){
                    body.AddBuff(bdef);
                    if(!Util.BodyIsMitchell(body)){
                      Physics.IgnoreCollision(zoneCollider,col,false);
                    }
                    else{
                      draggedBodies.Add(body,GameObject.Instantiate(tetherPrefab).GetComponent<BezierCurveLine>());
                    }
                    parentZone.radius++;
                }
            }

            public void FixedUpdate(){
                foreach(var body in draggedBodies.Keys){
                   if(!body){
                     Destroy(draggedBodies[body].gameObject);
                     continue;
                   }
                   Vector3 vec = (transform.position - body.corePosition).normalized * 12f * Time.fixedDeltaTime;
                   if(body.characterMotor){
                     body.characterMotor.rootMotion += vec;
                   }
                   else if(body.rigidbody){
                     body.rigidbody.velocity += vec;
                   }
                }
            }

            public void Update(){
                foreach(var body in draggedBodies.Keys){
                    if(body){
                        draggedBodies[body].endTransform.position = body.corePosition;
                    }
                }
            }

            public void OnTriggerStay(Collider col){
                var body = col.GetComponentInParent<CharacterBody>();
                if(body && !body.HasBuff(bdef) && FriendlyFireManager.ShouldSplashHitProceed(body.healthComponent,proj.teamFilter.teamIndex)){
                    body.AddBuff(bdef);
                }
            }
        }
    }
}
