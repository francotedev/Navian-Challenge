using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityVolumeRendering;

namespace NavianChallenge.EditorTools
{
    /// <summary>Batch verification helpers for the base scene (edit mode only).</summary>
    public static class ChallengeVerify
    {
        const string ScenePath = "Assets/NavianChallenge/Scenes/NavianChallenge_Main.unity";
        const string ShotDir = "/tmp/navian_shots";

        // Opens the scene fresh and inspects it. The [ExecuteAlways] loader creates the
        // MRI volume on scene open (no Play), so we also assert it is present.
        public static void StructureVerify()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            bool ok = true;

            GameObject atlas = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "AtlasRoot");
            ok &= Check(atlas != null, "AtlasRoot present");

            Transform meshesRoot = atlas ? atlas.transform.Find("MeshesRoot") : null;
            ok &= Check(meshesRoot != null, "MeshesRoot present");
            if (meshesRoot != null)
            {
                Debug.Log($"[Verify] MeshesRoot localScale={meshesRoot.localScale} localEuler={meshesRoot.localEulerAngles}");
                foreach (var n in new[] { "GrayMatter", "WhiteMatter", "Veins", "Skin" })
                {
                    var t = meshesRoot.Find(n);
                    var mf = t ? t.GetComponent<MeshFilter>() : null;
                    var mr = t ? t.GetComponent<MeshRenderer>() : null;
                    bool full = t && mf && mf.sharedMesh && mr && mr.sharedMaterial;
                    ok &= Check(full, $"mesh '{n}' complete" + (full ? $" (tris={mf.sharedMesh.triangles.Length / 3}, mat={mr.sharedMaterial.name})" : ""));
                }
            }

            // [ExecuteAlways] preview: MRI volume must be present in edit mode (no Play).
            // Search the hierarchy (FindFirstObjectByType does not return DontSave objects).
            var vol = atlas ? atlas.GetComponentInChildren<VolumeRenderedObject>(true) : null;
            ok &= Check(vol != null, "MRI volume auto-created in EDIT mode (visible without Play)"
                + (vol != null ? $" - parent={vol.transform.parent?.name}" : ""));
            ok &= Check(vol == null || (vol.gameObject.hideFlags & HideFlags.DontSave) != 0,
                "edit-mode volume is DontSave (not serialized into the scene)");

            var loader = Object.FindAnyObjectByType<NavianChallenge.AtlasVolumeLoader>();
            ok &= Check(loader != null && loader.dataset != null, "AtlasVolumeLoader has VolumeDataset");

            var ctrl = Object.FindAnyObjectByType<NavianChallenge.AtlasSceneController>();
            ok &= Check(ctrl != null, "AtlasSceneController present (camera helper, no show/hide)");

            ok &= Check(Object.FindAnyObjectByType<Camera>() != null, "Camera present");
            ok &= Check(Object.FindObjectsByType<Light>(FindObjectsInactive.Include).Any(l => l.type == LightType.Directional), "Directional light present");
            ok &= Check(EditorBuildSettings.scenes.Any(s => s.path == ScenePath && s.enabled), "scene in Build Settings");

            Debug.Log(ok ? "###STRUCTURE_OK###" : "###STRUCTURE_FAIL###");
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Check(bool cond, string msg)
        {
            Debug.Log((cond ? "[Verify][PASS] " : "[Verify][FAIL] ") + msg);
            return cond;
        }

        // Opens the scene (edit mode) and renders a few shots of the MRI + meshes as they
        // appear on open (no Play). Confirms the volume renders and is aligned.
        public static void EditModeRender()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(ShotDir);

            var atlas = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.name == "AtlasRoot");
            var meshes = atlas ? atlas.Find("MeshesRoot") : null;
            var cam = Object.FindAnyObjectByType<Camera>();
            var vol = atlas ? atlas.GetComponentInChildren<VolumeRenderedObject>(true) : null;
            Debug.Log($"[EditRender] volume present on open: {vol != null}");

            Bounds b = new Bounds(Vector3.zero, Vector3.one);
            if (meshes != null)
            {
                bool has = false;
                foreach (var r in meshes.GetComponentsInChildren<Renderer>())
                { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
            }
            float dist = b.extents.magnitude * 2.4f;

            Shot(cam, b, new Vector3(0.35f, 0.25f, -1f), dist, "editmode_combo_front");
            Shot(cam, b, new Vector3(-1f, 0.15f, -0.2f), dist, "editmode_combo_left");

            // clip the MRI so the internal meshes show through
            if (vol != null)
            {
                var mat = vol.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                mat.SetFloat("_MinVal", 0.30f);
                Shot(cam, b, new Vector3(0.35f, 0.25f, -1f), dist, "editmode_hero_front");
            }

            Debug.Log("###EDITRENDER_DONE###");
            EditorApplication.Exit(0);
        }

        static void Shot(Camera cam, Bounds b, Vector3 dir, float dist, string name)
        {
            if (cam == null) return;
            dir = dir.normalized;
            cam.transform.position = b.center - dir * dist;
            cam.transform.rotation = Quaternion.LookRotation((b.center - cam.transform.position).normalized, Vector3.up);

            var rt = new RenderTexture(1024, 1024, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"{ShotDir}/{name}.png", tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log($"[EditRender] shot {name}");
        }
    }
}
