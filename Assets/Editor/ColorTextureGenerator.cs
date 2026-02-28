using UnityEngine;
using UnityEditor;

public class ColorTextureGenerator : EditorWindow
{
    private Color textureColor = Color.white;
    private string textureName = "NewColorTexture";
    private int textureResolution = 256;

    [MenuItem("Tools/Color Texture Generator")]
    public static void ShowWindow()
    {
        GetWindow<ColorTextureGenerator>("Texture Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Solid Color Texture Generator", EditorStyles.boldLabel);

        textureName = EditorGUILayout.TextField("Texture Name", textureName);
        textureColor = EditorGUILayout.ColorField("Color", textureColor);
        textureResolution = EditorGUILayout.IntField("Resolution (w/h)", textureResolution);

        if (GUILayout.Button("Generate Texture"))
        {
            GenerateAndSaveTexture();
        }
    }

    private void GenerateAndSaveTexture()
    {
        // Ensure resolution is reasonable
        if (textureResolution < 1) textureResolution = 1;
        if (textureResolution > 4096) textureResolution = 4096;

        // Create the texture
        Texture2D texture = new Texture2D(textureResolution, textureResolution, TextureFormat.RGBA32, false);
        
        // Fill texture with the selected color
        Color[] pixels = new Color[textureResolution * textureResolution];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = textureColor;
        }

        texture.SetPixels(pixels);
        texture.Apply();

        // Encode to PNG
        byte[] bytes = texture.EncodeToPNG();

        // Check if the Assets/GeneratedTextures folder exists, if not create it
        if (!AssetDatabase.IsValidFolder("Assets/GeneratedTextures"))
        {
            AssetDatabase.CreateFolder("Assets", "GeneratedTextures");
        }

        // Save the file
        string filePath = "Assets/GeneratedTextures/" + textureName + ".png";
        
        // Ensure unique name if file already exists
        int counter = 1;
        string originalName = textureName;
        while (AssetDatabase.LoadAssetAtPath<Texture2D>(filePath) != null)
        {
            textureName = originalName + "_" + counter;
            filePath = "Assets/GeneratedTextures/" + textureName + ".png";
            counter++;
        }

        System.IO.File.WriteAllBytes(filePath, bytes);

        // Refresh database to show the new asset in Unity
        AssetDatabase.Refresh();

        Debug.Log("Generated solid color texture saved to: " + filePath);
        
        // Focus on the newly created asset
        Texture2D newTex = AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);
        if (newTex != null)
        {
            EditorGUIUtility.PingObject(newTex);
            Selection.activeObject = newTex;
        }
    }
}