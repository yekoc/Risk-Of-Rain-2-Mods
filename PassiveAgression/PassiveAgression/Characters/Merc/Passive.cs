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
    public static class FlickerPassive{
     public static AssignableSkillDef def;
     public static CustomPassiveSlot slot;
     public static BuffDef bDef;
     public static bool isHooked = false;

     private static void OrbInflictorHook(ILContext il){
         ILCursor c = new ILCursor(il);
     }


     static FlickerPassive(){
         slot = new CustomPassiveSlot("RoR2/Base/Merc/MercBody.prefab");
         LanguageAPI.Add("PASSIVEAGRESSION_MERCFLICKER","Stopped Clock");
         LanguageAPI.Add("PASSIVEAGRESSION_MERCFLICKER_DESC","Attacks that would <style=cIsUtility>expose</style> enemies instead temporarily <style=cIsUtility> freezes them in time </style>");
         bDef = ScriptableObject.CreateInstance<BuffDef>();
         (bDef as ScriptableObject).name = "PASSIVEAGRESSION_MERCFLICKER_BUFF";
         ContentAddition.AddBuffDef(bDef);
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_MERCFLICKER";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MERCFLICKER_DESC";
         def.onAssign = (GenericSkill slot) => {
             slot.characterBody.baseJumpCount--;
             if(!isHooked){
                isHooked = true;
                IL.RoR2.EntityStateMachine.FixedUpdate += DesyncedHook;
                IL.RoR2.EntityStateMachine.Update += DesyncedHook;
                IL.RoR2.HealthComponent.TakeDamage += Apply;
                On.RoR2.CharacterBody.OnBuffFirstStackGained += Desync;
                On.RoR2.CharacterBody.OnBuffFinalStackLost += Resync;
                Run.onRunDestroyGlobal += cleanup;
             }
             return null;
         };
         def.onUnassign = (GenericSkill slot) =>{
             slot.characterBody.baseJumpCount++;
         };
         def.icon = slot.family.variants[0].skillDef.icon;
         def.baseRechargeInterval = 0f;
         def.activationStateMachineName = "Body";
         def.cancelSprintingOnActivation = false;
         def.activationState = new SerializableEntityStateType(typeof(GenericCharacterMain));
         def.cancelSprintingOnActivation = false;
         def.canceledFromSprinting = false;
         ContentAddition.AddSkillDef(def);

         void cleanup(Run run){
             if(isHooked){
               IL.RoR2.EntityStateMachine.FixedUpdate -= DesyncedHook;
               IL.RoR2.EntityStateMachine.Update -= DesyncedHook;
               IL.RoR2.HealthComponent.TakeDamage -= Apply;
               On.RoR2.CharacterBody.OnBuffFirstStackGained -= Desync;
               On.RoR2.CharacterBody.OnBuffFinalStackLost -= Resync;
               isHooked = false;
             }
             Run.onRunDestroyGlobal -= cleanup;
         }
     }

     static void Desync(On.RoR2.CharacterBody.orig_OnBuffFirstStackGained orig,CharacterBody self,BuffDef def){
         orig(self,def);
         if(def == bDef){
            if(self.modelLocator && self.modelLocator.modelTransform){
              var ani = self.modelLocator.modelTransform.GetComponent<Animator>();
              if(ani)
                  ani.enabled = false;
            }
            if(self.rigidbody && !self.rigidbody.isKinematic){
                self.rigidbody.velocity = Vector3.zero;
                var rmot = self.GetComponent<RigidbodyMotor>();
                if(rmot)
                    rmot.moveVector = Vector3.zero;
            }
            if(self.characterMotor){
                self.characterMotor.velocity = Vector3.zero;
            }
         }
     }
     static void Resync(On.RoR2.CharacterBody.orig_OnBuffFinalStackLost orig,CharacterBody self,BuffDef def){
         orig(self,def);
         if(def == bDef){
            if(self.modelLocator && self.modelLocator.modelTransform){
              var ani = self.modelLocator.modelTransform.GetComponent<Animator>();
              if(ani)
                  ani.enabled = true;
            }
         }
     }
     static void Apply(ILContext il){
         ILCursor c = new ILCursor(il);
         if(c.TryGotoNext(MoveType.After,
                 x => x.MatchLdsfld(typeof(RoR2Content.Buffs).GetField(nameof(RoR2Content.Buffs.MercExpose),(System.Reflection.BindingFlags)(-1))),
                 x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetMethod(nameof(CharacterBody.AddBuff),new Type[]{typeof(BuffDef)})))){
             c.Index--;
             c.Emit(OpCodes.Ldarg_1);
             c.EmitDelegate<Func<BuffDef,DamageInfo,BuffDef>>((orig,dInfo) => (dInfo.attacker && def.IsAssigned(dInfo.attacker.GetComponent<CharacterBody>()))? bDef : orig);
         }
     }
     static void DesyncedHook(ILContext il){
        ILCursor c = new ILCursor(il);
        c.Index = -1;
        var label = c.MarkLabel();
        c.Index = 0;
        var label2 = c.MarkLabel();
        c.MoveBeforeLabels();
        c.Emit(OpCodes.Ldarg_0);
        c.Emit(OpCodes.Ldfld,typeof(EntityStateMachine).GetField(nameof(EntityStateMachine.commonComponents)));
        c.Emit(OpCodes.Ldfld,typeof(EntityStateMachine.CommonComponentCache).GetField(nameof(EntityStateMachine.CommonComponentCache.characterBody)));
        c.Emit(OpCodes.Call,typeof(UnityEngine.Object).GetMethod("op_Implicit",(System.Reflection.BindingFlags)(-1)));
        c.Emit(OpCodes.Brfalse,label2);
        c.Emit(OpCodes.Ldarg_0);
        c.Emit(OpCodes.Ldfld,typeof(EntityStateMachine).GetField(nameof(EntityStateMachine.commonComponents)));
        c.Emit(OpCodes.Ldfld,typeof(EntityStateMachine.CommonComponentCache).GetField(nameof(EntityStateMachine.CommonComponentCache.characterBody)));
        c.Emit(OpCodes.Ldsfld,typeof(FlickerPassive).GetField(nameof(FlickerPassive.bDef)));
        c.Emit(OpCodes.Call,typeof(CharacterBody).GetMethod(nameof(CharacterBody.HasBuff),new Type[]{typeof(BuffDef)}));
        c.Emit(OpCodes.Brtrue,label);
     }
    }
}
