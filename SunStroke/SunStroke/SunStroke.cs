using BepInEx;
using BepInEx.Configuration;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using System.Security;
using System.Security.Permissions;
using System.Collections.Generic;
using static RoR2.DotController.DotIndex;
using Mono.Cecil.Cil;
using MonoMod.Cil;

#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace SunStroke
{
    [BepInPlugin("xyz.yekoc.SunStroke", "Unmatched Power of the Sun's Heat","1.0.0" )]
    [BepInDependency("com.rob.Paladin",BepInDependency.DependencyFlags.SoftDependency)]
    public class SunStrokePlugin : BaseUnityPlugin
    {
        public ConfigEntry<bool> vanillaSun;
	
	private void Awake()
        {
            vanillaSun = Config.Bind("Balance/Compat","Vanilla Sun Behavior",false,"If set to true,doesn't apply the hook that prevents the sun from applying extra burn.Set for compatibility or to buff grandparents.Default Value: False");
            On.RoR2.StrengthenBurnUtils.CheckDotForUpgrade += (On.RoR2.StrengthenBurnUtils.orig_CheckDotForUpgrade orig,Inventory inv,ref InflictDotInfo info) =>{
              if((info.dotIndex == Burn || info.dotIndex == StrongerBurn || info.dotIndex == Helfire) && (info.victimObject)){
                  var stacks = Mathf.Max(1,info.victimObject.GetComponent<CharacterBody>().GetBuffCount(RoR2Content.Buffs.Overheat) - 2);
                  info.damageMultiplier *= stacks;
                  info.totalDamage *= stacks;
                  Debug.Log("new burn at " + stacks);
              }
              orig(inv,ref info);
            };
            if(!vanillaSun.Value){
              IL.RoR2.GrandParentSunController.ServerFixedUpdate += (il) =>{
                 var c = new ILCursor(il);
                 if(c.TryGotoNext(x => x.MatchSub(),x => x.MatchStloc(out _))){
                     c.Index++;
                     c.EmitDelegate<System.Func<int,int>>((orig) => Mathf.Min(orig,1));
                 }
              };
              if(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rob.Paladin")){
                  PaladinCompat();
              }
            }
	}

        private void PaladinCompat(){
          new MonoMod.RuntimeDetour.ILHook(typeof(PaladinSunController).GetMethod("ServerFixedUpdate",(System.Reflection.BindingFlags)(-1)),(ILContext il) => {
             var c = new ILCursor(il);
             int locIndex = -1;
             if(c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetMethod("GetBuffCount",new System.Type[]{typeof(BuffDef)})), x => x.MatchStloc(out locIndex),x => x.MatchLdloc(locIndex)) && c.TryGotoNext(MoveType.After,x => x.MatchLdloc(locIndex))){
                c.EmitDelegate<System.Func<int,int>>((orig) => Mathf.Min(orig,1));
             }
             else{
               Logger.LogError("Paladin compat hook failed,his sun will double dip into overheat damage");
            }
          });
        }
    }
}
