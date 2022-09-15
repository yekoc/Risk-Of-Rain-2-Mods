using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Linq;

namespace PassiveAgression{
 public static class Util{

    public static Xoroshiro128Plus miscRNG = new Xoroshiro128Plus(0);

    static Util(){
      Run.onRunStartGlobal += (run) => {
          miscRNG = new Xoroshiro128Plus(run.runRNG);
      };

    }
    public static bool GetRandomDebuffOrDot(CharacterBody body,out BuffIndex debuff,out DotController.DotStack dot){
        debuff = BuffIndex.None;
        dot = null;
        if(!NetworkServer.active || !body ){
            return false;
        }
        var list = new List<BuffIndex>();
        var dotlist = new List<DotController.DotStack>();
        if((body.activeBuffsList?.Length??0) > 0){
            list = body.activeBuffsList.Where((buffIndex) => BuffCatalog.GetBuffDef(buffIndex).isDebuff).ToList();
        }
        DotController dotc;
        if(DotController.dotControllerLocator.TryGetValue(body.gameObject.GetInstanceID(),out dotc) && dotc.dotStackList != null){
            dotlist = dotc.dotStackList;
        }
        foreach(var stack in dotlist){
            if(list.Count > 0 && stack.dotDef.associatedBuff){
              list.Remove(stack.dotDef.associatedBuff.buffIndex);
            }
        }
        if(list.Count + dotlist.Count <= 0){
            return false;
        }
        int index = miscRNG.RangeInt(0,list.Count + dotlist.Count);
        if(index < list.Count){
          debuff = list[index];
        }
        else{
          dot = dotlist[index - list.Count];
        }
        return true;
    }

 }
}
