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
    public static class IceGolemSpecial{
     public static AssignableSkillDef def;
     public static bool isHooked;
     public static GameObject golemPrefab;
     public static Material iceMat;

     static IceGolemSpecial(){
         LanguageAPI.Add("PASSIVEAGRESSION_MAGEICEARMOR","Snowsculpt");
         LanguageAPI.Add("PASSIVEAGRESSION_MAGEICEARMOR_DESC","Encase yourself in a body of snow,protecting you from harm until it is broken or you leave it.");

         var mat = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Material>("RoR2/Base/Common/VFX/matIsFrozen.mat");

         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_MAGEICEARMOR";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MAGEICEARMOR_DESC";
         def.baseRechargeInterval = 6f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = true;
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(SnowmanState));
         def.icon = Util.SpriteFromFile("SnowmanIcon.png");

         def.onAssign += (skill) =>{
            if(!isHooked){
              On.RoR2.CharacterMaster.GetBodyObject += (orig,self) => {
                var result = orig(self);
                if(result){
                var body = result.GetComponent<CharacterBody>();
                if(body && body.currentVehicle && body.currentVehicle.gameObject && body.currentVehicle.gameObject.GetComponent<GolemMechBehaviour>()){
                  return body.currentVehicle.gameObject;
                }}
                return result;
              };
              On.RoR2.CharacterModel.UpdateOverlays += (orig,self) =>{
                orig(self);
                if(self.body && self.body.GetComponent<GolemMechBehaviour>()){
                  self.currentOverlays[self.activeOverlayCount < CharacterModel.maxOverlays? self.activeOverlayCount++ : (CharacterModel.maxOverlays - 1)] = iceMat; 
                }
              };
              On.RoR2.CharacterMaster.OnBodyDeath += (orig,self,body) =>{
                if(!body.gameObject.GetComponent<GolemMechBehaviour>()){
                    orig(self,body);
                }
              };
              IL.RoR2.GlobalEventManager.OnCharacterDeath += (il) =>{
                 ILCursor c = new ILCursor(il);
                 if(c.TryGotoNext(x => x.MatchCallOrCallvirt<GlobalEventManager>("OnPlayerCharacterDeath")) && c.TryGotoPrev(x => x.MatchCallOrCallvirt(typeof(UnityEngine.Object).GetMethod("op_Implicit",(System.Reflection.BindingFlags)(-1))),x => x.MatchBrfalse(out _))){
                    c.Index++;
                    c.Emit(OpCodes.Ldarg_1);
                    c.EmitDelegate<Func<DamageReport,bool>>((report) => report.victimBody.gameObject.GetComponent<GolemMechBehaviour>());
                    c.Emit(OpCodes.And);
                 }
              };
              isHooked = true;
            }
            return null;
         };

         golemPrefab = PrefabAPI.InstantiateClone(UnityEngine.AddressableAssets.Addressables.LoadAsset<GameObject>("RoR2/Base/Golem/GolemBody.prefab").WaitForCompletion(),"MageSnowmanMech");
         GameObject.Destroy(golemPrefab.GetComponent<BaseAI>());
         golemPrefab.AddComponent<CharacterMaster>();
         golemPrefab.AddComponent<VehicleSeat>();
         golemPrefab.AddComponent<GolemMechBehaviour>();
         foreach(var l in golemPrefab.GetComponentsInChildren<Light>()) {l.color = new Color(0,0,1);}
         var esm = EntityStateMachine.FindByCustomName(golemPrefab,"Body");
         if(esm){
            esm.initialStateType = esm.mainStateType;
         }
         var locat = golemPrefab.GetComponent<SkillLocator>();
         locat.primary = null;
         locat.secondary = null;
         locat.utility = null;
         locat.special = null;
         golemPrefab.GetComponent<CharacterBody>().bodyFlags |= CharacterBody.BodyFlags.Masterless;

         iceMat = mat.WaitForCompletion();
         LoadoutAPI.AddSkillDef(def);
         LoadoutAPI.AddSkill(typeof(SnowmanState));
    
     }

     public class GolemMechBehaviour : MonoBehaviour {
        VehicleSeat seat;

        public void Awake(){
          seat = GetComponent<VehicleSeat>();
          seat.seatPosition = transform;
          seat.exitPosition = seat.seatPosition;
          seat.passengerState = new SerializableEntityStateType(typeof(Idle));

          seat.onPassengerEnter += (pass) =>{
            CharacterBody body = pass.GetComponent<CharacterBody>();
            if(body){
              body.healthComponent.godMode = true;
              pass.GetComponent<Collider>().enabled = false;
            }
            foreach(var cam in CameraRigController.readOnlyInstancesList){
              if(cam.target == pass){
                cam.target = gameObject;
              }
            }
         };
         seat.onPassengerExit += (pass) =>{
            CharacterBody body = pass.GetComponent<CharacterBody>();
            if(body){
              body.healthComponent.godMode = false;
              pass.GetComponent<Collider>().enabled = true;
              EntityStateMachine.FindByCustomName(pass,"Body")?.SetNextStateToMain();
            }
            foreach(var cam in CameraRigController.readOnlyInstancesList){
              if(cam.target == gameObject){
                cam.target = pass;
              }
            }
            if(NetworkServer.active)
              GetComponent<CharacterBody>().healthComponent.Suicide();
         };
        }
        
        public void FixedUpdate(){
            var body = GetComponent<CharacterBody>();
            if(body && body.inputBank.interact.justPressed)
              seat.EjectPassenger();
        }

     }


     public class SnowmanState : BaseState {
         public static float duration = 0.5f;
         public static GameObject muzzleFlash;
         public GameObject golemPrefab;
         static SnowmanState(){
             muzzleFlash = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Mage/MuzzleflashMageIceLarge.prefab").WaitForCompletion();
         }
         public override void OnEnter(){
	     PlayAnimation("Gesture, Additive", "PrepWall", "PrepWall.playbackRate", duration);
             golemPrefab = IceGolemSpecial.golemPrefab; 
             base.OnEnter();
         }
         public override void FixedUpdate(){
             base.FixedUpdate();
             if(base.fixedAge >= duration){
                 outer.SetNextStateToMain();
             }
         }
         public override void OnExit(){
             base.OnExit();
             PlayAnimation("Gesture, Additive", "FireWall");
             EffectManager.SimpleMuzzleFlash(muzzleFlash, base.gameObject, "MuzzleLeft", transmit: true);
             EffectManager.SimpleMuzzleFlash(muzzleFlash, base.gameObject, "MuzzleRight", transmit: true);
             if(NetworkServer.active){
                var gameObject = GameObject.Instantiate(golemPrefab,transform.position,transform.rotation);
                var seat = gameObject.GetComponent<VehicleSeat>();
                gameObject.GetComponent<TeamComponent>().teamIndex = teamComponent.teamIndex;
                var body = gameObject.GetComponent<CharacterBody>();
                var master = gameObject.GetComponent<CharacterMaster>();
                //master.bodyInstanceObject = gameObject;
                //body.masterObject = gameObject;
                //body.inventory = characterBody.inventory;
                body.masterObject = characterBody.masterObject;

                if(seat){
                  seat.AssignPassenger(characterBody.gameObject);
                  NetworkUser clientAuthorityOwner = characterBody?.master?.playerCharacterMasterController?.networkUser;
                  if(clientAuthorityOwner){
                   NetworkServer.SpawnWithClientAuthority(gameObject,clientAuthorityOwner.gameObject);
                  }
                  else{
                   NetworkServer.Spawn(gameObject);
                  }
                }
             }
         }
         public override InterruptPriority GetMinimumInterruptPriority(){
             return InterruptPriority.PrioritySkill;
         }
     }

    } 
}
