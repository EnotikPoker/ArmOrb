using UnityEditor;
using UnityEngine;

namespace ArmOrb.Editor
{
    public sealed class CosmeticImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/ArmOrb/Resources/Cosmetics/"))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=128;importer.maxTextureSize=128;importer.filterMode=FilterMode.Point;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
        }
    }
}
