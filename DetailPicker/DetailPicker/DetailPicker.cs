using BepInEx;
using BepInEx.Configuration;
using RoR2;
using System;
using System.Security;
using System.Security.Permissions;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using static RoR2.UI.LoadoutPanelController;
using static RoR2.SkinDef;
using static RoR2.CharacterModel;
using HG;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2BepInExPack.Utilities;

#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618
[assembly: HG.Reflection.SearchableAttribute.OptIn]

namespace DetailPicker
{
    [BepInPlugin("xyz.yekoc.DetailPicker", "Skin Detail Picker","2.0.0" )]
    [BepInDependency("com.bepis.r2api")]
    [BepInDependency("com.rune580.riskofoptions",BepInDependency.DependencyFlags.SoftDependency)]
    [R2API.Utils.NetworkCompatibility(R2API.Utils.CompatibilityLevel.EveryoneMustHaveMod)]
    public class SkinDetailPickerPlugin : BaseUnityPlugin{
        public static Dictionary<BodyIndex,CharacterDetailCatalog> detailCatalog = new();
        public static List<string> blacklist = new List<string>(){
            "RobHunkBody"
        };

        public class CharacterDetail<T>{
            public T detail;
            public SkinDef sourceSkin;
            public int index;
            public string path;
            public Action<GameObject> applyAction;
        }
        public class CharacterDetailCatalog{
            public Dictionary<BodyIndex,List<CharacterDetail<MinionSkinReplacement>>> minionSkins = new();
            public Dictionary<GameObject,List<CharacterDetail<ProjectileGhostReplacement>>> projectiles = new();
            public Dictionary<Renderer,List<CharacterDetail<RendererInfo>>> infoTs = new();
            public Dictionary<Renderer,List<CharacterDetail<MeshReplacement>>> meshTs = new();
            public Dictionary<GameObject,List<CharacterDetail<bool>>> actT = new();
        }

        [SystemInitializer(new Type[]{typeof(SurvivorCatalog),typeof(SkinCatalog)})]
        public static void CreateCatalog(){
            foreach(var surv in SurvivorCatalog.allSurvivorDefs){
                var index = SurvivorCatalog.survivorIndexToBodyIndex[(int)surv.survivorIndex];
                if(blacklist.Contains(surv.bodyPrefab.name)){
                    continue;
                }
                if(detailCatalog.ContainsKey(index)){
                    //Two Survivors with the same body, would this actually happen?
                    continue;
                }
                var subCatalog = new CharacterDetailCatalog();
                var skins = SkinCatalog.GetBodySkinDefs(index);
                foreach(var skin in skins){
                    var name = Language.GetString(skin.nameToken);
                    foreach(var minion in skin.minionSkinReplacements){
                        var mIndex = BodyCatalog.FindBodyIndex(minion.minionBodyPrefab);
                        var mList = (subCatalog.minionSkins.ContainsKey(mIndex))? subCatalog.minionSkins[mIndex] : new List<CharacterDetail<MinionSkinReplacement>>();
                        mList.Add(new CharacterDetail<MinionSkinReplacement>{ detail = minion,sourceSkin = skin,index = mList.Count});
                        if(mList.Count <= 1){
                           subCatalog.minionSkins.Add(mIndex,mList);
                        }
                    }
                    foreach(var proj in skin.projectileGhostReplacements){
                        var pList = (subCatalog.projectiles.ContainsKey(proj.projectilePrefab)) ? subCatalog.projectiles[proj.projectilePrefab] : new List<CharacterDetail<ProjectileGhostReplacement>>();
                        pList.Add(new CharacterDetail<ProjectileGhostReplacement>{detail = proj,sourceSkin = skin,index = pList.Count});
                        if(pList.Count <= 1){
                           subCatalog.projectiles.Add(proj.projectilePrefab,pList);
                        }
                    }
                   foreach(var actT in skin.gameObjectActivations){
                        var aList = (subCatalog.actT.ContainsKey(actT.gameObject)) ? subCatalog.actT[actT.gameObject] : new List<CharacterDetail<bool>>();
                        aList.Add(new CharacterDetail<bool>{detail = actT.shouldActivate,sourceSkin = skin,index = aList.Count,path = Util.BuildPrefabTransformPath(skin.rootObject.transform,actT.gameObject.transform)});
                        if(aList.Count <= 1){
                            subCatalog.actT.Add(actT.gameObject,aList);
                        }
                    }
                    foreach(var infoT in skin.rendererInfos){
                        var iList = (subCatalog.infoTs.ContainsKey(infoT.renderer)) ? subCatalog.infoTs[infoT.renderer] : new List<CharacterDetail<RendererInfo>>();
                        iList.Add(new CharacterDetail<RendererInfo>{detail = infoT,sourceSkin = skin,index = iList.Count,path = Util.BuildPrefabTransformPath(skin.rootObject.transform,infoT.renderer.transform)});
                        if(iList.Count <= 1){
                            subCatalog.infoTs.Add(infoT.renderer,iList);
                        }
                    }
                    foreach(var meT in skin.meshReplacements){
                        var meList = (subCatalog.meshTs.ContainsKey(meT.renderer)) ? subCatalog.meshTs[meT.renderer] : new List<CharacterDetail<MeshReplacement>>();
                        meList.Add(new CharacterDetail<MeshReplacement>{detail = meT,sourceSkin = skin,index = meList.Count,path = Util.BuildPrefabTransformPath(skin.rootObject.transform,meT.renderer.transform)});
                        if(meList.Count <= 1){
                            subCatalog.meshTs.Add(meT.renderer,meList);
                        }
                    }
                }
                //Adjust default buttons for projectiles & minions
                foreach(var mini in subCatalog.minionSkins){
                    if(!(skins[0].minionSkinReplacements.Any(m => BodyCatalog.FindBodyIndex(m.minionBodyPrefab) == mini.Key))){
                       foreach(var entry in mini.Value){
                         entry.index++;
                       }
                       mini.Value.Insert(0,new CharacterDetail<MinionSkinReplacement>{detail = default,sourceSkin = skins[0],index = (-2)});
                    }
                }
                foreach(var proj in subCatalog.projectiles){
                    if(!(skins[0].projectileGhostReplacements.Any(m => m.projectilePrefab == proj.Key))){
                       foreach(var entry in proj.Value){
                         entry.index++;
                       }
                       proj.Value.Insert(0,new CharacterDetail<ProjectileGhostReplacement>{detail = default,sourceSkin = skins[0],index = (-2)});
                    }
                }
                var specialindex = BodyCatalog.FindBodyIndex("EngiBody");
                if(index == specialindex){
                 /*   var engiSkills =  BodyCatalog.GetBodyPrefabBodyComponent(specialindex).skillLocator.special.skillFamily.variants.Select(s => s.skillDef);
                    var turret1 = BodyCatalog.FindBodyIndex("");
                    var turret2 = BodyCatalog.FindBodyIndex("");
                    foreach(var turret in subCatalog.minionSkins.Where(k => k.Key == turret1 || k.Key == turret2).SelectMany((a) => a.Value)){
                        turret.applyAction += (GameObject model) =>{
                          var load = model.GetComponentInParent<RoR2.SurvivorMannequins.SurvivorMannequinSlotController>()?.currentLoadout;
                          if(load != null){
                              turret.detail.minionSkin.Apply(model.transform.Find("mdlEngiTurret").gameObject); 
                          }
                        };
                    }*/
                }
                else if(index == BodyCatalog.FindBodyIndex("RobPaladinBody")){
                    HandlePaladin(subCatalog);
                }
                else if(index == BodyCatalog.FindBodyIndex("RobRavagerBody")){
                    HandleRavager(subCatalog);
                }
                else if(index == BodyCatalog.FindBodyIndex("PathfinderBody")){
                    HandlePathfinder(subCatalog);
                }
                else if(index == BodyCatalog.FindBodyIndex("RedMistBody")){
                    HandleRuina(subCatalog);
                }
                detailCatalog.Add(index,subCatalog);
            }
        }

        public class OverlaySkin{
           public SkinDef orig;
           public RuntimeSkin rSkin;
           public BodyIndex bodyIndex;
           public bool seperateMaterials;
           public List<MinionSkinReplacement> minions;
           public List<ProjectileGhostReplacement> projectiles;
           public int[] syncInfo;
           public Action<GameObject> onApply;

           public int FindIndex(int category){
              return (syncInfo != null && category < syncInfo.Length) ? syncInfo[category] : 0;
           }

           public static void GenerateSyncFromSkinRows(ref int[] sync,SkinDef skin,IEnumerable<Row> detailRows){
              var i = 0;
              foreach(var row in (detailRows as IEnumerable<Row>).Reverse()){
                  var index = row.buttons.FindIndex((b) => { var t = b.GetComponent<RoR2.UI.TooltipProvider>().overrideTitleText;
                          return t.Contains(Language.GetString(skin.nameToken));
                  });
                  if(index == -1 && skin.baseSkins.Any()){
                      index =row.buttons.FindIndex((b) => { var t = b.GetComponent<RoR2.UI.TooltipProvider>().overrideTitleText;
                          return t.Contains(Language.GetString(skin.baseSkins.Last().nameToken));
                  });

                  }
                  if(index == -1){
                      index =(row.buttons.FindIndex((b) => { var t = b.GetComponent<RoR2.UI.TooltipProvider>().overrideTitleText;
                          return t.Contains(Language.GetString("Disabled"));//Just in case someone translates this.
                  }) != (-1)) ? (-2) : (-1);
                  }
                  if(index != -1){
                    sync[rowCountStore -1 -i] = index;
                    row.UpdateHighlightedChoice();
                  }
                  i++;
              }
           }

           public void BuildRSkin(int[] syncInfo){
               orig.Bake();
               rSkin = new RuntimeSkin{
                   meshReplacementTemplates = ArrayUtils.Clone(orig.runtimeSkin.meshReplacementTemplates),
                   rendererInfoTemplates = ArrayUtils.Clone(orig.runtimeSkin.rendererInfoTemplates),
                   gameObjectActivationTemplates = ArrayUtils.Clone(orig.runtimeSkin.gameObjectActivationTemplates)
               };
               minions.Clear();
               projectiles.Clear();
               onApply = null;
               int count = 0;
               var skins = BodyCatalog.GetBodySkins(bodyIndex);
               if(!detailCatalog.ContainsKey(bodyIndex) || syncInfo.Length <= 0){
                   minions.AddRange(orig.minionSkinReplacements);
                   projectiles.AddRange(orig.projectileGhostReplacements); 
                   return;
               }
               var details = detailCatalog[bodyIndex];
               if(seperateMaterials){
                foreach(var m in details.meshTs ){
                   if(m.Value.Count <= 1){
                       continue;
                   }
                   var sync = syncInfo[count];
                   if(sync >= m.Value.Count){
                       count++;
                       continue;
                   }
                   if(sync == (-2) || (sync == 0 && !skins[0].meshReplacements.Any(r => r.renderer == m.Key) )){
                       var index = Array.FindIndex(rSkin.meshReplacementTemplates, r => r.path == m.Value[0].path);
                       if(index != (-1)){
                           ArrayUtils.ArrayRemoveAtAndResize(ref rSkin.meshReplacementTemplates,index);
                       } 
                   }
                   else{
                       var repl = m.Value[sync];
                       ArrayUtils.ArrayAppend(ref rSkin.meshReplacementTemplates,new MeshReplacementTemplate{path = repl.path,mesh = repl.detail.mesh});
                       onApply += repl.applyAction;
                   }
                   count++;
                }
                foreach(var i in details.infoTs){
                   if(i.Value.Count <= 1){
                       continue;
                   }
                   var sync = syncInfo[count];
                   if(sync >= i.Value.Count){
                       count++;
                       continue;
                   }
                   var index = Array.FindIndex(rSkin.rendererInfoTemplates, r => r.path == i.Value[0].path);
                   if(index != (-1)){
                       ArrayUtils.ArrayRemoveAtAndResize(ref rSkin.rendererInfoTemplates,index);
                   }
                   if(!(sync == (-2) || (sync == 0 && !skins[0].rendererInfos.Any(r => r.renderer == i.Key) ))){
                       var repl = i.Value[sync];
                       ArrayUtils.ArrayAppend(ref rSkin.rendererInfoTemplates,new RendererInfoTemplate{path = repl.path,data = repl.detail});
                       onApply += repl.applyAction;
                   }
                   count++;
                }
               }
               else{
                foreach(var r in details.meshTs.Keys.Union(details.infoTs.Keys).Distinct()){
                   int bailCount = 0;
                   if(details.meshTs.ContainsKey(r)){
                       bailCount += details.meshTs[r].Count;
                       var index = Array.FindIndex(rSkin.meshReplacementTemplates, t => t.path == details.meshTs[r][0].path);
                       if(index != (-1)){
                           ArrayUtils.ArrayRemoveAtAndResize(ref rSkin.meshReplacementTemplates,index);
                       } 
                   }
                   if(details.infoTs.ContainsKey(r)){
                       bailCount += details.infoTs[r].Count;
                       var index = Array.FindIndex(rSkin.rendererInfoTemplates, t => t.path == details.infoTs[r][0].path);
                       if(index != (-1)){
                           ArrayUtils.ArrayRemoveAtAndResize(ref rSkin.rendererInfoTemplates,index);
                       }
                   }
                   if(bailCount <= 1){
                       continue;
                   }
                   var sync = syncInfo[count];
                   if(!(sync == (-2) || (sync == 0 && !skins[0].meshReplacements.Any(m => m.renderer == r) && !skins[0].rendererInfos.Any(i => i.renderer == r )))){
                       SkinDef meshskin = null;
                       if(details.meshTs.ContainsKey(r) && details.meshTs[r].Count > sync){
                           var repl = details.meshTs[r][sync];
                           meshskin = repl.sourceSkin;
                           ArrayUtils.ArrayAppend(ref rSkin.meshReplacementTemplates,new MeshReplacementTemplate{path = repl.path,mesh = repl.detail.mesh});
                           onApply += repl.applyAction;
                           if(details.infoTs.ContainsKey(r)){
                             var matrepl = details.infoTs[r].FirstOrDefault(i => i.sourceSkin == repl.sourceSkin);
                             if(matrepl.sourceSkin){
                               ArrayUtils.ArrayAppend(ref rSkin.rendererInfoTemplates,new RendererInfoTemplate{path = matrepl.path,data = matrepl.detail});
                               onApply += matrepl.applyAction;
                             }
                           }
                       }
                       else if(details.infoTs.ContainsKey(r) && details.infoTs[r].Count > sync){
                           var repl = details.infoTs[r][sync];
                               ArrayUtils.ArrayAppend(ref rSkin.rendererInfoTemplates,new RendererInfoTemplate{path = repl.path,data = repl.detail});
                               onApply += repl.applyAction;
                       }
                   }
                   count++;
                }
               }
               /*//Object Activation
               foreach(var act in details.actT.Keys.Except(details.meshTs.Keys.Concat(details.infoTs.Keys).Select(r => r.gameObject))){
                   var sync = syncInfo[count];
                   var index = Array.FindIndex(rSkin.gameObjectActivationTemplates, t => t.path == details.actT[act][0].path);
                   bool detail = (sync == 0);
                   if(index != (-1) && rSkin.gameObjectActivationTemplates[index].shouldActivate != detail){
                       ArrayUtils.ArrayRemoveAtAndResize(ref rSkin.gameObjectActivationTemplates,index);
                   }
                   else if(index == (-1)){
                       ArrayUtils.ArrayAppend(ref rSkin.gameObjectActivationTemplates,new GameObjectActivationTemplate{path = details.actT[act][0].path, shouldActivate = detail });
                   }
                   count++;
               }
               //*/
               foreach(var m in details.minionSkins){
                   var sync = syncInfo[count];
                   if(sync >= m.Value.Count){
                       count++;
                       continue;
                   }
                   if(sync == (-2)){
                     minions.RemoveAll(r => BodyCatalog.FindBodyIndex(r.minionBodyPrefab) == m.Key);
                     foreach(var r in m.Value){
                         onApply -= r.applyAction;
                         if(r.index == (-2)){
                             onApply += r.applyAction;
                         }
                     }
                   }
                   else if(m.Value[sync].detail.minionSkin){
                     minions.Add(m.Value[sync].detail);
                     onApply += m.Value[sync].applyAction;
                   }
                   count++;
               }
               foreach(var p in details.projectiles){
                   var sync = syncInfo[count];
                   if(sync >= p.Value.Count){
                       count++;
                       continue;
                   }
                   if(sync == (-2)){
                     projectiles.RemoveAll(r => r.projectilePrefab == p.Key);
                     foreach(var r in p.Value){
                         onApply -= r.applyAction;
                         if(r.index == (-2)){
                             onApply += r.applyAction;
                         }
                     }
                   }
                   else if(p.Value[sync].detail.projectileGhostReplacementPrefab){
                     projectiles.Add(p.Value[sync].detail);
                     onApply += p.Value[sync].applyAction;
                   }
                   count++;
               }
               
           }
        }

        public static bool lobbySkinFix = false;
        public static bool ravagerFix = false;
        public static bool pathfinderFix = false;
        public Sprite icon = LegacyResourcesAPI.Load<RoR2.Skills.SkillDef>("SkillDefs/CaptainBody/CaptainSkillUsedUp").icon;
        public static OverlaySkin oSkin {
           get => networkOSkins.GetOrCreateValue(localProfile.loadout);
           set { networkOSkins.Remove(localProfile.loadout); networkOSkins.Add(localProfile.loadout,value);}
        } 
        public static FixedConditionalWeakTable<Loadout,OverlaySkin> networkOSkins = new();
        public ConfigEntry<bool> seperateMaterials;
        public static UserProfile localProfile;
        public List<Row> detailRows = new List<Row>();
        public static int rowCountStore;



        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void HandleRiskOfOptions(){
            RiskOfOptions.ModSettingsManager.SetModDescription("Mix and Match Skins");
            RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(seperateMaterials));
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void HandleLobbySkinFix(){
          new MonoMod.RuntimeDetour.Hook(typeof(LobbySkinsFix.ReverseSkin).GetMethod("Initialize",(System.Reflection.BindingFlags)(-1)),hook);
          [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
          void hook(Action<LobbySkinsFix.ReverseSkin,GameObject,SkinDef> orig,LobbySkinsFix.ReverseSkin self,GameObject model,SkinDef skin){
            var rt = skin.runtimeSkin;
            var load = model.GetComponent<CharacterModel>()?.body?.master?.loadout;
            skin.runtimeSkin = (load != null && networkOSkins.TryGetValue(load,out var oSkin) && oSkin.orig == skin)? oSkin.rSkin : rt;
            orig(self,model,skin);
            skin.runtimeSkin = rt;
          }
        }

        
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void HandleRuina(CharacterDetailCatalog details){
            foreach(var repl in details.meshTs.SelectMany(x => x.Value)){
                repl.applyAction += (model) =>{
                    var redTracker = model.GetComponent<CharacterModel>()?.body?.GetComponent<RiskOfRuinaMod.Modules.Components.RedMistStatTracker>();
                    if(!redTracker){return;}
                    if(repl.sourceSkin.nameToken == "COF_REDMIST_BODY_MASTERY_SKIN_NAME"){
                        if(repl.path.Contains("Body")){
                           var childLocator = redTracker.gameObject.GetComponentInChildren<ChildLocator>();
                            redTracker.EGOActivatePrefab = RiskOfRuinaMod.Modules.Assets.argaliaEGOActivate;
                            redTracker.musicName = "Play_ArgaliaMusic";
                            if(childLocator){
                                redTracker.mistEffect = childLocator.FindChild("ArgaliaCloud").GetComponent<ParticleSystem>();
				childLocator.FindChild("ParticleHair").GetChild(0).gameObject.SetActive(false);
				childLocator.FindChild("ParticleHair").GetChild(1).gameObject.SetActive(true);
                            }
                        }
                    }
                    else if( repl.sourceSkin.nameToken == "COF_REDMIST_BODY_DEFAULT_SKIN_NAME"){
                        if(repl.path.Contains("Body")){
                            var childLocator = redTracker.gameObject.GetComponentInChildren<ChildLocator>();
                            redTracker.musicName = "Play_Ruina_Boss_Music";
                            redTracker.EGOActivatePrefab = RiskOfRuinaMod.Modules.Assets.argaliaEGOActivate;
                            if(childLocator){
                                redTracker.mistEffect = childLocator.FindChild("BloodCloud").GetComponent<ParticleSystem>();
				childLocator.FindChild("ParticleHair").GetChild(0).gameObject.SetActive(true);
				childLocator.FindChild("ParticleHair").GetChild(1).gameObject.SetActive(false);
                            }
                        }
                    }
                };
            }
            foreach(var repl in details.infoTs.SelectMany(x => x.Value)){
                repl.applyAction += (model) =>{
                    var redTracker = model.GetComponent<CharacterModel>()?.body?.GetComponent<RiskOfRuinaMod.Modules.Components.RedMistStatTracker>();
                    if(!redTracker){return;}
                    if(repl.sourceSkin.nameToken == "COF_REDMIST_BODY_MASTERY_SKIN_NAME"){
                        if(repl.path.Contains("Mimicry")){
                           redTracker.slashPrefab = RiskOfRuinaMod.Modules.Assets.argaliaSwordSwingEffect;
                           redTracker.piercePrefab = RiskOfRuinaMod.Modules.Assets.argaliaSpearPierceEffect;
                           redTracker.EGOSlashPrefab = RiskOfRuinaMod.Modules.Assets.argaliaEGOSwordSwingEffect;
                           redTracker.EGOPiercePrefab = RiskOfRuinaMod.Modules.Assets.argaliaEGOSpearPierceEffect;
                           redTracker.hitEffect = RiskOfRuinaMod.Modules.Assets.argaliaSwordHitEffect;
                           redTracker.phaseEffect = RiskOfRuinaMod.Modules.Assets.argaliaPhaseEffect;
                           redTracker.groundPoundEffect = RiskOfRuinaMod.Modules.Assets.argaliaGroundPoundEffect;
                           redTracker.spinPrefab = RiskOfRuinaMod.Modules.Assets.argaliaSwordSpinEffect;
                           redTracker.spinPrefabTwo = RiskOfRuinaMod.Modules.Assets.argaliaSwordSpinEffectTwo;
                           redTracker.counterBurst = RiskOfRuinaMod.Modules.Assets.argaliaCounterBurst;
                           redTracker.afterimageBlock = RiskOfRuinaMod.Modules.Assets.argaliaAfterimageBlock;
                           redTracker.afterimageSlash = RiskOfRuinaMod.Modules.Assets.argaliaAfterimageSlash;
                        }
                    }
                    else if( repl.sourceSkin.nameToken == "COF_REDMIST_BODY_DEFAULT_SKIN_NAME"){
                        if(repl.path.Contains("Mimicry")){
                           redTracker.slashPrefab = RiskOfRuinaMod.Modules.Assets.swordSwingEffect;
                           redTracker.piercePrefab = RiskOfRuinaMod.Modules.Assets.spearPierceEffect;
                           redTracker.EGOSlashPrefab = RiskOfRuinaMod.Modules.Assets.EGOSwordSwingEffect;
                           redTracker.EGOPiercePrefab = RiskOfRuinaMod.Modules.Assets.EGOSpearPierceEffect;
                           redTracker.hitEffect = RiskOfRuinaMod.Modules.Assets.swordHitEffect;
                           redTracker.phaseEffect = RiskOfRuinaMod.Modules.Assets.phaseEffect;
                           redTracker.groundPoundEffect = RiskOfRuinaMod.Modules.Assets.groundPoundEffect;
                           redTracker.spinPrefab = RiskOfRuinaMod.Modules.Assets.swordSpinEffect;
                           redTracker.spinPrefabTwo = RiskOfRuinaMod.Modules.Assets.swordSpinEffectTwo;
                           redTracker.counterBurst = RiskOfRuinaMod.Modules.Assets.counterBurst;
                           redTracker.afterimageBlock = RiskOfRuinaMod.Modules.Assets.afterimageBlock;
                           redTracker.afterimageSlash = RiskOfRuinaMod.Modules.Assets.afterimageSlash;
                        }
                    }
                };
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void HandlePathfinder(CharacterDetailCatalog details){
            if(!pathfinderFix){
                new MonoMod.RuntimeDetour.Hook(typeof(Pathfinder.Components.SquallController).GetMethod("Start",(System.Reflection.BindingFlags)(-1)),hook);
                pathfinderFix = true;
            }
            foreach(var repl in details.minionSkins[BodyCatalog.FindBodyIndex("SquallBody")]){
                repl.applyAction += (model) =>{
                   var comp = model.GetComponent<CharacterSelectSurvivorPreviewDisplayController>();
                   var responseIndex = Math.Max(0,repl.index);
                   if(comp && comp.skinChangeResponses.Length > responseIndex  && model.GetComponentInParent<RoR2.SurvivorMannequins.SurvivorMannequinSlotController>()){
                       comp.skinChangeResponses[responseIndex].response?.Invoke();
                   }
                };
            }

            void hook(Action<Pathfinder.Components.SquallController> orig, Pathfinder.Components.SquallController self){
                orig(self);
                if(self.owner && self.selfBody){
                    OverlaySkin oSkin;
                    if(networkOSkins.TryGetValue(self.owner.GetComponent<CharacterBody>()?.master?.loadout,out oSkin)){
                        var skinCont = self.GetComponent<ModelLocator>()?.modelTransform.GetComponent<ModelSkinController>();
                        var minion = oSkin.minions.FirstOrDefault(m => m.minionBodyPrefab == self.selfBody.master.bodyPrefab);
                        if(skinCont){
                           skinCont.ApplySkin((minion.minionSkin) ? SkinCatalog.FindLocalSkinIndexForBody(self.selfBody.bodyIndex,minion.minionSkin) : 0);
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void HandleRavager(CharacterDetailCatalog details){
            if(!ravagerFix){
                new MonoMod.RuntimeDetour.ILHook(typeof(RedGuyMod.Content.Components.RedGuyController).GetMethod("ApplySkin"),Ilhook);
                ravagerFix = true;
            }
            foreach(var repl in details.meshTs.SelectMany(x => x.Value)){
                repl.applyAction += (model) => {
                    var redguyskin = RecreateRedGuySkin(model);
                    if(!redguyskin){ return;}
                    var origredskin = RedGuyMod.RavagerSkinCatalog.GetSkin(repl.sourceSkin.nameToken);
                    if(repl.path.Contains("meshSword")){
                        redguyskin.basicSwingEffectPrefab = origredskin.basicSwingEffectPrefab;
                        redguyskin.bigSwingEffectPrefab = origredskin.bigSwingEffectPrefab;
                        redguyskin.slashEffectPrefab = origredskin.slashEffectPrefab;
                        var locate = model.GetComponent<ChildLocator>();
                        locate.FindChild("SwordElectricity").gameObject.GetComponent<ParticleSystemRenderer>().trailMaterial = origredskin.electricityMat;
                        locate.FindChild("SwordLight").gameObject.GetComponent<Light>().color = origredskin.glowColor;
                    }
                    if(repl.path.Contains("meshBody")){
                        redguyskin.bloodBombEffectPrefab = origredskin.bloodBombEffectPrefab;
                        redguyskin.bloodOrbEffectPrefab = origredskin.bloodOrbEffectPrefab;
                        redguyskin.bloodOrbOverlayMaterial = origredskin.bloodOrbOverlayMaterial;
                        redguyskin.bloodRushActivationEffectPrefab = origredskin.bloodRushActivationEffectPrefab;
                        redguyskin.bloodRushOverlayMaterial = origredskin.bloodRushOverlayMaterial;
                        redguyskin.consumeSoundString = origredskin.consumeSoundString;
                        redguyskin.healSoundString = origredskin.healSoundString;
                        redguyskin.swordElectricityMat = origredskin.swordElectricityMat;
                        redguyskin.electricityMat = origredskin.electricityMat;
                        redguyskin.glowColor = origredskin.glowColor;
                        redguyskin.leapEffectPrefab = origredskin.leapEffectPrefab; 
                    }
                    if(repl.path.Contains("ImpWrap")){
                        redguyskin.useAltAnimSet = origredskin.nameToken.Contains("MAHORAGA") ? true : origredskin.useAltAnimSet;
                    }
                };
            }

            void Ilhook(ILContext il){
                ILCursor c = new(il);
                if(c.TryGotoNext(MoveType.After,x => x.MatchStfld(typeof(RedGuyMod.Content.Components.RedGuyController).GetField("cachedSkinDef",(System.Reflection.BindingFlags)(-1))))){
                   var lab = c.MarkLabel();
                   c.GotoPrev(MoveType.After,x => x.MatchBrfalse(out _));
                   c.Emit(OpCodes.Ldarg_0);
                   c.EmitDelegate<Func<RedGuyMod.Content.Components.RedGuyController,bool>>((self) => self.cachedSkinDef);
                   c.Emit(OpCodes.Brtrue,lab);
                   c.GotoNext(x => x.MatchLdstr("SwordElectricity"));
                   c.GotoPrev(x => x.MatchLdarg(0));
                   var movepoint = c.MarkLabel();
                   c.GotoNext(MoveType.After,x => x.MatchCallOrCallvirt(typeof(ParticleSystemRenderer).GetProperty("trailMaterial").GetSetMethod()));
                   lab = c.MarkLabel();
                   c.GotoLabel(movepoint);
                   c.Emit(OpCodes.Br,lab);
                   //c.GotoNext(x => x.MatchCallOrCallvirt(typeof(Component).GetMethod("op_Implicit")));
                   //c.Emit(OpCodes.Ldarg_0);
                   //c.EmitDelegate<Func<Light,RedGuyMod.Content.Components.RedGuyController,Light>>((light,redguy) => (light == redguy.childLocator?.FindChild("SwordLight")?.GetComponent<Light>()) ? null : light);
                }
            }



            RavagerSkinDef RecreateRedGuySkin(GameObject model){
                var redguy = model.GetComponentInChildren<CharacterModel>()?.body?.GetComponentInChildren<RedGuyMod.Content.Components.RedGuyController>();
                if(!redguy){return null;}
                redguy.skinController = redguy.GetComponentInChildren<ModelSkinController>();
                if(!redguy.cachedSkinDef){redguy.cachedSkinDef = RedGuyMod.RavagerSkinCatalog.GetSkin(redguy.skinController.skins[model.GetComponent<CharacterModel>().body.skinIndex].nameToken);}
                if(RedGuyMod.RavagerSkinCatalog.skinDefs.Contains(redguy.cachedSkinDef)){
                    redguy.cachedSkinDef = ScriptableObject.Instantiate(redguy.cachedSkinDef);
                }
                return redguy.cachedSkinDef;
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void HandlePaladin(CharacterDetailCatalog details){
            List<Renderer> toRemove = new();
            foreach(var renderer in details.meshTs.Keys){
                if(renderer.name == "Crystal"){
                    toRemove.Add(renderer);
                }
            }
            foreach(var renderer in details.infoTs.Keys){
                if(renderer.name == "Crystal"){
                    toRemove.Add(renderer);
                }
            }

            foreach(var r in toRemove){
                details.meshTs.Remove(r);
                details.infoTs.Remove(r);
            }

            /*
            foreach(var repl in details.meshTs.SelectMany(x => x.Value)){
                repl.applyAction += (model) => {
                    CharacterBody palbod = model.GetComponent<CharacterModel>()?.body;
                    var palguy = palbod?.GetComponentInChildren<PaladinMod.Misc.PaladinSwordController>();
                    if(!palguy){return;}
                    palbod.master.onBodyStart += (bod) => palguy.body = bod;
                    palguy.body = null;
                    var origSkin = PaladinMod.Modules.Effects.GetSkinInfo(model.GetComponent<ModelSkinController>().skins[palbod.skinIndex].nameToken);
                    Debug.Log(repl.path);
                    if(repl.path.Contains("meshSword")){
                        palguy.skinInfo.isWeaponBlunt = origSkin.isWeaponBlunt;
                        palguy.isBlunt = origSkin.isWeaponBlunt;
                        palguy.skinInfo.swingEffect = origSkin.swingEffect;
                        palguy.skinInfo.hitEffect = origSkin.hitEffect;
                        palguy.skinInfo.swingSoundString = origSkin.swingSoundString;
                        palguy.skinInfo.empoweredSpinSlashEffect = origSkin.empoweredSpinSlashEffect;
                        palguy.skinInfo.passiveEffectName = origSkin.passiveEffectName;
                    }
                    if(repl.path.Contains("meshBody")){
                        palguy.skinInfo.eyeTrailColor = origSkin.eyeTrailColor;
                        palguy.skinName = origSkin.skinName;
                        palguy.EditEyeTrail();
                    }
                };
            }*/
        }

	private void Awake(){

          lobbySkinFix = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.KingEnderBrine.LobbySkinsFix");
          seperateMaterials = Config.Bind("Configuration","Seperate Materials",false,"Present extra selections for materials (the default merges them with mesh replacements)");
          if(lobbySkinFix){
              HandleLobbySkinFix();
          }
          if(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions")){
              HandleRiskOfOptions();
          }
          On.RoR2.ProjectileGhostReplacementManager.FindProjectileGhostPrefab += (orig,projcontroller) =>{
            var ret = orig(projcontroller);
            var prefab = ProjectileCatalog.GetProjectilePrefab(projcontroller.catalogIndex);
            var load = projcontroller.owner?.GetComponent<CharacterBody>()?.master?.loadout;
            var rep = (load != null && networkOSkins.TryGetValue(load,out var oSkin))? oSkin?.projectiles?.Find(p => p.projectilePrefab == prefab) : null;
            return (rep != null && ((ProjectileGhostReplacement)rep).projectilePrefab) ? ((SkinDef.ProjectileGhostReplacement)rep).projectileGhostReplacementPrefab : ret;
          };
          IL.RoR2.MasterSummon.Perform += (il) =>{
             ILCursor c = new(il);
             if(c.TryGotoNext(MoveType.After,x => x.MatchLdfld(typeof(RoR2.SkinDef).GetField("minionSkinReplacements")))){
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<MinionSkinReplacement[],MasterSummon,MinionSkinReplacement[]>>((reps,self) =>{
                 var load = self.summonerBodyObject?.GetComponent<CharacterBody>()?.master?.loadout;
                 return (load != null && networkOSkins.TryGetValue(load,out var oSkin) && oSkin.minions != null)? oSkin.minions.ToArray() : reps;});
             }
          };
          On.RoR2.SkinDef.Apply += (orig,self,model) =>{
             orig(self,model);
             var load = model.GetComponent<CharacterModel>()?.body?.master?.loadout;
             bool diorama;
             if(load == null){ //Diorama case
                load = model.GetComponentInParent<RoR2.SurvivorMannequins.SurvivorMannequinSlotController>()?.currentLoadout;
                if(load != null){
                  diorama = true;
                }
             }
             if(load != null && networkOSkins.TryGetValue(load,out var oSkin) && oSkin.orig == self){
               oSkin.rSkin?.Apply(model);
               oSkin.onApply?.Invoke(model);
             }
          };
          RoR2.UserProfile.onLoadoutChangedGlobal += (u) =>{
             if(u == localProfile){
                var bodIndex = SurvivorCatalog.GetBodyIndexFromSurvivorIndex(u.survivorPreference.survivorIndex);
                var currSkin = SkinCatalog.GetBodySkinDef(bodIndex,(int)u.loadout.bodyLoadoutManager.GetSkinIndex(bodIndex));
                if((oSkin.bodyIndex != bodIndex || oSkin.orig != currSkin) && detailRows.Any()){
                  currSkin.Bake();
                  oSkin = new OverlaySkin{
                        orig = currSkin,
                        bodyIndex = bodIndex,
                        minions = currSkin.minionSkinReplacements.ToList(),
                        projectiles = currSkin.projectileGhostReplacements.ToList(),
                        seperateMaterials = seperateMaterials.Value,
                        rSkin = new RuntimeSkin{
                            meshReplacementTemplates = ArrayUtils.Clone(currSkin.runtimeSkin.meshReplacementTemplates),
                            rendererInfoTemplates = ArrayUtils.Clone(currSkin.runtimeSkin.rendererInfoTemplates),
                            gameObjectActivationTemplates = ArrayUtils.Clone(currSkin.runtimeSkin.gameObjectActivationTemplates)
                        },
                        syncInfo = new int[rowCountStore]
                  };
                  OverlaySkin.GenerateSyncFromSkinRows(ref oSkin.syncInfo,currSkin,detailRows);
                }
             }
          };
          IL.RoR2.Loadout.BodyLoadoutManager.RemoveBodyLoadoutIfDefault_int += (il) =>{
              ILCursor c = new(il);
              if(c.TryGotoNext(x => x.MatchBrfalse(out _))){
                  c.Emit(OpCodes.Ldarg_0);
                  c.Emit(OpCodes.Ldarg_1);
                  c.EmitDelegate<Func<bool,Loadout.BodyLoadoutManager,int,bool>>((orig,self,index) => orig && (!detailCatalog.ContainsKey(self.modifiedBodyLoadouts[index].bodyIndex) || oSkin.bodyIndex != self.modifiedBodyLoadouts[index].bodyIndex ));
              }
          };
          On.RoR2.UI.LoadoutPanelController.Rebuild += (orig,self) => {
              orig(self);
              var bodyIndex = self.currentDisplayData.bodyIndex;
              if(bodyIndex != BodyIndex.None){
                  localProfile = self.currentDisplayData.userProfile;
                  var diorama = GameObject.Find("SurvivorMannequinDiorama").GetComponent<RoR2.SurvivorMannequins.SurvivorMannequinDioramaController>().mannequinSlots[0];
                  var defaultSkin = SkinCatalog.GetBodySkinDef(bodyIndex,0);
                  var currSkin = SkinCatalog.GetBodySkinDef(bodyIndex,(int)self.currentDisplayData.userProfile.loadout.bodyLoadoutManager.GetSkinIndex(bodyIndex));
                  var rowcount = 0;
                  bool skinChanged = false;
                  if(oSkin.bodyIndex != bodyIndex || oSkin.orig != currSkin){
                    skinChanged = true;
                    currSkin?.Bake();
                    oSkin = new OverlaySkin{
                        orig = currSkin,
                        bodyIndex = self.currentDisplayData.bodyIndex, 
                        seperateMaterials = seperateMaterials.Value,
                        minions = currSkin.minionSkinReplacements.ToList(),
                        projectiles = currSkin.projectileGhostReplacements.ToList(),
                        rSkin = new RuntimeSkin{
                            meshReplacementTemplates = ArrayUtils.Clone(currSkin.runtimeSkin.meshReplacementTemplates),
                            rendererInfoTemplates = ArrayUtils.Clone(currSkin.runtimeSkin.rendererInfoTemplates),
                            gameObjectActivationTemplates = ArrayUtils.Clone(currSkin.runtimeSkin.gameObjectActivationTemplates)
                        },
                    };
                  }
                  detailRows.Clear();
                  CharacterDetailCatalog detail;
                  if(!detailCatalog.TryGetValue(bodyIndex,out detail)){
                          return;
                  }
                  if(seperateMaterials.Value){
                    foreach(var m in detail.meshTs){
                      var mesh = m.Value;
                      var row = new Row(self,bodyIndex,mesh.First().detail.renderer.name + " Mesh");
                      var catVal = rowcount;
                      row.findCurrentChoice = (l) => {var val = oSkin.FindIndex(catVal); return (val >= 0) ? val : row.buttons.Count - 1;};
                      string disableList = String.Empty;
                      foreach(var repl in mesh){
                        if(!repl.detail.mesh){
                          disableList += Language.GetString(repl.sourceSkin.nameToken) + '\n';
                          continue;
                        }
                        var skin = repl.sourceSkin;
                        row.AddButton(self,skin.icon,skin.nameToken,skin.nameToken,row.primaryColor,delegate{
                                oSkin.syncInfo[catVal] = repl.index;
                                oSkin.BuildRSkin(oSkin.syncInfo);
                                row.UpdateHighlightedChoice();
                                diorama.loadoutDirty = true;
                        },skin.unlockableDef?.cachedName ?? "",null);
                      }
                      if(detail.actT.ContainsKey(m.Key.gameObject)){
                      foreach(var act in detail.actT[m.Key.gameObject]){
                        if(!act.detail){
                          disableList += Language.GetString(act.sourceSkin.nameToken) + '\n';
                        }
                      }
                      }
                      if(disableList != String.Empty){
                         row.AddButton(self,icon,"Disabled",disableList,row.primaryColor,delegate{
                            oSkin.syncInfo[catVal] = -2;
                            oSkin.BuildRSkin(oSkin.syncInfo);
                            row.UpdateHighlightedChoice();
                            diorama.loadoutDirty = true;
                         },"",null);
                      }
                      if(row.buttons.Count > 1) { row.FinishSetup(); self.rows.Add(row); detailRows.Add(row); rowcount++;}else{row.Dispose();}
                    }
                    foreach(var m in detail.infoTs){
                      var info = m.Value;
                      var row = new Row(self,bodyIndex,info.First().detail.renderer.name + " Material");
                      var catVal = rowcount;
                      row.findCurrentChoice = (l) => {var val = oSkin.FindIndex(catVal); return (val >= 0) ? val : row.buttons.Count - 1;};
                      string disableList = String.Empty;
                      foreach(var repl in info){
                        var skin = repl.sourceSkin;
                        row.AddButton(self,skin.icon,skin.nameToken,skin.nameToken,row.primaryColor,delegate{
                                oSkin.syncInfo[catVal] = repl.index;
                                oSkin.BuildRSkin(oSkin.syncInfo);
                                row.UpdateHighlightedChoice();
                                diorama.loadoutDirty = true;
                        },skin.unlockableDef?.cachedName ?? "",null);
                      }
                      if(detail.actT.ContainsKey(m.Key.gameObject)){
                      foreach(var act in detail.actT[m.Key.gameObject]){
                        if(!act.detail){
                          disableList += Language.GetString(act.sourceSkin.nameToken) + '\n';
                        }
                       }
                      }
                      if(disableList != String.Empty){
                         row.AddButton(self,icon,"Disabled",disableList,row.primaryColor,delegate{
                            oSkin.syncInfo[catVal] = -2;
                            oSkin.BuildRSkin(oSkin.syncInfo);
                            row.UpdateHighlightedChoice();
                            diorama.loadoutDirty = true;
                         },"",null);
                      }
                      if(row.buttons.Count > 1) { row.FinishSetup(); self.rows.Add(row); detailRows.Add(row); rowcount++;}else{row.Dispose();}
                    }
                  }
                  else{
                    foreach(var r in detail.meshTs.Keys.Concat(detail.infoTs.Keys).Distinct()){
                      var row = new Row(self,bodyIndex,r.name);
                      var catVal = rowcount;
                      row.findCurrentChoice = (l) => {var val = oSkin.FindIndex(catVal); return (val >= 0) ? val : row.buttons.Count - 1;};
                      string disableList = String.Empty;
                      if(detail.meshTs.ContainsKey(r)){
                          foreach(var repl in detail.meshTs[r]){
                            if(!repl.detail.mesh){
                              disableList += Language.GetString(repl.sourceSkin.nameToken) + '\n';
                              continue;
                            }
                            var skin = repl.sourceSkin;
                            row.AddButton(self,skin.icon,skin.nameToken,skin.nameToken,row.primaryColor,delegate{
                                    oSkin.syncInfo[catVal] = repl.index;
                                    oSkin.BuildRSkin(oSkin.syncInfo);
                                    row.UpdateHighlightedChoice();
                                    diorama.loadoutDirty = true;
                            },skin.unlockableDef?.cachedName ?? "",null);
                          }
                      }
                      else{
                          foreach(var repl in detail.infoTs[r]){
                            var skin = repl.sourceSkin;
                            row.AddButton(self,skin.icon,skin.nameToken,skin.nameToken,row.primaryColor,delegate{
                                    oSkin.syncInfo[catVal] = repl.index;
                                    oSkin.BuildRSkin(oSkin.syncInfo);
                                    row.UpdateHighlightedChoice();
                                    diorama.loadoutDirty = true;
                            },skin.unlockableDef?.cachedName ?? "",null);
                          }
                      }

                      if(detail.actT.ContainsKey(r.gameObject)){
                      foreach(var act in detail.actT[r.gameObject]){
                        if(!act.detail){
                          disableList += Language.GetString(act.sourceSkin.nameToken) + '\n';
                        }
                       }
                      }
                      if(disableList != String.Empty){
                         row.AddButton(self,icon,"Disabled",disableList,row.primaryColor,delegate{
                            oSkin.syncInfo[catVal] = -2;
                            oSkin.BuildRSkin(oSkin.syncInfo);
                            row.UpdateHighlightedChoice();
                            diorama.loadoutDirty = true;
                         },"",null);
                      }
                      if(row.buttons.Count > 1) { row.FinishSetup(); self.rows.Add(row); detailRows.Add(row); rowcount++;}else{row.Dispose();}
                    }
                  }

                  /*//Object Activation
                  foreach(var acti in detail.actT.Where(a => {var rend = a.Key.GetComponent<Renderer>(); return !rend || (!detail.meshTs.ContainsKey(rend) && !detail.infoTs.ContainsKey(rend));})){
                      var options = acti.Value;
                      var row = new Row(self,bodyIndex,acti.Key.name);
                      var catVal = rowcount;
                      row.findCurrentChoice = (l) => {var val = oSkin.FindIndex(catVal); return (val >= 0) ? val : row.buttons.Count - 1;};
                      string disableList = String.Empty;
                      string enableList = String.Empty;
                      foreach(var togg in options){
                          if(togg.detail){
                              enableList += Language.GetString(togg.sourceSkin.nameToken) + '\n';
                          }
                          else{
                              disableList += Language.GetString(togg.sourceSkin.nameToken) + '\n';
                          }
                      }
                      if(enableList != String.Empty){
                         row.AddButton(self,icon,"Enabled",enableList,row.primaryColor,delegate{
                            oSkin.syncInfo[catVal] = 0;
                            oSkin.BuildRSkin(oSkin.syncInfo);
                            row.UpdateHighlightedChoice();
                            diorama.loadoutDirty = true;
                         },"",null);
                      }
                      if(disableList != String.Empty){
                         row.AddButton(self,icon,"Disabled",disableList,row.primaryColor,delegate{
                            oSkin.syncInfo[catVal] = -2;
                            oSkin.BuildRSkin(oSkin.syncInfo);
                            row.UpdateHighlightedChoice();
                            diorama.loadoutDirty = true;
                         },"",null);
                      }
                      if(row.buttons.Count > 1) { row.FinishSetup(); self.rows.Add(row); detailRows.Add(row); rowcount++;}else{row.Dispose();}

                  }//*/

                  foreach(var mini in detail.minionSkins){
                     var row = new Row(self,bodyIndex,BodyCatalog.GetBodyName(mini.Key));
                      var catVal = rowcount;
                      row.findCurrentChoice = (l) => Math.Max(0, oSkin.FindIndex(catVal));
                     if(!mini.Value.Any(r => r.sourceSkin == defaultSkin)){
                         row.AddButton(self,defaultSkin?.icon,defaultSkin?.nameToken,defaultSkin?.nameToken,row.primaryColor,delegate{
                                 oSkin.syncInfo[catVal] = -2;
                                 oSkin.BuildRSkin(oSkin.syncInfo);
                                 row.UpdateHighlightedChoice();
                                 diorama.loadoutDirty = true;
                         },defaultSkin.unlockableDef?.cachedName ?? "",null);
                     }
                     foreach(var miniS in mini.Value){
                         row.AddButton(self,miniS.sourceSkin.icon,miniS.sourceSkin.nameToken,miniS.sourceSkin.nameToken,row.primaryColor,delegate{
                                 oSkin.syncInfo[catVal] = miniS.index;
                                 oSkin.BuildRSkin(oSkin.syncInfo);
                                 row.UpdateHighlightedChoice();
                                 diorama.loadoutDirty = true;
                         },miniS.sourceSkin.unlockableDef?.cachedName ?? "",null);
                     }
                     if(row.buttons.Count > 1) { row.FinishSetup(); self.rows.Add(row); detailRows.Add(row); rowcount++;}else{row.Dispose();}
                  }

                  foreach(var proj in detail.projectiles){
                     var row = new Row(self,bodyIndex,proj.Key.name);
                      var catVal = rowcount;
                      row.findCurrentChoice = (l) => Math.Max(0, oSkin.FindIndex(catVal));
                     if(!proj.Value.Any(r => r.sourceSkin == defaultSkin)){
                         row.AddButton(self,defaultSkin?.icon,defaultSkin?.nameToken,defaultSkin?.nameToken,row.primaryColor,delegate{
                                 oSkin.syncInfo[catVal] = -2;
                                 oSkin.BuildRSkin(oSkin.syncInfo);
                                 row.UpdateHighlightedChoice();
                                 diorama.loadoutDirty = true;
                         },defaultSkin.unlockableDef?.cachedName ?? "",null);
                     }
                     foreach(var ghost in proj.Value){
                         row.AddButton(self,ghost.sourceSkin.icon,ghost.sourceSkin.nameToken,ghost.sourceSkin.nameToken,row.primaryColor,delegate{
                                 oSkin.syncInfo[catVal] = ghost.index;
                                 oSkin.BuildRSkin(oSkin.syncInfo);
                                 row.UpdateHighlightedChoice();
                                 diorama.loadoutDirty = true;
                         },ghost.sourceSkin.unlockableDef?.cachedName ?? "",null);
                     }
                     if(row.buttons.Count > 1) { row.FinishSetup(); self.rows.Add(row); detailRows.Add(row); rowcount++;} else{row.Dispose();}
                  }
                  rowCountStore = rowcount;
                  if(skinChanged){
                    oSkin.syncInfo ??= new int[rowcount];
                    OverlaySkin.GenerateSyncFromSkinRows(ref oSkin.syncInfo,currSkin,detailRows);
                    oSkin.BuildRSkin(oSkin.syncInfo);
                  }
                  diorama.loadoutDirty = true;

                  HG.ArrayUtils.EnsureCapacity(ref oSkin.syncInfo,rowcount);
                  var dontSaveEverytime = localProfile.saveRequestPending;
                  localProfile.OnLoadoutChanged();//Find out why we need this. (Skin details don't update till the loadout changes at least once)
                  localProfile.saveRequestPending = dontSaveEverytime;
              }
          };
          On.RoR2.Loadout.Serialize += (orig,self,writer) =>{
            orig(self,writer);
            var oSkin = networkOSkins.GetOrCreateValue(self);
            if(oSkin == null || oSkin.orig == null || !detailCatalog.ContainsKey(oSkin.bodyIndex) || oSkin.syncInfo.Length <= 0 ){
              writer.Write((int)-1);
            }
            else{
            writer.Write((int)oSkin.orig.skinIndex);
            writer.WriteBodyIndex(oSkin.bodyIndex);
            writer.Write(oSkin.seperateMaterials);
            writer.Write(oSkin.syncInfo.Length);
            foreach(var val in oSkin.syncInfo){
               writer.Write(val);
            }
            }
          };

          On.RoR2.Loadout.Deserialize += (orig,self,reader) =>{
            orig(self,reader);
            var skinIndex = (SkinIndex)reader.ReadInt32();
            if((int)skinIndex == -1){
              return;
            }
            var oSkin = networkOSkins.GetOrCreateValue(self);
            oSkin.orig = SkinCatalog.GetSkinDef(skinIndex);
            oSkin.bodyIndex = reader.ReadBodyIndex();
            oSkin.seperateMaterials = reader.ReadBoolean();
            var length = reader.ReadInt32();
            oSkin.syncInfo = new int[length];
            for(int i = 0 ; i < length ; i++){
               oSkin.syncInfo[i] = reader.ReadInt32();
            }
            oSkin.minions = new();
            oSkin.projectiles = new();
            if(detailCatalog.ContainsKey(oSkin.bodyIndex)){
                oSkin.BuildRSkin(oSkin.syncInfo);
            }
          };
          On.RoR2.Loadout.Copy += (orig,self,target) =>{
            orig(self,target);
            OverlaySkin val;
            if(networkOSkins.TryGetValue(self,out val)){
             networkOSkins.Remove(target);
             networkOSkins.Add(target,val);
            }
          };
        }
    }
}
