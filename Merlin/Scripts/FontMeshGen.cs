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
using System.Linq;
using UnityEditor;
using UnityEngine;

public class FontMeshGen : EditorWindow
{
    public DefaultAsset DirectoryToConvert = null;

    public string FilenamePattern = "";

    Vector2Int AtlasColumnsRows = new Vector2Int(4, 4);
    public bool UseTextureCompression = false;

    TextAsset StringFile = null;

    Transform ArmatureRoot = null;

    [MenuItem("Tools/Lyuma/FontMeshGen")]
    public static void ShowWindow()
    {
        EditorWindow window = EditorWindow.GetWindow(typeof(FontMeshGen));
        window.maxSize = new Vector2(400, 200);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Font Mesh Generator", EditorStyles.boldLabel);

        DirectoryToConvert = (DefaultAsset)EditorGUILayout.ObjectField("Texture Directory:", DirectoryToConvert, typeof(DefaultAsset), false);

        FilenamePattern = "_msdfAtlas";

        AtlasColumnsRows = EditorGUILayout.Vector2IntField("Atlas columns/rows", AtlasColumnsRows);

        UseTextureCompression = EditorGUILayout.Toggle("Compress Atlas", UseTextureCompression);

        StringFile = (TextAsset)EditorGUILayout.ObjectField("Strings File:", StringFile, typeof(TextAsset), false);

        ArmatureRoot = (Transform)EditorGUILayout.ObjectField("Output Transform:", ArmatureRoot, typeof(Transform), true);


        EditorGUI.BeginDisabledGroup(DirectoryToConvert == null || StringFile == null || ArmatureRoot == null);
        string buttonText = "Generate Atlas and Text Mesh";
        if (GUILayout.Button(buttonText))
        {
            ConvertDirectory(AssetDatabase.GetAssetPath(DirectoryToConvert), FilenamePattern,
                    AssetDatabase.GetAssetPath(StringFile), ArmatureRoot);
        }
        EditorGUI.EndDisabledGroup();
    }

    private void ConvertDirectory(string dirPath, string pattern, string stringFilename, Transform rootTransform)
    {
        List<Texture2D> atlasTextures = new List<Texture2D>();
        List<Font> fonts = new List<Font>();
        Dictionary<string, int> fontNameToIndex = new Dictionary<string, int>();
        foreach (string guid in AssetDatabase.FindAssets(pattern, new string[]{dirPath})) {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex != null) {
                string withoutExtension = assetPath.Replace(pattern + ".png", "");
                Font fon = null;
                foreach (string fonguid in AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(withoutExtension), new string[]{dirPath})) {
                    string fonpath = AssetDatabase.GUIDToAssetPath(fonguid);
                    string fonpathNoExt = fonpath.Substring(0, fonpath.LastIndexOf('.'));
                    if (fonpathNoExt.Equals(withoutExtension)) {
                        fon = AssetDatabase.LoadAssetAtPath<Font>(fonpath);
                    }
                }
                if (fon != null) {
                    fontNameToIndex[fon.name] = fonts.Count();
                    Debug.Log("Adding font " + fon.name);
                    atlasTextures.Add(tex);
                    fonts.Add(fon);
                } else {
                    Debug.LogError("Missing font " + withoutExtension + " for " + assetPath);
                }
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

        List<Vector2> newUV = new List<Vector2>();
        List<Vector3> newPositions = new List<Vector3>();
        List<int> newIndices = new List<int>();
        List<BoneWeight> newBoneWeights = new List<BoneWeight>();
        List<Transform> bones = new List<Transform>();
        List<Matrix4x4> bindposes = new List<Matrix4x4>();

        for (int i = rootTransform.childCount; i-- > 0;) {
            Undo.DestroyObjectImmediate(rootTransform.GetChild(i).gameObject);
        }

        string[] lines = File.ReadAllLines(stringFilename, System.Text.Encoding.UTF8);
        int currentFontIdx = 0;
        int boneId = -1;
        int srcLineNum = 0;
        int lineCount = 0;
        float lineHeight = 0;
        {
            CharacterInfo test;
            if (fonts[currentFontIdx].GetCharacterInfo('M', out test)) {
                lineHeight = test.glyphHeight * 1.5f;
            }
        }
        foreach (string xline in lines) {
            int wordCount = 0;
            srcLineNum++;
            string wordText = "";
            string lineText = "";
            Transform lineObject = null;
            Transform wordObject = null;
            string line = xline;
            bool splitChars = false;
            bool splitWords = false;
            if (line.StartsWith("__")) {
                splitChars = true;
                splitWords = true;
                line = line.Substring(2);
            } else if (line.StartsWith("_")) {
                splitWords = true;
                line = line.Substring(1);
            }
            float x = 0;
            float y = 0;
            for (int i = 0; i < line.Length; i++) {
                char c = line[i];
                if (c == '<') {
                    if (line[i + 1] == '<') {
                        i++;
                    } else {
                        int pos = line.IndexOf('>', i + 1);
                        if (pos == -1) {
                            Debug.LogError("Mismatched <> tokens on line " + srcLineNum + ".");
                        } else {
                            string newFont = line.Substring(i + 1, pos - i - 1);
                            if (!fontNameToIndex.ContainsKey(newFont)) {
                                Debug.LogError("Font " + newFont + " not found on line " + srcLineNum + ".");
                            }
                            currentFontIdx = fontNameToIndex[newFont];
                            i = pos;
                            continue;
                        }
                    }
                }
                lineText += c;
                if (c != ' ' && lineObject == null) {
                    lineObject = new GameObject().transform;
                    lineObject.SetParent(rootTransform, false);
                    lineObject.position = new Vector3(0, -lineHeight * lineCount, 0);
                    lineCount++;
                    if (!splitWords) {
                        boneId++;
                        bones.Add(lineObject);
                        bindposes.Add(Matrix4x4.identity);
                    }
                }
                if (splitWords) {
                    if (c == ' ') {
                        if (wordObject != null) {
                            wordObject.gameObject.name = wordText + wordCount;
                            wordObject = null;
                            wordText = "";
                        }
                    } else {
                        if (wordObject == null) {
                            wordCount++;
                            wordObject = new GameObject().transform;
                            wordObject.SetParent(lineObject, false);
                            if (!splitChars) {
                                boneId++;
                                bones.Add(wordObject);
                                bindposes.Add(Matrix4x4.identity);
                            }
                        }
                        if (splitChars) {
                            Transform charObject = new GameObject().transform;
                            charObject.SetParent(wordObject, false);
                            charObject.gameObject.name = "" + c + wordText.Length;
                            boneId++;
                            bones.Add(charObject);
                            bindposes.Add(Matrix4x4.identity);
                        }
                        wordText += c;
                    }
                }
                BoneWeight bw = new BoneWeight();
                bw.boneIndex0 = boneId;
                bw.weight0 = 1;
                CharacterInfo ci;
                if (!fonts[currentFontIdx].GetCharacterInfo(c, out ci)) {
                    Debug.LogError("Font missing glyph for character '" + c + "'", fonts[currentFontIdx]);
                    continue;
                }
                if (c == ' ') {
                    x += ci.advance;
                    continue;
                }
                Debug.Log("char '" + c + "' minX:" + ci.minX + ",Y:" + ci.minY + ", maxX:" + ci.maxX + ",Y:" + ci.maxY);
                Debug.Log("ci.uvs" + ci.uvBottomLeft.x + "," + ci.uvBottomLeft.y + ";" + ci.uvTopLeft.x + "," + ci.uvTopLeft.y + ";" +
                        "ci.uvs" + ci.uvBottomRight.x + "," + ci.uvBottomRight.y + ";" + ci.uvTopRight.x + "," + ci.uvTopRight.y);
                int offset = newPositions.Count();
                newIndices.Add(offset + 0);
                newIndices.Add(offset + 1);
                newIndices.Add(offset + 2);
                newIndices.Add(offset + 2);
                newIndices.Add(offset + 1);
                newIndices.Add(offset + 3);
                newPositions.Add(new Vector3(x + ci.minX, y + ci.minY, 0));
                newUV.Add(scaleUV(ci.uvBottomLeft, AtlasColumnsRows, currentFontIdx));
                newBoneWeights.Add(bw);
                newPositions.Add(new Vector3(x + ci.minX, y + ci.maxY, 0));
                newUV.Add(scaleUV(ci.uvTopLeft, AtlasColumnsRows, currentFontIdx));
                newBoneWeights.Add(bw);
                newPositions.Add(new Vector3(x + ci.maxX, y + ci.minY, 0));
                newUV.Add(scaleUV(ci.uvBottomRight, AtlasColumnsRows, currentFontIdx));
                newBoneWeights.Add(bw);
                newPositions.Add(new Vector3(x + ci.maxX, y + ci.maxY, 0));
                newUV.Add(scaleUV(ci.uvTopRight, AtlasColumnsRows, currentFontIdx));
                newBoneWeights.Add(bw);
                x += ci.advance;
            }
            if (wordObject != null) {
                wordObject.gameObject.name = wordText + wordCount;
            }
            if (lineObject != null) {
                lineObject.gameObject.name = lineText;
                Undo.RegisterCreatedObjectUndo(lineObject.gameObject, lineText);
            }
        }

        Mesh newMesh = new Mesh ();
        newMesh.vertices = newPositions.ToArray();
        newMesh.SetUVs (0, newUV);
        newMesh.bindposes = bindposes.ToArray();
        newMesh.boneWeights = newBoneWeights.ToArray();
        newMesh.subMeshCount = 1;
        newMesh.SetIndices (newIndices.ToArray(), MeshTopology.Triangles, 0);
        //newMesh.bounds = new Bounds(new Vector3(), new Vector3(1, 1, 1));
        newMesh.name = Path.GetFileNameWithoutExtension(stringFilename) + "_text";
        Mesh meshAfterUpdate = newMesh;
        SkinnedMeshRenderer smr = rootTransform.gameObject.GetOrAddComponent<SkinnedMeshRenderer>();
        Undo.RecordObject (smr, "Built Font Mesh");
        smr.bones = bones.ToArray();
        smr.sharedMesh = newMesh;
        smr.rootBone = rootTransform;
        string fileName = Path.GetDirectoryName(stringFilename) + "/" + newMesh.name + ".asset";
        AssetDatabase.CreateAsset (meshAfterUpdate, fileName);
        AssetDatabase.SaveAssets ();
    }

    private Vector2 scaleUV(Vector2 origUV, Vector2Int AtlasColumnsRows, int fontIdx) {
        int xPos = fontIdx % AtlasColumnsRows.x;
        int yPos = fontIdx / AtlasColumnsRows.x;
        return new Vector2(
            (origUV.x + xPos) / AtlasColumnsRows.x,
            (origUV.y + yPos) / AtlasColumnsRows.y
        );
    }
}
#endif
