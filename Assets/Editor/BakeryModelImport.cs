using System;
using UnityEditor;

namespace XanaduYonder.EditorTools
{
    internal sealed class BakeryModelImport : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Resources/BakeryBasket/Models/", StringComparison.Ordinal)) return;
            var model = (ModelImporter)assetImporter;
            model.isReadable = true;
            model.importAnimation = false;
            model.importCameras = false;
            model.importLights = false;
        }
    }
}
