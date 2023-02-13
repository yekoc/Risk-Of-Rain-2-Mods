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
         LanguageAPI.Add("PASSIVEAGRESSION_PATHFCARRY","Forced Move");
         LanguageAPI.Add("PASSIVEAGRESSION_PATHFCARRY_DESC","<style=cIsDamage>Stunning</style>. Deal 75% damage, nearby allies are <style=cIsUtility>cleansed from a debuff</style>.");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_BANDITSTARCH";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_BANDITSTARCH_DESC";
         def.baseRechargeInterval = 6f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Body";
         def.activationState = new SerializableEntityStateType(typeof(FloatState));
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(FloatState),out _);
     }

     public class FloatState : GenericCharacterMain {

     }
    }
}
