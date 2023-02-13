using BepInEx;
using BepInEx.Configuration;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using System.Security;
using System.Security.Permissions;
using System.Collections.Generic;

#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace SnapStalk
{
    [BepInPlugin("xyz.yekoc.SnapStalk", "Snappier Stalks","1.0.2" )]
    public class SnapStalkPlugin : BaseUnityPlugin
    {
        public static HashSet<SkillDef> forbidden = new();
        public static HashSet<SkillDef> allowed = new();
	
	private void Awake()
        {
                SkillCatalog.skillsDefined.CallWhenAvailable(() => {
                   foreach(var skill in SkillCatalog.allSkillDefs){
                     if(skill.beginSkillCooldownOnSkillEnd){
                       forbidden.Add(skill);
                     }
                     else if(!skill.mustKeyPress){
                       allowed.Add(skill);
                     }
                   }
                });
		On.RoR2.Skills.SkillDef.OnExecute += (orig,self,slot) => {
		    orig(self,slot);
		    if(slot.characterBody.HasBuff(RoR2Content.Buffs.NoCooldowns) && !forbidden.Contains(self)){
                        if(!allowed.Contains(self) && slot.stateMachine.state.GetType() == self.activationState.stateType){
                          if(slot.stateMachine.CanInterruptState(self.interruptPriority)){
                              forbidden.Add(self);
                              return;
                          }
                          else{
                              allowed.Add(self);
                          }
                        }
		    	slot.RestockSteplike();
		    }
		};
	}
    }
}
