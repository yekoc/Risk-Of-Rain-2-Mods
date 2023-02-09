using RoR2;
using RoR2.Skills;
using EntityStates;
using R2API;
using EntityStates.Enforcer;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Networking;
using MonoMod.RuntimeDetour;

namespace PassiveAgression.ModCompat{

    public static class EnforcerSundowner {

        public static SkillDef def,def2;
        public static BuffDef activebdef,cooldownbdef;
        public static bool isHooked;
        public static int chargeAmount = 4;

        static EnforcerSundowner(){
         LanguageAPI.Add("PASSIVEAGRESSION_ENFORCESUNDOWN_RAISE","Giving It a Chance");
         LanguageAPI.Add("PASSIVEAGRESSION_ENFORCESUNDOWN_RAISEDESC",$"Raise your shield. \nThe next {chargeAmount} times you block an explosion or melee attack, trigger an explosion for <style=cIsDamage>600% damage</style> and deal heavy knockback. \nThe fourth explosion will <style=cIsHealth>break your stance</style>.");
         LanguageAPI.Add("PASSIVEAGRESSION_ENFORCESUNDOWN_LOWER","Enough Chances");
         LanguageAPI.Add("PASSIVEAGRESSION_ENFORCESUNDOWN_LOWER_DESC","Become Vincible.");
         def = UnityEngine.ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_ENFORCESUNDOWN_RAISE";
         (def as UnityEngine.ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_ENFORCESUNDOWN_RAISEDESC";
         def.baseRechargeInterval = 0f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = true;
         def.mustKeyPress = true;
         def.interruptPriority = InterruptPriority.PrioritySkill;
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(EnforcerSundownerState));
         def.icon = Util.SpriteFromFile("SundownShield.png");
         def2 = UnityEngine.ScriptableObject.Instantiate(def);
         def2.skillNameToken = "PASSIVEAGRESSION_ENFORCESUNDOWN_LOWER";
         (def2 as UnityEngine.ScriptableObject).name = def2.skillNameToken;
         def2.skillDescriptionToken = "PASSIVEAGRESSION_ENFORCESUNDOWN_LOWERDESC";
         def2.icon = Modules.Characters.EnforcerSurvivor.shieldExitDef.icon;

         activebdef = UnityEngine.ScriptableObject.CreateInstance<BuffDef>();
         activebdef.iconSprite = Util.SpriteFromFile("SundownBuff.png");
         activebdef.canStack = true;
         cooldownbdef = UnityEngine.ScriptableObject.CreateInstance<BuffDef>();
         cooldownbdef.iconSprite = activebdef.iconSprite;
         cooldownbdef.buffColor = UnityEngine.Color.grey;
         cooldownbdef.isCooldown = true;

         On.RoR2.BlastAttack.HandleHits += (orig,self,hits) =>{
            foreach(var hit in hits){
              if(hit.hurtBox.damageModifier == HurtBox.DamageModifier.Barrier && hit.hurtBox.healthComponent && hit.hurtBox.healthComponent.body && self.baseDamage > 0){
                var body = hit.hurtBox.healthComponent.body;
                if(body.HasBuff(activebdef)){ 
                   EffectManager.SpawnEffect(LegacyResourcesAPI.Load<UnityEngine.GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick"), new EffectData
                   {
                           origin = hit.hitPosition,
                           scale = 4f,
                           rotation = RoR2.Util.QuaternionSafeLookRotation((hit.hitNormal)), 
                   }, transmit: true);
                   new BlastAttack{
                       attacker = body.gameObject,
                       falloffModel = BlastAttack.FalloffModel.None,
                       teamIndex = TeamComponent.GetObjectTeam(body.gameObject),
                       radius = 4f,
                       crit = body.RollCrit(),
                       baseForce = 5000f,
                       baseDamage = body.damage * 6f,
                       position = hit.hitPosition,
                       canRejectForce = false,
                       attackerFiltering = AttackerFiltering.NeverHitSelf
                   }.Fire();
                   //FireBlastAttack
                   body.RemoveBuff(activebdef);
                }
              }
            }
            orig(self,hits);
         };
         On.RoR2.OverlapAttack.ProcessHits += (orig,self,hits) =>{
            foreach(var hit in (hits as List<OverlapAttack.OverlapInfo>)){
              if(hit.hurtBox.damageModifier == HurtBox.DamageModifier.Barrier && hit.hurtBox.healthComponent && hit.hurtBox.healthComponent.body && self.damage > 0){
                var body = hit.hurtBox.healthComponent.body;
                if(body.HasBuff(activebdef)){
                   //FireBlastAttack
                   EffectManager.SpawnEffect(LegacyResourcesAPI.Load<UnityEngine.GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick"), new EffectData
                   {
                           origin = hit.hitPosition,
                           scale = 4f,
                           rotation = RoR2.Util.QuaternionSafeLookRotation((hit.pushDirection * -1)), 
                   }, transmit: true);
                   new BlastAttack{
                       attacker = body.gameObject,
                       falloffModel = BlastAttack.FalloffModel.None,
                       teamIndex = TeamComponent.GetObjectTeam(body.gameObject),
                       radius = 4f,
                       crit = body.RollCrit(),
                       baseForce = 5000f,
                       baseDamage = body.damage * 6f,
                       position = hit.hitPosition,
                       canRejectForce = false,
                       attackerFiltering = AttackerFiltering.NeverHitSelf
                   }.Fire();
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
         new Hook(typeof(EnforcerWeaponComponent).GetMethod("GetShield",(BindingFlags)(-1)),typeof(EnforcerSundowner).GetMethod("GetShieldHook"));

         ContentAddition.AddBuffDef(activebdef);
         ContentAddition.AddBuffDef(cooldownbdef);
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddSkillDef(def2);
         ContentAddition.AddEntityState(typeof(EnforcerSundownerState),out _);
        }

        public static EnforcerWeaponComponent.EquippedShield GetShieldHook(System.Func<EnforcerWeaponComponent,EnforcerWeaponComponent.EquippedShield> orig,EnforcerWeaponComponent self){
            var ret = orig(self);
            if(self.charBody && self.charBody.skillLocator && self.charBody.skillLocator.special.skillDef.skillNameToken.Contains("ENFORCESUNDOWN")){
               return EnforcerWeaponComponent.EquippedShield.SHIELD;
            }
            return ret;
        }

        public class EnforcerSundownerState : ProtectAndServe  {
            public override void OnEnter(){
                base.OnEnter();
                if(skillLocator){
                    if(HasBuff(Modules.Buffs.protectAndServeBuff)){
                        skillLocator.special.SetBaseSkill(def2); 
                    }
                    else{
                        skillLocator.special.SetBaseSkill(def);
                    }
                }
                var localCharge = chargeAmount + activatorSkillSlot.maxStock - 1;
                if(NetworkServer.active){
                  if(HasBuff(Modules.Buffs.protectAndServeBuff)){
                    if(!HasBuff(cooldownbdef))
                      characterBody.SetBuffCount(activebdef.buffIndex,localCharge);
                    else
                      characterBody.ClearTimedBuffs(cooldownbdef); 
                  }
                  else{
                    characterBody.AddTimedBuff(cooldownbdef,localCharge - characterBody.GetBuffCount(activebdef));
                    characterBody.SetBuffCount(activebdef.buffIndex,0);
                  }
                }
            }
        }

    }
}
