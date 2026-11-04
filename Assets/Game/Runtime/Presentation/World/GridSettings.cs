using UnityEngine;

namespace GreatVoyage.Presentation
{
    [CreateAssetMenu(menuName = "Great Voyage/Global Grid Settings")]
    public sealed class GridSettings : ScriptableObject
    {
        [SerializeField, Min(.01f)] private float cellWorldSize = 1;
        public float CellWorldSize => cellWorldSize;
    }
}
