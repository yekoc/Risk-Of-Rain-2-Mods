using RoR2;
using RoR2.Skills;
using EntityStates;
using R2API;
using System;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;

namespace PassiveAgression.Commando
{
    public static class CommandoStimpack{
     public static SkillDef def;
     public static BuffDef bdef;

     static CommandoStimpack(){
         LanguageAPI.Add("PASSIVEAGRESSION_COMMANDOSTIM","Stim Shot");
         LanguageAPI.Add("PASSIVEAGRESSION_COMMANDOSTIM_DESC","Administer a standard issue <style=cIsHealing>UES Emergency Stimulant™</style>,guaranteed to boost general health and response speed! \n<style=cSub><size=40%>(side effects may include but are not limited to...)</size></style>");
         LanguageAPI.Add("PASSIVEAGRESSION_COMMANDOSTIM_KEYWORD","<style=cKeywordName>Stimulated</style><style=cSub>Heals for <style=cIsHealing>50% of missing health</style> and boosts attack and move speed by <style=cIsUtility>+30%</style></style>");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_COMMANDOSTIM";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_COMMANDOSTIM_DESC";
         def.baseRechargeInterval = 20f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
         def.isCombatSkill = false;
         def.keywordTokens = new string[]{"PASSIVEAGRESSION_COMMANDOSTIM_KEYWORD"};
         def.activationState = new SerializableEntityStateType(typeof(StimState));
         def.icon = Util.SpriteFromFile("StimShot.png");
         bdef = ScriptableObject.CreateInstance<BuffDef>();
         bdef.buffColor = Color.yellow;
         var sprite = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Texture2D>("RoR2/Base/Croco/texBuffRegenBoostIcon.tif").WaitForCompletion();
         bdef.iconSprite = Sprite.Create(sprite, new Rect(0.0f, 0.0f, sprite.width, sprite.height), new Vector2(0.5f, 0.5f), 100.0f);
         bdef.name = "PASSIVEAGRESSION_COMMANDOSTIM_BUFF";
         bdef.canStack = true;
         
         R2API.ContentAddition.AddBuffDef(bdef);
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(StimState),out _);
         RecalculateStatsAPI.GetStatCoefficients += (sender,args) =>{
             var stacks = sender.GetBuffCount(bdef);
             args.attackSpeedMultAdd += 0.3f * stacks;
             args.moveSpeedMultAdd += 0.3f * stacks;
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
