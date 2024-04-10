using RoR2;
using EntityStates;
using System;
using System.Linq;
using UnityEngine;
using R2API;
using RoR2.Skills;
using RobDriver;
using RobDriver.SkillStates.Driver;
using RobDriver.Modules.Components;

namespace PassiveAgression.ModCompat{
    public class DriverStarSeed : RobDriver.Modules.Weapons.BaseWeapon<DriverStarSeed>{
        internal static SkillDef def1;
        internal static SkillDef def2;
        public static Material defaultMat,shineMat,darkMat,indicatorMat;
        public static GameObject spherePrefab;
        public override string weaponNameToken => "PASSIVEAGRESSION_DRIVERSTAR";
        public override string weaponName => "Starseed";
        public override string weaponDesc => "A star in the palm of your hands.";
        public override string iconName => "";
        public override DriverWeaponTier tier => DriverWeaponTier.Unique;
        public override int shotCount => 20;
        public override Mesh mesh => null;
        public override Material material => null;
        public override DriverWeaponDef.AnimationSet animationSet => DriverWeaponDef.AnimationSet.Default;
        public override string calloutSoundString => "sfx_driver_callout_generic";
        public override string configIdentifier => "Starseed";
        public override float dropChance => 100f;
        public override bool addToPool => false;
        public override string uniqueDropBodyName => "Grandparent";
        public override SkillDef primarySkillDef => def1;
        public override SkillDef secondarySkillDef => def2;
        public override GameObject crosshairPrefab => null;
        
        static DriverStarSeed(){
         var matLoad1 = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Material>("RoR2/Base/Grandparent/matGrandparentEggDead.mat");
         var matLoad2 = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Material>("RoR2/Base/Grandparent/matGrandParentSunCore.mat");
         var matLoad3 = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Material>("RoR2/Base/Common/VFX/matGravsphereCore.mat");
         var matLoad4 = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Material>("RoR2/Base/Grandparent/matGrandParentSunFresnel.mat");
         var tetherLoad = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Blackhole/GravSphereTether.prefab");
         spherePrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
         GameObject.Destroy(spherePrefab.GetComponent<Collider>());
         spherePrefab.name = "PASSIVEAGRESSION_DRIVERSTAR_EFFECTPREFAB";
         spherePrefab.GetComponent<MeshRenderer>().sharedMaterial = defaultMat;
         spherePrefab.AddComponent<TeamFilter>();
         spherePrefab.AddComponent<TetherVfxOrigin>().tetherPrefab = tetherLoad.WaitForCompletion();
         var force = spherePrefab.AddComponent<RadialForce>();
         force.radius = 0f;
         force.forceMagnitude = -900f;
         GameObject.DontDestroyOnLoad(spherePrefab);
         spherePrefab.SetActive(false);
         def1 = ScriptableObject.CreateInstance<SkillDef>();
         def1.skillNameToken = "PASSIVEAGRESSION_DRIVERSTAR_SHINE";
         (def1 as ScriptableObject).name = def1.skillNameToken;
         def1.skillDescriptionToken = "PASSIVEAGRESSION_DRIVERSTAR_SHINE_DESC";
         def1.icon = PassiveAgressionPlugin.unfinishedIcon; 
         def1.baseRechargeInterval = 0f;
         def1.activationStateMachineName = "Weapon";
         def1.cancelSprintingOnActivation = false;
         def1.canceledFromSprinting = false;
         def1.activationState = new SerializableEntityStateType(typeof(DriverStarShineState));
         def1.mustKeyPress = true;
         def2 = ScriptableObject.CreateInstance<SkillDef>();
         def2.skillNameToken = "PASSIVEAGRESSION_DRIVERSTAR_SHINE";
         (def2 as ScriptableObject).name = def2.skillNameToken;
         def2.skillDescriptionToken = "PASSIVEAGRESSION_DRIVERSTAR_SHINE_DESC";
         def2.icon = PassiveAgressionPlugin.unfinishedIcon; 
         def2.baseRechargeInterval = 0f;
         def2.activationStateMachineName = "Weapon";
         def2.cancelSprintingOnActivation = false;
         def2.canceledFromSprinting = false;
         def2.mustKeyPress = true;
         def2.activationState = new SerializableEntityStateType(typeof(DriverStarCollapseState));
         ContentAddition.AddSkillDef(def1);
         ContentAddition.AddSkillDef(def2);
         ContentAddition.AddEntityState<DriverStarHoldState>(out _);
         ContentAddition.AddEntityState<DriverStarShineState>(out _);
         ContentAddition.AddEntityState<DriverStarCollapseState>(out _);
         defaultMat = matLoad1.WaitForCompletion();
         shineMat = matLoad2.WaitForCompletion();
         darkMat = matLoad3.WaitForCompletion();
         indicatorMat = matLoad4.WaitForCompletion();

        }

        public override void Init(){
            base.CreateLang();
            base.CreateWeapon();
            new MonoMod.RuntimeDetour.Hook(typeof(DriverController).GetMethod("PickUpWeapon"),WeaponChange);
        }

        public static void WeaponChange(Action<DriverController,DriverWeaponDef,float> orig,DriverController self,DriverWeaponDef weapon,float ammo){
            bool a = weapon == DriverStarSeed.instance.weaponDef;
            bool b = self.weaponDef == DriverStarSeed.instance.weaponDef;
            orig(self,weapon,ammo);
            if(a || b){
              EntityStateMachine.FindByCustomName(self.gameObject,"Shard").SetNextState(a ? new DriverStarHoldState() : new Idle() );
            }
        }

        public class DriverStarHoldState : BaseDriverSkillState{
            EntityStateMachine weapon;
            public GameObject effectSphere;

            public static EntityStateMachine.ModifyNextStateDelegate animDelegate = (EntityStateMachine m,ref EntityState s) => {
                m.state.PlayAnimation("Gesture, Override", "ReadyVoidButton", "Action.playbackRate", 0.8f);
                m.state.PlayAnimation("AimPitch", "SteadyAimPitch");
            };
            public override void OnEnter(){
                base.OnEnter(); 
                PlayAnimation("Gesture, Override", "ReadyVoidButton", "Action.playbackRate", 0.8f);
                PlayAnimation("AimPitch", "SteadyAimPitch");
                weapon = EntityStateMachine.FindByCustomName(gameObject,"Weapon");
                if(weapon){
                    weapon.nextStateModifier += animDelegate;
                }
                effectSphere = GameObject.Instantiate(spherePrefab,FindModelChild("HandL"));
                effectSphere.SetActive(true);
                effectSphere.transform.localRotation = Quaternion.identity;
                effectSphere.transform.localPosition = new Vector3(-0.5f,0f,-0.2f);
                effectSphere.transform.localScale /= 3f;
                effectSphere.GetComponent<MeshRenderer>().material = defaultMat;
                effectSphere.GetComponent<TeamFilter>().teamIndex = teamComponent.teamIndex;
            }


            public override void OnExit(){
                base.OnExit();
                PlayAnimation("AimPitch", "AimPitch");
                PlayAnimation("Gesture, Override", "BufferEmpty");
                if(weapon){
                  weapon.nextStateModifier -= animDelegate;
                }
            }
        }
        public class DriverStarShineState : BaseDriverSkillState{
            GameObject effectSphere;
            bool oldflag;
            GrandParentSunController sun;
            GameObject indicatorSphere;
            public override void OnEnter(){
                base.OnEnter();
                PlayAnimation("Gesture, Override", "PressVoidButton","Action.playbackRate", iDrive.ammo*20f);
                effectSphere = (EntityStateMachine.FindByCustomName(gameObject,"Shard").state as DriverStarHoldState)?.effectSphere;
                if(effectSphere){
                   effectSphere.GetComponent<MeshRenderer>().material = shineMat;
                   sun = effectSphere.AddComponent<GrandParentSunController>();
                }
                oldflag = ((characterBody.bodyFlags & CharacterBody.BodyFlags.OverheatImmune) != 0);
                if(!oldflag){
                  characterBody.bodyFlags |= CharacterBody.BodyFlags.OverheatImmune;
                }
                if(sun){
                    sun.maxDistance = 26f;
                    sun.minimumStacksBeforeApplyingBurns = 1;
                    sun.buffDef = RoR2Content.Buffs.Overheat;
                    sun.ownership.ownerObject = gameObject;
                    indicatorSphere = GameObject.Instantiate(spherePrefab,sun.transform);
                    indicatorSphere.transform.localScale *= 26f;
                    indicatorSphere.GetComponent<MeshRenderer>().material = indicatorMat;
                }
            }

            public override void FixedUpdate(){
                base.FixedUpdate();
                if(isAuthority && !IsKeyDownAuthority()){
                   outer.SetNextStateToMain();
                }
                if(iDrive){
                    iDrive.ConsumeAmmo(0.05f);
                }
            }

            public override void OnExit(){
                base.OnExit();
                if(effectSphere){
                    effectSphere.GetComponent<MeshRenderer>().material = defaultMat;
                }
                if(sun){
                    GameObject.Destroy(sun);
                    GameObject.Destroy(indicatorSphere);
                }
                if(!oldflag){
                 characterBody.bodyFlags &= ~CharacterBody.BodyFlags.OverheatImmune;
                }
            }
        }
        public class DriverStarCollapseState : BaseDriverSkillState{
            GameObject effectSphere;
            SphereSearch search;
            TetherVfxOrigin teth;
            public override void OnEnter(){
                base.OnEnter();
                PlayAnimation("Gesture, Override", "PressVoidButton","Action.playbackRate", iDrive.ammo * 20f);
                effectSphere = (EntityStateMachine.FindByCustomName(gameObject,"Shard").state as DriverStarHoldState)?.effectSphere;
                if(effectSphere){
                   effectSphere.GetComponent<MeshRenderer>().material = darkMat;
                   teth = effectSphere.GetComponent<TetherVfxOrigin>();
                }
                search = new SphereSearch{
                    radius = 40f,
                    mask = LayerIndex.entityPrecise.mask,
                };
            }

            public override void FixedUpdate(){
                base.FixedUpdate();
                if(isAuthority && !IsKeyDownAuthority()){
                   outer.SetNextStateToMain();
                }
                search.origin = effectSphere.transform.position;
                search.RefreshCandidates();
                search.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(teamComponent.teamIndex));
                search.FilterCandidatesByDistinctColliderEntities();
                search.OrderCandidatesByDistance();
                var targets = search.GetHurtBoxes();
                if(teth){
                    teth.SetTetheredTransforms(targets.Select(h => h?.transform).ToList());
                }
                foreach(var targetBody in targets.Select(h => h?.healthComponent?.body)){
                    Vector3 direction = (search.origin - targetBody.corePosition).normalized;
                    var physinfo = new PhysForceInfo{
                        force = direction * (15 - Vector3.Project(targetBody.rigidbody.velocity,direction).magnitude),
                        massIsOne = true,
                        ignoreGroundStick = true,
                        disableAirControlUntilCollision = true
                    };
                    if(targetBody.characterMotor){
                      targetBody.characterMotor.Motor.ForceUnground();
                      targetBody.characterMotor.disableAirControlUntilCollision = true;
                      targetBody.characterMotor.velocity += physinfo.force;
                    }
                    else{
                      var motor = targetBody.GetComponent<RigidbodyMotor>();
                        motor?.ApplyForceImpulse(physinfo);
                    }
                }

                if(iDrive){
                    iDrive.ConsumeAmmo(0.05f);
                }
            }

            public override void OnExit(){
                base.OnExit();
                if(effectSphere){
                    effectSphere.GetComponent<RadialForce>().radius = 0f;
                    effectSphere.GetComponent<MeshRenderer>().material = defaultMat;
                }
            }

        } 

    }
}
