using RoR2;
using EntityStates;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PassiveAgression{
 public static class Util{

    public static Xoroshiro128Plus miscRNG = new Xoroshiro128Plus(0);
    public static EntityStateIndex freezeState,stunState,shockState,idleState = (EntityStateIndex)(-1);
    public static BodyIndex mitchellIndex1,mitchellIndex2 = BodyIndex.None;

    static Util(){
      Run.onRunStartGlobal += (run) => {
          if(NetworkServer.active)
            miscRNG = new Xoroshiro128Plus(run.runRNG);
      };
    }
    public static void OnStateWorkFinished(EntityStateMachine machine,EntityStateMachine.ModifyNextStateDelegate del,List<Type> additionalStops = null){
        if(!machine) return;
        if(idleState == (EntityStateIndex)(-1)){
          freezeState = EntityStateCatalog.GetStateIndex(typeof(FrozenState));
          stunState = EntityStateCatalog.GetStateIndex(typeof(StunState));
          shockState = EntityStateCatalog.GetStateIndex(typeof(ShockState));
          idleState = EntityStateCatalog.GetStateIndex(typeof(Idle));
        }
        machine.nextStateModifier += logic;
        void logic(EntityStateMachine mach,ref EntityState state){
            Type type = state.GetType();
            EntityStateIndex stateIndex = EntityStateCatalog.GetStateIndex(type);
            if(machine.mainStateType.stateType == type || (additionalStops != null && additionalStops.Contains(type))  || stateIndex == freezeState || stateIndex == stunState || stateIndex == shockState || stateIndex == idleState){
                del(mach,ref state);
                mach.nextStateModifier -= logic;
            }
        }
    }
    public static bool BodyIsMitchell(CharacterBody body){
        if(!body) return false;
        if(mitchellIndex2 == BodyIndex.None){
          mitchellIndex1 = BodyCatalog.FindBodyIndex("BrotherBody");
          mitchellIndex2 = BodyCatalog.FindBodyIndex("BrotherHurtBody");
        }
        return (body.bodyIndex == mitchellIndex1) || (body.bodyIndex == mitchellIndex2);
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
    public static int CountUniqueItemWithTag(Inventory inventory,ItemTag tag){
        return ItemCatalog.GetItemsWithTag(tag).Intersect(inventory.itemAcquisitionOrder).Count();
    }

    public static Sprite SpriteFromFile(string name){
         var texture = new Texture2D(2,2,TextureFormat.RGBA32,mipChain: false);
         try{
         texture.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),"Assets/" + name)));
         }
         catch(System.IO.FileNotFoundException e){
            PassiveAgressionPlugin.Logger.LogError("Failed to read file at " + e.FileName);
            return null;
         }
         return Sprite.Create(texture,new Rect(0f,0f,texture.height,texture.width),new Vector2(0.5f,0.5f),100); 
    }
     public static void recursebull(Transform transform,int acc = 0){
        string log = "";
        for(int i = 0; i<acc;i++){
            log += "-";
        }
        Debug.Log(log + transform);
        foreach(var comp in transform.gameObject.GetComponents<Component>()){
            Debug.Log(log + " *" + comp.GetType());
        }
        for(int i = 0;i < transform.childCount;i++){
          recursebull(transform.GetChild(i),acc +1);
        }
     }
 }
}
