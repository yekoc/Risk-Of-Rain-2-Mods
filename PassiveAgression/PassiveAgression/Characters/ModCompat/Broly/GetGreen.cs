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

namespace PassiveAgression.ModCompat
{
    public static class BrolyGetGreen{
     public static bool isHooked = false;


     static BrolyGetGreen(){
     }

     public class GetGreenState : EntityStates.BaseState {

     }
    }
}
