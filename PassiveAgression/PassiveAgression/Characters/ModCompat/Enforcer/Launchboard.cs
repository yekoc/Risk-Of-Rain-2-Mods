using RoR2;
using RoR2.Skills;
using EntityStates;
using R2API;

namespace PassiveAgression.ModCompat{

    public static class EnforcerLaunchboard {

        public static SkillDef def;

        static EnforcerLaunchboard(){
         LanguageAPI.Add("PASSIVEAGRESSION_ENFORCELAUNCH","Launchboard");
         LanguageAPI.Add("PASSIVEAGRESSION_ENFORCELAUNCH_DESC","Prepare to launch an ally upwards using your shield.");
         def = UnityEngine.ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_ENFORCELAUNCH";
         (def as UnityEngine.ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_ENFORCELAUNCH_DESC";
         def.baseRechargeInterval = 4f;
         def.canceledFromSprinting = true;
         def.cancelSprintingOnActivation = true;
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(EnforcerLaunchboardState));
         LoadoutAPI.AddSkillDef(def);
         LoadoutAPI.AddSkill(typeof(EnforcerLaunchboardState));
        }


        public class EnforcerLaunchboardState : BaseSkillState {

            float duration = 2f;

            static EnforcerLaunchboardState(){
            }
            public override void OnEnter(){
            }
            public override void OnExit(){
            }
            public override void FixedUpdate(){
             if(base.fixedAge >= duration){
                 outer.SetNextStateToMain();
             }
            }
            public override InterruptPriority GetMinimumInterruptPriority(){
                return InterruptPriority.Skill;
            }
        }

    }
}
