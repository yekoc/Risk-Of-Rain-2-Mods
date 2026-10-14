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
    [BepInPlugin("xyz.yekoc.YeOldeBurn", "Ye Olde Burn","1.0.2" )]
    public class OldBurnPlugin : BaseUnityPlugin
    {
	
	private void Awake(){
	  IL.RoR2.GlobalEventManager.ProcessHitEnemy += (il) =>{
	    ILCursor c = new ILCursor(il);
	    int InflictLoc = -1;
	    c.GotoNext(x => x.MatchStfld(typeof(InflictDotInfo).GetField("totalDamage")));
	    c.GotoNext(MoveType.After,x => x.MatchStloc(out InflictLoc));
	    c.Emit(OpCodes.Ldloca,InflictLoc);
	    c.Emit(OpCodes.Ldarg,1);
	    c.Emit(OpCodes.Call,typeof(OldBurnPlugin).GetMethod(nameof(ProcessHitEnemy),(System.Reflection.BindingFlags)(-1)));
	  };
	  IL.RoR2.GlobalEventManager.ProcIgniteOnKill += (il) =>{
	    ILCursor c = new ILCursor(il);
	    int InflictLoc = -1;
	    c.GotoNext(x => x.MatchStfld(typeof(InflictDotInfo).GetField("totalDamage")));
	    c.GotoNext(MoveType.After,x => x.MatchStloc(out InflictLoc));
	    c.Emit(OpCodes.Ldloca,InflictLoc);
	    c.Emit(OpCodes.Ldarg,1);
	    c.Emit(OpCodes.Call,typeof(OldBurnPlugin).GetMethod(nameof(ProcIgniteOnKill),(System.Reflection.BindingFlags)(-1)));
	  };
          IL.RoR2.OverheatSystem.AddOverheatServer += (il) =>{
            ILCursor c = new ILCursor(il);
	    int InflictLoc = -1;
	    int numloc = -1;
            c.GotoNext(x => x.MatchStfld(typeof(InflictDotInfo).GetField("totalDamage")));
	    c.GotoPrev(x => x.MatchLdloca(out InflictLoc));
	    c.GotoPrev(x => x.MatchLdloc(out numloc),x => x.MatchLdcI4(0),x => x.MatchBle(out _));
	    c.GotoNext(MoveType.After,x => x.MatchStfld(typeof(InflictDotInfo).GetField("totalDamage")));
            c.Emit(OpCodes.Ldloca,InflictLoc);
            c.Emit(OpCodes.Ldloc,numloc);
            c.Emit(OpCodes.Conv_R4);
            c.Emit(OpCodes.Ldarg,0);
            c.Emit(OpCodes.Call,typeof(OldBurnPlugin).GetMethod(nameof(AddOverheatServer),(System.Reflection.BindingFlags)(-1)));
          };
	 /* IL.RoR2.GrandParentSunController.ServerFixedUpdate += (il) =>{
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
	       dot.duration = 1f;//self.burnDuration;
	       dot.damageMultiplier = num4;
	     });
	    }
	  }; //They moved this */
	}

        private static void ProcessHitEnemy(ref InflictDotInfo dot, DamageInfo damage){
            dot.totalDamage = null;
            dot.duration = damage.procCoefficient * 4f;
        }
        private static void ProcIgniteOnKill(ref InflictDotInfo dot, int ignitecount){
            dot.totalDamage = null;
            dot.duration = 1.5f * (1+ignitecount);
        }
        private static void AddOverheatServer(ref InflictDotInfo dot, float heatCount,in OverheatSystem.OverheatInfo heat){
            dot.totalDamage = null;
            dot.duration = heat.burnTotalDamageScale;
            dot.damageMultiplier = heatCount;
        }
    }
}
