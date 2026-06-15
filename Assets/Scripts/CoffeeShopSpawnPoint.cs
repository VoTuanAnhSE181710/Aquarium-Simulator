using UnityEngine;

public sealed class CoffeeShopSpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnPointId = "Default";

    public string SpawnPointId => spawnPointId;
}
