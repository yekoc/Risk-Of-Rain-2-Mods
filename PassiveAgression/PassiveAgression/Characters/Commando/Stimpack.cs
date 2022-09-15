using RoR2;
using EntityStates;
using System;

namespace PassiveAgression.Commando
{
    public class StimpackState : BaseSkillState
    {
	    public override void OnEnter(){
		    base.OnEnter();
	    }
	    public override void FixedUpdate(){
	    }
	    public override void OnExit(){
		    base.OnExit();

	    }
	    public override InterruptPriority GetMinimumInterruptPriority(){
		    return InterruptPriority.PrioritySkill;
	    }
    }
}
