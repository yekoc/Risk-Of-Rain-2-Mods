using RoR2;
using RoR2.Skills;
using EntityStates;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static RoR2.BulletAttack;
using R2API;
using BepInEx.Configuration;

namespace PassiveAgression.Engineer
{
    public class ResonancePrimary {
        public static GameObject resonanceAttachment;
        public static SkillDef def;
        public static ConfigEntry<float> maxDamageCoef;
        public static ConfigEntry<bool> teamlessResonance;

        static ResonancePrimary(){
         var basePrefab = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/QuestVolatileBattery/QuestVolatileBatteryAttachment.prefab");
         var effectPrefab = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/QuestVolatileBattery/VolatileBatteryPreDetonation.prefab");
         var explosionPrefab = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/QuestVolatileBattery/VolatileBatteryExplosion.prefab");
         LanguageAPI.Add("PASSIVEAGRESSION_ENGIRESO","Cascading Resonance");
         maxDamageCoef = PassiveAgression.PassiveAgressionPlugin.config.Bind("EngiResonance","Maximum Damage Coefficient",4000f,$"Maximum damage that can be dealt by {Language.GetString("PASSIVEAGRESSION_ENGIRESO")} as a percentage of base damage.");
         teamlessResonance = PassiveAgression.PassiveAgressionPlugin.config.Bind("EngiResonance","Universal Friendly Fire",false,"Whether the resonance can be used on friendly targets as well.");
         LanguageAPI.Add("PASSIVEAGRESSION_ENGIRESO_DESC",$"Focus on an enemy to blow them up for up to <style=cIsDamage>{maxDamageCoef.Value}% damage</style>,requires unbroken line of sight.");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_ENGIRESO";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_ENGIRESO_DESC";
         def.icon = Util.SpriteFromFile("ResonanceJitter.png"); 
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(ResonanceState));
         def.canceledFromSprinting = true;
         def.cancelSprintingOnActivation = true;
         def.baseRechargeInterval = 0f;
         def.mustKeyPress = true;
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState<EnemyResonatingState>(out _);
         resonanceAttachment = R2API.PrefabAPI.InstantiateClone(basePrefab.WaitForCompletion(),"ResonatingNetworkAttachment");
         var esm = resonanceAttachment.GetComponent<EntityStateMachine>();
         esm.initialStateType = ContentAddition.AddEntityState<ResonanceState>(out _);
         esm.mainStateType = esm.initialStateType;
         resonanceAttachment.AddComponent<JitterBones>();
         resonanceAttachment.AddComponent<GenericOwnership>();
         EnemyResonatingState.explosionEffectPrefab = explosionPrefab.WaitForCompletion();
         EnemyResonatingState.vfxEffectPrefab = R2API.PrefabAPI.InstantiateClone(effectPrefab.WaitForCompletion(),"vfxResonance");
         GameObject.Destroy(EnemyResonatingState.vfxEffectPrefab.GetComponent<LoopSound>());
         GameObject.Destroy(EnemyResonatingState.vfxEffectPrefab.GetComponent<RTPCController>());
         GameObject.Destroy(EnemyResonatingState.vfxEffectPrefab.GetComponent<AkEvent>());
         GameObject.Destroy(EnemyResonatingState.vfxEffectPrefab.GetComponent<AkGameObj>());
        }
        public class ResonanceState : BaseSkillState
        {
                private List<NetworkedBodyAttachment> possible = new();
                private int animTimer = 0;
                public override void OnEnter(){
                        base.OnEnter(); 
		        PlayCrossfade("Gesture Left Cannon, Additive", "FireGrenadeLeft",0.1f);
		        PlayCrossfade("Gesture Right Cannon, Additive", "FireGrenadeRight",0.1f);
                }
                public override void FixedUpdate(){
                    base.FixedUpdate();
                    StartAimMode();
                    animTimer++;
                    if(animTimer > 25){
		     PlayCrossfade("Gesture Left Cannon, Additive", "FireGrenadeLeft",0.1f);
		     PlayCrossfade("Gesture Right Cannon, Additive", "FireGrenadeRight",0.1f);
                     animTimer = 0;
                    }
                    new BulletAttack{
                        damage = 0f,
                        radius = 1f,
                        hitCallback = (BulletAttack bulletAttack,ref BulletHit hitInfo) =>{
                            bool result = false;
                            if ((bool)hitInfo.collider)
                            {
                                    result = ((1 << hitInfo.collider.gameObject.layer) & (int)bulletAttack.stopperMask) == 0;
                            }
                            GameObject entity = hitInfo.entityObject;
                            if(entity && entity.GetComponent<CharacterBody>() && (teamlessResonance.Value || FriendlyFireManager.ShouldDirectHitProceed(hitInfo.hitHurtBox.healthComponent,bulletAttack.owner.GetComponent<TeamComponent>().teamIndex))){
                              NetworkedBodyAttachment.FindBodyAttachments(entity.GetComponent<CharacterBody>(),possible);
                              var index = possible.FindIndex((n) => n.name.Contains("ResonatingNetworkAttachment"));
                              var attach = (index == -1) ? GameObject.Instantiate(resonanceAttachment) : possible[index].gameObject;
                              attach.GetComponent<GenericOwnership>().ownerObject = bulletAttack.owner;
                              if(index == -1){
                               attach.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(entity);
                              }
                              var esm = attach.GetComponent<EntityStateMachine>();
                              var state = esm.state as EnemyResonatingState;
                              if(state == null){
                                state = new EnemyResonatingState();
                                esm.SetState(state);
                              }
                              state.progress += 0.1f;
                              possible.Clear();
                            }
                            return result;
                        },
                        smartCollision = true,
                        owner = base.gameObject,
                        aimVector = GetAimRay().direction,
                        origin = GetAimRay().origin,
                        
                    }.Fire();
                    if(base.isAuthority && !base.IsKeyDownAuthority()){
                        outer.SetNextStateToMain();
                    }
                }
                public override void OnExit(){
                        base.OnExit();

                }
                public override InterruptPriority GetMinimumInterruptPriority(){
                        return InterruptPriority.Pain;
                }

        }

        public class EnemyResonatingState : BaseBodyAttachmentState{
            public GameObject attacker;
            public CharacterBody attackerBody;
            public float progress = 0f;
            public float progressTarget = 10f;
            public float progressBufferFrame = -1f;
            public float oldProgress = -1f;
            public static GameObject explosionEffectPrefab;
            public static GameObject vfxEffectPrefab;
            public JitterBones bones;
            public float explosionRadius = 3f;
            public GameObject vfx;

            public override void OnEnter(){
              base.OnEnter();
              attacker = gameObject.GetComponent<GenericOwnership>().ownerObject;
              attackerBody = attacker.GetComponent<CharacterBody>();
              bones = base.GetComponent<JitterBones>();
              if(bones){
              bones.skinnedMeshRenderer = attachedBody.modelLocator.modelTransform.GetComponent<CharacterModel>()?.mainSkinnedMeshRenderer;
              bones.perlinNoiseFrequency = 1f;
              bones.headBonusStrength = 40f;
              }
              vfx = UnityEngine.Object.Instantiate(vfxEffectPrefab, attachedBody.corePosition,Quaternion.identity);
              vfx.transform.parent = (attachedBody.coreTransform) ? attachedBody.coreTransform : attachedBody.transform;
              vfx.transform.localPosition = Vector3.zero;
              vfx.transform.localRotation = Quaternion.identity;
              vfx.transform.localScale *= attachedBody.radius;
              if(attachedBody.isChampion){
                progressTarget = 20f;
              }
            }

            public override void FixedUpdate(){
                if(progress >= progressTarget){
                    Detonate();
                }
                if(oldProgress >= progress){
                    outer.SetNextState(new Idle());
                }
                if(bones){
                  bones.perlinNoiseStrength = progress / progressTarget;
                }
                oldProgress = progressBufferFrame;
                progressBufferFrame = progress;
            }
            public override void OnExit(){
               Destroy(vfx);
               if(bones){
                 bones.perlinNoiseStrength = 0f;
               }
            }

            public void Detonate()
            {
                    if ((bool)base.attachedBody)
                    {
                            Vector3 corePosition = base.attachedBody.corePosition;
                            float baseDamage = 0f;
                            if ((bool)base.attachedBody.healthComponent)
                            {
                                    baseDamage = Mathf.Min(base.attachedBody.healthComponent.fullCombinedHealth * 1.5f,attackerBody.damage * maxDamageCoef.Value / 100f);
                            }
                            EffectManager.SpawnEffect(explosionEffectPrefab, new EffectData
                            {
                                    origin = corePosition,
                                    scale = explosionRadius
                            }, transmit: true);
                            BlastAttack blastAttack = new BlastAttack();
                            blastAttack.position = corePosition;
                            blastAttack.radius = explosionRadius;
                            blastAttack.falloffModel = BlastAttack.FalloffModel.Linear;
                            blastAttack.attacker = attacker;
                            blastAttack.baseDamage = baseDamage;
                            blastAttack.baseForce = 5000f;
                            blastAttack.bonusForce = Vector3.zero;
                            blastAttack.attackerFiltering = AttackerFiltering.AlwaysHit;
                            blastAttack.procChainMask = default(ProcChainMask);
                            blastAttack.procCoefficient = 0.1f;
                            blastAttack.teamIndex = attackerBody.teamComponent.teamIndex;
                            blastAttack.Fire();
                            outer.SetNextState(new Idle());
                    }
            }
        }
    }
}
