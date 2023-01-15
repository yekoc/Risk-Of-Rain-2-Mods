using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Bootstrap;
using RoR2;
using RoR2.Skills;
using R2API;
using R2API.Utils;
using System.Security;
using System.Security.Permissions;
using UnityEngine;
using System.Runtime.CompilerServices;
using System.Linq;

#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace PassiveAgression
{
    //[BepInDependency("com.bepis.r2api", BepInDependency.DependencyFlags.HardDependency)] 
    [BepInDependency(LanguageAPI.PluginGUID,BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(DamageAPI.PluginGUID,BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(RecalculateStatsAPI.PluginGUID,BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(PrefabAPI.PluginGUID,BepInDependency.DependencyFlags.HardDependency)]
    //[BepInDependency(R2APIContentManager.PluginGUID,BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.DestroyedClone.AncientScepter", BepInDependency.DependencyFlags.SoftDependency)] 
    [BepInDependency("com.KingEnderBrine.ExtraSkillSlots", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.rob.Paladin",BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.Moffein.SniperClassic",BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.EnforcerGang.Enforcer",BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.rob.HenryMod",BepInDependency.DependencyFlags.SoftDependency)]
    //[BepInDependency("com.Gnome.ChefMod",BepInDependency.DependencyFlags.SoftDependency)]
    //[BepInDependency("com.Bog.Pathfinder",BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.rob.DiggerUnearthed",BepInDependency.DependencyFlags.SoftDependency)]

    //[R2APISubmoduleDependency(nameof(LanguageAPI),nameof(LoadoutAPI),nameof(RecalculateStatsAPI),nameof(DamageAPI))]
    [BepInPlugin("xyz.yekoc.PassiveAgression", "Passive Agression","1.0.2" )]
    public class PassiveAgressionPlugin : BaseUnityPlugin
    {
        public static ConfigEntry<bool> unfinishedContent,devIcons;
        internal new static ManualLogSource Logger { get; set; }
        internal static ConfigFile config;
        internal struct ModList{
            internal bool Paladin;
            internal bool Scepter;
            internal bool Chef;
            internal bool Enforcer;
            internal bool Pathfinder;
            internal bool Sniper;
            internal bool Digger;
            internal bool Henry;
        };
        internal ModList modCompat;
        private void Awake(){
            Logger = base.Logger;
            config = Config;
            unfinishedContent =  Config.Bind("Configuration","Enable Unfinished Content",false,"Enables Unfinished/Potentially Broken Content");
            devIcons =  Config.Bind("Configuration","Prefer Dev Icons",false,"Use jank dev-made icons even when better ones are available");
            SetupVanilla();
        }
        private void Start(){
            if(modCompat.Paladin = Chainloader.PluginInfos.ContainsKey("com.rob.Paladin"))
                SetupPaladin();
            if(modCompat.Chef = Chainloader.PluginInfos.ContainsKey("com.Gnome.ChefMod"))
                SetupChef();
            if(modCompat.Enforcer = Chainloader.PluginInfos.ContainsKey("com.EnforcerGang.Enforcer"))
                SetupEnforcer();
            if(modCompat.Sniper = Chainloader.PluginInfos.ContainsKey("com.Moffein.SniperClassic"))
                SetupSniper();
            if(modCompat.Pathfinder = Chainloader.PluginInfos.ContainsKey("com.Bog.Pathfinder"))
                SetupPathfinder();
            if(modCompat.Digger = Chainloader.PluginInfos.ContainsKey("com.rob.DiggerUnearthed"))
                SetupDigger();
            if(modCompat.Henry = Chainloader.PluginInfos.ContainsKey("com.rob.HenryMod"))
                SetupHenry();
            if(modCompat.Scepter = Chainloader.PluginInfos.ContainsKey("com.DestroyedClone.AncientScepter"))
                SetupScepter();

        }
	private void OnDestroy(){

	}
        private void SetupVanilla(){

         GameObject body;

         #region Commando
         body = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Commando/CommandoBody.prefab").WaitForCompletion();
         HG.ArrayUtils.ArrayAppend(ref body.GetComponent<SkillLocator>().special.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Commando.CommandoStimpack.def,
            viewableNode = new ViewablesCatalog.Node(Commando.CommandoStimpack.def.skillNameToken,false,null)
         });
         Coin.SetMarksman(body,SkillSlot.Secondary);
         #endregion

         #region Bandit
         body = Bandit.StandoffPassive.slot.bodyPrefab;
         HG.ArrayUtils.ArrayAppend(ref Bandit.StandoffPassive.slot.family.variants,new SkillFamily.Variant{
            skillDef = Bandit.StandoffPassive.def,
            viewableNode = new ViewablesCatalog.Node(Bandit.StandoffPassive.def.skillNameToken,false,null)
         });
         HG.ArrayUtils.ArrayAppend(ref body.GetComponent<SkillLocator>().utility.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Bandit.StarchUtil.def,
            viewableNode = new ViewablesCatalog.Node(Bandit.StarchUtil.def.skillNameToken,false,null)
         });
         Coin.SetMarksman(body,SkillSlot.Secondary);
         /* Crash
         HG.ArrayUtils.ArrayAppend(ref body.GetComponent<SkillLocator>().special.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Bandit.ChainSpecial.def,
            viewableNode = new ViewablesCatalog.Node(Bandit.ChainSpecial.def.skillNameToken,false,null)
         });*/
         #endregion

         #region Captain
         body = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Captain/CaptainBody.prefab").WaitForCompletion();
         if(unfinishedContent.Value)
         HG.ArrayUtils.ArrayAppend(ref body.GetComponent<SkillLocator>().special.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Captain.IntegratedBeacon.def,
            viewableNode = new ViewablesCatalog.Node(Captain.IntegratedBeacon.def.skillNameToken,false,null)
         });

         #endregion

         #region Croco
         body = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Croco/CrocoBody.prefab").WaitForCompletion();
         HG.ArrayUtils.ArrayAppend(ref body.GetComponent<SkillLocator>().special.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Croc.PathogenSpecial.def,
            viewableNode = new ViewablesCatalog.Node(Croc.PathogenSpecial.def.skillNameToken,false,null)
         });
         #endregion


         #region Engi
         body = Engineer.ScrapPassive.slot.bodyPrefab;
         HG.ArrayUtils.ArrayAppend(ref Engineer.ScrapPassive.slot.family.variants,new SkillFamily.Variant{
            skillDef = Engineer.ScrapPassive.def,
            viewableNode = new ViewablesCatalog.Node(Engineer.ScrapPassive.def.skillNameToken,false,null)
         });
         #endregion

         #region Huntress
         body = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Huntress/HuntressBody.prefab").WaitForCompletion();
         if(unfinishedContent.Value)
         HG.ArrayUtils.ArrayAppend(ref body.GetComponent<SkillLocator>().secondary.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Huntress.LightMine.def,
            viewableNode = new ViewablesCatalog.Node(Huntress.LightMine.def.skillNameToken,false,null)
         });
         #endregion

         #region Loader
         body = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Loader/LoaderBody.prefab").WaitForCompletion();
         if(unfinishedContent.Value)
         HG.ArrayUtils.ArrayAppend(ref body.GetComponent<SkillLocator>().secondary.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Loader.LoaderZipline.def,
            viewableNode = new ViewablesCatalog.Node(Loader.LoaderZipline.def.skillNameToken,false,null)
         });
         HG.ArrayUtils.ArrayAppend(ref body.GetComponent<SkillLocator>().special.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Loader.ElectrifySpecial.def,
            viewableNode = new ViewablesCatalog.Node(Loader.ElectrifySpecial.def.skillNameToken,false,null)
         });
         #endregion
        
         #region Mage
         body = Mage.BleedPassive.slot.bodyPrefab;
         HG.ArrayUtils.ArrayAppend(ref Mage.BleedPassive.slot.family.variants,new SkillFamily.Variant{
            skillDef = Mage.BleedPassive.def,
            viewableNode = new ViewablesCatalog.Node(Mage.BleedPassive.def.skillNameToken,false,null)
         });
         HG.ArrayUtils.ArrayAppend(ref body.GetComponent<SkillLocator>().primary.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Mage.BloodPrimary.def,
            viewableNode = new ViewablesCatalog.Node(Mage.BloodPrimary.def.skillNameToken,false,null)
         });
         if(unfinishedContent.Value)
         HG.ArrayUtils.ArrayAppend(ref body.GetComponent<SkillLocator>().secondary.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Mage.FloatingChaos.def,
            viewableNode = new ViewablesCatalog.Node(Mage.FloatingChaos.def.skillNameToken,false,null)
         });
         HG.ArrayUtils.ArrayAppend(ref body.GetComponent<SkillLocator>().special.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Mage.IceGolemSpecial.def,
            viewableNode = new ViewablesCatalog.Node(Mage.IceGolemSpecial.def.skillNameToken,false,null)
         });
         #endregion

         #region Merc
         if(unfinishedContent.Value)
         body = Merc.FlickerPassive.slot.bodyPrefab;
         /*HG.ArrayUtils.ArrayAppend(ref Merc.FlickerPassive.slot.family.variants ,new SkillFamily.Variant{
            skillDef = Merc.FlickerPassive.def,
            viewableNode = new ViewablesCatalog.Node(Merc.FlickerPassive.def.skillNameToken,false,null)
         });*/
         #endregion

         #region Treebot
         //body = Treebot.SprintClimbPassive.slot.bodyPrefab;
         /*Super Buggy
         HG.ArrayUtils.ArrayAppend(ref Treebot.SprintClimbPassive.slot.family.variants,new SkillFamily.Variant{
            skillDef = Treebot.SprintClimbPassive.def,
            viewableNode = new ViewablesCatalog.Node(Treebot.SprintClimbPassive.def.skillNameToken,false,null)
         });*/
         #endregion

         #region Viend
         body = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/VoidSurvivor/VoidSurvivorBody.prefab").WaitForCompletion();
         HG.ArrayUtils.ArrayAppend(ref body.GetComponent<GenericSkill>().skillFamily.variants,new SkillFamily.Variant{
            skillDef = VoidSurvivor.InfestationPassive.def,
            viewableNode = new ViewablesCatalog.Node(VoidSurvivor.InfestationPassive.def.skillNameToken,false,null)
         });
         HG.ArrayUtils.ArrayAppend(ref body.GetComponent<SkillLocator>().utility.skillFamily.variants,new SkillFamily.Variant{
            skillDef = VoidSurvivor.TearUtil.def,
            viewableNode = new ViewablesCatalog.Node(VoidSurvivor.TearUtil.def.skillNameToken,false,null)
         });
         #endregion

         #region Railgunner
         body = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/Railgunner/RailgunnerBody.prefab").WaitForCompletion();
         Coin.SetMarksman(body,SkillSlot.Utility);
         HG.ArrayUtils.ArrayAppend(ref body.GetComponent<GenericSkill>().skillFamily.variants,new SkillFamily.Variant{
            skillDef = Railgunner.RailgunnerPassive.def,
            viewableNode = new ViewablesCatalog.Node(Railgunner.RailgunnerPassive.def.skillNameToken,false,null)
         });
         #endregion
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupPaladin(){
            var skillFamily = PaladinMod.Modules.Prefabs.paladinPrefab.GetComponent<SkillLocator>()?.special?.skillFamily;
            if(skillFamily){
            HG.ArrayUtils.ArrayAppend(ref skillFamily.variants, new SkillFamily.Variant{
                    skillDef = ModCompat.PaladinGlassShadow.def,
                    viewableNode = new ViewablesCatalog.Node(ModCompat.PaladinGlassShadow.def.skillNameToken,false,null),
                    unlockableDef = PaladinMod.Modules.Unlockables.paladinLunarShardSkillDef
            });
            HG.ArrayUtils.ArrayAppend(ref skillFamily.variants, new SkillFamily.Variant{
                    skillDef = ModCompat.PaladinResolve.def,
                    viewableNode = new ViewablesCatalog.Node(ModCompat.PaladinResolve.def.skillNameToken,false,null)
            });
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupScepter(){
         AncientScepter.AncientScepterItem.instance.RegisterScepterSkill(Commando.CommandoStimpackScepter.def, "CommandoBody", Commando.CommandoStimpack.def);
         AncientScepter.AncientScepterItem.instance.RegisterScepterSkill(Croc.PathogenSpecialScepter.def,"CrocoBody",Croc.PathogenSpecial.def);
         //AncientScepter.AncientScepterItem.instance.RegisterScepterSkill(Mage.IceGolemScepter.def,"MageBody",Mage.IceGolemSpecial.def); // Buggy
         if(modCompat.Paladin){
           ModCompat.PaladinGlassShadow.SetUpScepter();
           ModCompat.PaladinResolve.SetUpScepter();
         }
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupChef(){

        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupEnforcer(){
            var skillFamily = Modules.Characters.EnforcerSurvivor.instance.bodyPrefab.GetComponent<SkillLocator>()?.special?.skillFamily;
            if(skillFamily)
            HG.ArrayUtils.ArrayAppend(ref skillFamily.variants, new SkillFamily.Variant{
                    skillDef = ModCompat.EnforcerSundowner.def,
                    viewableNode = new ViewablesCatalog.Node(ModCompat.EnforcerSundowner.def.skillNameToken,false,null),
                    unlockableDef = Modules.EnforcerUnlockables.enforcerDesperadoSkinUnlockableDef
            });

        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupSniper(){
            Coin.SetMarksman(SniperClassic.SniperClassic.SniperBody,SkillSlot.Utility);
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupPathfinder(){

        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupDigger(){
         HG.ArrayUtils.ArrayAppend(ref ModCompat.DiggerBlacksmithPassive.slot.family.variants,new SkillFamily.Variant{
            skillDef = ModCompat.DiggerBlacksmithPassive.def,
            viewableNode = new ViewablesCatalog.Node(ModCompat.DiggerBlacksmithPassive.def.skillNameToken,false,null),
            unlockableDef = DiggerPlugin.Unlockables.blacksmithUnlockableDef
         });
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupHenry(){
         /* CharacterBody body = HenryMod.Modules.Survivors.Henry.instance.bodyPrefab.GetComponent<CharacterBody>();
          if(unfinishedContent.Value){
             HG.ArrayUtils.ArrayAppend(ref body.skillLocator.secondary.skillFamily.variants,new SkillFamily.Variant{
                skillDef = ModCompat.HenryYomiJC.def,
                viewableNode = new ViewablesCatalog.Node(ModCompat.HenryYomiJC.def.skillNameToken,false,null),
             });
          }*/
        }


    }
}
