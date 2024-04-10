/*using UnityEngine;
using RoR2;
using RoR2.Projectile;
using RoR2.Orbs;
using RoR2BepInExPack.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PassiveAgression{
    public class OrbProjectileComponent : MonoBehaviour,IProjectileImpactBehavior{

        public GenericDamageOrb orb;

        public ProjectileSimple projeSimple;
        public IOrbFixedUpdateBehavior orbIsFixedUpdate;

        private void Start(){
            projeSimple = GetComponent<ProjectileSimple>();
            orbIsFixedUpdate = orb as IOrbFixedUpdateBehavior;
            projeSimple.velocity = orb.speed;
            transform.localScale = orb.scale;
        }

        public void OnProjectileImpact(ProjectileImpactInfo impactInfo){
            orb.target = impactInfo.collider.gameObject.GetComponent<HurtBox>();
            orb.OnArrival();
        }

        private void FixedUpdate(){
            orbIsFixedUpdate?.FixedUpdate();
        }

        private void OnDestroy(){
        }

    }
}*/
