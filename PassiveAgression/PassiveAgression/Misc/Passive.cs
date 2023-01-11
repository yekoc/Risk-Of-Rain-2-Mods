using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.AddressableAssets;
using R2API;
using System.Collections.Generic;
using System.Linq;
using RoR2.UI;
using MonoMod.Cil;
using Mono.Cecil.Cil;

using static RoR2.UI.CharacterSelectController;

namespace PassiveAgression{
 public class CustomPassiveSlot{

        internal string prefabString = null;
        internal GameObject bodyPrefab = null;
        internal GenericSkill skill = null;
        internal SkillFamily family => skill?.skillFamily;
        public static SkillDef NoneDef;
        //public SkillDef currentDef => skill?.skillDef;

        static CustomPassiveSlot(){
           LanguageAPI.Add("PASSIVEAGRESSION_NONE","None");
           LanguageAPI.Add("PASSIVEAGRESSION_NONE_DESC","Nothing");
           NoneDef = ScriptableObject.CreateInstance<SkillDef>();
           NoneDef.skillNameToken = "NONE";
           (NoneDef as ScriptableObject).name = NoneDef.skillNameToken;
           NoneDef.skillDescriptionToken = "NONE_DESC";
           //NoneDef.icon = LoadoutAPI.CreateSkinIcon(Color.black,Color.white,Color.grey,Color.grey);
           NoneDef.icon = Util.SpriteFromFile("nonedef.png");
           NoneDef.baseRechargeInterval = 0f;
           NoneDef.activationState = new EntityStates.SerializableEntityStateType(typeof(EntityStates.Idle));
           NoneDef.activationStateMachineName = "Body";
           ContentAddition.AddSkillDef(NoneDef);
           On.RoR2.UI.LoadoutPanelController.Row.FromSkillSlot += (orig,owner,bodyI,slotI,slot) => {
             LoadoutPanelController.Row row = (LoadoutPanelController.Row)orig(owner,bodyI,slotI,slot);
             if((slot.skillFamily as ScriptableObject).name.Contains("Passive")){
                 Transform label = row.rowPanelTransform.Find("SlotLabel") ?? row.rowPanelTransform.Find("LabelContainer").Find("SlotLabel");
                 if(label)
                  label.GetComponent<LanguageTextMeshController>().token = "Passive";
             }
             return row;
            };
           IL.RoR2.UI.CharacterSelectController.BuildSkillStripDisplayData += (il) => {
               ILCursor c = new ILCursor(il);
               int skillIndex = -1;
               int defIndex = -1;
               var label = c.DefineLabel();
               if(c.TryGotoNext(x => x.MatchLdloc(out skillIndex),x => x.MatchLdfld(typeof(GenericSkill).GetField("hideInCharacterSelect")),x => x.MatchBrtrue(out label)) && skillIndex != (-1) && c.TryGotoNext(MoveType.After,x => x.MatchLdfld(typeof(SkillFamily.Variant).GetField("skillDef")),x => x.MatchStloc(out defIndex))) {
                    c.Emit(OpCodes.Ldloc,defIndex); 
                    c.EmitDelegate<System.Func<SkillDef,bool>>((def) => def == NoneDef);
                    c.Emit(OpCodes.Brtrue,label);
                   if(c.TryGotoNext(x => x.MatchCallOrCallvirt(typeof(List<StripDisplayData>).GetMethod("Add")))){
                    c.Remove();
                    c.Emit(OpCodes.Ldloc,skillIndex);
                    c.EmitDelegate<System.Action<List<StripDisplayData>,StripDisplayData,GenericSkill>>((list,disp,ski) => {
                      if((ski.skillFamily as ScriptableObject).name.Contains("Passive")){
                        list.Insert(0,disp);
                      } else {
                        list.Add(disp);
                      }
                    });
                   }
               }
           };
           IL.RoR2.UI.LoadoutPanelController.Rebuild += (il) => {
               ILCursor c = new ILCursor(il);
               if(c.TryGotoNext(MoveType.After,x=>x.MatchLdloc(0),x=>x.MatchCallOrCallvirt(out _),x=>x.MatchCallOrCallvirt(out _),x => x.MatchStloc(1))){
                   c.Emit(OpCodes.Ldloc_1);
                   c.Emit(OpCodes.Ldarg_0);
                   c.EmitDelegate<System.Action<List<GenericSkill>,LoadoutPanelController>>((list,self) => {
                    foreach(var slot in list.Where((slot) => {return slot != list.First() && (slot.skillFamily as ScriptableObject).name.Contains("Passive") ;})){
                      self.rows.Add(LoadoutPanelController.Row.FromSkillSlot(self,self.currentDisplayData.bodyIndex,list.FindIndex((skill) => skill == slot),slot));
                    }
                    list.RemoveAll((slot) => {return slot != list.First() && (slot.skillFamily as ScriptableObject).name.Contains("Passive");});
                   });
               }
           };
        }
        public CustomPassiveSlot(string path){
            bodyPrefab = Addressables.LoadAssetAsync<GameObject>(path).WaitForCompletion();
            prefabString = path;
            Init(!bodyPrefab.GetComponent<SkillLocator>().passiveSkill.enabled);
        }
        public CustomPassiveSlot(string path,bool useNone){
            bodyPrefab = Addressables.LoadAssetAsync<GameObject>(path).WaitForCompletion();
            prefabString = path;
            Init(useNone);
        }
        public CustomPassiveSlot(GameObject prefab){
            bodyPrefab = prefab;
            Init(!bodyPrefab.GetComponent<SkillLocator>().passiveSkill.enabled);
        }
        public CustomPassiveSlot(GameObject prefab,bool useNone){
            bodyPrefab = prefab;
            Init(useNone);
        }
        public void Init(bool SetupNone = true){
            if(!bodyPrefab && prefabString != null){
                bodyPrefab = Addressables.LoadAssetAsync<GameObject>(prefabString).WaitForCompletion();
            }
            if(bodyPrefab){
                skill = bodyPrefab.AddComponent<GenericSkill>();
                SkillLocator locator = bodyPrefab.GetComponent<SkillLocator>();
                skill._skillFamily = ScriptableObject.CreateInstance<SkillFamily>();
                (skill.skillFamily as ScriptableObject).name = bodyPrefab.name + "Passive";
                skill.skillFamily.variants = new SkillFamily.Variant[1];
                skill.skillName = bodyPrefab.name + "Passive";
                if(SetupNone)
                  skill.skillFamily.variants[0] = new SkillFamily.Variant{skillDef = NoneDef,viewableNode = new ViewablesCatalog.Node(NoneDef.skillNameToken,false,null)}; 
                else if(locator.passiveSkill.enabled){ 
                  locator.passiveSkill.enabled = false;
                  SkillDef passiveDef = ScriptableObject.CreateInstance<SkillDef>();
                  passiveDef.skillNameToken = locator.passiveSkill.skillNameToken;
                  (passiveDef as ScriptableObject).name = passiveDef.skillNameToken;
                  passiveDef.skillDescriptionToken = locator.passiveSkill.skillDescriptionToken;
                  passiveDef.icon = locator.passiveSkill.icon;
                  passiveDef.keywordTokens = locator.passiveSkill.keywordToken.Length>0 ? new string[] {locator.passiveSkill.keywordToken} : null;
                  passiveDef.baseRechargeInterval = 0f;
                  passiveDef.activationStateMachineName = "Body";
                  passiveDef.activationState = new EntityStates.SerializableEntityStateType(typeof(EntityStates.GenericCharacterMain));
                  ContentAddition.AddSkillDef(passiveDef);
                  skill.skillFamily.variants[0] = new SkillFamily.Variant{skillDef = passiveDef,viewableNode = new ViewablesCatalog.Node(passiveDef.skillNameToken,false,null)};
                }
                ContentAddition.AddSkillFamily(skill.skillFamily);
            }

        }

        
    }
 public class AssignableSkillDef : SkillDef { 
        
        public System.Func<GenericSkill,SkillDef.BaseSkillInstanceData> onAssign;
        public System.Action<GenericSkill> onUnassign;

        public override BaseSkillInstanceData OnAssigned(GenericSkill skillSlot)
        {
                return onAssign?.Invoke(skillSlot);
        }

        public override void OnUnassigned(GenericSkill skillSlot)
        {
                base.OnUnassigned(skillSlot);
                onUnassign?.Invoke(skillSlot);
        } 
        public bool IsAssigned(CharacterBody body){
            return System.Array.Exists(body.skillLocator.allSkills,IsAssigned);
        }
        public bool IsAssigned(GenericSkill skill){
            return skill.skillDef == this;
        }
        public bool IsAssigned(CharacterBody body,SkillSlot skillSlot){
            return body.skillLocator.GetSkill(skillSlot).skillDef == this;
        }
        public GenericSkill GetSkill(CharacterBody body){
            if(body && body.skillLocator){
             for(int i = 0;i < body.skillLocator.allSkills.Length;i++){
               if(IsAssigned(body.skillLocator.allSkills[i])){
                 return body.skillLocator.allSkills[i];
               }
             }
            }
            return null;
        }
 }
 public class SingleUseSkillDef : AssignableSkillDef {
     public override void OnExecute(GenericSkill skillSlot){
        base.OnExecute(skillSlot);
        if(skillSlot.stock <= 0){
          skillSlot.UnsetSkillOverride(skillSlot.gameObject,this,GenericSkill.SkillOverridePriority.Contextual);
        }
     }
 }
}
