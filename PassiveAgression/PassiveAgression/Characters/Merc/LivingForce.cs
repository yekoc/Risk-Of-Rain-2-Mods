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
using EntityStates.Merc;
using static R2API.DamageAPI;

namespace PassiveAgression.Merc
{
    public static class LivingForce{
     public static AssignableSkillDef def;
     public static ModdedDamageType damageType;
     public static BuffDef bdef;

     static LivingForce(){
         LanguageAPI.Add("PASSIVEAGRESSION_MERCLIVING","Living Force");
         LanguageAPI.Add("PASSIVEAGRESSION_MERCLIVING_DESC","<style=cIsDamage>Slayer</style>. Unleash a finishing blow for <style=cIsDamage>350% damage</syle>. Hold to release your swords current aspect. Kills <style=cIsUtility>absorb the targets aspect into your blade</style>.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_MERCLIVING";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MERCLIVING_DESC";
         def.baseRechargeInterval = 6f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
         def.keywordTokens = new string[]{"KEYWORD_SLAYER"};
         def.activationState = new SerializableEntityStateType(typeof(ForceAttackState));
         def.icon = Util.SpriteFromFile("Showdown.png");
         bdef = ScriptableObject.CreateInstance<BuffDef>();
         (bdef as ScriptableObject).name = "PASSIVEAGRESSION_MERCLIVING_BUFF";
         bdef.canStack = false;
         bdef.iconSprite = Util.SpriteFromFile("nonedef.png");
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(ForceAttackState),out _);
         ContentAddition.AddBuffDef(bdef);
         damageType = ReserveDamageType();
         def.onAssign += (slot) => {
             GlobalEventManager.onCharacterDeathGlobal += (report)=>{
               if(report.victimIsElite && DamageAPI.HasModdedDamageType(report.damageInfo,damageType)){
                 var comp = report?.attackerBody?.GetComponent<BladeForceComponent>();
                 if(comp){
                     report.attackerBody.AddBuff(bdef);
                     comp.curElite = EliteCatalog.eliteDefs.First((el) => report.victimBody.GetBuffCount(el.eliteEquipmentDef.passiveBuffDef) > 0);
                     Debug.Log(comp.curElite);
                     comp.buffColor = comp.curElite.eliteEquipmentDef.passiveBuffDef.buffColor;
                 }
               }
             };
             On.RoR2.GlobalEventManager.OnHitAll += (orig,self,info,hObj) =>{
                var body = info?.attacker?.GetComponent<CharacterBody>();
                var comp = body?.GetComponent<BladeForceComponent>();
                if(comp && comp.curElite){
                    body.AddBuff(comp.curElite.eliteEquipmentDef.passiveBuffDef);
                    Debug.Log(body.HasBuff(comp.curElite.eliteEquipmentDef.passiveBuffDef));
                }
                orig(self,info,hObj);
                if(comp && comp.curElite){
                    body.RemoveBuff(comp.curElite.eliteEquipmentDef.passiveBuffDef);
                }
             };
             On.RoR2.GlobalEventManager.OnHitEnemy += (orig,self,info,hObj) =>{
                var body = info?.attacker?.GetComponent<CharacterBody>();
                var comp = body?.GetComponent<BladeForceComponent>();
                if(comp && comp.curElite){
                    body.AddBuff(comp.curElite.eliteEquipmentDef.passiveBuffDef);
                    Debug.Log(body.HasBuff(comp.curElite.eliteEquipmentDef.passiveBuffDef));
                }
                orig(self,info,hObj);
                if(comp && comp.curElite){
                    body.RemoveBuff(comp.curElite.eliteEquipmentDef.passiveBuffDef);
                }
             };
             On.RoR2.HealthComponent.TakeDamage += (orig,self,info) =>{
                var body = info?.attacker?.GetComponent<CharacterBody>();
                var comp = body?.GetComponent<BladeForceComponent>();
                if(comp && comp.curElite){
                    body.AddBuff(comp.curElite.eliteEquipmentDef.passiveBuffDef);
                    Debug.Log(body.HasBuff(comp.curElite.eliteEquipmentDef.passiveBuffDef));
                }
                orig(self,info);
                if(comp && comp.curElite){
                    body.RemoveBuff(comp.curElite.eliteEquipmentDef.passiveBuffDef);
                }
             };
             return null;
         };
     }

     public class BladeForceComponent : MonoBehaviour {
         public EliteDef curElite = null;
         public Color buffColor = Color.white;
         Renderer renderer = null;

         public void Start(){
            renderer = GetComponent<CharacterBody>().modelLocator.modelTransform.GetComponent<CharacterModel>().baseRendererInfos[1].renderer;
         }
         public void Update(){
            bdef.buffColor = buffColor;
            if(renderer && curElite){
              var index = curElite? curElite.shaderEliteRampIndex : -1;
              MaterialPropertyBlock props = new MaterialPropertyBlock();
              renderer.GetPropertyBlock(props);
              props?.SetFloat(CommonShaderProperties._EliteIndex,curElite.shaderEliteRampIndex +1);
            }
         }
     }

     public class ForceAttackState : BaseState {
         public static float duration;
         public BladeForceComponent bladeForce;
         public OverlapAttack overlapAttack;
         public override void OnEnter(){
             base.OnEnter();
             bladeForce = characterBody.GetComponent<BladeForceComponent>();
             if(!bladeForce){
               bladeForce = characterBody.gameObject.AddComponent<BladeForceComponent>();
             }
             var anim = GetModelAnimator();
             duration = GroundLight.baseFinisherAttackDuration / attackSpeedStat;
             //overlapAttack = InitMeleeOverlap(3.5f, GroundLight.hitEffectPrefab, GetModelTransform(), "SwordLarge");
             if (anim.GetBool("isMoving") || !anim.GetBool("isGrounded"))
             {
                     PlayAnimation("Gesture, Additive", "GroundLight3", "GroundLight.playbackRate", duration);
                     PlayAnimation("Gesture, Override", "GroundLight3", "GroundLight.playbackRate", duration);
             }
             else
             {
                     PlayAnimation("FullBody, Override", "GroundLight3", "GroundLight.playbackRate", duration);
             }
             overlapAttack = new OverlapAttack{
                attacker = base.gameObject,
                damage = damageStat,
                hitBoxGroup = FindHitBoxGroup("SwordLarge"),
                isCrit = RollCrit(),
                teamIndex = GetTeam(),
                damageType = DamageType.BonusToLowHealth
             };
             DamageAPI.AddModdedDamageType(overlapAttack,damageType);
             //slashChildName = "GroundLight3Slash";
             //swingEffectPrefab = finisherSwingEffectPrefab;
             //hitEffectPrefab = finisherHitEffectPrefab;
             //attackSoundString = finisherAttackSoundString;
         }
         public override void FixedUpdate(){
             base.FixedUpdate();
             if(base.fixedAge >= duration){
                 overlapAttack.Fire();
                 outer.SetNextStateToMain();
             }
         }
         public override void OnExit(){
             base.OnExit();
         }
         public override InterruptPriority GetMinimumInterruptPriority(){
             return InterruptPriority.PrioritySkill;
         }
     }

    } 
}
