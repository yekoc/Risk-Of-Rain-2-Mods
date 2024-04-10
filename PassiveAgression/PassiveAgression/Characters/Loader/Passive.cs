using RoR2;
using RoR2.Skills;
using EntityStates;
using EntityStates.Loader;
using RoR2.CharacterAI;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using MonoMod.RuntimeDetour;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;

namespace PassiveAgression.Loader{
    public static class LoaderPassiveZaHando{
     public static CustomPassiveSlot slot;
     public static AssignableSkillDef def;
     public static bool isHooked;

     static LoaderPassiveZaHando(){
         LanguageAPI.Add("PASSIVEAGRESSION_LOADERZAHANDO","Cavitation Generator");
         LanguageAPI.Add("PASSIVEAGRESSION_LOADERZAHANDO_DESC","The Loader is <style=cIsUtility>immune</style> to fall damage. Striking with the Loader's gauntlets <style=cIsUtility>pulls</style> enemies.");
         slot = new CustomPassiveSlot("RoR2/Base/Loader/LoaderBody.prefab");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_LOADERZAHANDO";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_LOADERZAHANDO_DESC";
         def.activationStateMachineName = "Body";
         def.onAssign = (GenericSkill slot) => {
             if(!isHooked){
                isHooked = true;
                IL.EntityStates.Loader.LoaderMeleeAttack.OnMeleeHitAuthority += DisableBarrier;
                //IL.EntityStates.BasicMeleeAttack.BeginMeleeAttackEffect += ZaHandoColor;
                On.EntityStates.BasicMeleeAttack.FixedUpdate += Cavitate;
                RoR2.Run.onRunDestroyGlobal += unhooker;
             }
             return null;
             void unhooker(Run run){
                if(isHooked){
                  isHooked = false;
                  IL.EntityStates.Loader.LoaderMeleeAttack.OnMeleeHitAuthority -= DisableBarrier;
                  On.EntityStates.BasicMeleeAttack.FixedUpdate -= Cavitate;
                }
                RoR2.Run.onRunDestroyGlobal -= unhooker;
             }
         };
         def.onUnassign = (GenericSkill slot) =>{
            
         };
         def.icon = Util.SpriteFromFile("MicroMissile.png"); 
         def.baseRechargeInterval = 0f;
         def.activationStateMachineName = "Body";
         def.activationState = new SerializableEntityStateType(typeof(GenericCharacterMain));
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         ContentAddition.AddSkillDef(def);
     }

     public static void Cavitate(On.EntityStates.BasicMeleeAttack.orig_FixedUpdate orig,BasicMeleeAttack self){
        orig(self);
        if(NetworkServer.active && def.IsAssigned(self.characterBody)){
	  if ((string.IsNullOrEmpty(self.mecanimHitboxActiveParameter) || self.animator.GetFloat(self.mecanimHitboxActiveParameter) > 0.5f || self.forceFire)){
            Transform hBox = self.hitBoxGroup.hitBoxes[0].transform;
            BullseyeSearch bSearch = new();
            bSearch.viewer = self.characterBody;
            bSearch.teamMaskFilter = TeamMask.GetEnemyTeams(self.teamComponent.teamIndex);
            bSearch.searchDirection = self.GetAimRay().direction;
            bSearch.searchOrigin = self.transform.position;
            bSearch.filterByLoS = true;
            bSearch.maxDistanceFilter = 50f;
            //bSearch.maxAngleFilter = Vector3.Angle(self.transform.position,(hBox.position + new Vector3(hBox.lossyScale.x * 0.5f,0,0)));
            bSearch.RefreshCandidates();
            foreach(HurtBox target in bSearch.GetResults()){
              Vector3 force = (hBox.position - target.transform.position);
              if(target?.healthComponent?.body?.characterMotor){
                target.healthComponent.body.characterMotor.ApplyForce(force,true);
              }
              else if(target?.healthComponent?.body?.rigidbody){
                target.healthComponent.body.rigidbody.AddForce(force,ForceMode.Impulse);
              }
            }
          }
        }
     }
     public static void ZaHandoColor(ILContext il){
         ILCursor c = new(il);
         if(c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(out _), x=> x.MatchStfld(typeof(BasicMeleeAttack).GetField("swingEffectInstance",(System.Reflection.BindingFlags)(-1))))){
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<BasicMeleeAttack>>((self) =>{
               if(def.IsAssigned(self.characterBody)){
                 self.swingEffectInstance.GetComponent<ParticleSystemRenderer>().material.SetColor("_Color",Color.green);
               }
            });
         }
     }
     public static void DisableBarrier(ILContext il){
         ILCursor c = new(il);
         ILLabel lab = c.DefineLabel();
         if(c.TryGotoNext(x => x.MatchRet())){
           lab = c.MarkLabel();
           if(c.TryGotoPrev(MoveType.After,x => x.MatchCallOrCallvirt(typeof(BasicMeleeAttack).GetMethod(nameof(BasicMeleeAttack.OnMeleeHitAuthority),(System.Reflection.BindingFlags)(-1))))){
              c.Emit(OpCodes.Ldarg_0);
              c.EmitDelegate<Func<LoaderMeleeAttack,bool>>((stat) => def.IsAssigned(stat.characterBody));
              c.Emit(OpCodes.Brtrue,lab);
           }
         }
     }



    }
}
