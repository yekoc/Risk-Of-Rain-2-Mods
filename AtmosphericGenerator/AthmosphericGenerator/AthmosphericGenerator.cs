using BepInEx;
using BepInEx.Configuration;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using System.Security;
using System.Security.Permissions;
using System.Collections.Generic;

#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace AtmosphericShields{

    [BepInPlugin("xyz.yekoc.AtmosphericShields", "Atmospheric Bubble Generator","1.0.0" )]
    public class AtmosphericShieldsPlugin : BaseUnityPlugin{
        public static GameObject shieldPrefab;

        public static List<FogDamageController> fog = new List<FogDamageController>();

	public void Awake(){
           shieldPrefab = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Engi/EngiBubbleShield.prefab").WaitForCompletion();
           var zone = shieldPrefab.AddComponent<SphereZone>();
           zone.radius = 10f;

           On.RoR2.FogDamageController.Start += (orig,self) =>{
              orig(self);
              fog.Add(self);
              self.gameObject.AddComponent<OnDestroyComp>();
           };
           
           On.EntityStates.Engi.EngiBubbleShield.Deployed.OnEnter += (orig,self) =>{
               orig(self);
               var zon = self.gameObject.GetComponent<SphereZone>();
               foreach(FogDamageController fdc in fog){
                 fdc.AddSafeZone(zon);
               }
           };
           On.EntityStates.Engi.EngiBubbleShield.Deployed.OnExit += (orig,self) =>{
              var zon = self.gameObject.GetComponent<SphereZone>();
              foreach(FogDamageController fdc in fog){
                fdc.RemoveSafeZone(zon);
              }
              orig(self);
           };
	}

        public class OnDestroyComp : MonoBehaviour{
           public void OnDestroy(){
             fog.Remove(gameObject.GetComponent<FogDamageController>());
           }
        }

    }
}
