using GreatVoyage.Presentation;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace GreatVoyage.Editor
{
    public sealed class WaterContactBuildValidator : IProcessSceneWithReport
    {
        public int callbackOrder => 0;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report != null)
                Strip(scene);
        }
        public static void Strip(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var source in root.GetComponentsInChildren<WaterContactSource>(true))
                    foreach (var c in source.Colliders)
                        if (c == null || c.gameObject != source.gameObject || !WaterlineGeometry.PhysicallyInactive(c))
                            throw new BuildFailedException(source.name + ": invalid/physically active water contact Collider.");
            // Role: validate only. Runtime Source, shape definitions and ShipWaterSurface must survive building.
        }
    }
}
