using RoR2;
using RoR2.Skills;
using EntityStates;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;

namespace PassiveAgression.Merc
{
    public static class ExposePassive{
     public static AssignableSkillDef def,def2;
     public static CustomPassiveSlot slot;
     public static BuffDef bDef;
     public static Material plumBlossomMat;
     public static HealthComponent dumbToggle;
     public static GameObject mercPlumEffect;
     public static bool isHooked = false;


     public class MercVisualEffectHolder : MonoBehaviour{
         public TemporaryVisualEffect plumBlossomEffect;
     }

     static ExposePassive(){
         slot = new CustomPassiveSlot("RoR2/Base/Merc/MercBody.prefab",false);
         slot.skill = slot.bodyPrefab.AddComponent<GenericSkill>();
         SkillLocator locator = slot.bodyPrefab.GetComponent<SkillLocator>();
         slot.skill._skillFamily = ScriptableObject.CreateInstance<SkillFamily>();
         (slot.skill.skillFamily as ScriptableObject).name = slot.bodyPrefab.name + "PassiveTwo";
         slot.skill.loadoutTitleToken = "Passive";
         SkillsAPI.SetOrderPriority(slot.skill,-1);
         slot.skill.skillFamily.variants = new SkillFamily.Variant[1];
         slot.skill.skillName = slot.bodyPrefab.name + "PassiveTwo";
         LanguageAPI.Add("PASSIVEAGRESSION_MERCEXPOSE_DEFAULT","Expose");
         LanguageAPI.Add("PASSIVEAGRESSION_MERCEXPOSE_DEFAULT_DESC","Attacks that would <style=cIsUtility>expose</style> enemies <style=cIsUtility>expose</style> enemies.");
         LanguageAPI.Add("PASSIVEAGRESSION_MERCEXPOSE_PLUMBLOSSOM","Red Plum Blossom");
         LanguageAPI.Add("PASSIVEAGRESSION_MERCEXPOSE_PLUMBLOSSOM_DESC","Attacks that would <style=cIsUtility>expose</style> enemies instead apply <style=cIsDamage>Red Plum Blossoms</style>.");
         LanguageAPI.Add("PASSIVEAGRESSION_MERCEXPOSE_PLUMBLOSSOM_KEYWORD","<style=cKeywordName>Red Plum Blossom</style><style=cSub>Directly hitting enemies with blossoms applied has an additional <style=cIsUtility>20%</style> chance to crit, crits apply <style=cIsDamage>50% total damage</style> as bleed and remove blossoms.");
         bDef = ScriptableObject.CreateInstance<BuffDef>();
         (bDef as ScriptableObject).name = "PASSIVEAGRESSION_MERCEXPOSE_PLUMBLOSSOM_BUFF";
         bDef.isDebuff = true;
         bDef.iconSprite = Util.SpriteFromFile("PlumBlossom.png");
         bDef.canStack = false;
         ContentAddition.AddBuffDef(bDef);
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_MERCEXPOSE_DEFAULT";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MERCEXPOSE_DEFAULT_DESC";
         def.keywordTokens = new string[]{"KEYWORD_EXPOSE"};
         def.icon = Util.SpriteFromFile("MercExposeIcon.png");
         def.baseRechargeInterval = 0f;
         def.activationStateMachineName = "Body";
         def.cancelSprintingOnActivation = false;
         def.activationState = new SerializableEntityStateType(typeof(GenericCharacterMain));
         def.cancelSprintingOnActivation = false;
         def.canceledFromSprinting = false;
         def2 = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def2.skillNameToken = "PASSIVEAGRESSION_MERCEXPOSE_PLUMBLOSSOM";
         (def2 as ScriptableObject).name = def2.skillNameToken;
         def2.skillDescriptionToken = "PASSIVEAGRESSION_MERCEXPOSE_PLUMBLOSSOM_DESC";
         def2.keywordTokens = new string[]{"PASSIVEAGRESSION_MERCEXPOSE_PLUMBLOSSOM_KEYWORD"};
         def2.onAssign = (GenericSkill slot) => {
             if(!isHooked){
                isHooked = true;
                IL.RoR2.HealthComponent.TakeDamageProcess += Apply;
                IL.RoR2.HealthComponent.TakeDamageProcess += Redeem;
                On.RoR2.HealthComponent.TakeDamageProcess += DumbToggle;
                On.RoR2.CharacterBody.UpdateAllTemporaryVisualEffects += VisualEffect;
                Run.onRunDestroyGlobal += cleanup;
             }
             if(!mercPlumEffect){
                 mercPlumEffect = PrefabAPI.InstantiateClone(CharacterBody.AssetReferences.mercExposeEffectPrefab,"PAPlumBlossomEffect");
                 foreach( var renderer in mercPlumEffect.GetComponentsInChildren<Renderer>()){
                         if(!plumBlossomMat){
                             plumBlossomMat = GameObject.Instantiate(renderer.material);
                             plumBlossomMat.mainTexture = bDef.iconSprite.texture;
                             plumBlossomMat.SetColor("_TintColor",Color.red);
                         }
                         renderer.material = plumBlossomMat;
                 }
                 foreach(var system in mercPlumEffect.GetComponentsInChildren<ParticleSystem>()){
                     var main = system.main;
                     main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                     var sizeOverTime = system.sizeOverLifetime;
                     sizeOverTime.sizeMultiplier *= 0.7f;
                 }
             }
             return null;
         };
         def2.icon = Util.SpriteFromFile("MercPlumBlossomIcon.png");
         def2.baseRechargeInterval = 0f;
         def2.activationStateMachineName = "Body";
         def2.cancelSprintingOnActivation = false;
         def2.activationState = new SerializableEntityStateType(typeof(GenericCharacterMain));
         def2.cancelSprintingOnActivation = false;
         def2.canceledFromSprinting = false;
         CustomPassiveSlot.NoneDefMerc = def;
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddSkillDef(def2);
         slot.skill.skillFamily.variants[0] = new SkillFamily.Variant{skillDef = def,viewableNode = new ViewablesCatalog.Node(def.skillNameToken,false,null)};


         void cleanup(Run run){
             if(isHooked){
               isHooked = false;
               IL.RoR2.HealthComponent.TakeDamageProcess -= Apply;
               IL.RoR2.HealthComponent.TakeDamageProcess -= Redeem;
               On.RoR2.HealthComponent.TakeDamageProcess -= DumbToggle;
               On.RoR2.CharacterBody.UpdateAllTemporaryVisualEffects -= VisualEffect;
             }
             Run.onRunDestroyGlobal -= cleanup;
         }
     }

     static void VisualEffect(On.RoR2.CharacterBody.orig_UpdateAllTemporaryVisualEffects orig, CharacterBody self){
         orig(self);
         var holder = self.gameObject.GetComponent<MercVisualEffectHolder>();
         var hasEffect = self.HasBuff(bDef);
         if(!holder && hasEffect){
             holder = self.gameObject.AddComponent<MercVisualEffectHolder>();
         }
         if(holder){
             self.UpdateSingleTemporaryVisualEffect(ref holder.plumBlossomEffect, mercPlumEffect , self.radius/2, hasEffect);
         }
     }

     static void DumbToggle(On.RoR2.HealthComponent.orig_TakeDamageProcess orig, HealthComponent self, DamageInfo info){
         if(!info.crit && (info.damageType & DamageType.AOE) == 0 && info.procChainMask.mask == 0 && self.body && self.body.HasBuff(bDef)){
             info.crit = RoR2.Util.CheckRoll(20f,0f,info.attacker.GetComponent<CharacterBody>()?.master);
         }
         orig(self,info);
         dumbToggle = null;
     }
     
     static void Apply(ILContext il){
         ILCursor c = new ILCursor(il);
         if(c.TryGotoNext(MoveType.After,
                 x => x.MatchLdsfld(typeof(RoR2Content.Buffs).GetField(nameof(RoR2Content.Buffs.MercExpose),(System.Reflection.BindingFlags)(-1))),
                 x => x.MatchLdcI4(1),
                 x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetMethod(nameof(CharacterBody.AddUnmanagedBuff),new Type[]{typeof(BuffDef),typeof(int)})))){
             c.Index-=2;
             c.Emit(OpCodes.Ldarg_0);
             c.Emit(OpCodes.Ldarg_1);
             c.EmitDelegate<Func<BuffDef,HealthComponent,DamageInfo,BuffDef>>((orig,self,dInfo) => {
                     if(dInfo.attacker && def2.IsAssigned(dInfo.attacker.GetComponent<CharacterBody>())){
                         if(self.body && !self.body.HasBuff(bDef)){
                            dumbToggle = self;
                         }
                         return bDef;
                     }
                     return orig;
             });
         }
         else{
             PassiveAgressionPlugin.Logger.LogError("MercPassive2 Apply Hook failed");
         }
     }

     static void Redeem(ILContext il){
         ILCursor c = new ILCursor(il);
         int local = -1;
             if(c.TryGotoNext(MoveType.After,x=> x.MatchLdloc(out local),x => x.MatchLdcR4(0),x => x.MatchBleUn(out _), x=> x.MatchLdloc(out _),x => x.MatchCallOrCallvirt(typeof(HealthComponent).GetMethod("SendDamageDealt",(System.Reflection.BindingFlags)(-1))))){
                 c.MoveBeforeLabels();
                 c.Emit(OpCodes.Ldarg_0);
                 c.Emit(OpCodes.Ldarg_1);
                 c.Emit(OpCodes.Ldloc,local);
                 c.EmitDelegate<Action<HealthComponent,DamageInfo,float>>((self,info,damage) => {
                         var body = self.body;
                         if(body && info.crit && self != dumbToggle && body.HasBuff(bDef) && damage > 0){
                            var infdotinf =  new InflictDotInfo{
                                    attackerObject = info.attacker,
                                    victimObject = body.gameObject,
                                    dotIndex = DotController.DotIndex.Bleed,
                                    totalDamage = damage/2,
                                    damageMultiplier = 1f
                            };
                            DotController.InflictDot(ref infdotinf);
			    EffectManager.SimpleImpactEffect(HealthComponent.AssetReferences.bossDamageBonusImpactEffectPrefab, info.position, -info.force, Color.red ,transmit: true);
                            body.RemoveBuff(bDef);
                         }
                 });
                 return;
             }
         PassiveAgressionPlugin.Logger.LogError("MercPassive2 Redeem Hook Failed");
     }

    }
}

