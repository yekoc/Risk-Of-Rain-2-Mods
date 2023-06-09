using BepInEx;
using BepInEx.Configuration;
using RoR2;
using MonoMod.RuntimeDetour;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Security;
using System.Security.Permissions;
using System.Reflection;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace PowerfullySlow
{
    [BepInPlugin("xyz.yekoc.PowerfullySlow", "Powerfully Slow Moon","1.2.1" )]
    [BepInIncompatibility("com.xoxfaby.UnlockAll")]
    [BepInDependency("com.KingEnderBrine.InLobbyConfig",BepInDependency.DependencyFlags.SoftDependency)]
    public class PowerfullySlowPlugin : BaseUnityPlugin
    {
	public static ConfigEntry<int> repeatChance {get; set;}
	internal bool hookSet = false;

	private void Awake()
        {
	  repeatChance = Config.Bind("Configuration","Repeat Chance",0,"Percent Chance of runs past first completion repeating old moon. Type:Int,Default:0,Max:100");

	  repeatChance.Value = System.Math.Min(System.Math.Max(repeatChance.Value,0),100);
	  if(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.KingEnderBrine.InLobbyConfig"))
	    HandleLobbyConfigCompat();
	
	  Run.onRunStartGlobal += (run) => {
                SceneCatalog.GetSceneDefFromSceneName("moon").sceneType = SceneType.Stage;
		if(!hookSet && NetworkServer.active && Util.CheckRoll(repeatChance.Value)){
		  On.EntityStates.LunarTeleporter.Active.OnEnter += EmpowerMoon;
		  void Cleanup(Run run2){ 
		    On.EntityStates.LunarTeleporter.Active.OnEnter -=EmpowerMoon;
		    Run.onRunDestroyGlobal -= Cleanup; 
		   }
		   Run.onRunDestroyGlobal += Cleanup;
		}
	  };
	  On.RoR2.Achievements.BaseEndingAchievement.OnInstall += (orig,self) => {
		  orig(self);
		  if(self.GetType() == typeof(RoR2.Achievements.CompleteMainEndingAchievement)){
		    On.EntityStates.LunarTeleporter.Active.OnEnter += EmpowerMoon;
		    hookSet = true;
		  }
	  };
	  On.RoR2.Achievements.BaseEndingAchievement.OnUninstall += (orig,self) => {
		  orig(self);
		  if(self.GetType() == typeof(RoR2.Achievements.CompleteMainEndingAchievement)){
		    On.EntityStates.LunarTeleporter.Active.OnEnter -= EmpowerMoon;
		    hookSet = false;
		  }
	  };
	  new Hook(typeof(EntityStates.Missions.BrotherEncounter.BrotherEncounterBaseState).GetProperty("shouldEnableArenaWalls",(BindingFlags)(-1)).GetMethod,typeof(PowerfullySlowPlugin).GetMethod(nameof(UnImprison),(BindingFlags)(-1)));
	}

	internal static bool UnImprison(System.Func<EntityStates.Missions.BrotherEncounter.BrotherEncounterBaseState,bool> orig,EntityStates.Missions.BrotherEncounter.BrotherEncounterBaseState self){
		return (self.GetType() == typeof(EntityStates.Missions.BrotherEncounter.EncounterFinished))? false : orig(self);
	}
	internal void EmpowerMoon(On.EntityStates.LunarTeleporter.Active.orig_OnEnter orig,EntityStates.LunarTeleporter.Active self){
		orig(self);
		if(NetworkServer.active)
		  self.teleporterInteraction.sceneExitController.destinationScene = SceneCatalog.GetSceneDefFromSceneName("moon");
	}
	internal void HandleLobbyConfigCompat(){
		var configEntry = new InLobbyConfig.ModConfigEntry();
		configEntry.DisplayName = "Powerfully Slow";
		configEntry.SectionFields.Add("Old Moon",new List<InLobbyConfig.Fields.IConfigField>{InLobbyConfig.Fields.ConfigFieldUtilities.CreateFromBepInExConfigEntry(repeatChance)});
		InLobbyConfig.ModConfigCatalog.Add(configEntry);
	}
    }
}
