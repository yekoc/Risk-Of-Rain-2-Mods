using BepInEx;
using BepInEx.Configuration;
using RoR2;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System;
using System.Security;
using System.Security.Permissions;
using static RoR2.DotController;

#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace OldBurn
{
    [BepInPlugin("xyz.yekoc.YeOldeBurn", "Ye Olde Burn","1.0.0" )]
    public class OldBurnPlugin : BaseUnityPlugin
    {
	
	private void Awake(){
	  IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>{
	    ILCursor c = new ILCursor(il);
	    int InflictLoc = -1;
	    c.GotoNext(x => x.MatchStfld(typeof(InflictDotInfo).GetField("totalDamage")));
	    c.GotoNext(MoveType.After,x => x.MatchStloc(out InflictLoc));
	    c.Emit(OpCodes.Ldloc,InflictLoc);
	    c.Emit(OpCodes.Ldarg,1);
	    c.EmitDelegate<Action<InflictDotInfo,DamageInfo>>((dot,damage) => {
	      dot.totalDamage = null;
	      dot.duration = damage.procCoefficient * 4f;
	    });
	  };
	  IL.RoR2.GlobalEventManager.ProcIgniteOnKill += (il) =>{
	    ILCursor c = new ILCursor(il);
	    int InflictLoc = -1;
	    c.GotoNext(x => x.MatchStfld(typeof(InflictDotInfo).GetField("totalDamage")));
	    c.GotoNext(MoveType.After,x => x.MatchStloc(out InflictLoc));
	    c.Emit(OpCodes.Ldloc,InflictLoc);
	    c.Emit(OpCodes.Ldarg,1);
	    c.EmitDelegate<Action<InflictDotInfo,int>>((dot,gasoline) => {
	      dot.totalDamage = null;
	      dot.duration = 0.75f + 0.75f*gasoline;
	    });
	  };
	  IL.RoR2.GrandParentSunController.ServerFixedUpdate += (il) =>{
	    ILCursor c = new ILCursor(il);
	    int InflictLoc = -1;
	    int num4loc = -1;
	    c.GotoNext(x => x.MatchStfld(typeof(InflictDotInfo).GetField("totalDamage")));
	    c.GotoPrev(x => x.MatchLdloca(out InflictLoc));
	    c.GotoPrev(x => x.MatchLdloc(out num4loc),x => x.MatchLdcI4(0),x => x.MatchBle(out _));
	    c.GotoNext(MoveType.After,x=> x.MatchStfld(typeof(InflictDotInfo).GetField("totalDamage")));
	    if(InflictLoc != -1 && num4loc != -1){
	     c.Emit(OpCodes.Ldloc,InflictLoc);
	     c.Emit(OpCodes.Ldarg,0);
	     c.Emit(OpCodes.Ldloc,num4loc);
	     c.EmitDelegate<Action<InflictDotInfo,GrandParentSunController,int>>((dot,self,num4) => {
	       dot.totalDamage = null;
	       dot.duration = self.burnDuration;
	       dot.damageMultiplier = num4;
	     });
	    }
	  };
	}
	private void OnDestroy(){

	}
    }
}
