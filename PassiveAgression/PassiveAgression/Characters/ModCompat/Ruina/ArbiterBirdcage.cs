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
using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using Mono.Cecil.Cil;

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
         def.icon = PassiveAgressionPlugin.unfinishedIcon;
         bdef = ScriptableObject.CreateInstance<BuffDef>();
         bdef.buffColor = Color.yellow;
         var sprite = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Texture2D>("RoR2/Base/Croco/texBuffRegenBoostIcon.tif").WaitForCompletion();
         bdef.iconSprite = Sprite.Create(sprite, new Rect(0.0f, 0.0f, sprite.width, sprite.height), new Vector2(0.5f, 0.5f), 100.0f);
         bdef.name = "PASSIVEAGRESSION_RUINABIRDCAGE_BUFF";
         bdef.canStack = false;
         bdef.isDebuff = true;

         projPrefab = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("Prefabs/Projectiles/SporeGrenadeProjectileDotZone"),"PASSIVEAGRESSION_RUINABIRDCAGE_PROJECTILE", false);
         GameObject.Destroy(projPrefab.GetComponent<ProjectileDotZone>());
         //GameObject.Destroy(projPrefab.transform.GetChild(0).gameObject);
         projPrefab.AddComponent<SphereZone>();
         var cageSphere = new GameObject("sphere");
         cageSphere.SetActive(false);
         cageSphere.AddComponent<Rigidbody>().isKinematic = true;
         cageSphere.AddComponent<SphereCollider>().isTrigger = true;
         cageSphere.layer = LayerIndex.world.intVal;
         cageSphere.AddComponent<BirdcageComponent>();
         cageSphere.transform.SetParent(projPrefab.transform);
         cageSphere.SetActive(true);
         PrefabAPI.RegisterNetworkPrefab(projPrefab);


         BirdcageComponent.tetherPrefab = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/TarTether"),"PASSIVEAGRESSION_RUINABIRDCAGE_TETHER",true);
         GameObject.Destroy(BirdcageComponent.tetherPrefab.GetComponent<TarTetherController>());
         BirdcageComponent.tetherPrefab.GetComponent<LineRenderer>().material = RiskOfRuinaMod.Modules.Asset.mainAssetBundle.LoadAsset<Material>("matChains");
         On.RoR2.RigidbodyMotor.FixedUpdate += (orig,self) =>{
             var cond = self.characterBody?.HasBuff(bdef) ?? false;
             var pos = self.rigid.position;
             orig(self);
             var newPos = pos + self.rigid.velocity;
             if(pos != newPos && cond){
                 Debug.Log(self.characterBody);
                 foreach(var zone in BirdcageComponent.birdZones.Where(z => z.IsInBounds(pos) && !z.IsInBounds(newPos))){
                     self.rigid.position = pos;
                     self.rigid.velocity = Vector3.zero;
                     self.rigid.angularVelocity = Vector3.zero;
                     break;
                 }
             }
         };
         //new MonoMod.RuntimeDetour.ILHook(typeof(KinematicCharacterController.KinematicCharacterMotor).GetMethod("InternalMoveCharacterPosition",(System.Reflection.BindingFlags)(-1)),MotorHook);
         
         R2API.ContentAddition.AddBuffDef(bdef);
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(AimBirdcageState),out _);
         ContentAddition.AddProjectile(projPrefab);

     }

        public static void MotorHook(ILContext il){
            var c = new ILCursor(il);
            int local = -1;
            if(c.TryGotoNext(x => x.MatchStloc(out local)) && local != (-1)){
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldarg_2);
                c.EmitDelegate<Func<int,KinematicCharacterController.KinematicCharacterMotor,Vector3,bool>>((orig,self,target) =>{
                        if(self.CharacterController is CharacterMotor cm && cm.body && cm.body.HasBuff(bdef)){
                          var outerTarget = self.TransientPosition + 2 * (target - self.TransientPosition);
                          foreach(var zones in BirdcageComponent.birdZones.Where(z => z.IsInBounds(self.TransientPosition) && !z.IsInBounds(outerTarget))){
                            cm.velocity = cm.isGrounded ? Vector3.zero : new Vector3(0f,1f,0f);
                            return false;
                          }  
                        }
                        return orig > 0;
                });
            }
        }
        public class AimBirdcageState : AimThrowableBase{
                static GameObject targetPrefab = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Huntress/HuntressArrowRainIndicator.prefab").WaitForCompletion();
                public override void OnEnter(){
                    projectilePrefab = projPrefab;
                    endpointVisualizerPrefab = targetPrefab;
                    baseMinimumDuration = 0.25f;
                    //maxDistance = 200;
                    endpointVisualizerRadiusScale = 4f;
                    base.OnEnter();
                    PlayAnimation("Gesture, Override", "Channel", "Channel.playbackRate", minimumDuration);
                }
                public override InterruptPriority GetMinimumInterruptPriority(){
                        return InterruptPriority.PrioritySkill;
                }
        }


        public class BirdcageComponent : MonoBehaviour{
            public ProjectileController proj;
            public SphereCollider zoneTrigger;
            public SphereZone parentZone;
            public static GameObject tetherPrefab;
            public Dictionary<CharacterBody,BezierCurveLine> draggedBodies = new();
            public static List<IZone> birdZones = new();

            public void Awake(){
                proj = transform.gameObject.GetComponentInParent<ProjectileController>();
                var cols = transform.gameObject.GetComponents<SphereCollider>();
                zoneTrigger = cols.First(z => z.isTrigger);
                parentZone = transform.gameObject.GetComponentInParent<SphereZone>();
                if(parentZone){
                    parentZone.Networkradius = 4;
                    birdZones.Add(parentZone);
                    parentZone.rangeIndicator = proj.gameObject.transform.GetChild(0);
                }
            }
            public void OnTriggerEnter(Collider col){
                var body = col.GetComponentInParent<CharacterBody>();
                if(body && !body.HasBuff(bdef) && FriendlyFireManager.ShouldSplashHitProceed(body.healthComponent,proj.teamFilter.teamIndex)){
                    body.AddBuff(bdef);
                    if(Util.BodyIsMitchell(body)){
                      draggedBodies.Add(body,GameObject.Instantiate(tetherPrefab,transform).GetComponent<BezierCurveLine>());
                    }
                    parentZone.Networkradius++;
                }
            }
            public void OnDestroy(){
                if(parentZone){
                 birdZones.Remove(parentZone);
                }
                foreach(var p in draggedBodies){
                     draggedBodies.Remove(p.Key);
                     Destroy(p.Value.gameObject);
                }
            }

            public void FixedUpdate(){
                if(!parentZone.rangeIndicator && proj?.ghost){
                    parentZone.rangeIndicator = proj.ghost.gameObject.transform;
                }
                foreach(var p in draggedBodies){
                   var body = p.Key;
                   if(!body){
                     draggedBodies.Remove(body);
                     Destroy(p.Value.gameObject);
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
               // zoneCollider.radius = parentZone.radius;
                zoneTrigger.radius = parentZone.radius - 0.2f;
                //exTrigger.radius = parentZone.radius * 1.2f;
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
                    if(Util.BodyIsMitchell(body)){
                      draggedBodies.Add(body,GameObject.Instantiate(tetherPrefab).GetComponent<BezierCurveLine>());
                    }
                    parentZone.Networkradius++;

                }
            }

            public void OnTriggerExit(Collider col){
                var body = col.GetComponentInParent<CharacterBody>();
                if(body && body.HasBuff(bdef)){
                    //body.RemoveBuff(bdef);
                }
            }
        }
    }
}
