using BepInEx;
using BepInEx.Logging;
using BepInEx.Bootstrap;
using RoR2;
using RoR2.Stats;
using RoR2.UI;
using System;
using System.Security;
using System.Security.Permissions;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.CompilerServices;
using System.Linq;
using Mono.Cecil.Cil;
using MonoMod.Cil;


#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace CompactSave
{
    [BepInPlugin("xyz.yekoc.CompactSave", "CompactSave","1.0.0" )]
    public class CompactSavePlugin : BaseUnityPlugin
    {
        void Awake(){
          IL.RoR2.XmlUtility.CreateStatsField += (il) =>{
            ILCursor c = new ILCursor(il);
            var loopLabel = c.DefineLabel();
            if(c.TryGotoNext(x => x.MatchBr(out loopLabel),x => x.MatchLdstr("stat"))){
              c.GotoLabel(loopLabel);
              c.Index -= 4;
              c.MarkLabel(loopLabel);
              c.GotoPrev(x => x.MatchLdstr("stat"));
              c.MoveAfterLabels();
              c.Emit(OpCodes.Ldloc_2);
              c.Emit(OpCodes.Ldarg_1);
              c.EmitDelegate<Func<int,StatSheet,bool>>((index,sheet) => {
                Debug.Log(sheet.fields[index]);
                return sheet.fields[index].ToString() != "0";
              });
              c.Emit(OpCodes.Brfalse,loopLabel);
            }
            else{
              Logger.LogError("CreateStatsField Hook Failed,mod will have no effect.");
            }
          };
        }
    }
}
