using RoR2;
using RoR2.Skills;
using EntityStates;
using R2API;
using EntityStates.Enforcer;
using System.Collections.Generic;
using UnityEngine.Networking;


namespace PassiveAgression.ModCompat{

    public static class EnforcerSundowner {

        public static SkillDef def,def2;
        public static BuffDef activebdef,cooldownbdef;
        public static bool isHooked;
        public static int chargeAmount = 4;

        static EnforcerSundowner(){
         LanguageAPI.Add("PASSIVEAGRESSION_ENFORCESUNDOWN_RAISE","");
         LanguageAPI.Add("PASSIVEAGRESSION_ENFORCESUNDOWN_RAISEDESC","Become Invincible");
         LanguageAPI.Add("PASSIVEAGRESSION_ENFORCESUNDOWN_LOWER","");
         LanguageAPI.Add("PASSIVEAGRESSION_ENFORCESUNDOWN_LOWER","Become Vincible.");
         def = UnityEngine.ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_ENFORCESUNDOWN_RAISE";
         (def as UnityEngine.ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_ENFORCESUNDOWN_RAISEDESC";
         def.baseRechargeInterval = 0f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.interruptPriority = InterruptPriority.PrioritySkill;
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(EnforcerSundownerState));
         def2 = UnityEngine.ScriptableObject.Instantiate(def);
         def2.skillNameToken = "PASSIVEAGRESSION_ENFORCESUNDOWN_LOWER";
         (def2 as UnityEngine.ScriptableObject).name = def2.skillNameToken;
         def2.skillDescriptionToken = "PASSIVEAGRESSION_ENFORCESUNDOWN_LOWERDESC";

         activebdef = UnityEngine.ScriptableObject.CreateInstance<BuffDef>();
         activebdef.canStack = true;
         cooldownbdef = UnityEngine.ScriptableObject.CreateInstance<BuffDef>();
         cooldownbdef.isCooldown = true;

         On.RoR2.BlastAttack.HandleHits += (orig,self,hits) =>{
            foreach(var hit in hits){
              if(hit.hurtBox.damageModifier == HurtBox.DamageModifier.Barrier && hit.hurtBox.healthComponent && hit.hurtBox.healthComponent.body){
                var body = hit.hurtBox.healthComponent.body;
                if(body.HasBuff(activebdef)){
                   //FireBlastAttack
                   body.RemoveBuff(activebdef);
                }
              }
            }
            orig(self,hits);
         };
         On.RoR2.OverlapAttack.ProcessHits += (orig,self,hits) =>{
            foreach(var hit in (hits as List<OverlapAttack.OverlapInfo>)){
              if(hit.hurtBox.damageModifier == HurtBox.DamageModifier.Barrier && hit.hurtBox.healthComponent && hit.hurtBox.healthComponent.body){
                var body = hit.hurtBox.healthComponent.body;
                if(body.HasBuff(activebdef)){
                   //FireBlastAttack
                   body.RemoveBuff(activebdef);
                }
              }
            }
            orig(self,hits);
         };
         On.RoR2.CharacterBody.OnBuffFinalStackLost += (orig,self,buff) => {
           orig(self,buff);
           if(buff == activebdef && self.HasBuff(Modules.Buffs.protectAndServeBuff)){
             EntityStateMachine.FindByCustomName(self.gameObject,"Weapon").SetInterruptState(new EnforcerSundownerState(),InterruptPriority.Frozen);
           } 
         };

         ContentAddition.AddBuffDef(activebdef);
         ContentAddition.AddBuffDef(cooldownbdef);
         LoadoutAPI.AddSkillDef(def);
         LoadoutAPI.AddSkillDef(def2);
         LoadoutAPI.AddSkill(typeof(EnforcerSundownerState));
        }


        public class EnforcerSundownerState : ProtectAndServe  {

            public override void OnEnter(){
                base.OnEnter();
                if(NetworkServer.active){
                  if(HasBuff(Modules.Buffs.protectAndServeBuff) && !HasBuff(cooldownbdef)){
                    characterBody.SetBuffCount(activebdef.buffIndex,chargeAmount);
                  }
                  else{
                    characterBody.AddTimedBuff(cooldownbdef,chargeAmount - characterBody.GetBuffCount(activebdef));
                    characterBody.SetBuffCount(activebdef.buffIndex,0);
                  }
                }
            }
        }

    }
}
