using RoR2;
using R2API;
using System;
using UnityEngine;
using UnityEngine.Networking;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using EntityStates;
using EntityStates.Captain.Weapon;
using EntityStates.CaptainSupplyDrop;

namespace PassiveAgression.Captain{
    public static class IntegratedBeacon{

        public static RoR2.Skills.CaptainSupplyDropSkillDef def;
        public static GameObject attachmentPrefab;

        static IntegratedBeacon(){
         var asset = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/QuestVolatileBattery/QuestVolatileBatteryAttachment.prefab");
         LanguageAPI.Add("PASSIVEAGRESSION_CAPTAINSELFBEACON","Self Integrated Beacon");
         LanguageAPI.Add("PASSIVEAGRESSION_CAPTAINSELFBEACON_DESC","<style=cIsDamage>Stunning.</style> Deal 75% damage,allies in area are <style=cIsUtility>cleansed from a debuff</style>.");
         def = ScriptableObject.CreateInstance<RoR2.Skills.CaptainSupplyDropSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_CAPTAINSELFBEACON";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_CAPTAINSELFBEACON_DESC";
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(SetupSupplyDrop));
            On.EntityStates.Captain.Weapon.CallSupplyDropBase.OnEnter += CallSupplyDropHook;
            On.EntityStates.Captain.Weapon.SetupSupplyDrop.OnEnter += (orig,self) =>{
                orig(self);
                if(self.characterBody.skillLocator.special.skillDef == def && self.blueprints){
                EntityState.Destroy(self.blueprints.gameObject);
                self.blueprints = null;
                };
            };
            On.EntityStates.CaptainSupplyDrop.BaseCaptainSupplyDropState.UpdateEnergyIndicator += (orig,self) =>{
                if(self.energyIndicator && self.energyIndicatorContainer){
                  orig(self);
                }
            };
            IL.EntityStates.CaptainSupplyDrop.BaseCaptainSupplyDropState.OnEnter += (il) =>{
              ILCursor c = new ILCursor(il);
              ILLabel lab = c.DefineLabel();
              c.GotoNext(x => x.MatchRet());
              c.MoveAfterLabels();
              c.MarkLabel(lab);
              if(c.TryGotoPrev(MoveType.After,x => x.MatchStfld(typeof(ProxyInteraction).GetField(nameof(ProxyInteraction.shouldIgnoreSpherecastForInteractability))))){
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<BaseCaptainSupplyDropState,bool>>((self) => self.GetModelTransform() && self.FindModelChild("EnergyIndicatorContainer") && self.FindModelChild("EnergyIndicator"));
                c.Emit(OpCodes.Brfalse,lab);
              }
            };
            On.EntityStates.CaptainSupplyDrop.HackingMainState.ScanForTarget += (orig,self) =>{
              self.sphereSearch.origin = self.transform.position;
              return orig(self);
            };
            On.EntityStates.CaptainSupplyDrop.HealZoneMainState.OnEnter += (orig,self) =>{
              orig(self);
              self.healZoneInstance.transform.SetParent(self.transform);
            };
            On.EntityStates.CaptainSupplyDrop.HackingInProgressState.FixedUpdate += (orig,self) =>{
                orig(self);
                var comp = self.targetIndicatorVfxInstance.GetComponent<ChildLocator>();
                if(comp && !self.FindModelChild("ShaftTip")){
                  var c2 = comp.FindChild("LineEnd");
                  if(c2){
                    c2.position = self.gameObject.transform.position;
                  }
                }
            };
         def.supplyDropSkillSlotNames = new String[]{"SupplyDrop1","SupplyDrop2"};
         def.exhaustedNameToken = "name";
         def.exhaustedDescriptionToken = "desc";

         attachmentPrefab = PrefabAPI.InstantiateClone(asset.WaitForCompletion(),"PASSIVEAGRESSION_CAPTAINSELFBEACON_ATTACHMENT");
         attachmentPrefab.GetComponent<EntityStateMachine>().customName = "SelfBeacon";
         attachmentPrefab.AddComponent<ProxyInteraction>();
         attachmentPrefab.AddComponent<GenericEnergyComponent>();
         attachmentPrefab.AddComponent<TeamFilter>();
         attachmentPrefab.AddComponent<GenericOwnership>();
         attachmentPrefab.AddComponent<DestroyOnTimer>().duration = 40f;
         ContentAddition.AddSkillDef(def);
        }


        public static void CallSupplyDropHook(On.EntityStates.Captain.Weapon.CallSupplyDropBase.orig_OnEnter orig,CallSupplyDropBase self){
            if(self.characterBody.skillLocator.special.skillDef == def){//def.IsAssigned(self.characterBody,SkillSlot.Special)){
              self.attackSpeedStat = self.characterBody.attackSpeed;
              self.damageStat = self.characterBody.damage;
              self.moveSpeedStat = self.characterBody.moveSpeed;
              self.critStat = self.characterBody.crit;
              if(NetworkServer.active){
              GameObject attach = GameObject.Instantiate(attachmentPrefab);
              var esm = self.supplyDropPrefab.GetComponent<EntityStateMachine>();
              var target = attach.GetComponent<EntityStateMachine>();
              if(esm && target){
                target.initialStateType = esm.initialStateType.stateType == typeof(EntryState) ? new SerializableEntityStateType(typeof(DeployState)) : esm.initialStateType ;
                target.mainStateType = esm.mainStateType;
              }
              else{
		self.PlayAnimation("Gesture, Override", "CallSupplyDrop", "CallSupplyDrop.playbackRate", self.duration);
		self.PlayAnimation("Gesture, Additive", "CallSupplyDrop", "CallSupplyDrop.playbackRate", self.duration);
                return;
              }
              if(self.supplyDropPrefab.transform.Find("Inactive/Core/Rings")){
                GameObject.Instantiate(self.supplyDropPrefab.transform.Find("Inactive/Core/Rings").gameObject,self.gameObject.transform).AddComponent<DestroyOnTimer>().duration = 40f;
              }
		self.PlayCrossfade("Gesture, Override", "BufferEmpty", 0.1f);
		self.PlayCrossfade("Gesture, Additive", "BufferEmpty", 0.1f);
              attach.GetComponent<GenericEnergyComponent>().capacity = self.supplyDropPrefab.GetComponent<GenericEnergyComponent>().capacity;
              attach.GetComponent<GenericEnergyComponent>().chargeRate = self.supplyDropPrefab.GetComponent<GenericEnergyComponent>().chargeRate;
              attach.GetComponent<TeamFilter>().teamIndex = self.teamComponent.teamIndex;
              attach.GetComponent<GenericOwnership>().ownerObject = self.characterBody.gameObject;
              attach.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(self.characterBody.gameObject);
              self.activatorSkillSlot.DeductStock(1);
              }
            }
            else{
              orig(self);
            }
        }


    }
}
