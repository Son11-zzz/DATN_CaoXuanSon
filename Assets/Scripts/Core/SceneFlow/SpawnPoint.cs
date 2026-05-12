using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnId = "Default";
    [SerializeField] private bool isDefault;

    public string SpawnId => spawnId;
    public bool IsDefault => isDefault;

    private void OnDrawGizmos()
    {
        Gizmos.color = isDefault ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }
}