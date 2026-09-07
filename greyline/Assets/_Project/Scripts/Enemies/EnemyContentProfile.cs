using System;
using UnityEngine;

namespace Greyline.Enemies
{
    [CreateAssetMenu(menuName="Greyline/Production/Enemy Profile")]
    public sealed class EnemyContentProfile : ScriptableObject
    {
        public string id;
        public EnemyArchetype archetype;
        public float health=80, damage=12, attackRange=2.6f;
        public float telegraph=.65f, active=.23f, recovery=.85f, cooldown=.8f;
        public float detectionRange=18, stopDistance=1.8f, moveSpeed=2.4f, turnSpeed=300, visualScale=1;
        public void Validate()
        {
            if(string.IsNullOrWhiteSpace(id) || !Enum.IsDefined(typeof(EnemyArchetype),archetype))
                throw new InvalidOperationException("Enemy profile requires a stable ID and supported archetype.");
            foreach(float value in new[]{health,damage,attackRange,telegraph,active,recovery,cooldown,detectionRange,stopDistance,moveSpeed,turnSpeed,visualScale})
                if(value<=0 || float.IsNaN(value) || float.IsInfinity(value))throw new InvalidOperationException("Invalid combat value on enemy profile: "+id);
            if(stopDistance>attackRange || visualScale>2)
                throw new InvalidOperationException("Enemy profile has unreachable melee spacing or unsupported visual scale: "+id);
        }
    }
}
