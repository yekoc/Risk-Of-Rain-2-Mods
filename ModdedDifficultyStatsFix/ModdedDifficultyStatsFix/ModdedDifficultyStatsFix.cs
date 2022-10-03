using BepInEx;
using RoR2;
using RoR2.Stats;
using RoR2.UI;
using System;
using System.Security;
using System.Security.Permissions;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using MonoMod.Cil;


#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

[assembly : HG.Reflection.SearchableAttribute.OptIn]
namespace ModdedDifficultyStatFix
{
    [BepInPlugin("xyz.yekoc.ModdedDifficultyStatFix", "ModdedDifficultyStatFix","1.0.0" )]
    public class ModdedDifficultyStatFixPlugin : BaseUnityPlugin
    {
        public static Dictionary<DifficultyIndex,PerBodyStatDef> infiniteTowerWaveDict = new Dictionary<DifficultyIndex, PerBodyStatDef>();
        public static Dictionary<DifficultyIndex,LanguageTextMeshController> infiniteTowerTextDict = new Dictionary<DifficultyIndex, LanguageTextMeshController>();

        void Awake(){
            IL.RoR2.Stats.StatManager.OnInfiniteTowerWaveInitialized += (il) =>{
              ILCursor c = new ILCursor(il);
              if(c.TryGotoNext(x => x.MatchLdnull(),x => x.MatchStloc(out _))){
                c.Index++;
                c.EmitDelegate<Func<PerBodyStatDef,PerBodyStatDef>>((orig) => {
                  infiniteTowerWaveDict.TryGetValue(Run.instance.selectedDifficulty,out orig);
                  return orig;
                });
              }
            };
            On.RoR2.UI.InfiniteTowerMenuController.UpdateDisplayedSurvivor += (orig,self) =>{
               orig(self);
               var userProfile = self.localUser?.userProfile;
               if(userProfile != null){
                 var sheet = userProfile.statSheet;
                 var surv = userProfile.GetSurvivorPreference();
                 if(surv){
                    foreach(var pair in infiniteTowerTextDict){
                      self.SetHighestWaveDisplay(pair.Value,infiniteTowerWaveDict[pair.Key],sheet,BodyCatalog.GetBodyName(SurvivorCatalog.GetBodyIndexFromSurvivorIndex(surv.survivorIndex)));
                      //pair.Value.textMeshPro.text = pair.Value.textMeshPro.text.Replace(Language.GetString(DifficultyCatalog.GetDifficultyDef(DifficultyIndex.Hard).nameToken),Language.GetString(DifficultyCatalog.GetDifficultyDef(pair.Key).nameToken));
                    }
                 }
               }
               else{
                Logger.LogError("Can't find localuser");
               }
            };
            On.RoR2.UI.InfiniteTowerMenuController.OnEnable += (orig,self) =>{
              infiniteTowerTextDict.Clear();
              infiniteTowerTextDict.Add(DifficultyIndex.Easy,self.easyHighestWaveText);
              infiniteTowerTextDict.Add(DifficultyIndex.Normal,self.normalHighestWaveText);
              infiniteTowerTextDict.Add(DifficultyIndex.Hard,self.hardHighestWaveText);
              foreach(DifficultyIndex diff in infiniteTowerWaveDict.Keys){
                if(!infiniteTowerTextDict.ContainsKey(diff)){
                 infiniteTowerTextDict.Add(diff,GameObject.Instantiate(self.hardHighestWaveText,self.hardHighestWaveText.transform.parent));
                 //infiniteTowerTextDict[diff].textMeshPro.text = Language.GetString(infiniteTowerTextDict[diff].token).Replace(Language.GetString(DifficultyCatalog.GetDifficultyDef(DifficultyIndex.Hard).nameToken),Language.GetString(DifficultyCatalog.GetDifficultyDef(diff).nameToken));
                }
              } 
              orig(self);
            };
            On.RoR2.UI.LanguageTextMeshController.ResolveString += (orig,self) =>{
                orig(self);
                if(self.token == "INFINITETOWER_TIME_HIGHEST_WAVE_HARD" && infiniteTowerTextDict.ContainsValue(self)){
                  var dif = infiniteTowerTextDict.First((pair) => pair.Value == self).Key;
                  self.resolvedString = self.resolvedString.Replace(Language.GetString(DifficultyCatalog.GetDifficultyDef(DifficultyIndex.Hard).nameToken),Language.GetString(DifficultyCatalog.GetDifficultyDef(dif).nameToken));
                }
            };
        }
        [SystemInitializer(new Type[]{typeof(RuleCatalog),typeof(BodyCatalog)})]
        public static void RegisterDifficulties(){
          infiniteTowerWaveDict.Add(DifficultyIndex.Easy,PerBodyStatDef.highestInfiniteTowerWaveReachedEasy);
          infiniteTowerWaveDict.Add(DifficultyIndex.Normal,PerBodyStatDef.highestInfiniteTowerWaveReachedNormal);
          infiniteTowerWaveDict.Add(DifficultyIndex.Hard,PerBodyStatDef.highestInfiniteTowerWaveReachedHard);
          foreach(var choice in RuleCatalog.FindRuleDef("Difficulty").choices){
            if(!(choice.excludeByDefault) && !infiniteTowerWaveDict.ContainsKey(choice.difficultyIndex)){
              var def = PerBodyStatDef.Register("highestInfiniteTowerWaveReached"+ choice.tooltipNameToken,StatRecordType.Max,StatDataType.ULong);
              infiniteTowerWaveDict.Add(choice.difficultyIndex,def);
              def.bodyIndexToStatDef = new StatDef[BodyCatalog.bodyCount];
              for(BodyIndex i = 0; (int)i < BodyCatalog.bodyCount; i++){
                string bodyName = BodyCatalog.GetBodyName(i);
                var sdef = StatDef.Register(def.prefix + "." + bodyName,def.recordType,def.dataType,0.0,def.displayValueFormatter);
                def.bodyIndexToStatDef[(int)i] = sdef;
                def.bodyNameToStatDefDictionary.Add(bodyName,sdef);
                def.bodyNameToStatDefDictionary.Add(bodyName + "(Clone)",sdef);
              }
            }
          }
          StatSheet.OnFieldsFinalized();
        }
    }
}
