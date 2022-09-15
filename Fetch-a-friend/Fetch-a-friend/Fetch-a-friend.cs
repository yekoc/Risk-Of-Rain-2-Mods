using BepInEx;
using BepInEx.Configuration;
using RoR2;
using RoR2.CharacterAI;
using EntityStates;
using EntityStates.Treebot;
using UnityEngine;
using UnityEngine.Networking;
using System.Security;
using System.Security.Permissions;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace FriendFetchMod
{
    [BepInPlugin("xyz.yekoc.FetchAFriend", "Fetch-a-friend Quest","1.1.3" )]
    public class FriendFetchPlugin : BaseUnityPlugin
    {
	public static ConfigEntry<bool> items {get; set;}
	public static ConfigEntry<bool> respawn {get; set;}
	private static List<AISkillDriver> follower = new List<AISkillDriver>();
	private static On.EntityStates.Treebot.UnlockInteractable.Unlock.hook_OnEnter Friend = (orig,self) => {
		orig(self);
		CharacterBody charBod = self.GetComponent<PurchaseInteraction>().lastActivator.GetComponent<CharacterBody>();	
		if(self.isAuthority){
		 CharacterMaster rex = new MasterSummon{
		   masterPrefab = MasterCatalog.FindMasterPrefab("TreebotMonsterMaster"),
		   summonerBodyObject = charBod.gameObject,
		   ignoreTeamMemberLimit = true,
		   inventoryToCopy = (items.Value? charBod.inventory : null),
		   useAmbientLevel = new bool?(true),
		   position = self.transform.position + Vector3.up,
		   rotation = Quaternion.identity,
		   preSpawnSetupCallback = (master) =>{	
		 	List<AISkillDriver> ai = master.aiComponents[0].skillDrivers.ToList();
		 	master.gameObject.AddComponent<AIOwnership>().ownerMaster = charBod.master;
			master.inventory.GiveItem(RoR2Content.Items.MinionLeash);
			ai.AddRange(follower);
			master.aiComponents[0].skillDrivers = ai.ToArray();
		 	DontDestroyOnLoad(master);
			master.destroyOnBodyDeath = !(respawn.Value);
		   }
		 }.Perform();
		}
		 if(self.modelLocator)
		 EntityState.Destroy(self.modelLocator.gameObject);
	};

	private void Awake()
        {
	 items = Config.Bind("Configuration","Item Share",true,"Whether Rex gets a copy of your items,default:True");
	 respawn = Config.Bind("Configuration","Revival",false,"Lets your new friend transcend death after teleporting to a new stage,default:False");
	 follower = LegacyResourcesAPI.Load<GameObject>("prefabs/charactermasters/engiwalkerturretmaster").GetComponents<AISkillDriver>().Where(ai => ai.moveTargetType == AISkillDriver.TargetType.CurrentLeader).ToList();
	 On.EntityStates.Treebot.UnlockInteractable.Unlock.OnEnter += Friend; 	
	}

	private void OnDestroy(){
	  On.EntityStates.Treebot.UnlockInteractable.Unlock.OnEnter -= Friend;
	}
    }
}
