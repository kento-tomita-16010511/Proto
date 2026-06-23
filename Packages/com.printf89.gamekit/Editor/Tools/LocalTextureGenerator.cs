using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class LocalTextureGenerator : EditorWindow
{
    private int textureSize = 512;
    private float noiseScale = 10f;
    private Color baseColor = new Color(0.3f, 0.4f, 0.3f); // モスグリーン風
    private Color detailColor = new Color(0.1f, 0.15f, 0.1f);

    [MenuItem("Tools/完全ローカル テクスチャ生成器")]
    public static void ShowWindow()
    {
        GetWindow<LocalTextureGenerator>("Texture Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Local Texture Generator (通信ゼロ・サ終なし)", EditorStyles.boldLabel);

        textureSize = EditorGUILayout.IntField("Size", textureSize);
        noiseScale = EditorGUILayout.Slider("Noise Scale", noiseScale, 1f, 50f);
        baseColor = EditorGUILayout.ColorField("Base Color", baseColor);
        detailColor = EditorGUILayout.ColorField("Detail Color", detailColor);

        if (GUILayout.Button("Generate and Import"))
        {
            GenerateAndSaveTexture();
        }
    }

    private void GenerateAndSaveTexture()
    {
        Debug.Log("テクスチャをローカルで計算中...");

        // 1. Texture2Dをメモリ上に新規作成
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, true);

        // 2. パーリンノイズを使ってシームレスな模様を計算
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                // シームレス（ループ）にするためのノイズ計算
                float u = (float)x / textureSize;
                float v = (float)y / textureSize;

                float nx = Mathf.Cos(u * Mathf.PI * 2) * noiseScale;
                float ny = Mathf.Sin(u * Mathf.PI * 2) * noiseScale;
                float nz = Mathf.Cos(v * Mathf.PI * 2) * noiseScale;
                float nw = Mathf.Sin(v * Mathf.PI * 2) * noiseScale;

                float noiseValue = Mathf.PerlinNoise(nx + 100, ny + nz + nw);

                // 色を線形補間
                Color finalColor = Color.Lerp(baseColor, detailColor, noiseValue);
                texture.SetPixel(x, y, finalColor);
            }
        }

        texture.Apply();

        // 3. PNGとしてローカル保存
        byte[] bytes = texture.EncodeToPNG();
        string folderPath = "Assets/GeneratedTextures/";
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        string fileName = $"tex_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        string fullPath = Path.Combine(folderPath, fileName);

        File.WriteAllBytes(fullPath, bytes);
        DestroyImmediate(texture); // メモリ解放

        AssetDatabase.Refresh();

        // 4. インポート設定の自動化（リピートできるように設定）
        ApplyTextureSettings(fullPath);

        Debug.Log($"完全ローカルでテクスチャを生成しました: {fullPath}");
    }

    private void ApplyTextureSettings(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat; // タイリング（繰り返し）可能に
            importer.filterMode = FilterMode.Bilinear;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }
    }
}