using RoR2;
using RoR2.Skills;
using EntityStates;
using R2API;
using UnityEngine;
using Pathfinder;


namespace PassiveAgression.ModCompat
{
    public static class PathfinderForcedMove{
     public static SkillDef def;
     public static GameObject bodyPrefab = Pathfinder.PathfinderPlugin.pathfinderBodyPrefab;

     static PathfinderForcedMove(){
         LanguageAPI.Add("PASSIVEAGRESSION_PATHFCARRY","Assisted Move");
         LanguageAPI.Add("PASSIVEAGRESSION_PATHFCARRY_DESC","<style=cIsUtility>Jump</style> a short distance. Calling Squall to <color=00FF00>carry</color> you, slowly draining battery.");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_PATHFCARRY";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_PATHFCARRY_DESC";
         def.baseRechargeInterval = 6f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Body";
         def.activationState = new SerializableEntityStateType(typeof(JumpState));
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(JumpState),out _);
     }

     public class JumpState : Skillstates.Pathfinder.Evade {
         public override void OnEnter(){
            base.OnEnter(); 
            controller.UnreadyJavelin();
            characterMotor.velocity.y += totalDashBonus/2;
         }

         public override void FixedUpdate(){
            var vel = characterMotor.velocity.y;
            base.FixedUpdate();
            characterMotor.velocity.y = fixedAge < baseDuration ? vel : 0;
         }

         public override void OnExit(){
            
         }
     }

    }
}
