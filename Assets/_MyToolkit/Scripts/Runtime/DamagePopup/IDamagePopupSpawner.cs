using UnityEngine;

namespace MyToolkit.DamagePopup
{
    public interface IDamagePopupSpawner
    {
        void Spawn(Vector3 worldPosition, DamagePopupInfo info);
        void Spawn(Transform target, Vector3 worldPosition, DamagePopupInfo info);
    }
}
