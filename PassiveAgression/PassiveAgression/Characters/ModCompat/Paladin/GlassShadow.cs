using RoR2;
using RoR2.Skills;
using EntityStates;
using RoR2.CharacterAI;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Networking;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using PaladinMod.States;
using R2API;

namespace PassiveAgression.ModCompat
{
    public static class PaladinGlassShadow{
     public static Dictionary<CharacterBody,List<CharacterBody>> dopList = new Dictionary<CharacterBody,List<CharacterBody>>();
     public static DoppelSkillDef def;
     public static AssignableSkillDef scepterdef;
     public static AsyncOperationHandle<Material> glassMat = Addressables.LoadAssetAsync<Material>("RoR2/Base/Brother/maBrotherGlassOverlay.mat"); 
     public static GameObject glassPrefab;
     private class PaladinDoppelInputBank : DoppelInputBank{} //To prevent hooks from affecting anything else that might use the inputbank
     public class DoppelSkillDef : AssignableSkillDef{
         public override bool IsReady(GenericSkill skillSlot){
             return base.IsReady(skillSlot) && dopList[skillSlot.characterBody].Count < skillSlot.maxStock;
         }
     }
     static PaladinGlassShadow(){
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINCLONE","Glass Shadow");
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINCLONE_DESC","Summon a glass facsimile with <style=cIsHealth>10%</style> health that copies your moves,<style=cIsUtility>hold to increase mimicry delay.</style>");
         def = ScriptableObject.CreateInstance<DoppelSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_PALADINCLONE";
         def.skillDescriptionToken = "PASSIVEAGRESSION_PALADINCLONE_DESC";
         def.baseRechargeInterval = 45f;
         def.dontAllowPastMaxStocks = false;
         def.fullRestockOnAssign = true;
         def.rechargeStock = 1;
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(PrepGlassShadowState));
         def.cancelSprintingOnActivation = false;
         def.canceledFromSprinting = false;
         def.isCombatSkill = false;
         (def as ScriptableObject).name = def.skillNameToken;
         def.icon = Util.SpriteFromFile("GShadowIcon.png");
         def.onAssign = (slot) =>{
            if(!dopList.ContainsKey(slot.characterBody)){
             dopList.Add(slot.characterBody,new List<CharacterBody>());
            }
            return null;
         };
         def.onUnassign = (slot) =>{
            if((dopList != null) && slot.characterBody){
             foreach(var glass in dopList[slot.characterBody]){
               glass.healthComponent.godMode = false;
               glass.master.TrueKill();
             }
             dopList.Remove(slot.characterBody);
            }
         };

         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(PrepGlassShadowState),out _);
         ContentAddition.AddEntityState(typeof(CastGlassShadowState),out _);
         On.RoR2.ModelSkinController.ApplySkin += (orig,self,index) =>
         {
             orig(self,index);
             bool isGlass = (self?.characterModel?.body?.master?.GetComponent<PaladinDoppelInputBank>())??false;
             if(isGlass){
                 self.characterModel.itemDisplayRuleSet = null;
                 self.characterModel.enabledItemDisplays.Clear();
                 var renderers = self.characterModel.baseRendererInfos;
                 for(var num = renderers.Length - 1 ;num >= 0; num--){
                     renderers[num].defaultMaterial = glassMat.Result??glassMat.WaitForCompletion();
                     renderers[num].ignoreOverlays = true;
                 }
                 self.characterModel.baseRendererInfos = renderers;
             }
         };
         RecalculateStatsAPI.GetStatCoefficients += (sender,args) =>{
             if(sender?.master?.GetComponent<PaladinDoppelInputBank>()){
                 args.healthMultAdd -= 0.9f;
                 args.regenMultAdd -= 1f; 
             }
         };
         IL.EntityStates.GenericCharacterDeath.OnEnter += (il) =>{
             ILCursor c = new ILCursor(il);
             if(c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetProperty(nameof(CharacterBody.isGlass)).GetGetMethod()))){
               c.Emit(OpCodes.Ldarg_0);
               c.EmitDelegate<Func<bool,GenericCharacterDeath,bool>>((glass,self) => {
                 if(self.characterBody && self.characterBody.master && self.characterBody.master.GetComponent<PaladinDoppelInputBank>()){
                   dopList[self.characterBody.master.minionOwnership.ownerMaster.GetBody()].Remove(self.characterBody);
                   self.characterBody.master.CancelInvoke("RespawnExtraLife");
                   self.characterBody.master.CancelInvoke("PlayExtraLifeSFX");
                   self.characterBody.master.CancelInvoke("RespawnExtraLifeVoid");
                   self.characterBody.master.CancelInvoke("PlayExtraLifeVoidSFX");
                   return true;
                 }
                 return glass;
                 });
             }
         };
     }

     [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining | System.Runtime.CompilerServices.MethodImplOptions.NoOptimization)]
     public static void SetUpScepter(){
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINCLONE_SCEPTER","True Reflection");
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINCLONE_SCEPTERDESC","Summon an <color=#d299ff>invincible</color> glass copy that mimics your moves,<style=cIsUtility>recast to resummon it to yourself with a new delay.</style>");
         scepterdef = ScriptableObject.CreateInstance<AssignableSkillDef>();
         scepterdef.skillNameToken = "PASSIVEAGRESSION_PALADINCLONE_SCEPTER";
         scepterdef.skillDescriptionToken = "PASSIVEAGRESSION_PALADINCLONE_SCEPTERDESC";
         scepterdef.baseRechargeInterval = 0f;
         scepterdef.dontAllowPastMaxStocks = true;
         scepterdef.fullRestockOnAssign = true;
         scepterdef.rechargeStock = 1;
         scepterdef.requiredStock = 0;
         scepterdef.activationStateMachineName = "Weapon";
         scepterdef.activationState = new SerializableEntityStateType(typeof(PrepGlassShadowState));
         scepterdef.cancelSprintingOnActivation = false;
         scepterdef.canceledFromSprinting = false;
         scepterdef.isCombatSkill = false;
         scepterdef.onUnassign = def.onUnassign;
         scepterdef.onAssign = def.onAssign;
         (scepterdef as ScriptableObject).name = scepterdef.skillNameToken;
         scepterdef.icon = Util.SpriteFromFile("GShadowIconScepter.png");
         ContentAddition.AddSkillDef(scepterdef);
         ContentAddition.AddEntityState(typeof(RegrabGlassShadowState),out _);
         AncientScepter.AncientScepterItem.instance.RegisterScepterSkill(scepterdef,"RobPaladinBody",def);
     }
     public class PrepGlassShadowState : BaseChannelSpellState{
	    ushort delay = 0;
            public override void OnEnter(){
                    baseDuration = 0.25f;
                    if(characterBody.master.GetComponent<PaladinDoppelInputBank>()){
                        outer.SetNextStateToMain();
                        outer.nextStateModifier += (EntityStateMachine esm,ref EntityState nextState) =>{
                          if(nextState is PrepGlassShadowState){
                            nextState = null;
                          }
                        }; 
                    }
		    base.OnEnter();
                    areaIndicatorInstance = null;
	    }
	    public override void FixedUpdate(){
                base.FixedUpdate();
                if(CalcCharge() >=1f)
                 delay++;
	    }
	    public override void OnExit(){
		    base.OnExit();
	    }
	    public override InterruptPriority GetMinimumInterruptPriority(){
		    return InterruptPriority.PrioritySkill;
	    }
            public override BaseCastChanneledSpellState GetNextState(){
                if(scepterdef && activatorSkillSlot.skillDef == scepterdef){
                 return new RegrabGlassShadowState(Math.Min(delay,(ushort)300));
                }
                else{
                 return new CastGlassShadowState(Math.Min(delay,(ushort)300));
                }
            }
            public override void PlayChannelAnimation(){
             base.PlayAnimation("Gesture, Override", "ChannelHeal", "Spell.playbackRate", this.baseDuration);
            }
     }
     public class CastGlassShadowState : BaseCastChanneledSpellState{
            public ushort delay = 0;
            public CastGlassShadowState(ushort delayFrames){
                delay = delayFrames;
            }
	    public override void OnEnter(){
                    baseDuration = 0.4f;
                    muzzleString = "HandL";
                    muzzleflashEffectPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Brother/MuzzleflashLunarShard.prefab").WaitForCompletion(); 
		    base.OnEnter();
                    new MasterSummonClient{
                        position = characterBody.transform.position,
                        rotation = characterBody.transform.rotation,
                        masterPrefab = PaladinMod.PaladinPlugin.instance.doppelganger,
                        inventoryToCopy = characterBody.inventory,
                        preSpawnSetupCallback = (master) =>{
                            master.gameObject.AddComponent<PaladinDoppelInputBank>().updateDelay = delay;
                            var ai = master.GetComponent<BaseAI>();
                            if(ai){
                             master.aiComponents = Array.Empty<BaseAI>();
                             Destroy(ai);
                            }
                            else{
                             master.playerCharacterMasterController = null;
                             Destroy(master.GetComponent<PlayerCharacterMasterController>());
                            }
                            master.onBodyStart += (body) =>{
                              body.teamComponent.hideAllyCardDisplay = true;
                              var machine = EntityStateMachine.FindByCustomName(body.gameObject,"Body");
                              machine.initialStateType = machine.mainStateType;
                              body.baseNameToken = Language.GetString(body.baseNameToken) + "GLASS";
                              body.skillLocator.primary.stock = characterBody.skillLocator.primary.stock;
                              body.skillLocator.secondary.stock = characterBody.skillLocator.secondary.stock;
                              body.skillLocator.utility.stock = characterBody.skillLocator.utility.stock;
                              body.outOfCombat = characterBody.outOfCombat;
                              body.outOfDanger = characterBody.outOfDanger;
                              body.gameObject.layer = LayerIndex.fakeActor.intVal;
                              body.characterMotor.Motor.RebuildCollidableLayers();
                              dopList[characterBody].Add(body);
                            };
                            master.inventory.onInventoryChanged +=  () =>{
                                master.luck = -10f;
                            };
                            master.destroyOnBodyDeath = true;
                        },
                        summonerBodyObject = characterBody.gameObject, 
                        loadout = characterBody.master.loadout
                    }.Perform();
	    }
	    public override void FixedUpdate(){
                    base.FixedUpdate();
	    }
	    public override void OnExit(){
		    base.OnExit();

	    }
	    public override InterruptPriority GetMinimumInterruptPriority(){
		    return InterruptPriority.PrioritySkill;
	    }
            public override void OnSerialize(NetworkWriter writer)
            {
                base.OnSerialize(writer);
                writer.Write(delay);
            }
            public override void OnDeserialize(NetworkReader reader)
            {
                base.OnDeserialize(reader);
                this.delay = reader.ReadUInt16();
            }
            public override void PlayCastAnimation()
            {
                base.PlayAnimation("Gesture, Override", "CastHeal", "Spell.playbackRate", this.baseDuration * 1.5f);
            }
     }
     public class RegrabGlassShadowState : BaseCastChanneledSpellState{
            public ushort delay = 0;
            public RegrabGlassShadowState(ushort delayFrames){
                delay = delayFrames;
            }
	    public override void OnEnter(){
                  baseDuration = 0.4f;
                  muzzleString = "HandL";
                  muzzleflashEffectPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Brother/MuzzleflashLunarShard.prefab").WaitForCompletion(); 
		  base.OnEnter();
                  var list = dopList[characterBody];
                  if(list.Count <= 0){
                    new MasterSummonClient{
                        position = characterBody.transform.position,
                        rotation = characterBody.transform.rotation,
                        masterPrefab = PaladinMod.PaladinPlugin.instance.doppelganger,
                        inventoryToCopy = characterBody.inventory,
                        preSpawnSetupCallback = (master) =>{
                            var bank = master.gameObject.AddComponent<PaladinDoppelInputBank>();
                            bank.updateDelay = delay;
                            bank.ownerInputs = characterBody.inputBank;
                            var ai = master.GetComponent<BaseAI>();
                            if(ai){
                             master.aiComponents = Array.Empty<BaseAI>();
                             Destroy(ai);
                            }
                            else{
                             master.playerCharacterMasterController = null;
                             Destroy(master.GetComponent<PlayerCharacterMasterController>());
                            }
                            master.onBodyStart += (body) =>{
                              body.teamComponent.hideAllyCardDisplay = true;
                              var machine = EntityStateMachine.FindByCustomName(body.gameObject,"Body");
                              machine.initialStateType = machine.mainStateType;
                              body.baseNameToken = Language.GetString(body.baseNameToken) + "GLASS";
                              body.skillLocator.primary.stock = characterBody.skillLocator.primary.stock;
                              body.skillLocator.secondary.stock = characterBody.skillLocator.secondary.stock;
                              body.skillLocator.utility.stock = characterBody.skillLocator.utility.stock;
                              body.outOfCombat = characterBody.outOfCombat;
                              body.outOfDanger = characterBody.outOfDanger;
                              body.gameObject.layer = LayerIndex.fakeActor.intVal;
                              body.characterMotor.Motor.RebuildCollidableLayers();
                              dopList[characterBody].Add(body);
                              body.healthComponent.godMode = true;
                            };
                            master.inventory.onInventoryChanged +=  () =>{
                                master.luck = -10f;
                            };
                            master.destroyOnBodyDeath = true;
                        },
                        summonerBodyObject = characterBody.gameObject, 
                        loadout = characterBody.master.loadout
                    }.Perform();
                  }
                  else{ 
                    var body = list.First();
                    if(body){
                        if(RoR2.Util.HasEffectiveAuthority(body.gameObject)){
                         TeleportHelper.TeleportBody(body,characterBody.footPosition);
                        }
                        var inp = body.master.GetComponent<PaladinDoppelInputBank>();
                        inp.updateDelay = delay;
                        inp.inputBuffer.Clear();
                        if(NetworkServer.active){
                         body.inventory.CopyItemsFrom(characterBody.inventory);
                        }
                    }
                  }
	    }
	    public override void FixedUpdate(){
                    base.FixedUpdate();
	    }
	    public override void OnExit(){
		    base.OnExit();

	    }
	    public override InterruptPriority GetMinimumInterruptPriority(){
		    return InterruptPriority.PrioritySkill;
	    }
            public override void OnSerialize(NetworkWriter writer)
            {
                base.OnSerialize(writer);
                writer.Write(delay);
            }
            public override void OnDeserialize(NetworkReader reader)
            {
                base.OnDeserialize(reader);
                this.delay = reader.ReadUInt16();
            }
            public override void PlayCastAnimation()
            {
                base.PlayAnimation("Gesture, Override", "CastHeal", "Spell.playbackRate", this.baseDuration * 1.5f);
            }
     }
    }
}
