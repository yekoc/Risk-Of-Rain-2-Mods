using System;
using RoR2;
using EntityStates;
using System.Linq;
using EntityStates.Loader;
using RoR2.Projectile;
using UnityEngine;

namespace PassiveAgression.Loader{
	class ElectricState : BaseSkillState{
	  private ProjectileGrappleController grappleController;
	  private FireHook hookState;



	  public override void OnEnter(){
		base.OnEnter();
		hookState = characterBody.GetComponents<EntityStateMachine>().First((esm) => esm.customName == "Hook" ).state as FireHook;
                if(hookState == null){
                    base.activatorSkillSlot.AddOneStock();
                    outer.SetNextStateToMain();
                    return;
                }
                if(hookState?.hookStickOnImpact?.stuckBody){
                    DamageInfo info = new DamageInfo{
                        attacker = base.gameObject,
                        inflictor = base.gameObject,
                        damageType = DamageType.Shock5s,
                        damage = damageStat
                    };
                    hookState.hookStickOnImpact.stuckBody.healthComponent.TakeDamage(info);
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
