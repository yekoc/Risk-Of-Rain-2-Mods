using RoR2;
using RoR2.CharacterAI;
using System;
using UnityEngine;
using UnityEngine.Networking;


namespace PassiveAgression
{
    public class MasterSummonClient : MasterSummon{
	        public new CharacterMaster Perform()
		{
			TeamIndex teamIndex = TeamIndex.None;
			if (teamIndexOverride.HasValue)
			{
				teamIndex = teamIndexOverride.Value;
			}
			else
			{
				if (!summonerBodyObject)
				{
					Debug.LogErrorFormat("Cannot spawn master {0}: No team specified.", masterPrefab);
					return null;
				}
				teamIndex = TeamComponent.GetObjectTeam(summonerBodyObject);
			}
			if (!ignoreTeamMemberLimit)
			{
				TeamDef teamDef = TeamCatalog.GetTeamDef(teamIndex);
				if (teamDef == null)
				{
					Debug.LogErrorFormat("Attempting to spawn master {0} on TeamIndex.None. Is this intentional?", masterPrefab);
					return null;
				}
				if (teamDef != null && teamDef.softCharacterLimit <= TeamComponent.GetTeamMembers(teamIndex).Count)
				{
					return null;
				}
			}
			CharacterBody characterBody = null;
			CharacterMaster characterMaster = null;
			SkinDef skinDef = null;
			if ((bool)summonerBodyObject)
			{
				characterBody = summonerBodyObject.GetComponent<CharacterBody>();
				skinDef = SkinCatalog.FindCurrentSkinDefForBodyInstance(summonerBodyObject);
			}
			if ((bool)characterBody)
			{
				characterMaster = characterBody.master;
			}
			Inventory inventory = characterMaster?.inventory;
			GameObject gameObject = UnityEngine.Object.Instantiate(masterPrefab, position, rotation);
			CharacterMaster component = gameObject.GetComponent<CharacterMaster>();
			component.teamIndex = teamIndex;
			Loadout loadout = Loadout.RequestInstance();
			this.loadout?.Copy(loadout);
			if ((bool)skinDef)
			{
				SkinDef.MinionSkinReplacement[] minionSkinReplacements = skinDef.minionSkinReplacements;
				if (minionSkinReplacements.Length != 0)
				{
					for (int i = 0; i < minionSkinReplacements.Length; i++)
					{
						BodyIndex bodyIndex = BodyCatalog.FindBodyIndex(minionSkinReplacements[i].minionBodyPrefab);
						int num = SkinCatalog.FindLocalSkinIndexForBody(bodyIndex, minionSkinReplacements[i].minionSkin);
						if (num != -1)
						{
							loadout.bodyLoadoutManager.SetSkinIndex(bodyIndex, (uint)num);
						}
					}
				}
			}
			component.SetLoadoutServer(loadout);
			Loadout.ReturnInstance(loadout);
			CharacterMaster characterMaster2 = characterMaster;
			if ((bool)characterMaster2 && (bool)characterMaster2.minionOwnership.ownerMaster)
			{
				characterMaster2 = characterMaster2.minionOwnership.ownerMaster;
			}
			component.minionOwnership.SetOwner(characterMaster2);
			if ((bool)summonerBodyObject)
			{
				AIOwnership component2 = gameObject.GetComponent<AIOwnership>();
				if ((bool)component2)
				{
					if ((bool)characterMaster)
					{
						component2.ownerMaster = characterMaster;
					}
					CharacterBody component3 = summonerBodyObject.GetComponent<CharacterBody>();
					if ((bool)component3)
					{
						CharacterMaster master = component3.master;
						if ((bool)master)
						{
							component2.ownerMaster = master;
						}
					}
				}
				BaseAI component4 = gameObject.GetComponent<BaseAI>();
				if ((bool)component4)
				{
					component4.leader.gameObject = summonerBodyObject;
				}
			}
			if ((bool)inventoryToCopy)
			{
				component.inventory.CopyEquipmentFrom(inventoryToCopy);
				component.inventory.CopyItemsFrom(inventoryToCopy, inventoryItemCopyFilter ?? Inventory.defaultItemCopyFilterDelegate);
			}
			inventorySetupCallback?.SetupSummonedInventory(this, component.inventory);
			bool flag = false;
			if (!useAmbientLevel.HasValue)
			{
				if ((bool)inventory && inventory.GetItemCount(RoR2Content.Items.UseAmbientLevel) > 0)
				{
					flag = true;
				}
			}
			else
			{
				flag = useAmbientLevel.Value;
			}
			if (flag)
			{
				component.inventory.GiveItem(RoR2Content.Items.UseAmbientLevel);
			}
			if ((bool)inventory && useAmbientLevel != false)
			{
				component.inventory.GiveItem(RoR2Content.Items.UseAmbientLevel, inventory.GetItemCount(RoR2Content.Items.UseAmbientLevel));
			}
			preSpawnSetupCallback?.Invoke(component);
                        NetworkConnection clientAuthorityOwner = characterMaster?.networkIdentity?.clientAuthorityOwner;
			NetworkServer.SpawnWithClientAuthority(gameObject,clientAuthorityOwner);
			component.Respawn(position, rotation);
			return component;
		}
    }
}
