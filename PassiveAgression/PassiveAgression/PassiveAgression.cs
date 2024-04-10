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
    [BepInDependency(PrefabAPI.PluginGUID,BepInDependency.DependencyFlags.HardDependency)]
    //[BepInDependency(R2APIContentManager.PluginGUID,BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.DestroyedClone.AncientScepter", BepInDependency.DependencyFlags.SoftDependency)] 
    [BepInDependency("com.KingEnderBrine.ExtraSkillSlots", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.rob.Paladin",BepInDependency.DependencyFlags.SoftDependency)]
    //[BepInDependency("com.Moffein.SniperClassic",BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.EnforcerGang.Enforcer",BepInDependency.DependencyFlags.SoftDependency)]
    //[BepInDependency("com.rob.HenryMod",BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.Gnome.ChefMod",BepInDependency.DependencyFlags.SoftDependency)]
    //[BepInDependency("com.Bog.Pathfinder",BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.rob.DiggerUnearthed",BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.Bog.Deputy",BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.JavAngle.HouseMod",BepInDependency.DependencyFlags.SoftDependency)]

    [BepInPlugin("xyz.yekoc.PassiveAgression", "Passive Agression","1.3.0" )]
    public class PassiveAgressionPlugin : BaseUnityPlugin
    {
        public static ConfigEntry<bool> unfinishedContent,devIcons;
        internal new static ManualLogSource Logger { get; set; }
        internal static ConfigFile config;
        internal static Sprite unfinishedIcon = Util.SpriteFromFile("StandoffIcon.png");
        internal struct ModList{
            internal bool Paladin;
            internal bool Scepter;
            internal bool Chef;
            internal bool Enforcer;
            internal bool Pathfinder;
            internal bool Sniper;
            internal bool Digger;
            internal bool Henry;
            internal bool Deputy;
            internal bool Ganon;
            internal bool House;
            internal bool Ruina;
            internal bool Ravager;
            internal bool Driver;
        };
        internal static ModList modCompat;
        private void Awake(){
            Logger = base.Logger;
            config = Config;
            unfinishedContent =  Config.Bind("Configuration","Enable Unfinished Content",false,"Enables Unfinished/Potentially Broken Content");
            devIcons =  Config.Bind("Configuration","Prefer Dev Icons",false,"Use jank dev-made icons even when better ones are available");
            SetupVanilla();
        }
        private void Start(){
            if(modCompat.Paladin = Chainloader.PluginInfos.ContainsKey("com.rob.Paladin")){
                SetupPaladin();
            }
            if(modCompat.Chef = Chainloader.PluginInfos.ContainsKey("com.Gnome.ChefMod")){
                SetupChef();
            }
            if(modCompat.Enforcer = Chainloader.PluginInfos.ContainsKey("com.EnforcerGang.Enforcer")){
                SetupEnforcer();
            }
            if(modCompat.Sniper = Chainloader.PluginInfos.ContainsKey("com.Moffein.SniperClassic")){
                SetupSniper();
            }
            if(modCompat.Pathfinder = Chainloader.PluginInfos.ContainsKey("com.Bog.Pathfinder")){
                SetupPathfinder();
            }
            if(modCompat.Digger = Chainloader.PluginInfos.ContainsKey("com.rob.DiggerUnearthed")){
                SetupDigger();
            }
            if(modCompat.Henry = Chainloader.PluginInfos.ContainsKey("com.rob.HenryMod")){
                SetupHenry();
            }
            if(modCompat.Deputy = Chainloader.PluginInfos.ContainsKey("com.Bog.Deputy")){
                SetupDeputy();
            }
            if(modCompat.Ganon = Chainloader.PluginInfos.ContainsKey("com.Ethanol10.Ganondorf")){
                SetupGanon();
            }
            if(modCompat.House = Chainloader.PluginInfos.ContainsKey("com.JavAngle.HouseMod")){
                SetupHouse();
            }
            if(modCompat.Ruina = Chainloader.PluginInfos.ContainsKey("com.Moonlol.UnofficialRiskOfRuina")){
                SetupRuina();
            }
            if(modCompat.Ravager = Chainloader.PluginInfos.ContainsKey("com.rob.Ravager")){
                SetupRavager();
            }
            if(modCompat.Driver = Chainloader.PluginInfos.ContainsKey("com.rob.Driver")){
               // SetupDriver();
            }
            if(modCompat.Scepter = Chainloader.PluginInfos.ContainsKey("com.DestroyedClone.AncientScepter"))
                SetupScepter();

        }
	private void OnDestroy(){

	}
        private void SetupVanilla(){

         GameObject body;

         #region Commando
         body = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Commando/CommandoBody.prefab").WaitForCompletion();
         ConfigAndAdd(ref body.GetComponent<SkillLocator>().special.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Commando.CommandoStimpack.def,
            viewableNode = new ViewablesCatalog.Node(Commando.CommandoStimpack.def.skillNameToken,false,null)
         });
         //Coin.SetMarksman(body,SkillSlot.Secondary);
         #endregion

         #region Bandit
         body = Bandit.StandoffPassive.slot.bodyPrefab;
         ConfigAndAdd(ref Bandit.StandoffPassive.slot.family.variants,new SkillFamily.Variant{
            skillDef = Bandit.StandoffPassive.def,
            viewableNode = new ViewablesCatalog.Node(Bandit.StandoffPassive.def.skillNameToken,false,null)
         },"BanditShowdown");
         ConfigAndAdd(ref body.GetComponent<SkillLocator>().utility.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Bandit.StarchUtil.def,
            viewableNode = new ViewablesCatalog.Node(Bandit.StarchUtil.def.skillNameToken,false,null)
         },"BandtiStarch");
         //Coin.SetMarksman(body,SkillSlot.Secondary);
         /* Crash
         ConfigAndAdd(ref body.GetComponent<SkillLocator>().special.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Bandit.ChainSpecial.def,
            viewableNode = new ViewablesCatalog.Node(Bandit.ChainSpecial.def.skillNameToken,false,null)
         });*/
         #endregion

         #region Captain
         body = Captain.RadiusPassive.slot.bodyPrefab; 
         ConfigAndAdd(ref Captain.RadiusPassive.slot.family.variants,new SkillFamily.Variant{
            skillDef = Captain.RadiusPassive.def,
            viewableNode = new ViewablesCatalog.Node(Captain.RadiusPassive.def.skillNameToken,false,null)
         },"CaptainSupportive",true);
         ConfigAndAdd(ref body.GetComponent<SkillLocator>().special.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Captain.IntegratedBeacon.def,
            viewableNode = new ViewablesCatalog.Node(Captain.IntegratedBeacon.def.skillNameToken,false,null)
         },"CaptainSelfBeacon");

         #endregion

         #region Croco
         body = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Croco/CrocoBody.prefab").WaitForCompletion();
         //ConfigAndAdd(ref body.GetComponent<GenericSkill>().skillFamily.variants,new SkillFamily.Variant{ skillDef = Croc.BeetlePassive.def,viewableNode = new ViewablesCatalog.Node(Croc.BeetlePassive.def.skillNameToken,false,null)});
         ConfigAndAdd(ref body.GetComponent<SkillLocator>().special.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Croc.PathogenSpecial.def,
            viewableNode = new ViewablesCatalog.Node(Croc.PathogenSpecial.def.skillNameToken,false,null)
         });
         #endregion


         #region Engi
         body = Engineer.ScrapPassive.slot.bodyPrefab;
         ConfigAndAdd(ref Engineer.ScrapPassive.slot.family.variants,new SkillFamily.Variant{
            skillDef = Engineer.ScrapPassive.def,
            viewableNode = new ViewablesCatalog.Node(Engineer.ScrapPassive.def.skillNameToken,false,null)
         },"EngiScrap");
         ConfigAndAdd(ref body.GetComponent<SkillLocator>().primary.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Engineer.ResonancePrimary.def,
            viewableNode = new ViewablesCatalog.Node(Engineer.ResonancePrimary.def.skillNameToken,false,null)
         },"EngiResonance");
         #endregion

         #region Huntress
         body = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Huntress/HuntressBody.prefab").WaitForCompletion();
         ConfigAndAdd(ref body.GetComponent<SkillLocator>().secondary.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Huntress.LightMine.def,
            viewableNode = new ViewablesCatalog.Node(Huntress.LightMine.def.skillNameToken,false,null)
         },"",true);
         #endregion

         #region Loader
         body = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Loader/LoaderBody.prefab").WaitForCompletion();
         /*ConfigAndAdd(ref Loader.LoaderPassiveZaHando.slot.family.variants,new SkillFamily.Variant{
              skillDef = Loader.LoaderPassiveZaHando.def,
              viewableNode = new ViewablesCatalog.Node(Loader.LoaderPassiveZaHando.def.skillNameToken,false,null)
         },"LoaderZaHando");*/
         ConfigAndAdd(ref body.GetComponent<SkillLocator>().secondary.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Loader.LoaderZipline.def,
            viewableNode = new ViewablesCatalog.Node(Loader.LoaderZipline.def.skillNameToken,false,null)
         },"",true);
         ConfigAndAdd(ref body.GetComponent<SkillLocator>().special.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Loader.ElectrifySpecial.def,
            viewableNode = new ViewablesCatalog.Node(Loader.ElectrifySpecial.def.skillNameToken,false,null)
         },"",true);
         #endregion
        
         #region Mage
         body = Mage.BleedPassive.slot.bodyPrefab;

         ConfigAndAdd(ref body.GetComponent<SkillLocator>().secondary.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Mage.FloatingChaos.def,
            viewableNode = new ViewablesCatalog.Node(Mage.FloatingChaos.def.skillNameToken,false,null)
         },"",true);
         ConfigAndAdd(ref body.GetComponent<SkillLocator>().special.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Mage.IceGolemSpecial.def,
            viewableNode = new ViewablesCatalog.Node(Mage.IceGolemSpecial.def.skillNameToken,false,null)
         },"MageIceArmor");
         ConfigAndAdd(ref Mage.BleedPassive.slot.family.variants,new SkillFamily.Variant{
            skillDef = Mage.BleedPassive.def,
            viewableNode = new ViewablesCatalog.Node(Mage.BleedPassive.def.skillNameToken,false,null)
         },"MageBloodrite");
         ConfigAndAdd(ref body.GetComponent<SkillLocator>().primary.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Mage.BloodPrimary.def,
            viewableNode = new ViewablesCatalog.Node(Mage.BloodPrimary.def.skillNameToken,false,null)
         },"MageBloodbolt");
         ConfigAndAdd(ref body.GetComponent<SkillLocator>().special.skillFamily.variants,new SkillFamily.Variant{
            skillDef = Mage.BloodDotSkill.def,
            viewableNode = new ViewablesCatalog.Node(Mage.BloodDotSkill.def.skillNameToken,false,null)
         },"MageBloodConclude");
         #endregion

         #region Merc
         body = Merc.FlickerPassive.slot.bodyPrefab;
         /*ConfigAndAdd(ref Merc.FlickerPassive.slot.family.variants ,new SkillFamily.Variant{
            skillDef = Merc.FlickerPassive.def,
            viewableNode = new ViewablesCatalog.Node(Merc.FlickerPassive.def.skillNameToken,false,null)
         },"",true);
         ConfigAndAdd(ref body.GetComponent<SkillLocator>().special.skillFamily.variants ,new SkillFamily.Variant{
            skillDef = Merc.LivingForce.def,
            viewableNode = new ViewablesCatalog.Node(Merc.LivingForce.def.skillNameToken,false,null)
         },"",true);*/
         #endregion

         #region Treebot
         //body = Treebot.SprintClimbPassive.slot.bodyPrefab;
         /*Super Buggy
         ConfigAndAdd(ref Treebot.SprintClimbPassive.slot.family.variants,new SkillFamily.Variant{
            skillDef = Treebot.SprintClimbPassive.def,
            viewableNode = new ViewablesCatalog.Node(Treebot.SprintClimbPassive.def.skillNameToken,false,null)
         });*/
         #endregion

         #region Viend
         body = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/VoidSurvivor/VoidSurvivorBody.prefab").WaitForCompletion();
         ConfigAndAdd(ref body.GetComponent<GenericSkill>().skillFamily.variants,new SkillFamily.Variant{
            skillDef = VoidSurvivor.InfestationPassive.def,
            viewableNode = new ViewablesCatalog.Node(VoidSurvivor.InfestationPassive.def.skillNameToken,false,null)
         },"ViendBug");
         ConfigAndAdd(ref body.GetComponent<SkillLocator>().utility.skillFamily.variants,new SkillFamily.Variant{
            skillDef = VoidSurvivor.TearUtil.def,
            viewableNode = new ViewablesCatalog.Node(VoidSurvivor.TearUtil.def.skillNameToken,false,null)
         },"ViendTear");
         #endregion

         #region Railgunner
         body = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/Railgunner/RailgunnerBody.prefab").WaitForCompletion();
         //Coin.SetMarksman(body,SkillSlot.Utility);
         ConfigAndAdd(ref body.GetComponent<GenericSkill>().skillFamily.variants,new SkillFamily.Variant{
            skillDef = Railgunner.RailgunnerPassive.def,
            viewableNode = new ViewablesCatalog.Node(Railgunner.RailgunnerPassive.def.skillNameToken,false,null)
         },"RailMissile");
         #endregion
        }

        private void ConfigAndAdd(ref SkillFamily.Variant[] family,SkillFamily.Variant variant,string name = "",bool unfinished = false){
           if(!unfinished || unfinishedContent.Value){
            var conf = Config.Bind<bool>((name != string.Empty) ? name : variant.skillDef.skillNameToken[17] + variant.skillDef.skillNameToken.Substring(18).ToLowerInvariant(),"Enabled",true,"Enables this skill");
            if(conf.Value){
              HG.ArrayUtils.ArrayAppend(ref family,variant);
            }
           }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupPaladin(){
            var body = ModCompat.PaladinDesignPassive.slot.bodyPrefab;
            ConfigAndAdd(ref ModCompat.PaladinDesignPassive.slot.family.variants,new SkillFamily.Variant{
                    skillDef = ModCompat.PaladinDesignPassive.def,
                    viewableNode = new ViewablesCatalog.Node(ModCompat.PaladinDesignPassive.def.skillNameToken,false,null),
                    unlockableDef = PaladinMod.Modules.Unlockables.paladinLunarShardSkillDef
            },"",true);
            var skillFamily = body.GetComponent<SkillLocator>()?.special?.skillFamily;
            if(skillFamily){
            ConfigAndAdd(ref skillFamily.variants, new SkillFamily.Variant{
                    skillDef = ModCompat.PaladinGlassShadow.def,
                    viewableNode = new ViewablesCatalog.Node(ModCompat.PaladinGlassShadow.def.skillNameToken,false,null),
                    unlockableDef = PaladinMod.Modules.Unlockables.paladinLunarShardSkillDef
            },"PaladinClone");
            ConfigAndAdd(ref skillFamily.variants, new SkillFamily.Variant{
                    skillDef = ModCompat.PaladinResolve.def,
                    viewableNode = new ViewablesCatalog.Node(ModCompat.PaladinResolve.def.skillNameToken,false,null)
            },"PaladinResolve",true);
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupScepter(){
         AncientScepter.AncientScepterItem.instance.RegisterScepterSkill(Commando.CommandoStimpackScepter.def, "CommandoBody", Commando.CommandoStimpack.def);
         AncientScepter.AncientScepterItem.instance.RegisterScepterSkill(Croc.PathogenSpecialScepter.def,"CrocoBody",Croc.PathogenSpecial.def);
         //AncientScepter.AncientScepterItem.instance.RegisterScepterSkill(Mage.IceGolemScepter.def,"MageBody",Mage.IceGolemSpecial.def); // Buggy
         if(modCompat.Paladin){
           ModCompat.PaladinGlassShadow.SetUpScepter();
           if(unfinishedContent.Value){
               ModCompat.PaladinResolve.SetUpScepter();
           }
         }
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupChef(){
           var body = ModCompat.ChefPantryPassive.slot.bodyPrefab;
           ConfigAndAdd(ref ModCompat.ChefPantryPassive.slot.family.variants,new SkillFamily.Variant{
                   skillDef = ModCompat.ChefPantryPassive.def,
                   viewableNode = new ViewablesCatalog.Node(ModCompat.ChefPantryPassive.def.skillNameToken,false,null)
           },"ChefPantry");
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupEnforcer(){
            var skillFamily = Modules.Characters.EnforcerSurvivor.instance.bodyPrefab.GetComponent<SkillLocator>()?.special?.skillFamily;
            if(skillFamily)
            ConfigAndAdd(ref skillFamily.variants, new SkillFamily.Variant{
                    skillDef = ModCompat.EnforcerSundowner.def,
                    viewableNode = new ViewablesCatalog.Node(ModCompat.EnforcerSundowner.def.skillNameToken,false,null),
                    unlockableDef = Modules.EnforcerUnlockables.enforcerDesperadoSkinUnlockableDef
            });

        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupSniper(){
            ////Coin.SetMarksman(SniperClassic.SniperClassic.SniperBody,SkillSlot.Utility);
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupPathfinder(){
         var body = Pathfinder.PathfinderPlugin.pathfinderBodyPrefab;
         ConfigAndAdd(ref body.GetComponent<SkillLocator>().secondary.skillFamily.variants,new SkillFamily.Variant{
            skillDef = ModCompat.PathfinderForcedMove.def,
            viewableNode = new ViewablesCatalog.Node(ModCompat.PathfinderForcedMove.def.skillNameToken,false,null),
         },"PathfinderForcedMove",true);
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupDeputy(){  
         ConfigAndAdd(ref ModCompat.DeputyPassive.slot.family.variants,new SkillFamily.Variant{
            skillDef = ModCompat.DeputyPassive.def,
            viewableNode = new ViewablesCatalog.Node(ModCompat.DeputyPassive.def.skillNameToken,false,null),
         },"DeputyBounce");
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupGanon(){ 
         ConfigAndAdd(ref ModCompat.GanonPassive.slot.family.variants,new SkillFamily.Variant{
            skillDef = ModCompat.GanonPassive.def,
            viewableNode = new ViewablesCatalog.Node(ModCompat.GanonPassive.def.skillNameToken,false,null),
         },"",true);
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupDigger(){
         ConfigAndAdd(ref ModCompat.DiggerBlacksmithPassive.slot.family.variants,new SkillFamily.Variant{
            skillDef = ModCompat.DiggerBlacksmithPassive.def,
            viewableNode = new ViewablesCatalog.Node(ModCompat.DiggerBlacksmithPassive.def.skillNameToken,false,null),
            unlockableDef = DiggerPlugin.Unlockables.blacksmithUnlockableDef
         },"DiggerBlacksmith");
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupHouse(){
          var body = HouseMod.Modules.Survivors.House.characterPrefab;
          var deckFam = body.GetComponent<GenericSkill>().skillFamily;
          (deckFam as ScriptableObject).name += "HouseDeck"; 
          foreach(var deck in ModCompat.HousePassive.defs){
            ConfigAndAdd(ref deckFam.variants,new SkillFamily.Variant{
               skillDef = deck,
               viewableNode = new ViewablesCatalog.Node(deck.skillNameToken,false,null),
            },"",true);
          }
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupRuina(){
          var body = ModCompat.ArgaliaPassive.slot.bodyPrefab;
            ConfigAndAdd(ref ModCompat.ArgaliaPassive.slot.family.variants,new SkillFamily.Variant{
               skillDef = ModCompat.ArgaliaPassive.def,
               viewableNode = new ViewablesCatalog.Node(ModCompat.ArgaliaPassive.def.skillNameToken,false,null),
               unlockableDef = RiskOfRuinaMod.Modules.Survivors.RedMist.masterySkinUnlockableDef
            },"RuinaArgaliaPassive");

          body = RiskOfRuinaMod.Modules.Survivors.AnArbiter.instance.bodyPrefab;
            ConfigAndAdd(ref body.GetComponent<SkillLocator>().secondary.skillFamily.variants,new SkillFamily.Variant{
               skillDef = ModCompat.ArbiterBirdcage.def,
               viewableNode = new ViewablesCatalog.Node(ModCompat.ArbiterBirdcage.def.skillNameToken,false,null),
            },"RuinaArbiterBirdcage",true);
          
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupRavager(){
          var body = ModCompat.RavagerBloodPassive.slot.bodyPrefab;
            ConfigAndAdd(ref ModCompat.RavagerBloodPassive.slot.family.variants,new SkillFamily.Variant{
               skillDef = ModCompat.RavagerBloodPassive.def,
               viewableNode = new ViewablesCatalog.Node(ModCompat.RavagerBloodPassive.def.skillNameToken,false,null)
            },"RavagerBloodPassive");
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupDriver(){
            if(unfinishedContent.Value){
                //new ModCompat.DriverStarSeed().Init();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void SetupHenry(){
         /* CharacterBody body = HenryMod.Modules.Survivors.Henry.instance.bodyPrefab.GetComponent<CharacterBody>();
          if(unfinishedContent.Value){
             ConfigAndAdd(ref body.skillLocator.secondary.skillFamily.variants,new SkillFamily.Variant{
                skillDef = ModCompat.HenryYomiJC.def,
                viewableNode = new ViewablesCatalog.Node(ModCompat.HenryYomiJC.def.skillNameToken,false,null),
             });
          }*/
        }


    }
}
