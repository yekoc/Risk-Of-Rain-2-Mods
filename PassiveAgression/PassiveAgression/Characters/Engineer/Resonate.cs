using RoR2;
using EntityStates;
using System;
using UnityEngine;
using static RoR2.BulletAttack;

namespace PassiveAgression.Engineer
{
    public class ResonanceState : BaseSkillState
    {
	    public override void OnEnter(){
		    base.OnEnter(); 
	    }
	    public override void FixedUpdate(){
                new BulletAttack{
                    damage = 0f,
                    hitCallback = (BulletAttack bulletAttack,ref BulletHit hitInfo) =>{
			bool result = false;
			if ((bool)hitInfo.collider)
			{
				result = ((1 << hitInfo.collider.gameObject.layer) & (int)bulletAttack.stopperMask) == 0;
			}
                        GameObject entity = hitInfo.entityObject;
                        if(entity){
                            JitterBones bone = null;
                            if(!(bone = entity.GetComponent<JitterBones>())){
                                bone = entity.AddComponent<JitterBones>();
                            }
                            bone.perlinNoiseStrength += 1f;
                        }
                        return result;
                    }
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
}
