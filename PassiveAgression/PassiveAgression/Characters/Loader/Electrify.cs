using System;
using RoR2;
using RoR2.Skills;
using EntityStates;
using System.Linq;
using EntityStates.Loader;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;
using R2API;

namespace PassiveAgression.Loader{
    public static class ElectrifySpecial{
        public static HookedSkillDef def;

        static ElectrifySpecial(){
         LanguageAPI.Add("PASSIVEAGRESSION_LOADERELEC","Power Pack Discharge");
         LanguageAPI.Add("PASSIVEAGRESSION_LOADERELEC_DESC","<style=cIsDamage>Shocking</style>. Unload stored charge through the gauntlet, draining all barrier and dealing <style=cIsDamage>400%-4000% damage</style> to the connected enemy.");
         def = ScriptableObject.CreateInstance<HookedSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_LOADERELEC";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_LOADERELEC_DESC";
         def.baseRechargeInterval = 10f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
         def.keywordTokens = new string[]{"KEYWORD_SHOCKING"};
         def.activationState = new SerializableEntityStateType(typeof(ElectricState));
         def.icon = Util.SpriteFromFile("HookDischarge.png");
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(ElectricState),out _);
        }

        public class HookedSkillDef : SkillDef{
            public override bool CanExecute(GenericSkill skillSlot){
               var state = (skillSlot.characterBody.GetComponents<EntityStateMachine>().First((esm) => esm.customName == "Hook" ).state as FireHook);
               return base.CanExecute(skillSlot) && state != null && state.hookStickOnImpact.stuck;
            }
        }

	class ElectricState : BaseSkillState{
	  //private ProjectileGrappleController grappleController;
	  private FireHook hookState;

	  public override void OnEnter(){
		base.OnEnter();
		hookState = characterBody.GetComponents<EntityStateMachine>().First((esm) => esm.customName == "Hook" ).state as FireHook;
                if(hookState == null || !hookState.hookStickOnImpact.stuck){
                    base.activatorSkillSlot.AddOneStock();
                    outer.SetNextStateToMain();
                    return;
                }
                if(hookState?.hookStickOnImpact?.stuckBody){
                    if(NetworkServer.active){
                     DamageInfo info = new DamageInfo{
                         attacker = base.gameObject,
                         inflictor = base.gameObject,
                         damageType = DamageType.Shock5s,
                         damage = damageStat * (4f * Mathf.Max(1f,(healthComponent.barrier/healthComponent.fullBarrier)*10f))
                     };
                     hookState.hookStickOnImpact.stuckBody.healthComponent.TakeDamage(info);
                     healthComponent.barrier = 0;
                    }
                }
                else{
                    LineRenderer line = hookState.hookInstance?.GetComponent<LineRenderer>();
                    if(line){
          /*              HitBoxGroup hitGroup = line.gameObject.AddComponent<HitBoxGroup>();
                        hitGroup.hitBoxes = new HitBox[]{line.gameObject.AddComponent<HitBox>()};
                        hitGroup.groupName = "Wire";
                        new OverlapAttack{
                            hitBoxGroup = hitGroup,
                            damageType = DamageType.Shock5s,
                            teamIndex = teamComponent.teamIndex,
                            damage = damageStat,
                            attacker = base.gameObject,
                            inflictor = base.gameObject
                        }.Fire();*/
                    }
                }
	  }


	}
    }
}
