using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ShopItemData))]
public class ShopItemDataEditor : Editor
{
    private SerializedProperty itemNameProp;
    private SerializedProperty categoryProp;
    private SerializedProperty itemIconProp;
    private SerializedProperty iconSizeProp;
    private SerializedProperty itemPriceProp;
    private SerializedProperty itemDescriptionProp;

    // Gun Settings
    private SerializedProperty gunDamageProp;
    private SerializedProperty shootSoundProp;
    private SerializedProperty maxAmmoInClipProp;
    private SerializedProperty recoilUpProp;
    private SerializedProperty recoilSideProp;

    // Scope Settings
    private SerializedProperty zoomMultiplierProp;
    private SerializedProperty fogStartDistanceProp;
    private SerializedProperty fogEndDistanceProp;
    private SerializedProperty batteryEfficiencyMultiplierProp;

    private void OnEnable()
    {
        // Item Basic Info
        itemNameProp = serializedObject.FindProperty("itemName");
        categoryProp = serializedObject.FindProperty("category");
        itemIconProp = serializedObject.FindProperty("itemIcon");
        iconSizeProp = serializedObject.FindProperty("iconSize");
        itemPriceProp = serializedObject.FindProperty("itemPrice");

        // Item Details
        itemDescriptionProp = serializedObject.FindProperty("itemDescription");

        // Gun Settings
        gunDamageProp = serializedObject.FindProperty("gunDamage");
        shootSoundProp = serializedObject.FindProperty("shootSound");
        maxAmmoInClipProp = serializedObject.FindProperty("maxAmmoInClip");
        recoilUpProp = serializedObject.FindProperty("recoilUp");
        recoilSideProp = serializedObject.FindProperty("recoilSide");

        // Scope Settings
        zoomMultiplierProp = serializedObject.FindProperty("zoomMultiplier");
        fogStartDistanceProp = serializedObject.FindProperty("fogStartDistance");
        fogEndDistanceProp = serializedObject.FindProperty("fogEndDistance");
        batteryEfficiencyMultiplierProp = serializedObject.FindProperty("batteryEfficiencyMultiplier");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // --- Item Basic Info ---
        EditorGUILayout.LabelField("Item Basic Info", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(itemNameProp);
        EditorGUILayout.PropertyField(categoryProp);
        EditorGUILayout.PropertyField(itemIconProp);
        EditorGUILayout.PropertyField(iconSizeProp);
        EditorGUILayout.PropertyField(itemPriceProp);

        EditorGUILayout.Space();

        // --- Item Details ---
        EditorGUILayout.LabelField("Item Details", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(itemDescriptionProp);

        // --- Gun Settings ---
        if (categoryProp.enumValueIndex == (int)ItemCategory.Gun)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Gun Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(gunDamageProp);
            EditorGUILayout.PropertyField(shootSoundProp);
            EditorGUILayout.PropertyField(maxAmmoInClipProp);
            EditorGUILayout.PropertyField(recoilUpProp);
            EditorGUILayout.PropertyField(recoilSideProp);
        }

        // --- Scope Settings ---
        // 여기서 현재 카테고리의 값이 ItemCategory.Scope 일 때만 아래 필드들을 그려줍니다.
        if (categoryProp.enumValueIndex == (int)ItemCategory.Scope)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scope Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(zoomMultiplierProp);
            EditorGUILayout.PropertyField(fogStartDistanceProp);
            EditorGUILayout.PropertyField(fogEndDistanceProp);
            EditorGUILayout.PropertyField(batteryEfficiencyMultiplierProp);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
