using RoR2;
using RoR2.CharacterAI;
using RoR2.Navigation;
using EntityStates.AI;
using RoR2.UI;
using System;
using System.Linq;
using UnityEngine;

namespace PingOrdering{
	public class AwaitOrders : BaseAIState{

		public enum Orders{
			None,
			Move,
			Attack,
			Assist
		}

		public Orders order;
		
		public Vector3? targetPosition;

		public GameObject target;

		public float sprintThreshold;


		public override void OnEnter(){
			base.OnEnter();
			sprintThreshold = ai.skillDrivers.FirstOrDefault((drive) => drive.shouldSprint)?.minDistanceSqr ?? float.PositiveInfinity;	
		}

		public override void FixedUpdate(){
			base.FixedUpdate();
			if(!target && !targetPosition.HasValue)
			  AimAt(ref bodyInputs,ai.leader); 
			switch(order){
			  case Orders.None:{
			    return;
			  }
			  case Orders.Attack:{
			    ai.currentEnemy.gameObject = target;
			    ai.enemyAttention = ai.enemyAttentionDuration;
			    outer.SetNextState(new EntityStates.AI.Walker.Combat());
			    break;
			  }
			  case Orders.Move:{
			    BroadNavigationSystem.Agent agent = ai.broadNavigationAgent;
			    agent.currentPosition = ai.body.footPosition;
			    ai.localNavigator.targetPosition = agent.output.nextPosition ?? ai.localNavigator.targetPosition; 
			    if(!agent.output.targetReachable)
				agent.InvalidatePath();
			    ai.localNavigator.Update(cvAIUpdateInterval.value);
			    bodyInputs.moveVector = ai.localNavigator.moveVector;
			    float sqrMagnitude = (base.body.footPosition - targetPosition.Value).sqrMagnitude;
			    bodyInputs.pressSprint = sqrMagnitude > sprintThreshold;
			    if(ai.localNavigator.wasObstructedLastUpdate)
			      base.ModifyInputsForJumpIfNeccessary(ref bodyInputs);
			    float num = base.body.radius * base.body.radius *4;
			    if(sqrMagnitude < num)
			     outer.SetNextStateToMain();
			    break;
			  }
			  case Orders.Assist:{
			    ai.buddy.gameObject = target;
			    ai.customTarget.gameObject = target;
			    outer.SetNextState(new EntityStates.AI.Walker.Combat());
			    break;
			  }
			};

		}

		public override void OnExit(){
			base.OnExit();

		}
		public void SubmitOrder(Orders command,GameObject target,Vector3? targetPosition = null){
			 order = command;
			 this.target = target;
			 this.targetPosition = targetPosition;
			 if(targetPosition.HasValue){
				 BroadNavigationSystem.Agent agent = ai.broadNavigationAgent;
				 agent.goalPosition = targetPosition;
				 agent.InvalidatePath();
			 }
			
		}

	}
}
