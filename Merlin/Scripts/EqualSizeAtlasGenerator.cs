/**
 *                                                                      
 * MIT License
 * 
 * Copyright(c) 2020 Lyuma, template by Merlin
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class EqualSizeAtlasGenerator : EditorWindow
{
    public DefaultAsset DirectoryToConvert = null;

    public string FilenamePattern = "";

    Vector2Int AtlasColumnsRows = new Vector2Int(4, 4);
    public bool UseTextureCompression = false;

    [MenuItem("Tools/Lyuma/Equal Size Atlas Generator")]
    public static void ShowWindow()
    {
        EditorWindow window = EditorWindow.GetWindow(typeof(EqualSizeAtlasGenerator));
        window.maxSize = new Vector2(400, 200);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("MSDF Atlas Generator", EditorStyles.boldLabel);

        DirectoryToConvert = (DefaultAsset)EditorGUILayout.ObjectField("Texture Directory:", DirectoryToConvert, typeof(DefaultAsset), false);

        FilenamePattern = EditorGUILayout.TextField("Asset Pattern", FilenamePattern);

        AtlasColumnsRows = EditorGUILayout.Vector2IntField("Atlas columns/rows", AtlasColumnsRows);

        UseTextureCompression = EditorGUILayout.Toggle("Compress Atlas", UseTextureCompression);

        EditorGUI.BeginDisabledGroup(DirectoryToConvert == null);
        string buttonText = "Generate Atlas to PNG";
        if (GUILayout.Button(buttonText))
        {
            ConvertDirectory(AssetDatabase.GetAssetPath(DirectoryToConvert), FilenamePattern);
        }
        EditorGUI.EndDisabledGroup();
    }

    private void ConvertDirectory(string dirPath, string pattern)
    {
        List<Texture2D> atlasTextures = new List<Texture2D>();
        foreach (string guid in AssetDatabase.FindAssets(pattern, new string[]{dirPath})) {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex != null) {
                atlasTextures.Add(tex);
            }
        }
        //Vector2 scale = new Vector2(1.0f / AtlasColumnsRows.x, 1.0f / AtlasColumnsRows.y);
        int indivWidth = atlasTextures[0].width;
        int indivHeight = atlasTextures[0].height;
        Texture2D outTex = new Texture2D(
            atlasTextures[0].width * AtlasColumnsRows.x,
            atlasTextures[0].height * AtlasColumnsRows.y, atlasTextures[0].format, false);
        for (int i = 0; i < atlasTextures.Count; i++) {
            Texture2D tex = atlasTextures[i];
            int xPos = i % AtlasColumnsRows.x;
            int yPos = i / AtlasColumnsRows.x;
            //Vector2 offset = new Vector2(xPos * scale.x, yPos * scale.y);
            Graphics.CopyTexture(tex, 0, 0, 0, 0, tex.width, tex.height,
                    outTex, 0, 0, xPos * indivWidth, yPos * indivHeight);
        }
        outTex.Apply();
        
        string savePath = dirPath + "_atlas.png";
        File.WriteAllBytes(savePath, ImageConversion.EncodeToPNG(outTex));
        AssetDatabase.Refresh();
        TextureImporter texImporter = AssetImporter.GetAtPath(savePath) as TextureImporter;
        texImporter.textureType = TextureImporterType.Default;
        texImporter.textureShape = TextureImporterShape.Texture2D;
        texImporter.mipmapEnabled = true;
        texImporter.textureCompression = UseTextureCompression ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
        texImporter.sRGBTexture = false;
        texImporter.SaveAndReimport();
        Texture2D newAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);

        EditorGUIUtility.PingObject(newAtlas);
    }
}

#endif