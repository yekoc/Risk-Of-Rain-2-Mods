using BepInEx.Configuration;
using RoR2;
using RoR2.Skills;
using EntityStates;
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
using RiskOfRuinaMod.Modules.Components;

namespace PassiveAgression.ModCompat{
    public static class ArgaliaPassive{
     public static AssignableSkillDef def;
     public static CustomPassiveSlot slot;
     public static GameObject indicatorPrefab;
     public static GameObject blockPrefab;
     public static bool isHooked;
     internal static ILHook mistHookRecalc;

     static ArgaliaPassive(){
         indicatorPrefab = PrefabAPI.InstantiateClone(UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/NearbyDamageBonus/NearbyDamageBonusIndicator.prefab").WaitForCompletion(),"ArgaliaRangeIndicator");
         var mat = GameObject.Instantiate(UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Material>("RoR2/Base/NearbyDamageBonus/matNearbyDamageBonusRangeIndicator.mat").WaitForCompletion());
         mat.SetColor("_TintColor",Color.blue);
         indicatorPrefab.GetComponentInChildren<MeshRenderer>().material = mat;
         blockPrefab = PrefabAPI.InstantiateClone(UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Bear/BearProc.prefab").WaitForCompletion(),"ArgaliaBlockedEffect");
         blockPrefab.GetComponentInChildren<EffectComponent>().soundName = "Play_Defense_Guard";
         slot = new CustomPassiveSlot(RiskOfRuinaMod.Modules.Survivors.RedMist.redMistPrefab);
         LanguageAPI.Add("PASSIVEAGRESSION_RUINAARGALIA","Faint Reverberation");
         LanguageAPI.Add("PASSIVEAGRESSION_RUINAARGALIA_DESC","All <style=cIsDamage>Attack Speed</style> and <style=cIsUtility>Movement Speed</style> bonuses are converted to block chance for long range attacks");
         def = ScriptableObject.CreateInstance<AssignableSkillDef>();
         def.skillNameToken = "PASSIVEAGRESSION_RUINAARGALIA";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_RUINAARGALIA_DESC";
         def.activationStateMachineName = "Body";
         def.onAssign = (GenericSkill slot) => {
             if(!isHooked){
                isHooked = true;
                mistHookRecalc = new ILHook(typeof(RiskOfRuinaMod.RiskOfRuinaPlugin).GetMethod("CharacterBody_RecalculateStats",(System.Reflection.BindingFlags)(-1)),(ILContext.Manipulator)UnProwess);
                On.RoR2.HealthComponent.TakeDamage += ImmuneToRanged;
             }
             slot.characterBody.baseDamage *= 2f;
             slot.characterBody.master.onBodyStart += effectHandler;
             return null;
             void unhooker(Run run){
                if(isHooked){
                   mistHookRecalc.Free();
                   On.RoR2.HealthComponent.TakeDamage -= ImmuneToRanged;
                   isHooked = false;
                }
                RoR2.Run.onRunDestroyGlobal -= unhooker;
             }
             
         };
         def.onUnassign = (GenericSkill slot) =>{
            var effect = slot.characterBody.transform.Find("ArgaliaRangeIndicator");
            if(effect){
              GameObject.Destroy(effect.gameObject);
            }
            slot.characterBody.baseDamage /= 2f;
            slot.characterBody.master.onBodyStart -= effectHandler;
         };
         def.icon = Util.SpriteFromFile("ArgaliaPassiveIcon.png");

      /* 
         var icon = slot.bodyPrefab.GetComponent<SkillLocator>().passiveSkill.icon;
         //Icon isn't readable smh
         var rendertex = RenderTexture.GetTemporary(
                            icon.texture.width,
                            icon.texture.height,
                            0,
                            RenderTextureFormat.Default,
                            RenderTextureReadWrite.Linear
                         );
                         Graphics.Blit(icon.texture,rendertex);
                         RenderTexture prev = RenderTexture.active;
                         RenderTexture.active = rendertex;
                         Texture2D activeTex = new Texture2D(icon.texture.width,icon.texture.height,TextureFormat.RGBA32,mipChain:false);
                         activeTex.ReadPixels(new Rect(0,0,icon.texture.width,icon.texture.height),0,0);
                         activeTex.Apply();
                         RenderTexture.active = prev;
                         RenderTexture.ReleaseTemporary(rendertex);
         File.WriteAllBytes(System.IO.Path.Combine(new string[]{BepInEx.Paths.PluginPath,"PassiveAgression","Assets", "ArgaliaPassiveIcon.png"}),activeTex.EncodeToPNG());*/
         def.baseRechargeInterval = 0f;
         def.activationStateMachineName = "Body";
         def.cancelSprintingOnActivation = false;
         def.canceledFromSprinting = false;
         def.activationState = EntityStateMachine.FindByCustomName(slot.bodyPrefab,"Body").mainStateType;
         ContentAddition.AddSkillDef(def);
         ContentAddition.AddEffect(blockPrefab);

         void effectHandler(CharacterBody body){
                 GameObject.Instantiate(indicatorPrefab,body.transform).GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(body.gameObject); 
         }
     }

     static void ImmuneToRanged(On.RoR2.HealthComponent.orig_TakeDamage orig,HealthComponent self,DamageInfo damage){
         if(damage.attacker && (damage.attacker.transform.position - self.transform.position).sqrMagnitude > 169f && def.IsAssigned(self.body)){
            var statTracker = self.body.GetComponent<RedMistStatTracker>();
            var egoTracker = self.body.GetComponent<RedMistEmotionComponent>();
            var chance = (statTracker.totalAttackSpeed - self.body.baseAttackSpeed)/self.body.baseAttackSpeed*10 + (statTracker.totalMoveSpeed - self.body.baseMoveSpeed)/self.body.baseMoveSpeed*10;
            if(egoTracker && egoTracker.inEGO){
                chance *= 3;
            }
            if(RoR2.Util.CheckRoll(chance,0f,self.body.master)){
              egoTracker?.AddEmotion(Mathf.Clamp(damage.damage/self.body.damage,0f,4f) * (1 + Run.instance.stageClearCount / (Run.instance.stageClearCount +1)));
              damage.damage = 0f;
              damage.rejected = true;
              EffectManager.SpawnEffect(blockPrefab,new EffectData{
                origin = damage.position,
                rotation = RoR2.Util.QuaternionSafeLookRotation(damage.force != Vector3.zero ? damage.force : UnityEngine.Random.onUnitSphere )
              },true);
            }
         }
         orig(self,damage);
     }

     static void UnProwess(ILContext il){
        ILCursor c = new ILCursor(il);
        if(c.TryGotoNext(MoveType.After,x => x.MatchCallOrCallvirt(typeof(Component).GetMethod("GetComponent",new Type[]{}).MakeGenericMethod(typeof(RedMistStatTracker))))){ // && c.TryGotoNext(MoveType.AfterLabel,x => x.MatchRet())){
          c.Emit(OpCodes.Ldarg_2);
          c.EmitDelegate<Func<RedMistStatTracker,CharacterBody,RedMistStatTracker>>((stat,bod) => {
            if(stat){
              stat.totalMoveSpeed = bod.moveSpeed;
              stat.totalAttackSpeed = bod.attackSpeed;
            }
            return stat;
          });
          c.GotoNext(MoveType.After,x => x.MatchLdloc(6));
          c.Emit(OpCodes.Ldarg_2);
          c.EmitDelegate<Func<float,CharacterBody,float>>((bonus,self) => (def.IsAssigned(self) ? 0f : bonus));
        }
     }
    }
}
