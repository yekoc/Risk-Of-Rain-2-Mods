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
    public static class PetalReap{
     public static SkillDef def;
     public static ModdedDamageType damageType;
     public static BuffDef bdef;

     static PetalReap(){
         LanguageAPI.Add("PASSIVEAGRESSION_MERCREAP","Living Force");
         LanguageAPI.Add("PASSIVEAGRESSION_MERCREAP_DESC","<style=cIsDamage>Slayer.</style> Unleash a finishing blow for <style=cIsDamage>350% damage</syle>.Hold to release your swords current aspect.Kills <style=cIsUtility>absorb the targets aspect into your blade</style>.");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_MERCREAP";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MERCREAP_DESC";
         def.baseRechargeInterval = 6f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
         def.keywordTokens = new string[]{"KEYWORD_SLAYER"};
         def.activationState = new SerializableEntityStateType(typeof(ForceAttackState));
         def.icon = Util.SpriteFromFile("Showdown.png");
         bdef = ScriptableObject.CreateInstance<BuffDef>();
         (bdef as ScriptableObject).name = "PASSIVEAGRESSION_MERCREAP_BUFF";
         bdef.canStack = false;
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(ForceAttackState),out _);
         damageType = ReserveDamageType();

         GlobalEventManager.onCharacterDeathGlobal += (report)=>{
           if(report.victimIsElite && DamageAPI.HasModdedDamageType(report.damageInfo,damageType)){
             var comp = report.attackerBody.GetComponent<BladeForceComponent>();
             if(comp){
                 report.attackerBody.AddBuff(bdef);
                 comp.curElite = EliteCatalog.eliteDefs.First((el) => report.victimBody.GetBuffCount(el.eliteEquipmentDef.passiveBuffDef) > 0);
                 comp.buffColor = comp.curElite.eliteEquipmentDef.passiveBuffDef.buffColor;
             }
           }
         };
         On.RoR2.GlobalEventManager.OnHitAll += (orig,self,info,hObj) =>{
            var body = info.attacker.GetComponent<CharacterBody>();
            var comp = body?.GetComponent<BladeForceComponent>();
            if(comp && comp.curElite){
                body.AddBuff(comp.curElite.eliteEquipmentDef.passiveBuffDef);
            }
            orig(self,info,hObj);
            if(comp && comp.curElite){
                body.RemoveBuff(comp.curElite.eliteEquipmentDef.passiveBuffDef);
            }
         };
     }

     public class BladeForceComponent : MonoBehaviour {
         public EliteDef curElite = null;
         public Color buffColor = Color.white;

         public void Update(){
            bdef.buffColor = buffColor;
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
             //slashChildName = "GroundLight3Slash";
             //swingEffectPrefab = finisherSwingEffectPrefab;
             //hitEffectPrefab = finisherHitEffectPrefab;
             //attackSoundString = finisherAttackSoundString;
         }
         public override void FixedUpdate(){
             base.FixedUpdate();
             if(base.fixedAge >= duration){
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
