using RoR2;
using R2API;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using EntityStates;
using EntityStates.Captain.Weapon;
using EntityStates.CaptainSupplyDrop;
using RoR2.Skills;

namespace PassiveAgression.Captain{
    public static class IntegratedBeacon{

        public static CaptainSupplyDropSkillDef def;
        public static bool isHooked;
        public static Dictionary<GameObject,GameObject> prefabs = new();
        public static RoR2BepInExPack.Utilities.FixedConditionalWeakTable<CharacterBody,GameObject> visualEffect = new();
        public static RoR2BepInExPack.Utilities.FixedConditionalWeakTable<EntityStateMachine,SphereSearch> hackRange = new();
        public static BuffDef beaconBuff;

        private static void BuffGained(On.RoR2.CharacterBody.orig_OnBuffFirstStackGained orig,CharacterBody body,BuffDef buff){
         orig(body,buff);
         if(buff == beaconBuff && !visualEffect.TryGetValue(body,out _)){
           var childLocator = body.modelLocator.modelTransform.GetComponent<ChildLocator>();
           var vfx = GameObject.Instantiate(SetupSupplyDrop.effectMuzzlePrefab,childLocator.FindChild("Head"));
           vfx.transform.localScale *= 2;
           visualEffect.Add(body,vfx);
         }
        }
        private static void BuffLost(On.RoR2.CharacterBody.orig_OnBuffFinalStackLost orig,CharacterBody body,BuffDef buff){
         orig(body,buff);
         if(buff == beaconBuff && visualEffect.TryGetValue(body,out var efx)){
            GameObject.Destroy(efx);
            visualEffect.Remove(body);
         }
        }
        private static void EnterSupplyDrop(ILContext il){
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
        }
        private static void EnterHackingMain(On.EntityStates.CaptainSupplyDrop.HackingMainState.orig_OnEnter orig,HackingMainState self){

                       orig(self);
                       hackRange.Remove(self.outer);
                       hackRange.Add(self.outer,self.sphereSearch);
        }
        private static PurchaseInteraction HackingScan(On.EntityStates.CaptainSupplyDrop.HackingMainState.orig_ScanForTarget orig,HackingMainState self){
                      self.sphereSearch.origin = self.transform.position;
                      return orig(self);

        }
        private static void HackingInProgress(On.EntityStates.CaptainSupplyDrop.HackingInProgressState.orig_FixedUpdate orig,HackingInProgressState self){

                        orig(self);
                        if(hackRange.TryGetValue(self.outer,out var search) && (self.transform.position - self.target.transform.position).magnitude > search.radius){
                            self.outer.SetNextStateToMain();
                        }
                        var comp = self.targetIndicatorVfxInstance.GetComponent<ChildLocator>();
                        if(comp && self.transform.parent){
                          var c2 = comp.FindChild("LineEnd");
                          if(c2){
                            c2.position = self.gameObject.transform.position;
                          }
                        }
        }
        private static void EnterHealZone(On.EntityStates.CaptainSupplyDrop.HealZoneMainState.orig_OnEnter orig,HealZoneMainState self){

                      orig(self);
                      self.healZoneInstance.transform.SetParent(self.transform);
                      if(self.transform.parent){
                       self.healZoneInstance.transform.eulerAngles = self.transform.parent.up;
                      }
        }
    /*    private static void EnergyIndicatorNullCheck(On.EntityStates.CaptainSupplyDrop.BaseCaptainSupplyDropState.orig_UpdateEnergyIndicator orig,BaseCaptainSupplyDropState self){
                        if(self.energyIndicator && self.energyIndicatorContainer){
                          orig(self);
                        }
        }*/


        static IntegratedBeacon(){
         var asset = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<CaptainSupplyDropSkillDef>("RoR2/Base/Captain/PrepSupplyDrop.asset");
         LanguageAPI.Add("PASSIVEAGRESSION_CAPTAINUPLINK","Integrated Beacon Uplink");
         LanguageAPI.Add("PASSIVEAGRESSION_CAPTAINUPLINK_DESC","Channel the effects of a Supply Beacon through your prosthetics. Can only be invoked <style=cIsUtility>twice per stage</style>.");
         beaconBuff = ScriptableObject.CreateInstance<BuffDef>();
         beaconBuff.canStack = true;
         beaconBuff.name = "Channeling Beacon";
         beaconBuff.iconSprite = Util.SpriteFromFile("IntegratedBeaconBuff.png");
         beaconBuff.buffColor = Color.red;
         def = ScriptableObject.CreateInstance<CaptainSupplyDropSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_CAPTAINUPLINK";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_CAPTAINUPLINK_DESC";
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(SetupSupplyUplink));
         def.icon = Util.SpriteFromFile("IntegratedBeaconIcon.png");
         CharacterBody.onBodyStartGlobal += (body) =>{
            if(body.skillLocator.FindSkillByDef(def)){
                if(!isHooked){
                    On.RoR2.CharacterBody.OnBuffFirstStackGained += BuffGained;
                    On.RoR2.CharacterBody.OnBuffFinalStackLost += BuffLost;
                    IL.EntityStates.CaptainSupplyDrop.BaseCaptainSupplyDropState.OnEnter += EnterSupplyDrop;
                    On.EntityStates.CaptainSupplyDrop.HackingMainState.OnEnter += EnterHackingMain;
                    On.EntityStates.CaptainSupplyDrop.HackingMainState.ScanForTarget += HackingScan;
                    On.EntityStates.CaptainSupplyDrop.HealZoneMainState.OnEnter += EnterHealZone;
                    On.EntityStates.CaptainSupplyDrop.HackingInProgressState.FixedUpdate += HackingInProgress;
                    foreach(var drop in body.skillLocator.FindSkill("SupplyDrop1").skillFamily.variants){
                       var stat = Activator.CreateInstance(drop.skillDef.activationState.stateType) as CallSupplyDropBase;
                       var gameObject = PrefabAPI.InstantiateClone(stat.supplyDropPrefab,"PASSIVEAGRESSION_CAPTAIN" + drop.skillDef.activationState.typeName + "UPLINK");
                       var esm1 = gameObject.GetComponent<EntityStateMachine>();
                       esm1.initialStateType = esm1.initialStateType.stateType == typeof(EntryState) ? new SerializableEntityStateType(typeof(DeployState)) : esm1.initialStateType;
                       gameObject.AddComponent<NetworkedBodyAttachment>();
                       gameObject.GetComponent<NetworkIdentity>().localPlayerAuthority = true;
                       gameObject.AddComponent<DestroyOnTimer>().duration = 40f;
                       GameObject.DontDestroyOnLoad(gameObject);
                       
                       prefabs.Add(stat.supplyDropPrefab,gameObject);
                    }
                    Run.onRunDestroyGlobal += unsub;
                    isHooked = true;
                }
                var esm = EntityStateMachine.FindByCustomName(body.gameObject,"Weapon");
                esm.nextStateModifier += (EntityStateMachine machine,ref EntityState nState) =>{
                   if(nState is CallSupplyDropBase){
                     var state = new AttachSupply();
                     state.obj = prefabs[(nState as CallSupplyDropBase).supplyDropPrefab];
                     state.activatorSkillSlot = (nState as BaseSkillState).activatorSkillSlot;
                     nState = state;
                   }
                };
            }
            void unsub(Run run){
             if(isHooked){
                    On.RoR2.CharacterBody.OnBuffFirstStackGained -= BuffGained;
                    On.RoR2.CharacterBody.OnBuffFinalStackLost -= BuffLost;
                    IL.EntityStates.CaptainSupplyDrop.BaseCaptainSupplyDropState.OnEnter -= EnterSupplyDrop;
                    On.EntityStates.CaptainSupplyDrop.HackingMainState.OnEnter -= EnterHackingMain;
                    On.EntityStates.CaptainSupplyDrop.HackingMainState.ScanForTarget -= HackingScan;
                    On.EntityStates.CaptainSupplyDrop.HealZoneMainState.OnEnter -= EnterHealZone;
                    On.EntityStates.CaptainSupplyDrop.HackingInProgressState.FixedUpdate -= HackingInProgress;
               Run.onRunDestroyGlobal -= unsub;
               isHooked = false;
             }
            }
         };
         def.supplyDropSkillSlotNames = new String[]{"SupplyDrop1","SupplyDrop2"};
         var assetReal = asset.WaitForCompletion();
         def.exhaustedNameToken = assetReal.exhaustedNameToken;
         def.exhaustedDescriptionToken = assetReal.exhaustedDescriptionToken;
         def.exhaustedIcon = assetReal.exhaustedIcon;
         def.disabledNameToken = assetReal.disabledNameToken;
         def.disabledDescriptionToken = assetReal.disabledDescriptionToken;
         def.disabledIcon = assetReal.disabledIcon;

         
         /*attachmentPrefab = PrefabAPI.InstantiateClone(asset.WaitForCompletion(),"PASSIVEAGRESSION_CAPTAINSELFBEACON_ATTACHMENT");
         attachmentPrefab.GetComponent<EntityStateMachine>().customName = "SelfBeacon";
         attachmentPrefab.AddComponent<ProxyInteraction>();
         attachmentPrefab.AddComponent<GenericEnergyComponent>();
         attachmentPrefab.AddComponent<TeamFilter>();
         attachmentPrefab.AddComponent<GenericOwnership>();
         attachmentPrefab.AddComponent<DestroyOnTimer>().duration = 40f;*/
         ContentAddition.AddBuffDef(beaconBuff);
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(SetupSupplyUplink),out _);
         ContentAddition.AddEntityState(typeof(AttachSupply),out _);
        }

        public class SetupSupplyUplink : BaseState{

            GenericSkill origPrim,origSec;
            GameObject effectMuzzleInstance;

            public override void OnEnter(){
               base.OnEnter();
               var modelAnimator = GetModelAnimator();
               PlayAnimation("Gesture, Override", "PrepSupplyDrop");
               PlayAnimation("Gesture, Additive", "PrepSupplyDrop");
               if ((bool)modelAnimator)
               {
                       modelAnimator.SetBool("PrepSupplyDrop", value: true);
               }
               Transform transform = FindModelChild(SetupSupplyDrop.effectMuzzleString);
               if ((bool)transform)
               {
                       effectMuzzleInstance = GameObject.Instantiate(SetupSupplyDrop.effectMuzzlePrefab, transform);
               }
               origPrim = skillLocator.primary;
               origSec = skillLocator.secondary;
               skillLocator.primary = skillLocator.FindSkill("SupplyDrop1");
               skillLocator.secondary = skillLocator.FindSkill("SupplyDrop2");
            }

            public override void OnExit(){
               base.OnExit();
               skillLocator.primary = origPrim;
               skillLocator.secondary = origSec;
               if(effectMuzzleInstance){
                 Destroy(effectMuzzleInstance);
               }
               GetModelAnimator()?.SetBool("PrepSupplyDrop",value:false);
            }

        }

        public class AttachSupply : BaseSkillState{
            public GameObject obj;

            public override void OnEnter(){
               base.OnEnter();
                       var skin = obj.transform.Find("ModelBase/captain supply drop/CaptainSupplyDropMesh");
                       if(skin){
                        skin.gameObject.SetActive(false);
                        GameObject.Destroy(skin.gameObject);
                       }
                       var highlight = obj.GetComponentInChildren<Highlight>();
                       if(highlight){
                         GameObject.Destroy(highlight);
                       }
               
               if(isAuthority){
                 activatorSkillSlot.DeductStock(1);
               }
               characterBody.SetAimTimer(3f);
               PlayAnimation("Gesture, Override", "CallSupplyDrop", "CallSupplyDrop.playbackRate", 1/attackSpeedStat);
               PlayAnimation("Gesture, Additive", "CallSupplyDrop", "CallSupplyDrop.playbackRate", 1/attackSpeedStat);
               if(NetworkServer.active){
                 var clone = GameObject.Instantiate(obj);
                 clone.GetComponent<TeamFilter>().teamIndex = teamComponent.teamIndex;
                 clone.GetComponent<GenericOwnership>().ownerObject = gameObject;
                 clone.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(gameObject);
                 characterBody.AddTimedBuff(beaconBuff,40f);
               }
                EntityStateMachine entityStateMachine = EntityStateMachine.FindByCustomName(base.gameObject, "Skillswap");
                if ((bool)entityStateMachine)
                {
                        entityStateMachine.SetNextStateToMain();
                }
            }

            public override void FixedUpdate(){
              base.FixedUpdate();
              if(fixedAge >= 1/attackSpeedStat){
                outer.SetNextStateToMain();
              }
            }
        }

       /* public static void CallSupplyDropHook(ILContext il){
         /*   if(self.characterBody.skillLocator.special.skillDef == def){//def.IsAssigned(self.characterBody,SkillSlot.Special)){
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
        }*/


    }
}
