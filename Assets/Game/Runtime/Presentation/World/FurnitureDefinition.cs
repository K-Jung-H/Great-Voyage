using UnityEngine;

namespace GreatVoyage.Presentation
{
    [CreateAssetMenu(menuName = "Great Voyage/Furniture Definition")]
    public sealed class FurnitureDefinition : ScriptableObject
    {
        [SerializeField] private string kindId;
        public string KindId => kindId;
    }
}
