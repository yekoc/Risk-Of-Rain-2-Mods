using UnityEngine;
using RoR2;
using RoR2BepInExPack.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PassiveAgression{
    public class SpeenComponent : MonoBehaviour{

        public static FixedConditionalWeakTable<Transform,List<SpeenComponent>> instances = new();
        public Transform radiusOrigin;
        public float turnAnimTimer;
        public float radius = -1f;
        public CharacterBody targetBody;

        private void Start(){
            if(radiusOrigin && !targetBody && radius == (-1f)){
             targetBody = radiusOrigin.gameObject.GetComponent<CharacterBody>();
            }
            if(targetBody){
             radius = targetBody.radius + 0.25f;
             radiusOrigin = targetBody.coreTransform;
            }
            instances.GetOrCreateValue(radiusOrigin).Add(this);
        }

        private void Update(){
            if(!radiusOrigin){
               Destroy(this);
               return;
            }
            var origin = radiusOrigin.position;
            turnAnimTimer += Time.deltaTime;
            var list = instances.GetOrCreateValue(radiusOrigin);
            var theta = Mathf.PI * 2 / list.Count;
            var angle = theta * list.IndexOf(this) + turnAnimTimer;
            transform.position = new Vector3((float)(radius * Math.Cos(angle) + origin.x), origin.y, (float)(radius * Math.Sin(angle) + origin.z));
        }

        private void OnDestroy(){
            if(radiusOrigin){
               instances.GetOrCreateValue(radiusOrigin).Remove(this);
            }
        }

    }
}
