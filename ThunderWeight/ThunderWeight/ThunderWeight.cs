using BepInEx;
using BepInEx.Configuration;
using RoR2;
using EntityStates;
using EntityStates.Loader;
using System.Security;
using System.Security.Permissions;
using AncientScepter;

#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace ThunderWeightMod
{
    [BepInPlugin("xyz.yekoc.ThunderWeight", "ThunderWeight","1.1.0" )]
    public class ThunderWeightPlugin : BaseUnityPlugin
    {
	public static ConfigEntry<float> speedtorad {get; set;}
	public static ConfigEntry<float> properRadius {get; set;}
        private void Awake()
        {
	  speedtorad = Config.Bind("Configuration","Speed to Range Multipler",0.15f,"Determines how much of your speed actually goes into expanding the ThunderDome,default:0.15");
	  properRadius = Config.Bind("Configuration","Base Range",10f,"The unmodified radius of the ThunderDome,if you change this make sure to modify Speed to Range accordingly,vanilla:10.0");
	  On.EntityStates.Loader.GroundSlam.FixedUpdate += MomentizeIons;
	  On.EntityStates.Loader.GroundSlam.OnExit += (orig,self) =>{
   	    GroundSlam.blastRadius = properRadius.Value;
	    orig(self);
	  };
	}
	
        private void MomentizeIons(On.EntityStates.Loader.GroundSlam.orig_FixedUpdate orig,GroundSlam self){
         if(self.isAuthority && self.characterMotor){   
	  float modifier = properRadius.Value + (self.characterMotor.velocity.y * -1f * speedtorad.Value);
	  if(modifier >= properRadius.Value + (speedtorad.Value * 25f)){
	    GroundSlam.blastRadius = modifier;
	  }
	  orig(self);
	 }
	}
    }
}
