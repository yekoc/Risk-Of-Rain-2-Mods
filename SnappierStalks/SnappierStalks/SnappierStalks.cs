using BepInEx;
using BepInEx.Configuration;
using RoR2;
using UnityEngine;
using System.Security;
using System.Security.Permissions;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace SnapStalk
{
    [BepInPlugin("xyz.yekoc.SnapStalk", "Snappier Stalks","1.0.0" )]
    public class SnapStalkPlugin : BaseUnityPlugin
    {
	
	private void Awake()
        {
		On.RoR2.Skills.SkillDef.OnExecute += (orig,self,slot) => {
		    orig(self,slot);
		    if(!slot.beginSkillCooldownOnSkillEnd && slot.characterBody.HasBuff(RoR2Content.Buffs.NoCooldowns)){
		    	slot.RestockSteplike();
		    }
		};
	}
	private void OnDestroy(){

	}
    }
}
