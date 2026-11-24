using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Automatically sets up the character walk sheets in Assets/03-Assets/Characters
/// as pixel-art sprite sheets and slices them into frames.
///
/// Sheet layout (made by Tools/SpriteGen/spritegen.py):
///   4 columns  = walk frames 0..3 (frame 0 doubles as idle)
///   8 rows     = directions, top to bottom: S, SE, E, NE, N, NW, W, SW
/// Frames are square; frame size = image width / 4.
///
/// Sprites are named e.g. "Player1_Walk_SE_2". Their IDs are derived from the name,
/// so re-importing a regenerated sheet keeps every reference (animations, prefabs) intact.
/// </summary>
public class CharacterSpriteImporter : AssetPostprocessor
{
    private const string Folder = "Assets/03-Assets/Characters/";
    private const int Columns = 4;
    private const int PixelsPerUnit = 32;
    private static readonly string[] Directions = { "S", "SE", "E", "NE", "N", "NW", "W", "SW" };

    // Where the feet are in each frame (0 = bottom, 1 = top), so sprites stand on their pivot.
    private static readonly Dictionary<string, float> FeetPivotY = new()
    {
        { "Player1_Walk", 3f / 32f },
        { "Player2_Walk", 3f / 32f },
        { "Zombie_Walk", 3f / 32f },
        { "ZombieWolf_Walk", 8f / 40f },
        { "Demon_Walk", 6f / 48f },
    };

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder) || !assetPath.EndsWith(".png")) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;               // crisp pixels, no blur
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        if (!TryReadPngSize(assetPath, out int width, out int height)) return;
        int size = width / Columns;
        if (size <= 0 || width % Columns != 0 || height % size != 0) return;
        int rows = height / size;

        string sheet = Path.GetFileNameWithoutExtension(assetPath);
        float pivotY = FeetPivotY.TryGetValue(sheet, out float p) ? p : 0.1f;

        var rects = new List<SpriteRect>();
        var ids = new List<SpriteNameFileIdPair>();
        for (int row = 0; row < rows; row++)
        {
            string dir = row < Directions.Length ? Directions[row] : $"R{row}";
            for (int col = 0; col < Columns; col++)
            {
                string name = $"{sheet}_{dir}_{col}";
                GUID id = StableGuid(name);
                rects.Add(new SpriteRect
                {
                    name = name,
                    spriteID = id,
                    // Unity measures rects from the bottom-left; the sheet's first row is at the top.
                    rect = new Rect(col * size, height - (row + 1) * size, size, size),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(0.5f, pivotY),
                });
                ids.Add(new SpriteNameFileIdPair(name, id));
            }
        }

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        provider.SetSpriteRects(rects.ToArray());
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(ids);
        provider.Apply();
    }

    // Reads width/height from the PNG header (the texture isn't loaded yet during preprocessing).
    private static bool TryReadPngSize(string path, out int width, out int height)
    {
        width = height = 0;
        try
        {
            using var stream = File.OpenRead(path);
            var header = new byte[24];
            if (stream.Read(header, 0, 24) < 24) return false;
            width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            return width > 0 && height > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static GUID StableGuid(string name)
    {
        using var md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes("CharacterSprite/" + name));
        return new GUID(BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant());
    }
}
