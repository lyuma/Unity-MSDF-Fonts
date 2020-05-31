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

    Vector2Int AtlasColumnsRows = new Vector2Int(2, 2);
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
            if (!assetPath.EndsWith("_msdfAtlas.png") && !assetPath.EndsWith("_msdfAtlas.asset")) {
                Debug.LogWarning("Ignoring font texture " + assetPath);
                continue;
            }
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex != null) {
                string withoutExtension = assetPath.Replace(pattern + ".png", "").Replace(pattern + ".asset", "");
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
                    return;
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

        List<Vector2> newUV = new List<Vector2>();
        List<Vector4> newUV3 = new List<Vector4>();
        List<Vector4> newUV4 = new List<Vector4>();
        List<Vector4> newUV5Shadow = new List<Vector4>();
        List<Vector4> newUV6Barycentric = new List<Vector4>();
        List<Color> newColor = new List<Color>();
        List<Vector3> newPositions = new List<Vector3>();
        List<Vector3> newNormals = new List<Vector3>();
        List<Vector4> newTangents = new List<Vector4>();
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
        float align = 0;
        float angle = 0, anglesin=0, anglecos=1;
        Matrix4x4 angleMat = Matrix4x4.identity;
        float advanceangle = 0, advanceanglesin=0, advanceanglecos=1;
        float spaceScale = 1;
        float sizeScale = 1;
        float lineHeightScale = 1;
        float vertStretch = 1;
        float horizStretch = 1;
        float blurSetting = 0;
        float thickSetting = 0.5f;
        float corrupt = 0;
        float italic = 0;
        float resetx = 0;
        float extra = 0;
        float z = 0;
        float lineYOffset = 0;
        bool separator = false;
        Vector4 currentUV4 = new Vector4();
        string parentObjName = "";
        Transform parentObj = null;
        Transform lastLineObject = null;
        Color currentColor = Color.white;
        Color currentColorRight = Color.white;
        Vector4 shadowColorVec = new Vector4(1,1,1,1);
        Vector3 zForwardNormal = new Vector3(0,0,1);
        Vector4 xLeftTangent = new Vector4(1,0,0,1);
        Dictionary<string, int> duplicateLines = new Dictionary<string, int>();
        foreach (string xline in lines) {
            char wordSep = ' ';
            int wordCount = 0;
            srcLineNum++;
            string wordText = "";
            string lineText = "";
            Transform lineObject = null;
            Transform wordObject = null;
            string line = xline;
            float[] nextGraphicUVs = null;
            bool splitChars = false;
            bool splitWords = false;
            if (line.StartsWith("#")) {
                continue;
            } else if (line.StartsWith("__$")) {
                splitChars = true;
                splitWords = true;
                wordSep = '$';
                line = line.Substring(3);
            } else if (line.StartsWith("__")) {
                splitChars = true;
                splitWords = true;
                line = line.Substring(2);
            } else if (line.StartsWith("_$")) {
                splitWords = true;
                wordSep = '$';
                line = line.Substring(2);
            } else if (line.StartsWith("_")) {
                splitWords = true;
                line = line.Substring(1);
            }
            float y = 0;
            float x = resetx;
            float wordx = 0;
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
                            if ("center".Equals(newFont)) {
                                align = 0.5f;
                            } else if ("left".Equals(newFont)) {
                                align = 0.0f;
                            } else if ("right".Equals(newFont)) {
                                align = 1.0f;
                            } else if (newFont.StartsWith("color=")) {
                                if (!ColorUtility.TryParseHtmlString(newFont.Split('=')[1], out currentColor)) {
                                    currentColor = Color.black;
                                    Debug.LogError("Failed to parse " + newFont);
                                }
                                currentColor = currentColor.linear;
                                currentColorRight = currentColor;
                            } else if (newFont.StartsWith("colgrad=")) {
                                if (!ColorUtility.TryParseHtmlString(newFont.Split('=')[1], out currentColorRight)) {
                                    currentColorRight = Color.black;
                                    Debug.LogError("Failed to parse " + newFont);
                                }
                                currentColorRight = currentColorRight.linear;
                            } else if (newFont.Equals("/shadow")) {
                                shadowColorVec = new Vector4(0,0,0,0);
                            } else if (newFont.StartsWith("shadow=")) {
                                Color shadowColor;
                                if (!ColorUtility.TryParseHtmlString(newFont.Split('=')[1], out shadowColor)) {
                                    shadowColorVec = new Vector4(0,0,0,0);
                                    Debug.LogError("Failed to parse " + newFont);
                                }
                                shadowColor = shadowColor.linear;
                                shadowColorVec = new Vector4(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a);
                            } else if (newFont.StartsWith("align=")) {
                                align = float.Parse(newFont.Substring(6));
                            } else if (newFont.StartsWith("space=")) {
                                spaceScale = float.Parse(newFont.Substring(6));
                            } else if (newFont.StartsWith("size=")) {
                                sizeScale = float.Parse(newFont.Substring(5));
                            } else if (newFont.StartsWith("blur=")) {
                                blurSetting = float.Parse(newFont.Substring(5));
                            } else if (newFont.StartsWith("lineheight=")) {
                                lineHeightScale = float.Parse(newFont.Substring(11));
                            } else if (newFont.StartsWith("stretch=")) {
                                vertStretch = float.Parse(newFont.Substring(8));
                            } else if (newFont.StartsWith("fatten=")) {
                                horizStretch = float.Parse(newFont.Substring(7));
                            } else if (newFont.StartsWith("corrupt=")) {
                                corrupt = float.Parse(newFont.Substring(8));
                            } else if (newFont.StartsWith("angle=") || newFont.StartsWith("anglexlate=")) {
                                string[] vector = newFont.Split('=')[1].Split(',');
                                Vector3 xlate = new Vector3();
                                if (newFont.StartsWith("anglexlate=")) {
                                    float.Parse(vector[3]);
                                    xlate.x = float.Parse(vector[vector.Count() - 3]);
                                    xlate.y = float.Parse(vector[vector.Count() - 2]);
                                    xlate.z = float.Parse(vector[vector.Count() - 1]);
                                }
                                angle = float.Parse(vector[0]) * Mathf.PI/180.0f;
                                anglesin = Mathf.Sin(angle);
                                anglecos = Mathf.Cos(angle);
                                angleMat = new Matrix4x4(
                                    new Vector4(anglecos, -anglesin,0,0),
                                    new Vector4(anglesin, anglecos,0,0),
                                    new Vector4(0,0,1,0),
                                    new Vector4(xlate.x,xlate.y,xlate.z,1));
                            } else if (newFont.StartsWith("advangle=")) {
                                advanceangle = float.Parse(newFont.Substring(9)) * Mathf.PI/180.0f;
                                advanceanglesin = Mathf.Sin(advanceangle);
                                advanceanglecos = Mathf.Cos(advanceangle);
                            } else if (newFont.StartsWith("extra=")) {
                                extra = float.Parse(newFont.Substring(6));
                            } else if (newFont.StartsWith("italic=")) {
                                italic = float.Parse(newFont.Substring(7));
                            } else if (newFont.Equals("i")) {
                                italic = 0.25f;
                            } else if (newFont.Equals("/i")) {
                                italic = 0;
                            } else if (newFont.StartsWith("img=") || newFont.StartsWith("blockimg=")) {
                                string []nextGraphic = newFont.Split('=')[1].Split(',');
                                nextGraphicUVs = new float[4] {
                                    float.Parse(nextGraphic[0]),
                                    float.Parse(nextGraphic[1]),
                                    float.Parse(nextGraphic[2]),
                                    float.Parse(nextGraphic[3]) };
                                pos -= 1; 
                                // the > will be replaced with the image we chose.
                                if (newFont.StartsWith("block")) {
                                    x -= horizStretch * sizeScale * lineHeight/2;
                                    y -= vertStretch * sizeScale * lineHeight/2;
                                }
                            } else if (newFont.StartsWith("thick=")) {
                                thickSetting = float.Parse(newFont.Substring(6));
                            } else if (newFont.Equals("b")) {
                                thickSetting = 1.0f;
                            } else if (newFont.Equals("/b")) {
                                thickSetting = 0.5f;
                            } else if (newFont.Equals("sep")) {
                                separator = true;
                            } else if (newFont.Equals("/sep")) {
                                separator = false;
                            } else if (newFont.StartsWith("translate=")) {
                                string[] vector = newFont.Substring(10).Split(',');
                                Vector3 newPosition = new Vector3(
                                    float.Parse(vector[0]), float.Parse(vector[1]), float.Parse(vector[2]));
                                (parentObj ?? lastLineObject).transform.localPosition = newPosition;
                                currentUV4 = new Vector4(newPosition.x, newPosition.y, newPosition.z, sizeScale);
                            } else if (newFont.StartsWith("distancefade=")) {
                                currentUV4 = new Vector4(currentUV4.x, currentUV4.y, currentUV4.z, float.Parse(newFont.Split('=')[1]));
                            } else if (newFont.StartsWith("rotate=")) {
                                string[] vector = newFont.Substring(7).Split(',');
                                (parentObj ?? lastLineObject).transform.localEulerAngles = new Vector3(
                                    float.Parse(vector[0]), float.Parse(vector[1]), float.Parse(vector[2]));
                            } else if (newFont.StartsWith("scale=")) {
                                string[] vector = newFont.Substring(6).Split(',');
                                (parentObj ?? lastLineObject).transform.localScale = new Vector3(
                                    float.Parse(vector[0]), float.Parse(vector[1]), float.Parse(vector[2]));
                            } else if (newFont.Equals("billboard")) {
                                xLeftTangent.w = -1;
                            } else if (newFont.Equals("/billboard")) {
                                xLeftTangent.w = 1;
                            } else if (newFont.Equals("doubleside")) {
                                zForwardNormal = new Vector3(1,0,0);
                            } else if (newFont.Equals("/doubleside")) {
                                zForwardNormal = new Vector3(0,0,1);
                            } else if (newFont.StartsWith("x=")) {
                                x = (newFont[2] == '+' ? x : 0) + float.Parse(newFont.Substring(newFont[2] == '+' ? 3 : 2));
                                resetx = x;
                            } else if (newFont.StartsWith("y=")) {
                                y = (newFont[2] == '+' ? y : 0) + float.Parse(newFont.Substring(newFont[2] == '+' ? 3 : 2));
                            } else if (newFont.StartsWith("z=")) {
                                z = (newFont[2] == '+' ? z : 0) + float.Parse(newFont.Substring(newFont[2] == '+' ? 3 : 2));
                            } else if (newFont.StartsWith("parent=")) {
                                y = 0;
                                lineYOffset = 0;
                                parentObjName = newFont.Substring(7);
                                parentObj = parentObjName.Length == 0 ? null :
                                    (new GameObject().transform);
                                if (parentObj != null) {
                                    parentObj.gameObject.name = parentObjName;
                                    parentObj.SetParent(rootTransform, false);
                                }
                            } else if (newFont.StartsWith("/parent")) {
                                parentObjName = "";
                                parentObj = null;
                            } else {
                                if (!fontNameToIndex.ContainsKey(newFont)) {
                                    Debug.LogError("Font " + newFont + " not found on line " + srcLineNum + ".");
                                }
                                currentFontIdx = fontNameToIndex[newFont];
                            }
                            i = pos;
                            continue;
                        }
                    }
                }
                lineText += c;
                if (c != ' ' && lineObject == null) {
                    lineObject = lastLineObject = new GameObject().transform;
                    lineObject.SetParent(parentObj == null ? rootTransform : parentObj, false);
                    lineObject.localPosition = new Vector3(0, lineYOffset - lineHeight * lineHeightScale, 0);
                    lineYOffset -= lineHeight * lineHeightScale;
                    lineCount++;
                    if (!splitWords) {
                        boneId++;
                        bones.Add(lineObject);
                        bindposes.Add(Matrix4x4.identity);
                    }
                }
                if (splitWords) {
                    if (c == wordSep) {
                        if (wordObject != null) {
                            wordObject.gameObject.name = wordText + wordCount;
                            wordObject = null;
                            wordText = "";
                        }
                    } else {
                        Vector3 xformOffset = new Vector3(x, 0, 0);
                        if (wordObject == null) {
                            wordCount++;
                            wordObject = new GameObject().transform;
                            wordObject.SetParent(lineObject, false);
                            wordObject.localPosition = xformOffset;
                            wordx = x;
                            if (!splitChars) {
                                boneId++;
                                bones.Add(wordObject);
                                Matrix4x4 mat = new Matrix4x4();
                                mat.SetTRS(-xformOffset, Quaternion.identity, Vector3.one);
                                bindposes.Add(mat);
                                bindposes[bindposes.Count() - 1].SetRow(3, new Vector4(x, 0, 0, 1));
                            }
                        }
                        if (splitChars) {
                            Transform charObject = new GameObject().transform;
                            charObject.SetParent(wordObject, false);
                            charObject.localPosition = xformOffset - new Vector3(wordx, 0, 0);
                            charObject.gameObject.name = "" + c + wordText.Length;
                            boneId++;
                            bones.Add(charObject);
                            Matrix4x4 mat = new Matrix4x4();
                            mat.SetTRS(-xformOffset, Quaternion.identity, Vector3.one);
                            bindposes.Add(mat);
                        }
                        wordText += c;
                    }
                }
                BoneWeight bw = new BoneWeight();
                bw.boneIndex0 = boneId;
                bw.weight0 = 1;
                CharacterInfo ci;
                if (nextGraphicUVs != null) {
                    c = 'X';
                }
                if (!fonts[currentFontIdx].GetCharacterInfo(c, out ci)) {
                    Debug.LogError("Font missing glyph for character '" + c + "'", fonts[currentFontIdx]);
                    continue;
                }
                if (c == ' ') {
                    x += ci.advance * spaceScale;
                    continue;
                } else if (c == wordSep) {
                    x = 0;
                    y -= lineHeight * lineHeightScale;
                    continue;
                }
                // Debug.Log("char '" + c + "' minX:" + ci.minX + ",Y:" + ci.minY + ", maxX:" + ci.maxX + ",Y:" + ci.maxY);
                // Debug.Log("ci.uvs" + ci.uvBottomLeft.x + "," + ci.uvBottomLeft.y + ";" + ci.uvTopLeft.x + "," + ci.uvTopLeft.y + ";" +
                //         "ci.uvs" + ci.uvBottomRight.x + "," + ci.uvBottomRight.y + ";" + ci.uvTopRight.x + "," + ci.uvTopRight.y);
                int offset = newPositions.Count();
                newIndices.Add(offset + 0);
                newIndices.Add(offset + 1);
                newIndices.Add(offset + 2);
                newIndices.Add(offset + 2);
                newIndices.Add(offset + 1);
                newIndices.Add(offset + 3);
                float uv3z = lineCount - 1.0f + Mathf.Min(1023.0f, i)/1024.0f;
                if (separator && (c == '_' || c == '|')) {
                    Vector2 uvMid = new Vector2(
                        (ci.uvBottomLeft.x + ci.uvTopRight.x) / 2,
                        (ci.uvBottomLeft.y + ci.uvTopRight.y) / 2);
                    ci.uvBottomRight = ci.uvBottomLeft = ci.uvTopRight = ci.uvTopLeft = uvMid;
                }
                bool doUVScale = true;
                if (nextGraphicUVs != null) {
                    doUVScale = false;
                    ci = new CharacterInfo();
                    ci.advance = (int)(lineHeight);
                    ci.minX = 0;
                    ci.maxX = (int)(lineHeight);
                    ci.minY = 0;
                    ci.maxY = (int)(lineHeight);
                    ci.uvBottomLeft = new Vector2(nextGraphicUVs[0] - 10, nextGraphicUVs[1]);
                    ci.uvBottomRight = new Vector2(nextGraphicUVs[2] - 10, nextGraphicUVs[1]);
                    ci.uvTopRight = new Vector2(nextGraphicUVs[2] - 10, nextGraphicUVs[3]);
                    ci.uvTopLeft = new Vector2(nextGraphicUVs[0] - 10, nextGraphicUVs[3]);
                    nextGraphicUVs = null;
                }
                float uv3w = Mathf.Floor(blurSetting * 1024.0f) + Mathf.Clamp(thickSetting, 1.0f/1024.0f, 1023.0f/1024.0f);
                Vector3 newPos;
                newPositions.Add(newPos = angleMat.MultiplyPoint(new Vector3(x + (italic * sizeScale * ci.minY) + horizStretch * sizeScale * ci.minX, y + vertStretch * sizeScale * ci.minY, z)));
                newNormals.Add(zForwardNormal);
                newTangents.Add(xLeftTangent);
                newUV3.Add(new Vector4(newPos.x, newPos.y, uv3z, uv3w));
                newUV4.Add(currentUV4);
                newUV5Shadow.Add(shadowColorVec);
                newUV6Barycentric.Add(new Vector4(1,0,0,extra));
                newUV.Add(scaleUV(doUVScale, ci.uvBottomLeft, AtlasColumnsRows, currentFontIdx));
                newColor.Add(currentColor);
                newBoneWeights.Add(bw);
                newPositions.Add(newPos = angleMat.MultiplyPoint(new Vector3(x + (italic * sizeScale * ci.maxY) + (1 + corrupt) * horizStretch * sizeScale * ci.minX, y + vertStretch * sizeScale * ci.maxY, z)));
                newNormals.Add(zForwardNormal);
                newTangents.Add(xLeftTangent);
                newUV3.Add(new Vector4(newPos.x, newPos.y, uv3z, uv3w));
                newUV4.Add(currentUV4);
                newUV5Shadow.Add(shadowColorVec);
                newUV6Barycentric.Add(new Vector4(0,1,0,extra));
                newUV.Add(scaleUV(doUVScale, ci.uvTopLeft, AtlasColumnsRows, currentFontIdx));
                newColor.Add(currentColor);
                newBoneWeights.Add(bw);
                newPositions.Add(newPos = angleMat.MultiplyPoint(new Vector3(x + (italic * sizeScale * ci.minY) + horizStretch * sizeScale * ci.maxX, y + vertStretch * sizeScale * ci.minY, z)));
                newNormals.Add(zForwardNormal);
                newTangents.Add(xLeftTangent);
                newUV3.Add(new Vector4(newPos.x, newPos.y, uv3z, uv3w));
                newUV4.Add(currentUV4);
                newUV5Shadow.Add(shadowColorVec);
                newUV6Barycentric.Add(new Vector4(0,0,1,extra));
                newUV.Add(scaleUV(doUVScale, ci.uvBottomRight, AtlasColumnsRows, currentFontIdx));
                newColor.Add(currentColorRight);
                newBoneWeights.Add(bw);
                newPositions.Add(newPos = angleMat.MultiplyPoint(new Vector3(x + (italic * sizeScale * ci.maxY) + (1 + corrupt) * horizStretch * sizeScale * ci.maxX, y + vertStretch * sizeScale * ci.maxY, z)));
                newNormals.Add(zForwardNormal);
                newTangents.Add(xLeftTangent);
                newUV3.Add(new Vector4(newPos.x, newPos.y, uv3z, uv3w));
                newUV4.Add(currentUV4);
                newUV5Shadow.Add(shadowColorVec);
                newUV6Barycentric.Add(new Vector4(1,0,0,extra));
                newUV.Add(scaleUV(doUVScale, ci.uvTopRight, AtlasColumnsRows, currentFontIdx));
                newColor.Add(currentColorRight);
                newBoneWeights.Add(bw);
                x += advanceanglecos * sizeScale * horizStretch * ci.advance;
                y += advanceanglesin * sizeScale * horizStretch * ci.advance;
                currentColor = currentColorRight;
            }
            if (wordObject != null) {
                wordObject.gameObject.name = wordText + wordCount;
            }
            if (lineObject != null) {
                lineText = lineText != null && lineText.Length > 50 ? lineText.Substring(0, 50) : lineText;
                int curCount = 0;
                if (!duplicateLines.TryGetValue(lineText, out curCount)) {
                    lineObject.gameObject.name = lineText;
                    curCount = 1;
                } else {
                    lineObject.gameObject.name = lineText + " " + curCount;
                    curCount++;
                }
                duplicateLines[lineText] = curCount;
                Vector3 lineOffset = new Vector3(align * x, 0, 0);
                lineObject.localPosition = lineObject.localPosition + lineOffset;
                if (wordObject != null) {
                    for (int i = 0; i < lineObject.childCount; i++) {
                        lineObject.GetChild(i).localPosition -= lineOffset;
                    }
                } else {
                    Matrix4x4 mat = new Matrix4x4();
                    mat.SetTRS(-lineOffset, Quaternion.identity, Vector3.one);
                    bindposes[bindposes.Count() - 1] = mat;
                }
                Undo.RegisterCreatedObjectUndo(lineObject.gameObject, lineText);
            }
            lineObject = null;
            wordObject = null;
        }

        Mesh newMesh = new Mesh ();
        newMesh.vertices = newPositions.ToArray();
        newMesh.normals = newNormals.ToArray();
        newMesh.tangents = newTangents.ToArray();
        newMesh.SetUVs (0, newUV);
        newMesh.SetUVs (2, newUV3);
        newMesh.SetUVs (3, newUV4);
        newMesh.SetUVs (4, newUV5Shadow);
        newMesh.SetUVs (5, newUV6Barycentric);
        newMesh.colors = newColor.ToArray();
        newMesh.bindposes = bindposes.ToArray();
        newMesh.boneWeights = newBoneWeights.ToArray();
        newMesh.subMeshCount = 1;
        newMesh.SetIndices (newIndices.ToArray(), MeshTopology.Triangles, 0);
        newMesh.RecalculateBounds();
        //newMesh.bounds = new Bounds(new Vector3(), new Vector3(1, 1, 1));
        newMesh.name = Path.GetFileNameWithoutExtension(stringFilename) + "_text";
        Mesh meshAfterUpdate = newMesh;
        Mesh meshAfterUpdateBaked = new Mesh();
        meshAfterUpdateBaked.name = Path.GetFileNameWithoutExtension(stringFilename) + "_baked";
        SkinnedMeshRenderer smr = rootTransform.gameObject.GetOrAddComponent<SkinnedMeshRenderer>();
        Undo.RecordObject (smr, "Built Font Mesh");
        smr.bones = bones.ToArray();
        smr.sharedMesh = newMesh;
        smr.rootBone = rootTransform;
        string fileName = Path.GetDirectoryName(stringFilename) + "/" + newMesh.name + ".asset";
        AssetDatabase.CreateAsset (meshAfterUpdate, fileName);
        smr.BakeMesh(meshAfterUpdateBaked);
        meshAfterUpdateBaked.RecalculateBounds();
        fileName = Path.GetDirectoryName(stringFilename) + "/" + meshAfterUpdateBaked.name + ".asset";
        AssetDatabase.CreateAsset (meshAfterUpdateBaked, fileName);

        var msdfAtlasMatPath = dirPath + "_atlas.mat";
        var mater = AssetDatabase.LoadAssetAtPath<Material>(msdfAtlasMatPath);
        Debug.Log("Found material " + mater.name);
        if (mater == null) {
            Shader s = Shader.Find("FontMeshGen/MSDFText");
            if (s == null) {
                Debug.LogError("Unable to find FontMeshGen/MSDFText shader");
            } else {
                mater = new Material(s);
                mater.SetTexture("_MSDFTex", newAtlas);
                AssetDatabase.CreateAsset(mater, msdfAtlasMatPath);
            }
        }
        AssetDatabase.SaveAssets ();
        if (smr.sharedMaterials.Length == 0 || smr.sharedMaterials[0] == null) {
            smr.sharedMaterials = new Material[]{mater};
        }

        EditorGUIUtility.PingObject(meshAfterUpdateBaked);

    }

    private Vector2 scaleUV(bool doUVScale, Vector2 origUV, Vector2Int AtlasColumnsRows, int fontIdx) {
        if (!doUVScale) {
            return origUV;
        }
        int xPos = fontIdx % AtlasColumnsRows.x;
        int yPos = fontIdx / AtlasColumnsRows.x;
        return new Vector2(
            (origUV.x + xPos) / AtlasColumnsRows.x,
            (origUV.y + yPos) / AtlasColumnsRows.y
        );
    }
}
