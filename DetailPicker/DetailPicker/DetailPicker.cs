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
    [BepInPlugin("xyz.yekoc.DetailPicker", "Skin Detail Picker","1.0.0" )]    
    [BepInDependency("com.bepis.r2api")]
    [R2API.Utils.NetworkCompatibility(R2API.Utils.CompatibilityLevel.EveryoneMustHaveMod)]
    public class SkinDetailPickerPlugin : BaseUnityPlugin{

        public class OverlaySkin{
           public SkinDef orig;
           public RuntimeSkin rSkin;
           public BodyIndex bodyIndex;
           public bool seperateMaterials;
           public List<MinionSkinReplacement> minions;
           public List<ProjectileGhostReplacement> projectiles;
           public int[] syncInfo;

           public int FindIndex(int category){
              return (syncInfo != null && category < syncInfo.Length) ? syncInfo[category] : 0;
           } 
        }

        public static bool lobbySkinFix = false;
        public Sprite icon = LegacyResourcesAPI.Load<RoR2.Skills.SkillDef>("SkillDefs/CaptainBody/CaptainSkillUsedUp").icon;
        public static OverlaySkin oSkin {
           get => networkOSkins.GetOrCreateValue(localProfile.loadout);
           set { networkOSkins.Remove(localProfile.loadout); networkOSkins.Add(localProfile.loadout,value);}
        } 
        public static FixedConditionalWeakTable<Loadout,OverlaySkin> networkOSkins = new();
        public ConfigEntry<bool> seperateMaterials;
        public static UserProfile localProfile;
        public List<Row> detailRows = new List<Row>();
        public int rowCountStore;


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

	private void Awake(){

          lobbySkinFix = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.KingEnderBrine.LobbySkinsFix");
          seperateMaterials = Config.Bind("Configuration","Seperate Materials",false,"Present extra selections for materials (the default merges them with mesh replacements)");
          if(lobbySkinFix){
              HandleLobbySkinFix();
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
             if(load == null){ //Diorama case
                load = model.GetComponentInParent<RoR2.SurvivorMannequins.SurvivorMannequinSlotController>()?.currentLoadout;
             }
             if(load != null && networkOSkins.TryGetValue(load,out var oSkin) && oSkin.orig == self){
               oSkin.rSkin?.Apply(model);
             }
          };
          RoR2.UserProfile.onLoadoutChangedGlobal += (u) =>{
             if(u == localProfile){
                var bodIndex = SurvivorCatalog.GetBodyIndexFromSurvivorIndex(u.survivorPreference.survivorIndex);
                var currSkin = SkinCatalog.GetBodySkinDef(bodIndex,(int)u.loadout.bodyLoadoutManager.GetSkinIndex(bodIndex));
                if(oSkin.bodyIndex != bodIndex || oSkin.orig != currSkin){
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
                  var i = 0;
                  foreach(var row in detailRows){
                      var index = row.buttons.FindIndex((b) => { var t = b.GetComponent<RoR2.UI.TooltipProvider>().overrideBodyText;
                              return t.Contains(Language.GetString(oSkin.orig.nameToken));
                      });
                      if(index == -1 && oSkin.orig.baseSkins.Any()){
                          index =row.buttons.FindIndex((b) => { var t = b.GetComponent<RoR2.UI.TooltipProvider>().overrideBodyText;
                              return t.Contains(Language.GetString(oSkin.orig.baseSkins.Last().nameToken));
                      });

                      }
                      if(index != -1){
                        oSkin.syncInfo[rowCountStore -1 -i] = index;
                        row.UpdateHighlightedChoice();
                      }
                      i++;
                  }
                }
             }
          };
          On.RoR2.UI.LoadoutPanelController.Rebuild += (orig,self) => {
              orig(self);
              if(self.currentDisplayData.bodyIndex != BodyIndex.None){
                  localProfile = self.currentDisplayData.userProfile;
                  var diorama = GameObject.Find("SurvivorMannequinDiorama").GetComponent<RoR2.SurvivorMannequins.SurvivorMannequinDioramaController>().mannequinSlots[0];
                  SkinDef[] skins = SkinCatalog.GetBodySkinDefs(self.currentDisplayData.bodyIndex);
                  var meshes = skins.SelectMany((s) => s.meshReplacements).Select(m => m.renderer).Distinct();
                  var infos = skins.SelectMany((s) => s.rendererInfos).Select(i => i.renderer).Distinct();
                  var minions = skins.SelectMany((s) => s.minionSkinReplacements).Select(m => m.minionBodyPrefab).Distinct();
                  var projectiles = skins.SelectMany((s) => s.projectileGhostReplacements).Select(p => p.projectilePrefab).Distinct();
                  var currSkin = SkinCatalog.GetBodySkinDef(self.currentDisplayData.bodyIndex,(int)self.currentDisplayData.userProfile.loadout.bodyLoadoutManager.GetSkinIndex(self.currentDisplayData.bodyIndex));
                  var rowcount = 0;
                  bool skinChange = false;
                  if(oSkin.bodyIndex != self.currentDisplayData.bodyIndex || oSkin.orig != currSkin){
                    currSkin.Bake();
                    skinChange = true;
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
                  if(seperateMaterials.Value){
                  foreach(var mesh in meshes){
                      var row = new Row(self,self.currentDisplayData.bodyIndex,mesh.name + " Mesh");
                      int locRowCount = (int)rowcount;
                      var count = 0;
                      string disableList = String.Empty;
                      foreach(var skin in skins.Where(s => s.gameObjectActivations.Any(a => a.gameObject == mesh.gameObject && a.shouldActivate == false))){
                         disableList += Language.GetString(skin.nameToken) + '\n';
                      }
                      if(!(skins[0].meshReplacements.Any(m => m.mesh == mesh))){
                         row.AddButton(self,skins[0].icon,skins[0].nameToken,skins[0].nameToken,row.primaryColor,delegate{
                            var path = Util.BuildPrefabTransformPath(skins[0].rootObject.transform,mesh.transform);
                            var list = oSkin.rSkin.meshReplacementTemplates.ToList();
                            list.RemoveAll(m => m.path == path);
                            oSkin.rSkin.meshReplacementTemplates = list.ToArray();
                            oSkin.syncInfo[locRowCount] = 0;
                            if(skins[0].gameObjectActivations.Any(g => g.gameObject == mesh)){
                             ArrayUtils.ArrayAppend(ref oSkin.rSkin.gameObjectActivationTemplates,new GameObjectActivationTemplate{path = path,shouldActivate = skins[0].gameObjectActivations.First(a => a.gameObject == mesh).shouldActivate});
                            }
                            row.UpdateHighlightedChoice();
                            diorama.loadoutDirty = true;
                         },skins[0].unlockableDef?.cachedName ?? "",null);
                         count++;
                      }
                      foreach(var skin in skins.Where(s => s.meshReplacements.Any(m => m.renderer == mesh))){
                        if(skin.meshReplacements.Any(s => s.renderer == mesh && s.mesh == null)){
                          disableList += Language.GetString(skin.nameToken) + '\n';
                          count++;
                          continue;
                        }
                        int locCount = (int)count;
                        row.AddButton(self,skin.icon,skin.nameToken,skin.nameToken,row.primaryColor,delegate{
                                 var path = Util.BuildPrefabTransformPath(skin.rootObject.transform,mesh.transform);
                                 if(skin.meshReplacements.Any(m => m.renderer == mesh)){
                                  ArrayUtils.ArrayAppend(ref oSkin.rSkin.meshReplacementTemplates,new MeshReplacementTemplate{path = path,mesh = skin.meshReplacements.First(m => m.renderer == mesh).mesh});
                                 }
                                 else if(skin.baseSkins.Any() && skin.baseSkins.Last().meshReplacements.Any(m => m.renderer == mesh)){ 
                                  ArrayUtils.ArrayAppend(ref oSkin.rSkin.meshReplacementTemplates,new MeshReplacementTemplate{path = path,mesh = skin.baseSkins.Last().meshReplacements.First(m => m.renderer == mesh).mesh});
                                 }
                                 else{
                                   var list = oSkin.rSkin.meshReplacementTemplates.ToList();
                                   list.RemoveAll(m => m.path == path);
                                   oSkin.rSkin.meshReplacementTemplates = list.ToArray();
                                 }
                                 ArrayUtils.ArrayAppend(ref oSkin.rSkin.gameObjectActivationTemplates,new GameObjectActivationTemplate{path = path,shouldActivate = true});
                                 oSkin.syncInfo[locRowCount] = locCount; 
                                 row.UpdateHighlightedChoice();
                                 diorama.loadoutDirty = true;
                                },skin.unlockableDef?.cachedName ?? "",null);
                        count++;
                      } 
                      if(disableList != String.Empty){
                        int locCount = count;
                        row.AddButton(self,icon,"Disabled",disableList,row.primaryColor,delegate{
                           var path = Util.BuildPrefabTransformPath(skins[0].rootObject.transform,mesh.transform);
                           ArrayUtils.ArrayAppend(ref oSkin.rSkin.meshReplacementTemplates,new MeshReplacementTemplate{path = path,mesh = null});
                           oSkin.syncInfo[locRowCount] = locCount;
                           row.UpdateHighlightedChoice();
                           diorama.loadoutDirty = true;
                        },"",null);
                      }
                      row.findCurrentChoice = (l) => oSkin.FindIndex(locRowCount);// rowindexes.ContainsKey(mesh.name + " Mesh") ? (int)rowindexes[mesh.name + " Mesh"] : 0;
                      row.FinishSetup();
                      if(count > 1){
                       self.rows.Add(row);
                       rowcount++;
                      }
                      else{
                       row.Dispose();
                      }
                  }
                  foreach(var info in infos){
                      var row = new Row(self,self.currentDisplayData.bodyIndex,info.name + " Material");
                      var count = 0; 
                      int locRowCount = rowcount;
                      string disableList = String.Empty;
                      foreach(var skin in skins.Where(s => s.gameObjectActivations.Any(a => a.gameObject == info.gameObject && a.shouldActivate == false))){
                         disableList += Language.GetString(skin.nameToken) + '\n';
                      }
                      if(!skins[0].meshReplacements.Any(m => m.renderer == info ) && !skins[0].rendererInfos.Any(m => m.renderer == info )){
                         row.AddButton(self,skins[0].icon,skins[0].nameToken,skins[0].nameToken,row.primaryColor,delegate{
                            var path = Util.BuildPrefabTransformPath(skins[0].rootObject.transform,info.transform);
                            var list = oSkin.rSkin.meshReplacementTemplates.ToList();
                            list.RemoveAll(m => m.path == path);
                            oSkin.rSkin.meshReplacementTemplates = list.ToArray();
                            oSkin.syncInfo[locRowCount] = 0;
                            if(skins[0].gameObjectActivations.Any(g => g.gameObject == info )){
                             ArrayUtils.ArrayAppend(ref oSkin.rSkin.gameObjectActivationTemplates,new GameObjectActivationTemplate{path = path,shouldActivate = skins[0].gameObjectActivations.First(a => a.gameObject == info ).shouldActivate});
                            }
                            row.UpdateHighlightedChoice();
                            diorama.loadoutDirty = true;

                         },skins[0].unlockableDef?.cachedName ?? "",null);
                         count++;
                      }
                      foreach(var skin in skins.Where(s => s.rendererInfos.Any(i => i.renderer == info))){
                        int locCount = (int)count;
                        row.AddButton(self,skin.icon,skin.nameToken,skin.nameToken,row.primaryColor,delegate{
                                 var rinfo = skin.rendererInfos.First(m => m.renderer == info);
                                 var path = Util.BuildPrefabTransformPath(skin.rootObject.transform,info.transform);
                                 ref var arr = ref oSkin.rSkin.rendererInfoTemplates;
                                 var index = Array.FindIndex(arr,r => r.path == path);
                                 index = index == (-1) ? arr.Length : index;
                                 ArrayUtils.ArrayInsert(ref arr,index,new RendererInfoTemplate{path = path,data = new RendererInfo{
                                    defaultMaterial = rinfo.defaultMaterial,
                                    hideOnDeath = rinfo.hideOnDeath,
                                    renderer = rinfo.renderer,
                                    ignoreOverlays = rinfo.ignoreOverlays,
                                    defaultShadowCastingMode = rinfo.defaultShadowCastingMode
                                 }});
                                 oSkin.syncInfo[locRowCount] = locCount;
                                 ArrayUtils.ArrayAppend(ref oSkin.rSkin.gameObjectActivationTemplates,new GameObjectActivationTemplate{path = path,shouldActivate = true});
                                 row.UpdateHighlightedChoice();
                                 diorama.loadoutDirty = true;
                                },skin.unlockableDef?.cachedName ?? "",null);
                        count++;
                      }
                      if(disableList != String.Empty){
                        int locCount = count;
                        row.AddButton(self,icon,"Disabled",disableList,row.primaryColor,delegate{
                           var path = Util.BuildPrefabTransformPath(skins[0].rootObject.transform,info.transform);
                           var rendererComp = info.GetComponent<Renderer>();
                           if(rendererComp is MeshRenderer || rendererComp is SkinnedMeshRenderer){
                             ArrayUtils.ArrayAppend(ref oSkin.rSkin.meshReplacementTemplates,new MeshReplacementTemplate{path = path,mesh = null});
                           }
                           else{
                             ArrayUtils.ArrayAppend(ref oSkin.rSkin.gameObjectActivationTemplates,new GameObjectActivationTemplate{path = path,shouldActivate = false});
                           }
                           oSkin.syncInfo[locRowCount] = locCount;
                           row.UpdateHighlightedChoice();
                           diorama.loadoutDirty = true;
                        },"",null);
                      }
                      row.findCurrentChoice = (l) => oSkin.FindIndex(locRowCount);// rowindexes.ContainsKey(info.name + " Material") ? (int)rowindexes[info.name + " Material"] : 0;
                      row.FinishSetup();
                      if(count > 1){
                        self.rows.Add(row);
                        rowcount++;
                      }
                      else{
                        row.Dispose();
                      }
                  }
                  }
                  else{
                    foreach(var renderer in meshes.Concat(infos).Distinct()){ 
                      var row = new Row(self,self.currentDisplayData.bodyIndex,renderer.name);
                      var count = 0;
                      int locRowCount = (int)rowcount;
                      string disableList = String.Empty;
                      if(!skins[0].meshReplacements.Any(m => m.renderer == renderer) && !skins[0].rendererInfos.Any(m => m.renderer == renderer)){
                         row.AddButton(self,skins[0].icon,skins[0].nameToken,skins[0].nameToken,row.primaryColor,delegate{
                            var path = Util.BuildPrefabTransformPath(skins[0].rootObject.transform,renderer.transform);
                            var list = oSkin.rSkin.meshReplacementTemplates.ToList();
                            list.RemoveAll(m => m.path == path);
                            oSkin.rSkin.meshReplacementTemplates = list.ToArray();
                            oSkin.syncInfo[locRowCount] = 0;
                            if(skins[0].gameObjectActivations.Any(g => g.gameObject == renderer)){
                             ArrayUtils.ArrayAppend(ref oSkin.rSkin.gameObjectActivationTemplates,new GameObjectActivationTemplate{path = path,shouldActivate = skins[0].gameObjectActivations.First(a => a.gameObject == renderer).shouldActivate});
                            }
                            row.UpdateHighlightedChoice();
                            diorama.loadoutDirty = true;

                         },skins[0].unlockableDef?.cachedName ?? "",null);
                         count++;
                      }
                      foreach(var skin in skins.Where(s => s.gameObjectActivations.Any(a => a.gameObject == renderer.gameObject && a.shouldActivate == false))){
                         disableList += Language.GetString(skin.nameToken) + '\n';
                      }
                      foreach(var skin in skins.Where(s => s.meshReplacements.Any(m => m.renderer == renderer) || s.rendererInfos.Any(r => r.renderer == renderer))){
                        if(skin.meshReplacements.Any(m => m.renderer == renderer && m.mesh == null)){
                          disableList += Language.GetString(skin.nameToken) + '\n';
                          continue;
                        }
                        int locCount = count;
                        row.AddButton(self,skin.icon,skin.nameToken,skin.nameToken,row.primaryColor,delegate{
                                 var path = Util.BuildPrefabTransformPath(skin.rootObject.transform,renderer.transform);
                                 if(skin.meshReplacements.Any(m => m.renderer == renderer)){
                                  ArrayUtils.ArrayAppend(ref oSkin.rSkin.meshReplacementTemplates,new MeshReplacementTemplate{path = path,mesh = skin.meshReplacements.First(m => m.renderer == renderer).mesh});
                                 }
                                 else if(skin.baseSkins.Any() && skin.baseSkins.Last().meshReplacements.Any(m => m.renderer == renderer)){ 
                                  ArrayUtils.ArrayAppend(ref oSkin.rSkin.meshReplacementTemplates,new MeshReplacementTemplate{path = path,mesh = skin.baseSkins.Last().meshReplacements.First(m => m.renderer == renderer).mesh});
                                 }
                                 else{
                                   var list = oSkin.rSkin.meshReplacementTemplates.ToList();
                                   list.RemoveAll(m => m.path == path);
                                   oSkin.rSkin.meshReplacementTemplates = list.ToArray();
                                 }
                                if(skin.rendererInfos.Any(m => m.renderer == renderer)){
                                 var info = skin.rendererInfos.First(m => m.renderer == renderer);
                                 var index = Array.FindIndex(oSkin.rSkin.rendererInfoTemplates,r => r.path == path);
                                 ArrayUtils.ArrayInsert(ref oSkin.rSkin.rendererInfoTemplates,index == (-1) ? oSkin.rSkin.rendererInfoTemplates.Length : index,new RendererInfoTemplate{path = path,data = new RendererInfo{
                                    defaultMaterial = info.defaultMaterial,
                                    hideOnDeath = info.hideOnDeath,
                                    renderer = info.renderer,
                                    ignoreOverlays = info.ignoreOverlays,
                                    defaultShadowCastingMode = info.defaultShadowCastingMode
                                 }
                                 });
                                 }
                                 ArrayUtils.ArrayAppend(ref oSkin.rSkin.gameObjectActivationTemplates,new GameObjectActivationTemplate{path = path,shouldActivate = true});
                                 diorama.loadoutDirty = true;
                                 oSkin.syncInfo[locRowCount] = locCount;
                                 row.UpdateHighlightedChoice();
                                },skin.unlockableDef?.cachedName ?? "",null);
                        count++;
                      }
                      if(disableList != String.Empty){
                        int locCount = count;
                        row.AddButton(self,icon,"Disabled",disableList,row.primaryColor,delegate{
                           var path = Util.BuildPrefabTransformPath(skins[0].rootObject.transform,renderer.transform);
                           var rendererComp = renderer.GetComponent<Renderer>();
                           if(rendererComp is MeshRenderer || rendererComp is SkinnedMeshRenderer){
                             ArrayUtils.ArrayAppend(ref oSkin.rSkin.meshReplacementTemplates,new MeshReplacementTemplate{path = path,mesh = null});
                           }
                           else{
                             ArrayUtils.ArrayAppend(ref oSkin.rSkin.gameObjectActivationTemplates,new GameObjectActivationTemplate{path = path,shouldActivate = false});
                           }
                           oSkin.syncInfo[locRowCount] = locCount;
                           row.UpdateHighlightedChoice();
                           diorama.loadoutDirty = true;
                        },"",null);
                      }
                      row.findCurrentChoice = (l) => oSkin.FindIndex(locRowCount);//rowindexes.ContainsKey(renderer.name) ? (int)rowindexes[renderer.name] : 0;
                      row.FinishSetup();
                      if(count > 1){
                        self.rows.Add(row);
                        rowcount++;
                      }
                      else{
                        row.Dispose();
                      }
                    }
                  }
                  foreach(var minion in minions){
                      var row = new Row(self,self.currentDisplayData.bodyIndex,Util.GetBestBodyName(minion));
                      var indexstring = BodyCatalog.GetBodyName(BodyCatalog.FindBodyIndex(minion));
                      var count = 0;
                      int locRowCount = (int)rowcount;
                      row.findCurrentChoice = (l) => oSkin.FindIndex(locRowCount);//rowindexes.ContainsKey(indexstring) ? (int)rowindexes[indexstring] : 0;
                      if(!skins[0].minionSkinReplacements.Any(m => m.minionBodyPrefab == minion)){
                        row.AddButton(self,skins[0].icon,skins[0].nameToken,skins[0].nameToken,row.primaryColor,delegate{
                          //ArrayUtils.ArrayAppend(ref oSkin.minions,skins[0].minionSkinReplacements.First(m => m.minionBodyPrefab == minion));
                          oSkin.minions.RemoveAll(m => m.minionBodyPrefab == minion);
                          oSkin.syncInfo[locRowCount] = 0;
                          row.UpdateHighlightedChoice();
                          diorama.loadoutDirty = true;
                        },skins[0].unlockableDef?.cachedName ?? "",null);
                        count++;
                      }
                      foreach(var skin in skins.Where(s => s.minionSkinReplacements.Any(m => m.minionBodyPrefab == minion))){
                          int locCount = (int)count;
                        row.AddButton(self,skin.icon,skin.nameToken,skin.nameToken,row.primaryColor,delegate{
                          oSkin.minions.Add(skin.minionSkinReplacements.First(m => m.minionBodyPrefab == minion));
                          oSkin.syncInfo[locRowCount] = locCount;
                          row.UpdateHighlightedChoice();
                          diorama.loadoutDirty = true;
                                },skin.unlockableDef?.cachedName ?? "",null);
                        count++;
                      }
                      row.FinishSetup();
                      if(count > 1){
                        self.rows.Add(row);
                        rowcount++;
                      }
                      else{
                        row.Dispose();
                      }
                  }
                  foreach(var proj in projectiles){
                      var row = new Row(self,self.currentDisplayData.bodyIndex,proj.name);
                      var count = 0;
                      int locRowCount = (int)rowcount;
                      if(!skins[0].projectileGhostReplacements.Any(p => p.projectilePrefab == proj)){
                        int locCount = (int)count;
                        row.AddButton(self,skins[0].icon,skins[0].nameToken,skins[0].nameToken,row.primaryColor,delegate{
                          oSkin.projectiles.RemoveAll(p => p.projectilePrefab == proj);
                          oSkin.syncInfo[locRowCount] = 0;
                          row.UpdateHighlightedChoice();
                          diorama.loadoutDirty = true;
                        },skins[0].unlockableDef?.cachedName ?? "",null);
                        count++;
                      }
                      foreach(var skin in skins.Where(s => s.projectileGhostReplacements.Any(p => p.projectilePrefab == proj))){
                        int locCount = (int)count;
                        row.AddButton(self,skin.icon,skin.nameToken,skin.nameToken,row.primaryColor,delegate{
                          oSkin.projectiles.Add(skin.projectileGhostReplacements.First(p => p.projectilePrefab == proj));
                          oSkin.syncInfo[locRowCount] = locCount;
                          row.UpdateHighlightedChoice();
                          diorama.loadoutDirty = true;
                        },skin.unlockableDef?.cachedName ?? "",null);
                        count++;
                      }
                      row.findCurrentChoice = (l) => oSkin.FindIndex(locRowCount);// rowindexes.ContainsKey(proj.name) ? (int)rowindexes[proj.name] : 0;
                      row.FinishSetup();
                      if(count > 1){
                        self.rows.Add(row);
                        rowcount++;
                      }
                      else{
                        row.Dispose();
                      }
                  }
                  rowCountStore = rowcount;
                  detailRows.Clear();
                    oSkin.syncInfo = new int[rowcount];
                    self.rows.Reverse();
                    for(int i = 0 ; i < rowcount ; i++){
                      var row = self.rows[i];
                      detailRows.Add(row);
                      if(skinChange){
                      var index = row.buttons.FindIndex((b) => { var t = b.GetComponent<RoR2.UI.TooltipProvider>().overrideBodyText;
                              return t.Contains(Language.GetString(oSkin.orig.nameToken));
                      });
                      if(index != -1){
                        oSkin.syncInfo[rowcount -1 -i] = index;
                        row.UpdateHighlightedChoice();
                      }
                    }
                    }
                    self.currentDisplayData.userProfile.OnLoadoutChanged();//Find out why we need this. (Skin details don't update till the loadout changes at least once)
              }
          };
          On.RoR2.Loadout.Serialize += (orig,self,writer) =>{
            orig(self,writer);
            var oSkin = networkOSkins.GetOrCreateValue(self);
            if(oSkin.orig == null){
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
            oSkin.orig.Bake();
            oSkin.rSkin = new RuntimeSkin{
                meshReplacementTemplates = ArrayUtils.Clone(oSkin.orig.runtimeSkin.meshReplacementTemplates),
                rendererInfoTemplates = ArrayUtils.Clone(oSkin.orig.runtimeSkin.rendererInfoTemplates),
                gameObjectActivationTemplates = ArrayUtils.Clone(oSkin.orig.runtimeSkin.gameObjectActivationTemplates)
            };
            SkinDef[] skins = SkinCatalog.GetBodySkinDefs(oSkin.bodyIndex);
            var meshes = skins.SelectMany((s) => s.meshReplacements).Select(m => m.renderer).Distinct();
            var infos = skins.SelectMany((s) => s.rendererInfos).Select(i => i.renderer).Distinct();
            var minions = skins.SelectMany((s) => s.minionSkinReplacements).Select(m => m.minionBodyPrefab).Distinct();
            var projectiles = skins.SelectMany((s) => s.projectileGhostReplacements).Select(p => p.projectilePrefab).Distinct();
            var count = 0;
            if(oSkin.seperateMaterials){
               foreach(var renderer in meshes){
                 var path = Util.BuildPrefabTransformPath(oSkin.orig.rootObject.transform,renderer.transform);
                 if(oSkin.syncInfo[count] == 0 && !skins[0].meshReplacements.Any(m => m.renderer == renderer)){
                  var list = oSkin.rSkin.meshReplacementTemplates.ToList();
                  list.RemoveAll(m => m.path == path);
                  oSkin.rSkin.meshReplacementTemplates = list.ToArray();
                  var list3 = oSkin.rSkin.gameObjectActivationTemplates.ToList();
                  list3.RemoveAll(m => m.path == path && m.shouldActivate == false);
                  oSkin.rSkin.gameObjectActivationTemplates = list3.ToArray();
                  count++;
                  continue;
                 }
                 var validskins = skins.Where(s => s.meshReplacements.Any(m => m.renderer == renderer && m.mesh != null)).ToList();
                 if(validskins.Count <= oSkin.syncInfo[count]){  
                  ArrayUtils.ArrayAppend(ref oSkin.rSkin.meshReplacementTemplates,new MeshReplacementTemplate{path = path,mesh = null});
                  count++;
                  continue;
                 }
                 var skin = validskins[oSkin.syncInfo[count]];
                 
                 if(skin.meshReplacements.Any(m => m.renderer == renderer)){
                  ArrayUtils.ArrayAppend(ref oSkin.rSkin.meshReplacementTemplates,new MeshReplacementTemplate{path = path,mesh = skin.meshReplacements.First(m => m.renderer == renderer).mesh});
                 }
                 if(skin.rendererInfos.Any(m => m.renderer == renderer)){
                  var info = skin.rendererInfos.First(m => m.renderer == renderer);
                  var index = Array.FindIndex(oSkin.rSkin.rendererInfoTemplates,r => r.path == path);
                  ArrayUtils.ArrayInsert(ref oSkin.rSkin.rendererInfoTemplates,index == (-1) ? oSkin.rSkin.rendererInfoTemplates.Length : index,new RendererInfoTemplate{path = path,data = new RendererInfo{
                     defaultMaterial = info.defaultMaterial,
                     hideOnDeath = info.hideOnDeath,
                     renderer = info.renderer,
                     ignoreOverlays = info.ignoreOverlays,
                     defaultShadowCastingMode = info.defaultShadowCastingMode
                  }
                  });
                  }
                  ArrayUtils.ArrayAppend(ref oSkin.rSkin.gameObjectActivationTemplates,new GameObjectActivationTemplate{path = path,shouldActivate = true});
                  count++;
                }
               foreach(var renderer in infos){
                 var path = Util.BuildPrefabTransformPath(oSkin.orig.rootObject.transform,renderer.transform);
                 var validskins = skins.Where(s => s.rendererInfos.Any(m => m.renderer == renderer)).ToList();
                 if(validskins.Count <= oSkin.syncInfo[count]){  
                  ArrayUtils.ArrayAppend(ref oSkin.rSkin.gameObjectActivationTemplates,new GameObjectActivationTemplate{path = path,shouldActivate = false});
                  count++;
                  continue;
                 }
                 var skin = validskins[oSkin.syncInfo[count]];
                 if(skin.rendererInfos.Any(m => m.renderer == renderer)){
                  var info = skin.rendererInfos.First(m => m.renderer == renderer);
                  var index = Array.FindIndex(oSkin.rSkin.rendererInfoTemplates,r => r.path == path);
                  ArrayUtils.ArrayInsert(ref oSkin.rSkin.rendererInfoTemplates,index == (-1) ? oSkin.rSkin.rendererInfoTemplates.Length : index,new RendererInfoTemplate{path = path,data = new RendererInfo{
                     defaultMaterial = info.defaultMaterial,
                     hideOnDeath = info.hideOnDeath,
                     renderer = info.renderer,
                     ignoreOverlays = info.ignoreOverlays,
                     defaultShadowCastingMode = info.defaultShadowCastingMode
                  }
                  });
                  }
                  ArrayUtils.ArrayAppend(ref oSkin.rSkin.gameObjectActivationTemplates,new GameObjectActivationTemplate{path = path,shouldActivate = true});
                  count++;
               }
            }
            else{
                foreach(var renderer in meshes.Concat(infos).Distinct()){
                 var path = Util.BuildPrefabTransformPath(oSkin.orig.rootObject.transform,renderer.transform);
                 if(oSkin.syncInfo[count] == 0 && !skins[0].meshReplacements.Any(m => m.renderer == renderer) && !skins[0].rendererInfos.Any(m => m.renderer == renderer)){
                  var list = oSkin.rSkin.meshReplacementTemplates.ToList();
                  list.RemoveAll(m => m.path == path);
                  oSkin.rSkin.meshReplacementTemplates = list.ToArray();
                  var list3 = oSkin.rSkin.gameObjectActivationTemplates.ToList();
                  list3.RemoveAll(m => m.path == path && m.shouldActivate == false);
                  oSkin.rSkin.gameObjectActivationTemplates = list3.ToArray();
                  count++;
                  continue;
                 }
                 var validskins = skins.Where(s => s.meshReplacements.Any(m => m.renderer == renderer && m.mesh != null) || s.rendererInfos.Any(r => r.renderer == renderer)).ToList();
                 if(validskins.Count <= oSkin.syncInfo[count]){  
                  ArrayUtils.ArrayAppend(ref oSkin.rSkin.meshReplacementTemplates,new MeshReplacementTemplate{path = path,mesh = null});
                  count++;
                  continue;
                 }
                 var skin = validskins[oSkin.syncInfo[count]];
                 
                 if(skin.meshReplacements.Any(m => m.renderer == renderer)){
                  ArrayUtils.ArrayAppend(ref oSkin.rSkin.meshReplacementTemplates,new MeshReplacementTemplate{path = path,mesh = skin.meshReplacements.First(m => m.renderer == renderer).mesh});
                 }
                 else{
                   var list = oSkin.rSkin.meshReplacementTemplates.ToList();
                   list.RemoveAll(m => m.path == path);
                   oSkin.rSkin.meshReplacementTemplates = list.ToArray();
                 }
                 if(skin.rendererInfos.Any(m => m.renderer == renderer)){
                  var info = skin.rendererInfos.First(m => m.renderer == renderer);
                  var index = Array.FindIndex(oSkin.rSkin.rendererInfoTemplates,r => r.path == path);
                  ArrayUtils.ArrayInsert(ref oSkin.rSkin.rendererInfoTemplates,index == (-1) ? oSkin.rSkin.rendererInfoTemplates.Length : index,new RendererInfoTemplate{path = path,data = new RendererInfo{
                     defaultMaterial = info.defaultMaterial,
                     hideOnDeath = info.hideOnDeath,
                     renderer = info.renderer,
                     ignoreOverlays = info.ignoreOverlays,
                     defaultShadowCastingMode = info.defaultShadowCastingMode
                  }
                  });
                  }
                  ArrayUtils.ArrayAppend(ref oSkin.rSkin.gameObjectActivationTemplates,new GameObjectActivationTemplate{path = path,shouldActivate = true});
                  count++;
                }
            }
            foreach(var minion in minions){
              oSkin.minions.Add(skins.Where(s => s.minionSkinReplacements.Any(m => m.minionBodyPrefab == minion)).ElementAt(oSkin.syncInfo[count]).minionSkinReplacements.First(m => m.minionBodyPrefab == minion));
              count++;
            }
            foreach(var proj in projectiles){
              oSkin.projectiles.Add(skins.Where(s => s.projectileGhostReplacements.Any(m => m.projectilePrefab == proj)).ElementAt(oSkin.syncInfo[count]).projectileGhostReplacements.First(m => m.projectilePrefab == proj));
              count++;
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
