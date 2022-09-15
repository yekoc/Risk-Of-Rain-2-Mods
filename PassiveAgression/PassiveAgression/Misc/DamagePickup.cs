using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace PassiveAgression{
public class DamagePickup : MonoBehaviour{

            [Tooltip("The base object to destroy when this pickup is consumed.")]
            public GameObject baseObject;

            [Tooltip("The team mask object which determines who can pick up this pack.")]
            public TeamMask teamFilter;

            public GameObject pickupEffect;

            public GameObject owner;

            public float damage;

            private bool alive = true;

            private void OnTriggerStay(Collider other)
            {
                    if (!NetworkServer.active || !alive || !teamFilter.HasTeam(TeamComponent.GetObjectTeam(other.gameObject)))
                    {
                            return;
                    }
                    CharacterBody component = other.GetComponent<CharacterBody>();
                    if ((bool)component)
                    {
                            HealthComponent healthComponent = component.healthComponent;
                            if ((bool)healthComponent)
                            {
                                    component.healthComponent.TakeDamage(new DamageInfo{
                                        damage = damage,
                                        attacker = owner,
                                        inflictor = gameObject,
                                        force = Vector3.zero,
                                        procCoefficient = 1f,
                                        position = base.transform.position
                                    });
                                    EffectManager.SpawnEffect(pickupEffect, new EffectData
                                    {
                                            origin = base.transform.position
                                    }, transmit: true);
                            }
                            Object.Destroy(baseObject);
                    }
            }

}
}
