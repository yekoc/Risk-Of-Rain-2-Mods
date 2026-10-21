using RoR2;
using RoR2.Skills;
using EntityStates;
using RoR2.CharacterAI;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using EntityStates.Mage;
using static RoR2.DotController;

namespace PassiveAgression.Mage
{
    public static class IcePassive{
     public static AssignableSkillDef def;
     public static CustomPassiveSlot slot;
     public static bool isHooked = false;

     private static void unJetHook(ILContext il){
         ILCursor c = new ILCursor(il);
         if(c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(typeof(CharacterMotor).GetProperty(nameof(CharacterMotor.isGrounded)).GetGetMethod()))){
           c.Emit(OpCodes.Ldarg_0);
           c.EmitDelegate<Func<bool,MageCharacterMain,bool>>((orig,self) => orig || def.IsAssigned(self.characterBody));
         }
     }

     private static void UpgradeFreeze(On.RoR2.SetStateOnHurt.orig_OnTakeDamageServer orig, SetStateOnHurt self,DamageReport rep){
         if(self.targetStateMachine && self.canBeFrozen 
                 && (IceRitualState.ownerInstances.Contains(rep.attackerBody)
                     ||  IceRitualState.ownerInstances.Contains(rep.attackerMaster?.minionOwnership?.ownerMaster?.GetBody()) )){
             self.targetStateMachine.nextStateModifier += modifier;
         }
         orig(self,rep);

         void modifier(EntityStateMachine esm, ref EntityState s){
             if(s is EntityStates.FrozenState f){
                 var fs = new FrozenTotemState();
                 fs.freezeDuration = f.freezeDuration;
                 fs.attackerBody = rep.attackerBody;
                 s = fs;
             }
             esm.nextStateModifier -= modifier;
         }
     }
     private static void UpgradeFrost(ILContext il){
         ILCursor c = new ILCursor(il);
         if(c.TryGotoNext(MoveType.After,x=>x.MatchLdsfld(typeof(DLC2Content.Buffs).GetField(nameof(DLC2Content.Buffs.Frost),(System.Reflection.BindingFlags)(-1))),
            x=>x.MatchLdcR4(out _),x=>x.MatchLdcI4(out _),
            x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetMethod(nameof(CharacterBody.AddTimedBuff),new Type[]{typeof(BuffDef),typeof(float),typeof(int)})))){
             c.Index--;
             c.Emit(OpCodes.Ldarg_1);
             c.Emit(OpCodes.Ldarg_2);
             c.EmitDelegate<Action<DamageReport,GameObject>>((rep,vic) =>{
                  var setState = vic.GetComponent<SetStateOnHurt>();
                  if(setState){
                   UpgradeFreeze((x,y) => {}, setState,rep);
                  }
             });
         }
     }


     static IcePassive(){
         slot = new CustomPassiveSlot("RoR2/Base/Mage/MageBody.prefab");
         LanguageAPI.Add("PASSIVEAGRESSION_MAGEFROSTRITE","Hoarfrost Rite");
         LanguageAPI.Add("PASSIVEAGRESSION_MAGEFROSTRITE_DESC","<style=cIsUtility>Frozen</style> enemies emit an aura which slows and damages enemies.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_MAGEFROSTRITE";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_MAGEFROSTRITE_DESC";
         def.onAssign = (GenericSkill slot) => {
             if(!isHooked){
                isHooked = true;
                IL.EntityStates.Mage.MageCharacterMain.ProcessJump += unJetHook;
                IL.RoR2.GlobalEventManager.ProcessHitEnemy += UpgradeFrost;
                On.RoR2.SetStateOnHurt.OnTakeDamageServer += UpgradeFreeze;
             }
             var jetMachine = EntityStateMachine.FindByCustomName(slot.characterBody.gameObject,"Jet");
             if(jetMachine){
              jetMachine.SetNextState(new IceRitualState());
              jetMachine.nextStateModifier += unJetLocal;
             }
             return null;
         };
         def.onUnassign = (GenericSkill slot) =>{
            var jetMachine = EntityStateMachine.FindByCustomName(slot.characterBody.gameObject,"Jet");
            if(jetMachine){
              jetMachine.SetNextState(new Idle());
              jetMachine.nextStateModifier -= unJetLocal;
            }
         };
         def.icon = Util.SpriteFromFile("HoarfrostIcon.png"); 
         def.baseRechargeInterval = 0f;
         def.activationStateMachineName = "Jet";
         def.activationState = new SerializableEntityStateType(typeof(IceRitualState));
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEntityState(typeof(IceRitualState),out _);
         ContentAddition.AddEntityState(typeof(FrozenTotemState),out _);

         void unJetLocal(EntityStateMachine esm,ref EntityState s){
             if(s.GetType() == typeof(Idle)){
                s = new IceRitualState();
             }
         }
     }

     public class IceRitualState : BaseState{
         public static List<CharacterBody> ownerInstances = new List<CharacterBody>();

         public override void OnEnter(){
            base.OnEnter();
            ownerInstances.Add(characterBody);
         }
         public override void OnExit(){
            base.OnExit();
            ownerInstances.Remove(characterBody);
         }
         public override InterruptPriority GetMinimumInterruptPriority(){
             return InterruptPriority.Death;
         }
     }

     public class FrozenTotemState : FrozenState{
         public BuffWard ward;
         public CharacterBody attackerBody;
         public float stopwatch = 0.5f;

         public override void OnEnter(){
             base.OnEnter();
             var obj = GameObject.Instantiate(RoR2.Items.IcicleBodyBehavior.icicleAuraPrefab,transform.position,Quaternion.identity);
             var auraCont = obj.GetComponent<RoR2.IcicleAuraController>();
             var particleArray = HG.ArrayUtils.Join(auraCont.auraParticles,auraCont.procParticles);
             Component.Destroy(auraCont);
             ward = obj.GetComponent<BuffWard>();
             ward.interval = 0.25f;
             if(NetworkServer.active){
                 ward.Networkradius = 8f;
                 NetworkServer.Spawn(obj);
             }
             ward.gameObject.transform.localScale = new Vector3(8f,8f,8f);
             foreach(var part in particleArray){
                 var pMain = part.main;
                 pMain.loop = true;
                 part.Play();
             }
             RoR2.Util.PlaySound("Play_item_proc_icicle",ward.gameObject);

         }
         public override void FixedUpdate(){
             base.FixedUpdate();
             stopwatch -= GetDeltaTime();
             if(NetworkServer.active && stopwatch <= 0){
                 new BlastAttack{
                     teamIndex = attackerBody.teamComponent.teamIndex,
                     attacker = attackerBody.gameObject,
                     inflictor = characterBody.gameObject,
                     radius = 8f,
                     position = transform.position,
                     procCoefficient = 0.2f,
                     attackerFiltering = AttackerFiltering.CustomFilter,
                     filterCallback = (res) => {return res.healthComponent != characterBody.healthComponent;},//return !res.healthComponent.isInFrozenState;},
                     baseDamage = ((attackerBody?.damage) ?? 1),
                     baseForce = 0f
                 }.Fire();
                 stopwatch = 0.5f;
             }
         }
         public override void Update(){
             base.Update();
         }
         public override void OnExit(){
             base.OnExit();
             if(ward){
                 GameObject.Destroy(ward.gameObject);
             }
         }
     }
    }
}
