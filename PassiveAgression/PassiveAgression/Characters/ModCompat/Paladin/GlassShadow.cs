using RoR2;
using RoR2.Skills;
using EntityStates;
using RoR2.CharacterAI;
using System;
using System.Linq;
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
     public static SkillDef def;
     public static AsyncOperationHandle<Material> glassMat = Addressables.LoadAssetAsync<Material>("RoR2/Base/Brother/maBrotherGlassOverlay.mat"); 
     public static GameObject glassPrefab;
     private class PaladinDoppelInputBank : DoppelInputBank{} //To prevent hooks from affecting anything else that might use the inputbank
     static PaladinGlassShadow(){
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINCLONE","Glass Shadow");
         LanguageAPI.Add("PASSIVEAGRESSION_PALADINCLONE_DESC","Summon a glass facsimile with <style=cIsHealth>10%</style> health that copies your moves,<style=cIsUtility>hold to increase mimicry delay.</style>");
         def = ScriptableObject.CreateInstance<SkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_PALADINCLONE";
         def.skillDescriptionToken = "PASSIVEAGRESSION_PALADINCLONE_DESC";
         def.baseRechargeInterval = 0f;
         def.dontAllowPastMaxStocks = false;
         def.fullRestockOnAssign = true;
         def.rechargeStock = 0;
         def.activationStateMachineName = "Weapon";
         def.activationState = new SerializableEntityStateType(typeof(PrepGlassShadowState));
         def.cancelSprintingOnActivation = false;
         def.canceledFromSprinting = false;
         (def as ScriptableObject).name = def.skillNameToken;
         def.icon = LoadoutAPI.CreateSkinIcon(Color.cyan,Color.cyan,Color.cyan,Color.cyan);
         LoadoutAPI.AddSkillDef(def);
         LoadoutAPI.AddSkill(typeof(PrepGlassShadowState));
         LoadoutAPI.AddSkill(typeof(CastGlassShadowState));
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
                 
             }
         };
         IL.EntityStates.GenericCharacterDeath.OnEnter += (il) =>{
             ILCursor c = new ILCursor(il);
             if(c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetProperty(nameof(CharacterBody.isGlass)).GetGetMethod()))){
               c.Emit(OpCodes.Ldarg_0);
               c.EmitDelegate<Func<bool,GenericCharacterDeath,bool>>((glass,self) => (glass || self.characterBody.master.GetComponent<PaladinDoppelInputBank>()));
             }
         };
     }
     public class PrepGlassShadowState : BaseChannelSpellState{
	    ushort delay = 0;
            public override void OnEnter(){
                    baseDuration = 0.25f;
                    if(characterBody.master.GetComponent<PaladinDoppelInputBank>()){
                        outer.SetNextStateToMain();
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
            protected override BaseCastChanneledSpellState GetNextState(){
                return new CastGlassShadowState(Math.Min(delay,(ushort)300));
            }
            protected override void PlayChannelAnimation(){
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
                    PassiveAgressionPlugin.Logger.LogError(delay);
                    new MasterSummonClient{
                        position = characterBody.transform.position,
                        rotation = characterBody.transform.rotation,
                        masterPrefab = PaladinMod.PaladinPlugin.instance.doppelganger,
                        //inventoryToCopy = characterBody.inventory,
                        preSpawnSetupCallback = (master) =>{
                            master.gameObject.AddComponent<PaladinDoppelInputBank>().updateDelay = delay;
                            master.inventory = characterBody.inventory;
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
                              body.gameObject.layer = LayerIndex.fakeActor.intVal;
                              body.characterMotor.Motor.RebuildCollidableLayers();
                            };
                            master.onBodyDeath.AddListener( () =>{
                              characterBody?.skillLocator.special.AddOneStock();
                            });
                            master.inventory.onInventoryChanged +=  () =>{
                                master.luck = -10f;
                            };
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
            protected override void PlayCastAnimation()
            {
                base.PlayAnimation("Gesture, Override", "CastHeal", "Spell.playbackRate", this.baseDuration * 1.5f);
            }
     }
    }
}
