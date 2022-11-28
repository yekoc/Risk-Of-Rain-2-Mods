using RoR2;
using RoR2.Skills;
using RoR2.Projectile;
using EntityStates;
using EntityStates.Croco;
using UnityEngine;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;

namespace PassiveAgression.Croc
{
    public static class PathogenSpecialScepter{
     public static AssignableSkillDef def;

     static PathogenSpecialScepter(){
         
         LanguageAPI.Add("PASSIVEAGRESSION_CROCSPREAD_SCEPTER","Virulent Carrier Pathogen"); 
         def = ScriptableObject.Instantiate(PathogenSpecial.def);
         def.skillNameToken = "PASSIVEAGRESSION_CROCSPREAD_SCEPTER";
         (def as ScriptableObject).name = def.skillNameToken;
         def.skillDescriptionToken = "PASSIVEAGRESSION_CROCSPREAD_SCEPTERDESC";
         LoadoutAPI.AddSkillDef(def);
         PathogenSpecial.scepterDef = def;
         LanguageAPI.Add("PASSIVEAGRESSION_CROCSPREAD_SCEPTERDESC",Language.GetString("PASSIVEAGRESSION_CROCSPREAD_DESC") + "\n<color=#d299ff>SCEPTER:Every debuff on the target spawns a new strain.</color>");
     }


    } 
}
