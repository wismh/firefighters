using System.Collections.Generic;
using UnityEngine;

namespace _Project.Vehicles
{
    /// <summary>
    /// Dents the car body where it is hit. An intact car draws the pack's low-poly body; the first hit swaps in its
    /// own copy of a denser body mesh (built in the editor) and pushes the vertices around the contact inward.
    /// Every vertex moves by a function of its own position, so the copies of a vertex split along a hard edge move
    /// together and the panels never come apart. The total push per vertex is capped, so the body never caves in.
    /// A dent throws sparks from the contact; a battered car smokes from under the bonnet.
    /// </summary>
    public sealed class VehicleDamage : MonoBehaviour
    {
        // Original vertices of each deformable mesh, shared by all cars built from it.
        private static readonly Dictionary<Mesh, Vector3[]> OriginalVertices = new Dictionary<Mesh, Vector3[]>();

        [SerializeField] private MeshFilter body;
        [SerializeField] private Mesh intactMesh;
        [SerializeField] private Mesh deformableMesh;
        [Tooltip("Impact speed (m/s, along the contact normal, times hardness) below which nothing dents.")]
        [SerializeField] private float minImpactSpeed = 3f;
        [Tooltip("Dent depth per m/s of impact speed above the minimum, metres.")]
        [SerializeField] private float depthPerSpeed = 0.04f;
        [SerializeField] private float maxDepthPerHit = 0.35f;
        [SerializeField] private float baseRadius = 0.5f;
        [SerializeField] private float radiusPerSpeed = 0.05f;
        [SerializeField] private float maxRadius = 1.5f;
        [Tooltip("How far any vertex may move from its original place over all hits, metres.")]
        [SerializeField] private float maxDisplacement = 0.4f;
        [Tooltip("Random spread of the push between vertices, so a dent looks crumpled rather than smooth.")]
        [Range(0f, 1f)] [SerializeField] private float crumple = 0.35f;
        [Tooltip("Random sideways shift of dented vertices, as a share of the push: creases catch the light.")]
        [Range(0f, 1f)] [SerializeField] private float wrinkle = 0.45f;
        [SerializeField] private float hitCooldown = 0.1f;

        [Header("Effects")]
        [Tooltip("World-space sparks, emitted by hand at each dent.")]
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private float sparksPerMetre = 120f;
        [Tooltip("Looping smoke under the bonnet, off until the car is battered.")]
        [SerializeField] private ParticleSystem smoke;
        [Tooltip("Total dent depth, metres, at which the car starts to smoke; it smokes fully at twice this.")]
        [SerializeField] private float smokeStartDamage = 1f;
        [SerializeField] private float smokeMaxRate = 10f;

        private Mesh _mesh;
        private Vector3[] _original;
        private Vector3[] _vertices;
        private bool _dented;
        private float _lastHitTime = float.NegativeInfinity;

        /// <summary>Sum of dent depths so far, metres: how battered the car is.</summary>
        public float TotalDamage { get; private set; }

        public void Setup(MeshFilter bodyFilter, Mesh intact, Mesh deformable, float sizeScale,
            ParticleSystem sparkSystem, ParticleSystem smokeSystem)
        {
            body = bodyFilter;
            intactMesh = intact;
            deformableMesh = deformable;
            maxDepthPerHit = 0.35f * sizeScale;
            maxRadius = 1.5f * sizeScale;
            maxDisplacement = 0.4f * sizeScale;
            smokeStartDamage = 1f * sizeScale;
            sparks = sparkSystem;
            smoke = smokeSystem;
        }

        /// <summary>
        /// Dents the body at <paramref name="worldPoint"/>. <paramref name="impactSpeed"/> is the closing speed along
        /// the contact normal, already scaled by how hard the other thing is. Returns the dent depth, 0 if none.
        /// </summary>
        public float Hit(Vector3 worldPoint, Vector3 worldNormal, float impactSpeed)
        {
            if (impactSpeed < minImpactSpeed || Time.time - _lastHitTime < hitCooldown)
            {
                return 0f;
            }

            _lastHitTime = Time.time;
            BeginDents();

            var model = body.transform;
            var point = model.InverseTransformPoint(worldPoint);
            var push = model.InverseTransformDirection(worldNormal).normalized;
            // Into the body, whichever way the physics normal happens to face.
            if (Vector3.Dot(push, intactMesh.bounds.center - point) < 0f)
            {
                push = -push;
            }

            var over = impactSpeed - minImpactSpeed;
            var depth = Mathf.Min(maxDepthPerHit, over * depthPerSpeed);
            var radius = Mathf.Min(maxRadius, baseRadius + impactSpeed * radiusPerSpeed);
            var radiusSq = radius * radius;
            var maxSq = maxDisplacement * maxDisplacement;
            for (var i = 0; i < _vertices.Length; i++)
            {
                var distanceSq = (_vertices[i] - point).sqrMagnitude;
                if (distanceSq >= radiusSq)
                {
                    continue;
                }

                var t = Mathf.Sqrt(distanceSq) / radius;
                var falloff = 1f - t * t * (3f - 2f * t);
                var original = _original[i];
                var spread = 1f + crumple * (Hash(original) * 2f - 1f);
                var amount = depth * falloff;
                var shake = new Vector3(Hash(original + Vector3.right) - 0.5f, Hash(original + Vector3.up) - 0.5f,
                    Hash(original + Vector3.forward) - 0.5f) * (2f * wrinkle * amount);
                var offset = _vertices[i] + push * (amount * spread) + shake - original;
                if (offset.sqrMagnitude > maxSq)
                {
                    offset = offset.normalized * maxDisplacement;
                }

                _vertices[i] = original + offset;
            }

            _mesh.SetVertices(_vertices);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            TotalDamage += depth;
            Spark(worldPoint, model.TransformDirection(-push), depth);
            UpdateSmoke();
            return depth;
        }

        /// <summary>Back to the intact body: a pooled traffic car returns to the road as new.</summary>
        public void Repair()
        {
            if (!_dented)
            {
                return;
            }

            _dented = false;
            TotalDamage = 0f;
            body.sharedMesh = intactMesh;
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Spark(Vector3 point, Vector3 outward, float depth)
        {
            sparks.transform.SetPositionAndRotation(point, Quaternion.LookRotation(outward));
            sparks.Emit(Mathf.Clamp(Mathf.RoundToInt(depth * sparksPerMetre), 6, 40));
        }

        private void UpdateSmoke()
        {
            if (TotalDamage < smokeStartDamage)
            {
                return;
            }

            var emission = smoke.emission;
            emission.rateOverTime = smokeMaxRate * Mathf.Clamp01(TotalDamage / smokeStartDamage - 0.5f);
            if (!smoke.isPlaying)
            {
                smoke.Play(true);
            }
        }

        private void BeginDents()
        {
            if (_dented)
            {
                return;
            }

            _dented = true;
            if (_mesh == null)
            {
                _mesh = Instantiate(deformableMesh);
                _mesh.MarkDynamic();
                if (!OriginalVertices.TryGetValue(deformableMesh, out _original))
                {
                    _original = deformableMesh.vertices;
                    OriginalVertices.Add(deformableMesh, _original);
                }

                _vertices = new Vector3[_original.Length];
            }

            // A repaired car reuses its mesh copy from the last time it was dented.
            System.Array.Copy(_original, _vertices, _original.Length);
            _mesh.SetVertices(_vertices);
            _mesh.RecalculateNormals();
            body.sharedMesh = _mesh;
        }

        // Stable pseudo-random value in [0, 1) for a position.
        private static float Hash(Vector3 p)
        {
            var h = Mathf.Sin(Vector3.Dot(p, new Vector3(12.9898f, 78.233f, 37.719f))) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }
    }
}
