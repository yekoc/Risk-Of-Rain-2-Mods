using RoR2;
using RoR2.Skills;
using EntityStates;
using R2API;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;

namespace PassiveAgression.Commando
{
    public static class CommandoStimpackScepter{
     public static SkillDef def;
     public static BuffDef bdef;

     static CommandoStimpackScepter(){
         
         LanguageAPI.Add("PASSIVEAGRESSION_COMMANDOSTIM_SCEPTER","Stim Shot++");
         def = ScriptableObject.Instantiate(CommandoStimpack.def);
         def.skillNameToken = "PASSIVEAGRESSION_COMMANDOSTIM_SCEPTER";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_COMMANDOSTIM_SCEPTERDESC";
         def.baseRechargeInterval = 20f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
         def.keywordTokens = new string[]{"PASSIVEAGRESSION_COMMANDOSTIM_KEYWORD"};
         def.icon = Util.SpriteFromFile("StimShotScepter.png");
         def.activationState = new SerializableEntityStateType(typeof(StimState));
         bdef = ScriptableObject.CreateInstance<BuffDef>();
         bdef.buffColor = Color.yellow;
         bdef.iconSprite = Util.SpriteFromFile("ScepterStim.png");
         bdef.name = "PASSIVEAGRESSION_COMMANDOSTIM_SCEPTERBUFF";
         bdef.canStack = CommandoStimpack.bdef.canStack;
         
         LanguageAPI.Add("PASSIVEAGRESSION_COMMANDOSTIM_SCEPTERDESC",Language.GetString("PASSIVEAGRESSION_COMMANDOSTIM_DESC") + "\n<color=#d299ff>SCEPTER: Proprietary Stimulant MAX™ formula now includes skin hardening xenominerals and ocular-hemo-attractors!.</color>");
         R2API.ContentAddition.AddBuffDef(bdef);
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(StimState),out _);
         RecalculateStatsAPI.GetStatCoefficients += (sender,args) =>{
             var stacks = sender.GetBuffCount(bdef);
             args.attackSpeedMultAdd += 0.3f * stacks;
             args.moveSpeedMultAdd += 0.3f * stacks;
             if(stacks > 0){
              args.armorAdd += 30f;
              args.critAdd += 10f;
             }
         };
     }

        public class StimState : BaseSkillState
        {
                public override void OnEnter(){
                        base.OnEnter();
                        if(NetworkServer.active){
                         healthComponent.Heal((healthComponent.fullHealth - healthComponent.health) * 0.5f,default(ProcChainMask));
                         characterBody.AddTimedBuff(bdef,5f); 
                        }
			PlayAnimation("Gesture, Override", "ReloadPistols", "ReloadPistols.playbackRate", 0.1f);
			PlayAnimation("Gesture, Additive", "ReloadPistols", "ReloadPistols.playbackRate", 0.1f);
			FindModelChild("GunMeshL")?.gameObject.SetActive(value: false);
                        FindModelChild("ReloadFXR")?.gameObject.SetActive(value: false);
                }
                public override void FixedUpdate(){
                    base.FixedUpdate();
                    if(fixedAge > 0.1f){
                     outer.SetNextStateToMain();
                    }
                }
                public override void OnExit(){
			FindModelChild("ReloadFXL")?.gameObject.SetActive(value: false);
			FindModelChild("ReloadFXR")?.gameObject.SetActive(value: false);
			FindModelChild("GunMeshL")?.gameObject.SetActive(value: true);
			PlayAnimation("Gesture, Override", "ReloadPistolsExit");
			PlayAnimation("Gesture, Additive", "ReloadPistolsExit");
                    base.OnExit();
                }
                public override InterruptPriority GetMinimumInterruptPriority(){
                        return InterruptPriority.PrioritySkill;
                }
        }
    }
}
