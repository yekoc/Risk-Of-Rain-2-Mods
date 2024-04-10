using RoR2;
using RoR2.Skills;
using EntityStates;
using RoR2.CharacterAI;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using MonoMod.RuntimeDetour;

namespace PassiveAgression.Captain
{
    public static class RadiusPassive{
     public static AssignableSkillDef def;
     public static CustomPassiveSlot slot;
     public static bool isHooked = false;
     public static float[] supportPower = null;
     public static Hook hWardSetterHook,pallyZoneHook;
     public static ILHook pallySunHook;
     //public static GameObject effectPrefab;
     public static RoR2BepInExPack.Utilities.FixedConditionalWeakTable<GameObject,GameObject> effects = new();

     static RadiusPassive(){
         var efx = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/CaptainDefenseMatrix/captain defense drone.fbx");
         //PassiveAgression.Util.recursebull(efx.WaitForCompletion().transform.GetChild(0).GetChild(0));
         slot = new CustomPassiveSlot("RoR2/Base/Captain/CaptainBody.prefab");
         LanguageAPI.Add("PASSIVEAGRESSION_CAPTAINZONE","Supportive Microbots");
         LanguageAPI.Add("PASSIVEAGRESSION_CAPTAINZONE_DESC","Holdout Zones and other persistent areas of effect are 50% larger.");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_CAPTAINZONE";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_CAPTAINZONE_DESC";
         def.onAssign = (GenericSkill slot) => {
             var defmatrix = slot.characterBody.GetComponent<CaptainDefenseMatrixController>();
             GameObject.DestroyImmediate(defmatrix);
             if(defmatrix){
               GameObject.Destroy(defmatrix);
             }
             if(!isHooked){
                isHooked = true;
                if(supportPower == null){
                  supportPower = Enumerable.Repeat(1f,TeamCatalog.teamDefs.Length).ToArray();
                }
                Run.onRunDestroyGlobal += unsub;
                On.RoR2.HoldoutZoneController.Start += Holdout;
                On.RoR2.BuffWard.Start += Ward;
                On.RoR2.HelfireController.ServerFixedUpdate += Hellfire;
                On.RoR2.Projectile.ProjectileDotZone.Start += DotZone;
                On.RoR2.IcicleAuraController.UpdateRadius += Frost;
                On.RoR2.RadialForce.Awake += RadialForce;
                On.RoR2.GrandParentSunController.Start += TheSun;
                On.RoR2.InfiniteTowerWaveController.FixedUpdate += WaveController;
                if(PassiveAgressionPlugin.modCompat.Paladin){
                    PallyCompat(true);
                }
                hWardSetterHook = new Hook(typeof(HealingWard).GetProperty("Networkradius").GetSetMethod(),typeof(RadiusPassive).GetMethod("Ward2"));
                On.RoR2.HealingWard.Awake += Ward2Awake;
                On.EntityStates.CaptainSupplyDrop.HackingMainState.OnEnter += HackWard;
             }
             supportPower[(int)slot.characterBody.teamComponent.teamIndex] += 0.5f;
             slot.characterBody.master.onBodyDestroyed += (body) =>{
              supportPower[(int) body.teamComponent.teamIndex] = Mathf.Max(1,supportPower[(int)body.teamComponent.teamIndex] - 0.5f);
             };
             return null;
             void unsub(Run run){
                Run.onRunDestroyGlobal -= unsub;
                if(isHooked){
                 isHooked = false;
                 On.RoR2.HoldoutZoneController.Start -= Holdout;
                 On.RoR2.BuffWard.Start -= Ward;
                 On.RoR2.HelfireController.ServerFixedUpdate -= Hellfire;
                 On.RoR2.Projectile.ProjectileDotZone.Start -= DotZone;
                 On.RoR2.IcicleAuraController.UpdateRadius -= Frost;
                 On.RoR2.RadialForce.Awake -= RadialForce;
                 On.RoR2.GrandParentSunController.Start -= TheSun;
                 On.RoR2.InfiniteTowerWaveController.FixedUpdate -= WaveController;
                 if(PassiveAgressionPlugin.modCompat.Paladin){
                     PallyCompat(false);
                 }
                 hWardSetterHook.Free();
                 On.RoR2.HealingWard.Awake -= Ward2Awake;
                 On.EntityStates.CaptainSupplyDrop.HackingMainState.OnEnter -= HackWard;
                }
             }
         };
         def.onUnassign = (GenericSkill slot) =>{
             if(slot.characterBody){
              if(slot.characterBody.gameObject.AddComponent<CaptainDefenseMatrixController>()){;
              supportPower[(int)slot.characterBody.teamComponent.teamIndex] = Mathf.Max(1,supportPower[(int)slot.characterBody.teamComponent.teamIndex] - 0.5f);
             }}
         };
         def.activationStateMachineName = "Body";
         def.activationState = EntityStateMachine.FindByCustomName(slot.bodyPrefab,"Body").mainStateType;
         def.icon = PassiveAgressionPlugin.unfinishedIcon;
         def.baseRechargeInterval = 0f;
         def.canceledFromSprinting = false;
         def.cancelSprintingOnActivation = false;
         ContentAddition.AddSkillDef(def);
         //effectPrefab = PrefabAPI.InstantiateClone(efx.WaitForCompletion(),"PASSIVEAGRESSION_CAPTAINZONE_EFFECT");
     }

     public static void Holdout(On.RoR2.HoldoutZoneController.orig_Start orig,HoldoutZoneController self){
         orig(self);
         self.calcRadius += (ref float radius) =>{
            radius *= supportPower[(int)self.chargingTeam];
         };
     }
     public static void Ward(On.RoR2.BuffWard.orig_Start orig,BuffWard self){
         orig(self);
         float multiplier = 1f;
         if(self.invertTeamFilter ^ self.buffDef.isDebuff){
            multiplier += supportPower[(int)self.teamFilter.teamIndex] - 1f;
         }
         else{
            for(int i = 0;i < supportPower.Length;i++){
              multiplier += ((TeamIndex)i == self.teamFilter.teamIndex)? 0 : (supportPower[(int)self.teamFilter.teamIndex] -1f);
            }
         }
         self.Networkradius *= multiplier;
     }
     public static void Ward2(Action<HealingWard,float> orig,HealingWard self,float value){
         value *= supportPower[(int)self.teamFilter.teamIndex];
         orig(self,value);
     }
     public static void Ward2Awake(On.RoR2.HealingWard.orig_Awake orig,HealingWard self){
         orig(self);
         var co = Ward2Waiter(self);
         self.StartCoroutine(co);
     }
     public static void HackWard(On.EntityStates.CaptainSupplyDrop.HackingMainState.orig_OnEnter orig,EntityStates.CaptainSupplyDrop.HackingMainState self){
         orig(self);
         var mult = supportPower[(int)self.teamFilter.teamIndex];
         self.radius *= mult;
         self.sphereSearch.radius *= mult;
         self.GetModelTransform().Find("Indicator").Find("IndicatorRing").localScale = new Vector3(mult,mult,mult/10f);
     }
     public static System.Collections.IEnumerator Ward2Waiter(HealingWard ward){
         yield return new WaitForFixedUpdate();
         ward.Networkradius = ward.Networkradius;
     }
     public static void WaveController(On.RoR2.InfiniteTowerWaveController.orig_FixedUpdate orig,InfiniteTowerWaveController self){
         orig(self);
         if(NetworkServer.active && !self.isInSuddenDeath){
            self.Network_zoneRadiusPercentage = supportPower[(int)TeamIndex.Player];
         }
     }
     public static void Hellfire(On.RoR2.HelfireController.orig_ServerFixedUpdate orig,HelfireController self){
         orig(self);
         self.radius *= supportPower[(int)self.networkedBodyAttachment.attachedBody.teamComponent.teamIndex];
     }
     public static void DotZone(On.RoR2.Projectile.ProjectileDotZone.orig_Start orig,RoR2.Projectile.ProjectileDotZone self){
         orig(self);
         self.gameObject.transform.localScale *= supportPower[(int)self.attack.teamIndex];
     }
     public static void RadialForce(On.RoR2.RadialForce.orig_Awake orig,RadialForce self){
         orig(self);
         var proj = self.gameObject.GetComponent<RoR2.Projectile.ProjectileController>();
         if(proj){
          proj.onInitialized += (pro) =>{
           pro.gameObject.GetComponent<RadialForce>().radius *= supportPower[(int)pro.teamFilter.teamIndex];
          };
         }
         else if(self.teamFilter.teamIndex != TeamIndex.None){
          self.radius *= supportPower[(int)self.teamFilter.teamIndex];
         }
     }
     public static void Frost(On.RoR2.IcicleAuraController.orig_UpdateRadius orig,IcicleAuraController self){
         orig(self);
         if(self.cachedOwnerInfo.characterBody && self.cachedOwnerInfo.characterBody.teamComponent)
          self.actualRadius *= supportPower[(int)self.cachedOwnerInfo.characterBody.teamComponent.teamIndex];
     }
     public static void TheSun(On.RoR2.GrandParentSunController.orig_Start orig,GrandParentSunController self){
         orig(self);
         var body = self.ownership?.ownerObject?.GetComponent<CharacterBody>();
         if(body){
           self.maxDistance *= supportPower[(int)body.teamComponent.teamIndex];
           self.gameObject.transform.Find("VfxRoot/Mesh/AreaIndicator").localScale *= supportPower[(int)body.teamComponent.teamIndex];
         }
     }
     public class PallyComp{
     [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
     public static void TheSunBottled(ILContext il){
         ILCursor c = new ILCursor(il);
         if(c.TryGotoNext(MoveType.After,x => x.MatchLdsfld(typeof(PaladinMod.StaticValues).GetField("cruelSunAoE",(System.Reflection.BindingFlags)(-1))))){
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float,PaladinSunController,float>>((orig,self) =>{
                var power = supportPower[(int)self.ownerBody.teamComponent.teamIndex];
                self.gameObject.transform.Find("Mesh/AreaIndicator").localScale = new Vector3(power,power,power);
                return orig * power;
                });
         }
     }
     [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
     public static void ThePalaZone(Action<PaladinMod.Misc.PaladinHealZoneController> orig,PaladinMod.Misc.PaladinHealZoneController self){
         if(self.teamFilter.teamIndexInternal != -1){
          self.radius *= supportPower[(int)self.teamFilter.teamIndex];
         }
         orig(self);
     }
     }
     [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
     public static void PallyCompat(bool onoff){
        if(onoff){
           pallyZoneHook = new Hook(typeof(PaladinMod.Misc.PaladinHealZoneController).GetMethod("Start",(System.Reflection.BindingFlags)(-1)),typeof(RadiusPassive.PallyComp).GetMethod("ThePalaZone"));
           pallySunHook = new ILHook(typeof(PaladinSunController).GetMethod("SearchForTargets",(System.Reflection.BindingFlags)(-1)),(ILContext.Manipulator)PallyComp.TheSunBottled);
        }
        else{
           pallyZoneHook?.Free();
           pallySunHook?.Free();
        }
     }
    }
}
