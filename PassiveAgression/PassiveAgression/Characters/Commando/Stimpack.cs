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
         LanguageAPI.Add("PASSIVEAGRESSION_COMMANDOSTIM_KEYWORD","<style=cKeywordName>Stimulated</style><style=cSub>Heals for <style=cIsHealing>20% of missing health</style> and boosts attack and move speed by <style=cIsUtility>+25%</style></style>");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_COMMANDOSTIM";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_COMMANDOSTIM_DESC";
         def.baseRechargeInterval = 20f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
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
             args.attackSpeedMultAdd += 0.25f * stacks;
             args.moveSpeedMultAdd += 0.25f * stacks;
         };
     }

        public class StimState : BaseSkillState
        {
                public override void OnEnter(){
                        base.OnEnter();
                        if(NetworkServer.active){
                         healthComponent.Heal((healthComponent.fullHealth - healthComponent.health) * 0.2f,default(ProcChainMask));
                         characterBody.AddTimedBuff(bdef,5f); 
                        }
                }
                public override void FixedUpdate(){
                    base.FixedUpdate();
                    outer.SetNextStateToMain();
                }
                public override void OnExit(){
                    base.OnExit();
                }
                public override InterruptPriority GetMinimumInterruptPriority(){
                        return InterruptPriority.PrioritySkill;
                }
        }
    }
}
