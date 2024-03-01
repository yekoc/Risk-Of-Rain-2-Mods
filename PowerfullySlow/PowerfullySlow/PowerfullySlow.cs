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
    [BepInPlugin("xyz.yekoc.PowerfullySlow", "Powerfully Slow Moon","1.3.0" )]
    [BepInIncompatibility("com.xoxfaby.UnlockAll")]
    [BepInDependency("com.KingEnderBrine.InLobbyConfig",BepInDependency.DependencyFlags.SoftDependency)]
    public class PowerfullySlowPlugin : BaseUnityPlugin
    {
	public static ConfigEntry<int> repeatChance {get; set;}
        public static ConfigEntry<bool> frogger;
        internal bool hookSet = false;
        internal static bool froggy = false;
        internal static GameObject frogPrefab = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/moon/FrogInteractable.prefab").WaitForCompletion();

	private void Awake()
        {
	  repeatChance = Config.Bind("Configuration","Repeat Chance",0,"Percent Chance of runs past first completion repeating old moon. Type:Int,Default:0,Max:100");
          frogger = Config.Bind("Configuration","Frog, My Dudes",true,"Whether a glass frog is spawned in the boss area after fights on repeat runs of old moon. Does nothing on new moon or on the first, non-repeat, old moon run. Type:Bool,Default:True");

	  repeatChance.Value = System.Math.Min(System.Math.Max(repeatChance.Value,0),100);
	  if(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.KingEnderBrine.InLobbyConfig"))
	    HandleLobbyConfigCompat();
	
	  Run.onRunStartGlobal += (run) => {
                SceneCatalog.GetSceneDefFromSceneName("moon").sceneType = SceneType.Stage;
		if(!hookSet && NetworkServer.active && Util.CheckRoll(repeatChance.Value)){
		  On.EntityStates.LunarTeleporter.Active.OnEnter += EmpowerMoon;
                  froggy = frogger.Value;
		  void Cleanup(Run run2){ 
		    On.EntityStates.LunarTeleporter.Active.OnEnter -=EmpowerMoon;
		    Run.onRunDestroyGlobal -= Cleanup;
                    froggy = false;
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
          On.EntityStates.Missions.BrotherEncounter.EncounterFinished.OnEnter += (orig,self) =>{
              orig(self);
              if(froggy){
                  var transform = self.childLocator.FindChild("CenterOrbEffect");
                  var fro = GameObject.Instantiate(frogPrefab,transform.position,Quaternion.identity);
                  var lunColor = PickupCatalog.FindPickupIndex(ItemTier.Lunar);
                  foreach(var high in fro.GetComponentsInChildren<Highlight>()){
                   high.pickupIndex = lunColor;
                   high.highlightColor = Highlight.HighlightColor.pickup;
                   high.isOn = true;
                  }
                  fro.AddComponent<Light>().color = lunColor.GetPickupColor();
                  if(NetworkServer.active){
                      NetworkServer.Spawn(fro);
                  }
                  EffectManager.SpawnEffect( HealthComponent.AssetReferences.fragileDamageBonusBreakEffectPrefab , new EffectData{origin = transform.position},false);
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
		configEntry.SectionFields.Add("Old Moon",new List<InLobbyConfig.Fields.IConfigField>{InLobbyConfig.Fields.ConfigFieldUtilities.CreateFromBepInExConfigEntry(repeatChance),InLobbyConfig.Fields.ConfigFieldUtilities.CreateFromBepInExConfigEntry(frogger)});
		InLobbyConfig.ModConfigCatalog.Add(configEntry);
	}
    }
}
