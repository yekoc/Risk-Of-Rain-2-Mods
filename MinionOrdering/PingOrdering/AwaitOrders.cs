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

                public PingIndicator ping;

                public AwaitOrders(PingIndicator ing = null){
                   ping = ing;
                }

		public override void OnEnter(){
			base.OnEnter();
                        if(!ai || !body){
                          outer.SetNextStateToMain();
                          AIOrdersPlugin.subordinateDict[characterMaster.minionOwnership.ownerMaster].Remove(this);
                          return;
                        }
                        if(!ping){
                           ping = UnityEngine.Object.Instantiate(LegacyResourcesAPI.Load<GameObject>("Prefabs/PingIndicator")).GetComponent<PingIndicator>();
                           ping.pingOwner = characterMaster.minionOwnership?.ownerMaster?.gameObject;
                           ping.pingOrigin = body?.transform?.position ?? base.transform.position;
                           ping.pingNormal = Vector3.zero;
                           ping.pingTarget = body.gameObject;
                           ping.transform.position = body.transform.position;
                           ping.positionIndicator.targetTransform = body.transform;
                           ping.positionIndicator.defaultPosition = body.transform.position;
                           ping.targetTransformToFollow = body.coreTransform;
                           ping.pingDuration = float.PositiveInfinity;
                           ping.fixedTimer = float.PositiveInfinity;
                           ping.pingColor = Color.cyan;
                           ping.pingText.color = ping.textBaseColor * ping.pingColor;
                           ping.pingText.text = Util.GetBestMasterName(characterMaster.minionOwnership?.ownerMaster);
                           ping.pingObjectScaleCurve.enabled = false;
                           ping.pingObjectScaleCurve.enabled = true;
                           ping.pingHighlight.highlightColor = (Highlight.HighlightColor)(451);
                           ping.pingHighlight.targetRenderer = body.modelLocator?.modelTransform?.GetComponentInChildren<CharacterModel>()?.baseRendererInfos?.First((r) => !r.ignoreOverlays).renderer;
                           ping.pingHighlight.strength = 1f;
                           ping.pingHighlight.isOn = true;
                           foreach(var o in ping.enemyPingGameObjects){
                             o.SetActive(true);
                             var sprit = o.GetComponent<SpriteRenderer>();
                             if(sprit){
                               sprit.color = Color.cyan;
                             }
                             var part = o.GetComponent<ParticleSystem>();
                             if(part){
                               var main = part.main;
                               var sC = main.startColor;
                               sC.colorMax = Color.cyan;
                               sC.colorMin = Color.cyan;
                               sC.color = Color.cyan;
                             }
                           }
                        }
			sprintThreshold = ai.skillDrivers.FirstOrDefault((drive) => drive.shouldSprint)?.minDistanceSqr ?? float.PositiveInfinity;	
		}

		public override void FixedUpdate(){
			base.FixedUpdate();
                        if(!ai){
                          outer.SetNextStateToMain();
                          AIOrdersPlugin.subordinateDict[characterMaster.minionOwnership.ownerMaster].Remove(this);
                          return;
                        }
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
                            if(!body || body.moveSpeed == 0){
                              outer.SetNextStateToMain();
                            }
			    BroadNavigationSystem.Agent agent = ai.broadNavigationAgent;
			    agent.currentPosition = ai.body.footPosition;
                            ai.SetGoalPosition(targetPosition);
			    ai.localNavigator.targetPosition = agent.output.nextPosition ?? ai.localNavigator.targetPosition; 
			    if(!agent.output.targetReachable){
				agent.InvalidatePath();
                            }
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
                        if(ping){
                          ping.fixedTimer = 0f;
                        }
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
