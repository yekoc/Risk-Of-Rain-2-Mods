using BepInEx;
using BepInEx.Configuration;
using RoR2;
using RoR2.UI;
using RoR2.CharacterAI;
//using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using UnityEngine;
using System;
using System.Security;
using System.Security.Permissions;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace PingOrdering
{
    [BepInPlugin("xyz.yekoc.PingOrdering", "Ping Ordering","1.0.2" )]
    //[BepInDependency("com.bepis.r2api",BepInDependency.DependencyFlags.HardDependency)]
    public class BetterAIPlugin : BaseUnityPlugin
    {
	static public Dictionary<CharacterMaster,AwaitOrders> subordinateDict = new Dictionary<CharacterMaster,AwaitOrders>();  
	private void Awake(){
		IL.RoR2.UI.PingIndicator.RebuildPing += (il) =>{
			ILCursor c = new ILCursor(il);
			c.GotoNext(x => x.MatchLdstr("PLAYER_PING_ENEMY"));
			c.Index+= 6;
			ILLabel l = c.MarkLabel();
			c.GotoPrev(x => x.MatchLdstr("PLAYER_PING_ENEMY"));
			c.Emit(OpCodes.Ldarg_0);
			c.EmitDelegate<Func<PingIndicator,bool>>((PingIndicator self) => {
			  CharacterMaster ownerMaster = self.pingOwner.GetComponent<CharacterMaster>();
			  CharacterMaster targetMaster = self.pingTarget.GetComponent<CharacterBody>().master;
			  if(subordinateDict.ContainsKey(ownerMaster)){
			        bool flag = TeamManager.IsTeamEnemy(ownerMaster.teamIndex,targetMaster.teamIndex);
			  	subordinateDict[ownerMaster].SubmitOrder(flag ? AwaitOrders.Orders.Attack : AwaitOrders.Orders.Assist , self.pingTarget);
				subordinateDict.Remove(ownerMaster);
				PingIndicator.instancesList.First((ping) => ping.pingOwner == self.pingOwner && ping.pingColor == Color.cyan).fixedTimer =0f;
				//Chat.AddMessage(string.Format(Language.GetString("PING_ORDER_ENEMY"),self.pingText.text,Util.GetBestBodyName(subordinateDict[ownerMaster].characterBody),Util.GetBestBodyName(targetMaster.characterBody));
				self.pingDuration = 1f;
				return true;
			  }
			  else if(targetMaster.GetComponent<BaseAI>()?.leader.characterBody?.master == ownerMaster){
			    self.pingOwner.GetComponent<PingerController>().pingIndicator = null;
			    self.pingOwner.GetComponent<PingerController>().pingStock++;
			    subordinateDict.Add(ownerMaster,new AwaitOrders());
			    targetMaster.GetComponent<EntityStateMachine>().SetState(subordinateDict[ownerMaster]);
			    self.pingColor = Color.cyan;
			    self.pingDuration = float.PositiveInfinity;
			    self.enemyPingGameObjects[0].GetComponent<SpriteRenderer>().color = Color.cyan;
			    self.pingHighlight.highlightColor = (Highlight.HighlightColor)(451);
			    return true;
			  }
			  return false;
			});
			c.Emit(OpCodes.Brtrue,l);
			c.Index = 0;
			c.GotoNext(x => x.MatchLdstr("PLAYER_PING_DEFAULT"));
			c.Index +=5;
			l = c.MarkLabel();
			c.GotoPrev(x => x.MatchLdstr("PLAYER_PING_DEFAULT"));
			c.Emit(OpCodes.Ldarg_0);
			c.EmitDelegate<Func<PingIndicator,bool>>((PingIndicator self) => {
			  CharacterMaster ownerMaster = self.pingOwner.GetComponent<CharacterMaster>();
			  if(subordinateDict.ContainsKey(ownerMaster)){
			  	subordinateDict[ownerMaster].SubmitOrder(AwaitOrders.Orders.Move , null,self.pingOrigin);
				subordinateDict.Remove(ownerMaster);
				PingIndicator.instancesList.First((ping) => ping.pingOwner == self.pingOwner && ping.pingColor == Color.cyan).fixedTimer = 0f;
				//Chat.AddMessage(string.Format(Language.GetString("PING_ORDER_ENEMY"),self.pingText.text,Util.GetBestBodyName(subordinateDict[ownerMaster].characterBody),Util.GetBestBodyName(targetMaster.characterBody));
				self.pingDuration = 1f;
				return true;
			  }
			  return false;
			  });
			 c.Emit(OpCodes.Brtrue,l);
		};
                On.RoR2.Highlight.GetColor += (orig,self) => {
                  var ret = orig(self);
                  if(ret == Color.magenta && self.highlightColor == (Highlight.HighlightColor)(451)){
                    return Color.cyan + new Color(0.01f,0,0);
                  }
                  return ret;
                };
	}
	private void OnDestroy(){

	}
    }
}
