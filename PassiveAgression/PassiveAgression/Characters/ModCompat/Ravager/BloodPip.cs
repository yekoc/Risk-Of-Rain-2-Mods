using BepInEx.Configuration;
using RoR2;
using RoR2.Skills;
using EntityStates;
using RoR2.CharacterAI;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using MonoMod.RuntimeDetour;
using R2API;
using R2API.Networking.Interfaces;
using System.Reflection;
using System.Collections.Generic;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using RedGuyMod.Content.Components;
using RedGuyMod.SkillStates.Ravager;

namespace PassiveAgression.ModCompat{
    public static class RavagerBloodPassive{
     public static AssignableSkillDef def;
     public static CustomPassiveSlot slot;
     public static Hook usePipHook,updatePipHook,skillIconHook,hudHook;
     public static ILHook coagulationHook,noModeChangeHook;
     public static bool isHooked;

     static RavagerBloodPassive(){
         slot = new CustomPassiveSlot(RedGuyMod.Content.Survivors.RedGuy.characterPrefab);
         slot.skill = slot.bodyPrefab.GetComponent<RedGuyPassive>().bloodPassiveSkillSlot;
         LanguageAPI.Add("PASSIVEAGRESSION_RAVAGERBLOODPIP","Coldblood");
         LanguageAPI.Add("PASSIVEAGRESSION_RAVAGERBLOODPIP_DESC",$"The Ravager stores up <style=cIsHealth>blood</style> into 3 pips with his strikes,a pip can be spent to <style=cIsDamage>empower a skill</style> or survive a <style=cDeath>lethal attack</style>.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_RAVAGERBLOODPIP";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_RAVAGERBLOODPIP_DESC";
         def.activationStateMachineName = "Body";
         def.onAssign = (GenericSkill slot) => {
             if(!isHooked){
                isHooked = true;
                usePipHook = new Hook(typeof(BaseRavagerSkillState).GetMethod("OnEnter"),EmpoweredHook);
                updatePipHook = new Hook(typeof(RedGuyController).GetMethod("FixedUpdate",(BindingFlags)(-1)),UpdateControllerHook);
                skillIconHook = new Hook(typeof(RedGuyMod.Content.RavagerSkillDef).GetMethod("OnFixedUpdate"),SkillIconPipReactivity);
                coagulationHook = new ILHook(typeof(RedGuyMod.SkillStates.Ravager.Heal).GetMethod("OnEnter"),CoagulateSinglePip);
                noModeChangeHook = new ILHook(typeof(RedGuyController).GetMethod("UpdateGauge"),PreventModeChange);
                hudHook = new Hook(typeof(RedGuyMod.Content.Survivors.RedGuy).GetMethod("HUDSetup",(BindingFlags)(-1)),HudSetup);
                RoR2.Run.onRunDestroyGlobal += unhooker;
             }
             //var pComp = slot.characterBody.GetComponent<RedGuyController>();
             HG.ArrayUtils.ArrayAppend(ref slot.characterBody.healthComponent.onTakeDamageReceivers,slot.characterBody.gameObject.AddComponent<BloodGaugePipDamageReciever>());
             return null;
             void unhooker(Run run){
                if(isHooked){
                   isHooked = false;
                   usePipHook.Free();
                   updatePipHook.Free();
                   skillIconHook.Free();
                   coagulationHook.Free();
                   noModeChangeHook.Free();
                   hudHook.Free();
                }
                RoR2.Run.onRunDestroyGlobal -= unhooker;
             }
             
         };
         def.onUnassign = (GenericSkill slot) =>{
             if(slot?.characterBody && slot.characterBody.healthComponent.alive){
                var comp = slot.characterBody.GetComponent<BloodGaugePipDamageReciever>();
                if(comp){
                    var list =slot.characterBody.healthComponent.onTakeDamageReceivers.ToList();
                    list.Remove(comp);
                    slot.characterBody.healthComponent.onTakeDamageReceivers = list.ToArray();
                    GameObject.Destroy(comp);
                }
             }
         };
         def.icon = Util.SpriteFromFile("ColdbloodIcon.png"); 
         def.baseRechargeInterval = 0f;
         def.activationStateMachineName = "Body";
         def.cancelSprintingOnActivation = false;
         def.canceledFromSprinting = false;
         def.activationState = EntityStateMachine.FindByCustomName(slot.bodyPrefab,"Body").mainStateType;
         ContentAddition.AddSkillDef(def);

     }

     public static void UpdateControllerHook(Action<RedGuyController> orig,RedGuyController redGuy){
         var meterboundary = Mathf.FloorToInt(redGuy.meter)/33; 
         orig(redGuy);
         if(def.IsAssigned(redGuy.passive.bloodPassiveSkillSlot)){
             redGuy.meter = Mathf.Min(Mathf.Max(redGuy.meter,33*meterboundary),99f);
         }
     }

     public static void PreventModeChange(ILContext il){
         ILCursor c = new(il);
         if(c.TryGotoNext(x => x.MatchBrfalse(out _))){
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<bool,RedGuyController,bool>>((c,r) => c && !def.IsAssigned(r.passive.bloodPassiveSkillSlot));
         }
     }

     public static void SkillIconPipReactivity(Action<RedGuyMod.Content.RavagerSkillDef,GenericSkill> orig,RedGuyMod.Content.RavagerSkillDef self,GenericSkill skill){
         orig(self,skill);
         if(skill != skill.characterBody.skillLocator.primary){
             var pComp = skill.characterBody.GetComponent<RedGuyController>();
             if(pComp && def.IsAssigned(pComp.passive.bloodPassiveSkillSlot) && pComp.meter >= 33f){
                self.icon = self.empoweredIcon;
             }
         }
     }

     public static void EmpoweredHook(Action<BaseRavagerSkillState> orig,BaseRavagerSkillState state){
         orig(state);
         if(def.IsAssigned(state.penis.passive.bloodPassiveSkillSlot) && state.activatorSkillSlot != null && state.activatorSkillSlot != state.skillLocator.primary){
            state.empowered = ConsumePip(state.penis);
         }
     }

     public static bool ConsumePip(RedGuyController controller){
         if(controller.meter >= 33f){
             controller.meter = Mathf.Max(controller.meter - 33f,0f);
             var netID = controller.GetComponent<NetworkIdentity>();
             if(netID && (NetworkServer.active || RoR2.Util.HasEffectiveAuthority(netID))){
                 new RedGuyMod.Content.SyncBloodWell(netID.netId,(ulong)(controller.meter * 100f)).Send(R2API.Networking.NetworkDestination.Clients);
             }
             return true;
         }
         return false;
     }

     public static void CoagulateSinglePip(ILContext il){
         ILCursor c = new(il);
         if(c.TryGotoNext(MoveType.After,x => x.MatchStfld(typeof(RedGuyMod.SkillStates.Ravager.Heal).GetField("amount",(BindingFlags)(-1))))){
             c.MoveBeforeLabels();
             c.Emit(OpCodes.Ldarg_0);
             c.EmitDelegate<Action<RedGuyMod.SkillStates.Ravager.Heal>>((self) => {
                if(def.IsAssigned(self.penis.passive.bloodPassiveSkillSlot) && self.empowered){
                  self.charge = 0.51f;
                  self.amount = RoR2.Util.Remap(33f,0f,100f,0f,self.healthComponent.fullHealth * 0.5f);
                }
             });
             c.GotoNext(x => x.MatchStfld(typeof(RedGuyController).GetField("meter")));
             c.Emit(OpCodes.Ldarg_0);
             c.EmitDelegate<Func<float,RedGuyMod.SkillStates.Ravager.Heal,float>>((meter,self) => (def.IsAssigned(self.penis.passive.bloodPassiveSkillSlot) && self.empowered) ? self.penis.meter : meter );
         }
     }

     public static void HudSetup(Action<RoR2.UI.HUD> orig,RoR2.UI.HUD self){
         orig(self);
         if(self.targetBodyObject && self.targetMaster.hasAuthority && self.targetMaster.bodyPrefab == slot.bodyPrefab && def.IsAssigned(self.targetMaster.GetBody())){
            if(RedGuyMod.Modules.Config.oldBloodWell.Value){
               var bloodGauge = self.transform.Find("MainContainer").Find("MainUIArea").Find("SpringCanvas").Find("BottomLeftCluster").Find("BloodGauge").GetComponent<BloodGauge>();
               var gauge2 = GameObject.Instantiate(bloodGauge.gameObject,bloodGauge.transform.parent);
               gauge2.transform.Find("LevelDisplayRoot").gameObject.SetActive(false);
               var gauge3 = GameObject.Instantiate(bloodGauge.gameObject,bloodGauge.transform.parent);
               gauge3.transform.Find("LevelDisplayRoot").gameObject.SetActive(false);
               var brect = bloodGauge.GetComponent<RectTransform>();
               brect.anchorMax = new Vector2(0.33f,1f);
               brect = gauge2.GetComponent<RectTransform>();
               brect.anchorMin = new Vector2(0.33f,0f);
               brect.anchorMax = new Vector2(0.66f,1f);
               brect = gauge3.GetComponent<RectTransform>();
               brect.anchorMax = new Vector3(0.99f,1f);
               brect.anchorMin = new Vector2(0.66f,0f);
               var manager = bloodGauge.gameObject.AddComponent<BloodGaugePipManager>();
               manager.redness = bloodGauge.target;
               manager.bars.Add(bloodGauge.fillRectTransform);
               manager.bars.Add(gauge2.GetComponentInChildren<BloodGauge>().fillRectTransform);
               manager.bars.Add(gauge3.GetComponentInChildren<BloodGauge>().fillRectTransform);
               manager.pips.Add(bloodGauge.GetComponentInChildren<BloodGauge>().transform.Find("ExpBarRoot").GetChild(0).GetChild(0).GetComponent<UnityEngine.UI.Image>());
               manager.pips.Add(gauge2.GetComponentInChildren<BloodGauge>().transform.Find("ExpBarRoot").GetChild(0).GetChild(0).GetComponent<UnityEngine.UI.Image>());
               manager.pips.Add(gauge3.GetComponentInChildren<BloodGauge>().transform.Find("ExpBarRoot").GetChild(0).GetChild(0).GetComponent<UnityEngine.UI.Image>());
            }
            else{ 
               var bloodGauge = self.transform.Find("MainContainer").Find("MainUIArea").Find("CrosshairCanvas").Find("CrosshairExtras").Find("ChargeRing(Clone)");
               var parentTransform = bloodGauge.transform.parent;
               var brect = bloodGauge.GetComponent<RectTransform>();
               brect.anchoredPosition = new Vector2(0f,0f);
               brect.pivot = new Vector2(0.5f,0.5f);
               brect.localScale = new Vector3(0.3f,0.3f,1f);
               var g2 = GameObject.Instantiate(bloodGauge,parentTransform);
               var g3 = GameObject.Instantiate(bloodGauge,parentTransform);
               if(!RedGuyMod.Modules.Config.centeredBloodWell.Value){
                   brect.position += new Vector3((float)Math.Cos(0),(float)Math.Sin(0),0f);
                   g2.GetComponent<RectTransform>().position = brect.position + new Vector3((float)Math.Cos(Mathf.PI * 2/3),(float)Math.Sin(Mathf.PI * 2/3),0f);
                   g3.GetComponent<RectTransform>().position = brect.position + new Vector3((float)Math.Cos(Mathf.PI * 4/3),(float)Math.Sin(Mathf.PI * 4/3),0f); 
               }
               else{
                   g2.GetComponent<RectTransform>().position = brect.position + new Vector3((float)Math.Cos(Mathf.PI * 2/3),(float)Math.Sin(Mathf.PI * 2/3),0f);
                   g3.GetComponent<RectTransform>().position = brect.position + new Vector3((float)Math.Cos(Mathf.PI * 4/3),(float)Math.Sin(Mathf.PI * 4/3),0f); 
                   brect.position += new Vector3((float)Math.Cos(0),(float)Math.Sin(0),0f);
               }
               var manager = parentTransform.gameObject.AddComponent<BloodGaugePipManager>();
               manager.pips.Add(bloodGauge.GetComponentInChildren<BloodGauge2>().fillBar);
               manager.pips.Add(g2.GetComponentInChildren<BloodGauge2>().fillBar);
               manager.pips.Add(g3.GetComponentInChildren<BloodGauge2>().fillBar);
            }
         }
     }

     public class BloodGaugePipDamageReciever : MonoBehaviour,IOnTakeDamageServerReceiver{
         public void OnTakeDamageServer(DamageReport damageReport){
             var redness = damageReport.victimBody.GetComponent<RedGuyController>();
             if(redness && damageReport.victim.wasAlive && !damageReport.victim.alive && ConsumePip(redness)){
                 damageReport.victim.Networkhealth = damageReport.combinedHealthBeforeDamage;
                 damageReport.victim.ospTimer += 0.2f;
                 new RedGuyMod.Content.SyncOrbOverlay(damageReport.victim.netId,damageReport.victim.gameObject).Send(R2API.Networking.NetworkDestination.Clients);
             }
         }
     }


     public class BloodGaugePipManager : MonoBehaviour{
         public List<RectTransform> bars = new();
         public List<UnityEngine.UI.Image> pips = new();
         public RedGuyController redness;

         public void Update(){
           if(!redness){
             redness = gameObject.GetComponentInChildren<BloodGauge>()?.target;
             if(!redness){
                 redness = gameObject.GetComponentInChildren<BloodGauge2>()?.target;
             }
           }
           if(!redness){
               return;
           }
           for(int i = 0;i < bars.Count;i++){
               if(!bars[i]){
                   continue;
               }
               var fill = Mathf.Min(RoR2.Util.Remap(redness.meter,i*33f,(i+1)*33f,0f,1f),1);
               bars[i].anchorMin = new Vector2(i/bars.Count,0f);
               bars[i].anchorMax = new Vector2(fill,1f);
               //bars[i].sizeDelta = new Vector2(1,1);
           }
           for(int i = 0;i < pips.Count;i++){
              if(!pips[i]){
                  continue;
              }
              if(redness.meter >= (i+1)*33f){
                 pips[i].color = new Color(1f,0f,46f/255f);
              }
              else{
                 pips[i].color = new Color(152f / 255f, 12f / 255f, 37f / 255f);
              }
              if(bars.Count == 0){
               pips[i].fillAmount = RoR2.Util.Remap(redness.meter,i*33f,(i+1)*33f,0f,1f);
              }
           }
         }
     }
    }
}
