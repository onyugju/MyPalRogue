#if UNITY_EDITOR
using UnityEditor;

namespace PalRogue.EditorTools
{
    /// <summary>
    /// Resources/3.VFX/PixelFx 의 pixelfx 시트 임포트 설정을 자동으로 맞춘다.
    /// 도트가 번지지 않고, 큰 시트가 줄어들지 않게 한다. SkillFx가 manifest.json 크기대로 잘라 쓰므로 원본 크기가 그대로여야 한다.
    /// </summary>
    public class PixelFxImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("Resources/3.VFX/PixelFx/")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.npotScale = TextureImporterNPOTScale.None;
            t.mipmapEnabled = false;
            t.alphaIsTransparency = true;
            t.filterMode = UnityEngine.FilterMode.Point;
            t.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.maxTextureSize = 4096;
        }
    }
}
#endif
