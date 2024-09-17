using RoR2;
using RoR2.Skills;
using RoR2.CameraModes;
using EntityStates;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using static RoR2.CameraModes.CameraModeBase;

namespace PassiveAgression.Treebot{
    public static class SprintClimbPassive{
     public static AssignableSkillDef def;
     public static CustomPassiveSlot slot;
     public static bool isHooked = false;
     public static Quaternion rotationOffs;
     public static float dampVelocity;


     internal static void motorHook(ILContext il){
         ILCursor c = new ILCursor(il);
         var isFlying = typeof(CharacterMotor).GetProperty("isFlying").GetGetMethod();
         while(c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(isFlying))){
             
         c.Emit(OpCodes.Ldarg_0);
         c.EmitDelegate<Func<CharacterMotor,bool>>((motor) => motor.isGrounded  /*&& motor.body.isSprinting */  && (Vector3.Angle(Vector3.up,motor.estimatedGroundNormal) >= motor.slopeLimit) && def.IsAssigned(motor.body));
             c.Emit(OpCodes.And);
         }
         c.Index = 0;
         if(c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(typeof(CharacterMotor).GetProperty("useGravity").GetGetMethod()))){
             PassiveAgressionPlugin.Logger.LogError("HOOOOOOK");
             c.Emit(OpCodes.Ldarg_0);
             c.Emit(OpCodes.Ldarg_1);
             c.EmitDelegate<Func<bool,CharacterMotor,float,bool>>((grav,motor,time) => {
                if(motor.isGrounded /*&& motor.body.isSprinting */ /*&& (Vector3.Angle(Vector3.up,motor.estimatedGroundNormal) >= motor.slopeLimit)*/ && def.IsAssigned(motor.body)){
                  motor.velocity -= motor.Motor.GroundingStatus.GroundNormal * time;
                  motor.velocity.y = Mathf.Max(motor.velocity.y,0);
                  return false;
                }
                return grav;
             });
         }
     } 
     internal static void motorRotate(On.RoR2.CharacterMotor.orig_UpdateRotation orig,CharacterMotor self,ref Quaternion curRot,float dTime){
        if(/*self.body.isSprinting && */ def.IsAssigned(self.body)){
            var upwardsDir = curRot * Vector3.up;
            if(self.isGrounded){
              curRot *= Quaternion.FromToRotation(upwardsDir,Vector3.Slerp(self.Motor.CharacterUp,self.estimatedGroundNormal,1f - Mathf.Exp(-10f * dTime)));
              self.Motor.SetTransientPosition(self.Motor.TransientPosition + (upwardsDir * self.Motor.Capsule.radius) + (curRot * Vector3.down * self.Motor.Capsule.radius));
              return;
            }
            curRot *= Quaternion.FromToRotation(upwardsDir,Vector3.Slerp(upwardsDir,Vector3.up,1f - Mathf.Exp(-10f * dTime)));
            return;
         }
        orig(self,ref curRot,dTime);
     } 
     internal static void controllerHook(ILContext il){
         ILCursor c = new ILCursor(il);
          
         if(c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(typeof(Quaternion).GetMethod("op_Multiply",new Type[]{typeof(Quaternion),typeof(Vector3)})),x => x.MatchStloc(0))){
             c.MoveBeforeLabels();
             c.Emit(OpCodes.Ldarg_0);
             c.Emit(OpCodes.Ldloc_0);
             c.Emit(OpCodes.Ldloc,6);
             c.Emit(OpCodes.Ldloc,4);
             c.EmitDelegate<Func<PlayerCharacterMasterController,Vector3,Vector3,CameraRigController,Vector3>>((cont,moveVector,inputVector,cameraRigController) => {
                 var motor = cont.bodyMotor;
                 if(motor.isGrounded  && def.IsAssigned(cont.body)){
                  PitchYawPair py = (cameraRigController.cameraMode.camToRawInstanceData[cameraRigController] as CameraModePlayerBasic.InstanceData).pitchYaw; 
                  moveVector =  rotationOffs * Quaternion.Euler(py.pitch, py.yaw, 0f) * new Vector3(inputVector.x, 0f, inputVector.y);
                 }
                return moveVector;
             });
             c.Emit(OpCodes.Stloc_0);

         }
     }
     internal static void playerBasicHook(ILContext il){
         ILCursor c = new ILCursor(il);
          
         if(c.TryGotoNext(MoveType.Before,x => x.MatchStfld(typeof(CameraState).GetField("rotation")))){
             c.Emit(OpCodes.Ldarg_1);
             c.Emit(OpCodes.Ldarg_2);
             c.Emit(OpCodes.Ldflda,typeof(CameraModeContext).GetField("targetInfo"));
             c.Emit(OpCodes.Ldfld,typeof(TargetInfo).GetField("body"));
             c.EmitDelegate<Func<Quaternion,CameraModePlayerBasic.InstanceData,CharacterBody,Quaternion>>((orig,self,body) => {
                 if(body && def && def.IsAssigned(body)){
                  var motor = body.characterMotor;
                  Vector3 toDir = Vector3.up;
                  if( motor.isGrounded  /*&& motor.body.isSprinting */ /* && (Vector3.Angle(Vector3.up,motor.estimatedGroundNormal) >= motor.slopeLimit)*/){
                    toDir = Vector3.Slerp(motor.Motor.CharacterUp,motor.Motor.GroundingStatus.GroundNormal, 1f - Mathf.Exp(-10f * Time.deltaTime));
                  }
                  rotationOffs =  Quaternion.Euler(
                     Mathf.SmoothDampAngle(rotationOffs.x, (Quaternion.FromToRotation(rotationOffs * Vector3.up, toDir) * rotationOffs).x,ref dampVelocity,0.75f, 160f,Time.deltaTime),
                     Mathf.SmoothDampAngle(rotationOffs.y, (Quaternion.FromToRotation(rotationOffs * Vector3.up, toDir) * rotationOffs).y,ref dampVelocity,0.75f, 160f,Time.deltaTime),
                     Mathf.SmoothDampAngle(rotationOffs.z, (Quaternion.FromToRotation(rotationOffs * Vector3.up, toDir) * rotationOffs).z,ref dampVelocity,0.75f, 160f,Time.deltaTime)
                     );
                  return rotationOffs * Quaternion.Euler(self.pitchYaw.pitch,self.pitchYaw.yaw,0);
                 }
                 return orig;
             });
         }
     }

     static SprintClimbPassive(){
         slot = new CustomPassiveSlot("RoR2/Base/Treebot/TreebotBody.prefab",true);
         LanguageAPI.Add("PASSIVEAGRESSION_REXSPRINT","Grasping Vines");
         LanguageAPI.Add("PASSIVEAGRESSION_REXSPRINT_DESC","Hang onto surfaces of <style=cIsUtility>any angle</style> white Sprinting.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_REXSPRINT";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_REXSPRINT_DESC";
         def.onAssign = (GenericSkill slot) => {
             if(!isHooked){
                isHooked = true;
                IL.RoR2.CharacterMotor.PreMove += motorHook;
                //IL.RoR2.PlayerCharacterMasterController.Update += controllerHook;
                IL.RoR2.CameraModes.CameraModePlayerBasic.UpdateInternal += playerBasicHook;
                //On.RoR2.CharacterMotor.UpdateRotation += motorRotate;
                Run.onRunDestroyGlobal += unsub;
             }
            slot.characterBody.characterMotor.slopeLimit = 89f;
             rotationOffs = Quaternion.FromToRotation(Vector3.up,slot.characterBody.characterMotor.estimatedGroundNormal);
             return null;

             void unsub(Run run){
                 if(isHooked){
                  IL.RoR2.CharacterMotor.PreMove -= motorHook;
                  //IL.RoR2.PlayerCharacterMasterController.Update -= controllerHook;
                  IL.RoR2.CameraModes.CameraModePlayerBasic.UpdateInternal -= playerBasicHook;
                  //On.RoR2.CharacterMotor.UpdateRotation -= motorRotate;
                  isHooked = false;
                  Run.onRunDestroyGlobal -= unsub;
                 }
             }
         };
         def.icon = slot.bodyPrefab.GetComponent<SkillLocator>().special.skillFamily.variants[1].skillDef.icon;
         def.baseRechargeInterval = 0f;
         def.activationState = EntityStateMachine.FindByCustomName(slot.bodyPrefab,"Body").mainStateType;
         def.activationStateMachineName = "Body";
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         ContentAddition.AddSkillDef(def);
     }
    }
}
