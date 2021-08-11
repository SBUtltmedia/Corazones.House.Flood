// NOTE: This no longer works in 2019 and older. Need a new solution :(
#if !UNITY_2019_1_OR_NEWER

using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.U2D;
using UnityEditor.Sprites;
using UnityEngine;

// From https://www.tutorialfor.com/questions-107574.htm

public static class SpritePhysicsShapeAdjuster
{
    private const byte AlphaTolerance = 128;
    private const bool HoleDetection = true;
    private const float OutlineTolerance = 1.0f;

	
	[MenuItem("Assets/Generate Sprite Hotspots",true)]
	static bool ContextCreateAnimationFromSpritesValidate(MenuCommand command) { return Selection.activeObject != null && Selection.activeObject is Texture; }


	[MenuItem("Assets/Generate Sprite Hotspots",false,33)]
	static void ContextCreateAnimationFromSprites(MenuCommand command)
	{

        var assets = Selection.GetFiltered<Object>(SelectionMode.Assets);
        if (assets == null)
        {
            Debug.LogError ("No assets.");
            return;
        }
        // Find outline calculation method ... If it fails, stop processing
        // internal static void GenerateOutline (Texture2D texture, Rect rect, float detail, byte alphaTolerance, bool holeDetection, out Vector2 [] [] paths)
        var generateOutline = typeof (SpriteUtility) .GetMethod (
            "GenerateOutline",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (generateOutline == null)
        {
            Debug.LogError ("GenerateOutline not found.");
            return;
        }
        foreach (var asset in assets)
        {
            var assetPath = AssetDatabase.GetAssetPath (asset);
            Debug.LogFormat ("Processing {0} ...", assetPath);
            // Get the sprite under the asset ... If it fails, stop processing
            var subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath (assetPath);
            if (subAssets == null)
            {
                Debug.LogError ("Sub asset (s) not found.");
                continue;
            }
            var sprites = subAssets.OfType<Sprite>(). ToArray ();
            if (sprites.Length<= 0)
            {
                Debug.LogError ("Sprite (s) not found.");
                continue;
            }
            // Get the SpriteEditorDataProvider ... If the failure or destruction, stop processing
            var textureImporter = AssetImporter.GetAtPath (assetPath) as TextureImporter;
            var spriteEditorDataProvider = textureImporter as ISpriteEditorDataProvider;
            if ((spriteEditorDataProvider == null) || spriteEditorDataProvider.Equals (null))
            {
                Debug.LogError ("SpriteEditorDataProvider not found.");continue;
            }
            spriteEditorDataProvider.InitSpriteEditorDataProvider ();
            // Get a TextureDataProvider ... If it fails, stop processing
            var textureDataProvider = spriteEditorDataProvider.GetDataProvider<ITextureDataProvider>();
            if (textureDataProvider == null)
            {
                Debug.LogError ("TextureDataProvider not found.");
                continue;
            }
            // Get the texture for the sprite ... If it fails, stop processing
            var texture = textureDataProvider.GetReadableTexture2D ();
            if (texture == null)
            {
                Debug.LogError ("Texture not found.");
                continue;
            }
            // Get SpritePhysicsOutlineDataProvider ... If it fails, stop processing
            var outlineDataProvider = spriteEditorDataProvider.GetDataProvider<ISpritePhysicsOutlineDataProvider>();
            if (outlineDataProvider == null)
            {
                Debug.LogError ("OutlineDataProvider not found.");
                continue;
            }
            // Get actual texture size, calculate scale
            // The scale seems to be necessary to support the maximum size limit texture and NPOT texture ...?
            int actualWidth, actualHeight;
            textureDataProvider.GetTextureActualWidthAndHeight (out actualWidth, out actualHeight);
            var textureWidth = texture.width;
            var textureHeight = texture.height;
            var rectScaleX = (float) textureWidth/actualWidth;
            var rectScaleY = (float) textureHeight/actualHeight;
            Debug.LogFormat ("Rect scale: ({0}, {1})", rectScaleX, rectScaleY);
            // calculate outline
            var spriteCount = sprites.Length;
            for (var i = 0;i<spriteCount;i ++)
            {
                var sprite = sprites [i];
                if (sprite == null)
                {
                    Debug.LogWarningFormat ("Sprite {0} not found.", i);
                    continue;
                }
                Debug.LogFormat ("Processing sprite {0} ...", sprite.name);
                var rect = sprite.rect;
                var halfExtents = rect.size * 0.5f;
                rect.xMax *= rectScaleX;
                rect.yMax *= rectScaleY;
                rect.xMin *= rectScaleX;
                rect.yMin *= rectScaleY;
                var args = new object [] {texture, rect, OutlineTolerance, AlphaTolerance, HoleDetection, null};
                generateOutline.Invoke (null, args);
                var paths = args [5] as Vector2 [] [];
                if (paths == null)
                {Debug.LogWarning ("Paths not found.");
                    continue;
                }
                var pathCount = paths.Length;
                Debug.LogFormat ("{0} path (s) found.", pathCount);
                for (var j = 0;j<pathCount;j ++)
                {
                    var path = paths [j];
                    if (path == null)
                    {
                        continue;
                    }
                    var vertexCount = path.Length;
                    Debug.LogFormat ("Path {0} has {1} vertices.", j, vertexCount);
                    for (var k = 0;k<vertexCount;k ++)
                    {
                        var p = path [k];
                        path [k] .x = Mathf.Clamp (p.x/rectScaleX, -halfExtents.x, halfExtents.x);
                        path [k] .y = Mathf.Clamp (p.y/rectScaleY, -halfExtents.y, halfExtents.y);
                    }
                }
                var spriteId = sprite.GetSpriteID ();
                outlineDataProvider.SetOutlines (spriteId, paths.ToList ());
                outlineDataProvider.SetTessellationDetail (spriteId, OutlineTolerance);
                Debug.LogFormat ("Processed {0} of {1} sprite (s).", i + 1, spriteCount);
            }
            // apply edits
            spriteEditorDataProvider.Apply ();
            // save/re-import
            Debug.LogFormat ("Reimport {0}.", assetPath);
            textureImporter.SaveAndReimport ();
        }
    }
}

#endif
