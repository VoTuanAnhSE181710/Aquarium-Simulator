using UnityEngine;

public sealed class AquariumOxygenProvider : MonoBehaviour
{
    [SerializeField] private float oxygenCapacityBonus = 1f;
    [SerializeField] private string providerName = "Aquarium Plant";

    public float OxygenCapacityBonus => Mathf.Max(0f, oxygenCapacityBonus);
    public string ProviderName => string.IsNullOrWhiteSpace(providerName) ? gameObject.name : providerName;

    public void Configure(string displayName, float capacityBonus)
    {
        providerName = string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        oxygenCapacityBonus = Mathf.Max(0f, capacityBonus);
    }
}
