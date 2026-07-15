using UnityEditor;

namespace NavianChallenge.EditorTools
{
    /// <summary>
    /// Import settings for the segmentation meshes. The .obj files ship without vertex
    /// normals (to keep them small), so we tell Unity to CALCULATE smooth normals on import.
    /// This also silences the "mesh has no normals, recalculating" import warning.
    /// </summary>
    public class MeshImportSettings : AssetPostprocessor
    {
        const string MeshFolder = "Assets/NavianChallenge/Data/Atlas/IXI025/Meshes/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(MeshFolder) || !assetPath.EndsWith(".obj"))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;
            importer.normalSmoothingAngle = 60f;
            importer.importTangents = ModelImporterTangents.None;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; // meshes use our own materials
        }
    }
}
