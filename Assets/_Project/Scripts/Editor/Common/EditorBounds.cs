using System.Collections.Generic;
using UnityEngine;

namespace _Project.Editor
{
    public static class EditorBounds
    {
        /// <summary>World bounds around all the renderers.</summary>
        public static Bounds Of(IReadOnlyList<Renderer> renderers)
        {
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }

            return bounds;
        }

        /// <summary>World bounds around every renderer under the object, inactive ones included.</summary>
        public static Bounds Of(GameObject instance)
        {
            return Of(instance.GetComponentsInChildren<Renderer>(true));
        }
    }
}
