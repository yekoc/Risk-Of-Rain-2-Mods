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
using EntityStates.Bandit2;

namespace PassiveAgression.Bandit
{
    public static class StarchUtil{
     public static SkillDef def;

     static StarchUtil(){
         LanguageAPI.Add("PASSIVEAGRESSION_BANDITSTARCH","Starch Bomb");
         LanguageAPI.Add("PASSIVEAGRESSION_BANDITSTARCH_DESC","<style=cIsDamage>Stunning.</style> Deal 75% damage,allies in area are <style=cIsUtility>cleansed from a debuff</style>.");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_BANDITSTARCH";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_BANDITSTARCH_DESC";
         def.baseRechargeInterval = 6f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Stealth";
         def.keywordTokens = new string[]{"KEYWORD_STUNNING"};
         def.activationState = new SerializableEntityStateType(typeof(StarchState));
         def.icon = Util.SpriteFromFile("StarchIcon.png");
         LoadoutAPI.AddSkillDef(def);
         LoadoutAPI.AddSkill(typeof(StarchState));
         }


     public class StarchState : BaseState {
         public static float duration;
         public static float blastAttackRadius;
         public static float blastAttackDamageCoefficient;
         public static float blastAttackProcCoefficient;
         public static float blastAttackForce;
         public static GameObject smokeBombEffect;
         public static string smokeBombMuzzleString;
         public static float shortHopVelocity;

         static StarchState(){
         }
         public override void OnEnter(){
             duration = ThrowSmokebomb.duration;
             blastAttackRadius = StealthMode.blastAttackRadius;
             blastAttackProcCoefficient = StealthMode.blastAttackProcCoefficient;
             blastAttackDamageCoefficient = 0.75f;
             blastAttackForce = StealthMode.blastAttackForce;
             smokeBombEffect = StealthMode.smokeBombEffectPrefab;
             smokeBombMuzzleString = StealthMode.smokeBombMuzzleString;
             shortHopVelocity = StealthMode.shortHopVelocity * 1.5f;
             base.OnEnter();
             PlayAnimation("Gesture, Additive", "ThrowSmokebomb", "ThrowSmokebomb.playbackRate", duration);
         }
         public override void FixedUpdate(){
             base.FixedUpdate();
             if(base.fixedAge >= duration){
                 outer.SetNextStateToMain();
             }
         }
         public override void OnExit(){
             base.OnExit();
             FireStarchBomb();
         }
         public override InterruptPriority GetMinimumInterruptPriority(){
             return InterruptPriority.PrioritySkill;
         }
         private void FireStarchBomb(){
             if(base.isAuthority){
                 new BlastAttack{
			radius = blastAttackRadius,
			procCoefficient = blastAttackProcCoefficient,
			position = base.transform.position,
			attacker = base.gameObject,
			crit = RoR2.Util.CheckRoll(base.characterBody.crit, base.characterBody.master),
			baseDamage = base.characterBody.damage * blastAttackDamageCoefficient,
			falloffModel = BlastAttack.FalloffModel.None,
			damageType = DamageType.Stun1s,
			baseForce = blastAttackForce,
                        attackerFiltering = AttackerFiltering.Default,
                        teamIndex = base.GetTeam()
                        }.Fire();
             }
             if(NetworkServer.active){
                 HurtBox[] starchy = new SphereSearch{
                     radius = blastAttackRadius,
                     origin = base.transform.position,
                     queryTriggerInteraction = QueryTriggerInteraction.UseGlobal,
                     mask = LayerIndex.entityPrecise.mask
                 }.RefreshCandidates().FilterCandidatesByDistinctHurtBoxEntities().FilterCandidatesByHurtBoxTeam(new TeamMask{a = (byte)(1L << (int)teamComponent.teamIndex)}).GetHurtBoxes();
                 foreach(var body in starchy.Select((s) => s.healthComponent.body)){
                     DotController.DotStack dot;
                     BuffIndex debuff;
                     if(PassiveAgression.Util.GetRandomDebuffOrDot(body,out debuff,out dot)){
                        if(debuff != BuffIndex.None){
                         body.ClearTimedBuffs(debuff);
                         continue;
                        }
                        var dotCont= DotController.dotControllerLocator[body.gameObject.GetInstanceID()];
                        if(dotCont){
                          dotCont.dotStackList.Remove(dot);
                          dotCont.OnDotStackRemovedServer(dot);
                          DotController.dotStackPool.Return(dot);
                        }
                     }
                 }
             }
             if(smokeBombEffect)
                EffectManager.SimpleMuzzleFlash(smokeBombEffect, base.gameObject, smokeBombMuzzleString, transmit: false);
             if(base.characterMotor)
             	base.characterMotor.velocity = new Vector3(base.characterMotor.velocity.x, shortHopVelocity, base.characterMotor.velocity.z);
         }
     }

    } 
}
