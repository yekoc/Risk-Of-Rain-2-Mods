using RoR2;
using EntityStates;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using ExtraSkillSlots;
using System.Runtime.CompilerServices;
using static RoR2.CharacterAI.BaseAI;
using static RoR2.PlayerCharacterMasterController;

namespace PassiveAgression
{
    [RequireComponent(typeof(CharacterMaster))]
    [RequireComponent(typeof(MinionOwnership))]
    public class DoppelInputBank : MonoBehaviour
    {
        internal static bool ExtraSSInputs = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.KingEnderBrine.ExtraSkillSlots");
        internal static bool EmoteAPIInputs = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.weliveinasociety.CustomEmotesAPI");
        internal Queue<bool[]> ExtraInputBuffer = ExtraSSInputs ? new Queue<bool[]>() : null;
        internal List<EmoteQueueItem> EmoteInputBuffer = EmoteAPIInputs ? new() : null;
        public Queue<BodyInputs> inputBuffer = new Queue<BodyInputs>(5);
        public CharacterMaster master {get; private set;}
        public InputBankTest bodyInputs;
        public InputBankTest ownerInputs;
        public NetworkIdentity networkIdentity { get; protected set; }
        public ushort updateDelay = 0;
        internal class EmoteQueueItem{
           internal uint command;
           internal ushort delay;
           internal string emoteName;
        }

        public void Awake(){
            master = GetComponent<CharacterMaster>(); 
        }
        public void Start(){
            bodyInputs = master.GetBody().inputBank;
            ownerInputs = master.minionOwnership.ownerMaster.GetBody().inputBank;
            if(EmoteAPIInputs)
              HandleEmoteInputs(true);
        }
        public void FixedUpdate(){
           if(ownerInputs) 
            inputBuffer.Enqueue(new BodyInputs{
                    desiredAimDirection = ownerInputs.aimDirection,
                    moveVector = ownerInputs.moveVector,
                    pressActivateEquipment = ownerInputs.activateEquipment.down,
                    pressJump = ownerInputs.jump.down,
                    pressSkill1 = ownerInputs.skill1.down,
                    pressSkill2 = ownerInputs.skill2.down,
                    pressSkill3 = ownerInputs.skill3.down,
                    pressSkill4 = ownerInputs.skill4.down,
                    pressSprint = ownerInputs.sprint.down
            });
            if(ExtraSSInputs)
              HandleExtraSlots(true);
            if(master.hasEffectiveAuthority && bodyInputs && inputBuffer.Count >= updateDelay){
                BodyInputs current = inputBuffer.Dequeue();
		bodyInputs.skill1.PushState(current.pressSkill1);
		bodyInputs.skill2.PushState(current.pressSkill2);
		bodyInputs.skill3.PushState(current.pressSkill3);
		bodyInputs.skill4.PushState(current.pressSkill4);
		bodyInputs.jump.PushState(current.pressJump);
		bodyInputs.sprint.PushState(current.pressSprint);
		bodyInputs.activateEquipment.PushState(current.pressActivateEquipment);
		bodyInputs.moveVector = current.moveVector;
                bodyInputs.aimDirection = current.desiredAimDirection;                
                if(ExtraSSInputs)
                    HandleExtraSlots(false);
                if(EmoteAPIInputs)
                    HandleEmoteInputs();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal void HandleExtraSlots(bool record){
            if(record){
                var extraInput = master.minionOwnership.ownerMaster.GetComponent<ExtraInputBankTest>();
                if(extraInput){
                    ExtraInputBuffer.Enqueue(new bool[4]{
                      extraInput.extraSkill1.down,
                      extraInput.extraSkill2.down,
                      extraInput.extraSkill3.down,
                      extraInput.extraSkill4.down
                    });
                }
            }
            else{ 
              var extraInput = master.GetComponent<ExtraInputBankTest>();
              if(extraInput && master.hasEffectiveAuthority && ExtraInputBuffer.Count >= updateDelay){
                bool[] current = ExtraInputBuffer.Dequeue();
		extraInput.extraSkill1.PushState(current[0]);
		extraInput.extraSkill2.PushState(current[1]);
		extraInput.extraSkill3.PushState(current[2]);
		extraInput.extraSkill4.PushState(current[3]);
              }
            }
        }

       [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal void HandleEmoteInputs(bool setup = false){
            var selfB = master?.GetBody()?.modelLocator?.modelTransform.GetComponentInChildren<BoneMapper>();
            if(!selfB){
              EmotesAPI.CustomEmotesAPI.animChanged -= animChanged;
              return;
            }
            if(setup){
              EmotesAPI.CustomEmotesAPI.animChanged += animChanged;
            }
            else{
              foreach(var emote in EmoteInputBuffer.ToList()){
                 emote.delay--;
                 if(emote.delay <= 0){
                   switch (emote.command){
                       case 0:
                          EmotesAPI.CustomEmotesAPI.PlayAnimation(emote.emoteName,selfB);
                          break;
                       case 2:
                          if(selfB.currentEmoteSpot?.GetComponent<EmoteLocation>()?.emoter){
                            var spots = selfB.currentEmoteSpot.transform.parent.GetComponentsInChildren<EmoteLocation>();
                            for(int i = 0; i < spots.Length;i++){
                                 var eloc = spots[i];
                                 if(!eloc.emoter){
                                    selfB.currentEmoteSpot = eloc.gameObject;
                                    break;
                                 }
                            }
                          }
                          selfB.JoinEmoteSpot();
                          break;
                       default:
                          break;
                   }
                   EmoteInputBuffer.Remove(emote);
                 }
              }
            }

          void animChanged(string a,BoneMapper b){
                if(b.mapperBody.inputBank == ownerInputs){
                  if(a != "none" && b.currentClip.joinSpots != null && b.currentClip.joinSpots.Length > 0){
                     EmotesAPI.CustomEmotesAPI.ReserveJoinSpot(b.emoteLocations.First().gameObject,selfB);
                     EmoteInputBuffer.Add(new EmoteQueueItem{command = 2,delay = Math.Max(updateDelay,(ushort)1),emoteName = a});
                     return;
                  }
                  EmoteInputBuffer.Add(new EmoteQueueItem{command = 0,delay = Math.Max(updateDelay,(ushort)1),emoteName = a});
                }
          }
        }

    }
}

