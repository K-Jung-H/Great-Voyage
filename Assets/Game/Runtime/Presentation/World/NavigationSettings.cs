using GreatVoyage.Domain;
using UnityEngine;

namespace GreatVoyage.Presentation
{
    [CreateAssetMenu(menuName = "Great Voyage/Navigation Settings")]
    public sealed class NavigationSettings : ScriptableObject
    {
        [SerializeField, Min(.00001f), Tooltip("Nautical miles per game second; .002 = 7.2 knots")]
        private float cruiseSpeed = .002f;
        [SerializeField, Min(.00001f)] private float turnDegreesPerGameSecond = .2f;
        public NavigationTuning CreateTuning() => new NavigationTuning(cruiseSpeed, turnDegreesPerGameSecond);
    }
}
