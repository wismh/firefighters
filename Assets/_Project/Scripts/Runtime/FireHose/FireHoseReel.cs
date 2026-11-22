using UnityEngine;

namespace _Project.FireHose
{
    /// <summary>Hose reel on a fire truck: where the hose starts and how far it reaches.</summary>
    public sealed class FireHoseReel : MonoBehaviour
    {
        [SerializeField] private Transform attachPoint;
        [SerializeField] private float length = 30f;

        public Transform AttachPoint => attachPoint;
        public float Length => length;
        public bool IsTaken { get; private set; }

        public void Setup(Transform attach)
        {
            attachPoint = attach;
        }

        public void Take()
        {
            IsTaken = true;
        }

        public void Return()
        {
            IsTaken = false;
        }
    }
}
