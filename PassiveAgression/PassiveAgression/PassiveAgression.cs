using BepInEx;
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
    [BepInDependency("com.bepis.r2api", BepInDependency.DependencyFlags.HardDependency)] 
    [BepInDependency("com.DestroyedClone.AncientScepter", BepInDependency.DependencyFlags.SoftDependency)] 
    [BepInDependency("com.KingEnderBrine.ExtraSkillSlots", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.rob.Paladin",BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.Moffein.SniperClassic",BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.EnforcerGang.Enforcer",BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.Gnome.ChefMod",BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.Bog.Pathfinder",BepInDependency.DependencyFlags.SoftDependency)]
    [R2APISubmoduleDependency(nameof(DamageAPI),nameof(LanguageAPI),nameof(LoadoutAPI),nameof(RecalculateStatsAPI))]
    [BepInPlugin("xyz.yekoc.PassiveAgression", "Passive Agression","1.0.0" )]
    public class PassiveAgressionPlugin : BaseUnityPlugin
    {
        internal new static ManualLogSource Logger { get; set; }
        internal struct ModList{
            internal bool Paladin;
            internal bool Scepter;
            internal bool Chef;
            internal bool Enforcer;
            internal bool Pathfinder;
            internal bool Sniper;
        };
        internal ModList modCompat;
        private void Awake(){
            Logger = base.Logger;
            SetupVanilla();
        }
        private void Start(){
            if(modCompat.Scepter = Chainloader.PluginInfos.ContainsKey("com.DestroyedClone.AncientScepter"))
                SetupScepter();
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

        }
	private void OnDestroy(){

	}
        private void SetupVanilla(){

         #region Bandit
         HG.ArrayUtils.ArrayAppend(ref Bandit.StandoffPassive.slot.family.variants,new SkillFamily.Variant{
            skillDef = Bandit.StandoffPassive.def,
            viewableNode = new ViewablesCatalog.Node(Bandit.StandoffPassive.def.skillNameToken,false,null)
         });
         HG.ArrayUtils.ArrayAppend(ref UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Bandit2/Bandit2Body.prefab").WaitForCompletion().GetComponent<SkillLocator>().utility.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Bandit.StarchUtil.def,
            viewableNode = new ViewablesCatalog.Node(Bandit.StarchUtil.def.skillNameToken,false,null)
         });
         #endregion

         #region Croco
         HG.ArrayUtils.ArrayAppend(ref UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Croco/CrocoBody.prefab").WaitForCompletion().GetComponent<SkillLocator>().special.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Croc.PathogenSpecial.def,
            viewableNode = new ViewablesCatalog.Node(Croc.PathogenSpecial.def.skillNameToken,false,null)
         });
         #endregion

         #region Huntress
         HG.ArrayUtils.ArrayAppend(ref UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Huntress/HuntressBody.prefab").WaitForCompletion().GetComponent<SkillLocator>().secondary.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Huntress.LightMine.def,
            viewableNode = new ViewablesCatalog.Node(Huntress.LightMine.def.skillNameToken,false,null)
         });
         #endregion

         #region Merc
         HG.ArrayUtils.ArrayAppend(ref Merc.FlickerPassive.slot.family.variants ,new SkillFamily.Variant{
            skillDef = Merc.FlickerPassive.def,
            viewableNode = new ViewablesCatalog.Node(Merc.FlickerPassive.def.skillNameToken,false,null)
         });
         #endregion

         #region Treebot
         HG.ArrayUtils.ArrayAppend(ref Treebot.SprintClimbPassive.slot.family.variants,new SkillFamily.Variant{
            skillDef = Treebot.SprintClimbPassive.def,
            viewableNode = new ViewablesCatalog.Node(Treebot.SprintClimbPassive.def.skillNameToken,false,null)
         });
         #endregion
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupPaladin(){
            var skillFamily = PaladinMod.Modules.Prefabs.paladinPrefab.GetComponent<SkillLocator>()?.special?.skillFamily;
            if(skillFamily)
            HG.ArrayUtils.ArrayAppend(ref skillFamily.variants, new SkillFamily.Variant{
                    skillDef = ModCompat.PaladinGlassShadow.def,
                    viewableNode = new ViewablesCatalog.Node(ModCompat.PaladinGlassShadow.def.skillNameToken,false,null)
            });
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupScepter(){

        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupChef(){

        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupEnforcer(){

        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupSniper(){

        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupPathfinder(){
        }
    }
}
