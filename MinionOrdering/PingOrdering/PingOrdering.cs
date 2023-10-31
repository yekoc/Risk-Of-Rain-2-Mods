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
using System.Runtime.CompilerServices;

#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace PingOrdering
{
    [BepInPlugin("xyz.yekoc.PingOrdering", "Ping Ordering","1.1.1" )]
    [BepInDependency("com.rune580.riskofoptions",BepInDependency.DependencyFlags.SoftDependency)]
    public class BetterAIPlugin : BaseUnityPlugin
    {
	static public Dictionary<CharacterMaster,List<AwaitOrders>> subordinateDict = new Dictionary<CharacterMaster,List<AwaitOrders>>();
        static public ConfigEntry<KeyboardShortcut> attentionButton;
	private void Awake(){
                attentionButton = Config.Bind<KeyboardShortcut>("Controls","Order All",new KeyboardShortcut(KeyCode.F),"Calls the attention of all subordiantes.");
                if(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions")){
                   RoOptionize();
                }

                On.RoR2.PlayerCharacterMasterController.Update += (orig,self) =>{
                  orig(self);
                  if(attentionButton.Value.IsPressed() && self.hasEffectiveAuthority){
                   var g = MinionOwnership.MinionGroup.FindGroup(self.master.netId);
                   if(g != null){
                    foreach(var minion in g.members){
                      if(minion?.gameObject){
                          var stat = new AwaitOrders();
                          if(!subordinateDict.ContainsKey(self.master)){
                            subordinateDict.Add(self.master,new List<AwaitOrders>());
                          }
                          subordinateDict[self.master].Add(stat);
                          minion?.gameObject?.GetComponent<EntityStateMachine>()?.SetState(stat);
                      }
                    }
                   }
                  }
                };
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
			  if(subordinateDict.ContainsKey(ownerMaster) && subordinateDict[ownerMaster].Any()){
			        bool flag = TeamManager.IsTeamEnemy(ownerMaster.teamIndex,targetMaster.teamIndex);
                                subordinateDict[ownerMaster].ForEach((m) => m.SubmitOrder(flag ? AwaitOrders.Orders.Attack : AwaitOrders.Orders.Assist , self.pingTarget));
				subordinateDict.Remove(ownerMaster);
				//Chat.AddMessage(string.Format(Language.GetString("PING_ORDER_ENEMY"),self.pingText.text,Util.GetBestBodyName(subordinateDict[ownerMaster].characterBody),Util.GetBestBodyName(targetMaster.characterBody));
				self.pingDuration = 1f;
				return true;
			  }
			  else if(targetMaster.GetComponent<BaseAI>()?.leader.characterBody?.master == ownerMaster){
			    self.pingOwner.GetComponent<PingerController>().pingIndicator = null;
			    self.pingOwner.GetComponent<PingerController>().pingStock++;
			    subordinateDict.Add(ownerMaster,new List<AwaitOrders>(){new AwaitOrders(self)});
			    targetMaster.GetComponent<EntityStateMachine>().SetState(subordinateDict[ownerMaster][0]);
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
                               subordinateDict[ownerMaster].ForEach((m) => m.SubmitOrder(AwaitOrders.Orders.Move , null,self.pingOrigin));
				subordinateDict.Remove(ownerMaster);
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
                RoR2.ContentManagement.ContentManager.collectContentPackProviders += (del) =>{
                 var prov = new RoR2.ContentManagement.SimpleContentPackProvider();
                 prov.identifier = "PingOrdering";
                 prov.generateContentPackAsyncImplementation += whycantthisbealambda;
                 prov.finalizeAsyncImplementation += rapidclapping;
                 prov.loadStaticContentImplementation += wow;
                 del(prov);
                };

	}
        private System.Collections.IEnumerator whycantthisbealambda(RoR2.ContentManagement.GetContentPackAsyncArgs args){
           args.output.entityStateTypes.Add(new Type[]{typeof(AwaitOrders)});
           args.ReportProgress(1f);
           yield break;
        }
        private System.Collections.IEnumerator wow(RoR2.ContentManagement.LoadStaticContentAsyncArgs args){
           args.ReportProgress(1f);
           yield break;
        }
        private System.Collections.IEnumerator rapidclapping(RoR2.ContentManagement.FinalizeAsyncArgs args){
           args.ReportProgress(1f);
           yield break;
        }
	private void OnDestroy(){

	}
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void RoOptionize(){
            RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.KeyBindOption(attentionButton));
        }
    }
}
