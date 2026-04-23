using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ShopItemData))]
public class ShopItemDataEditor : Editor
{
    private SerializedProperty itemNameProp;
    private SerializedProperty categoryProp;
    private SerializedProperty itemIconProp;
    private SerializedProperty inGameAmmoIconProp;
    private SerializedProperty iconSizeProp;
    private SerializedProperty itemPriceProp;
    private SerializedProperty itemDescriptionProp;

    // Gun Settings
    private SerializedProperty gunDamageProp;
    private SerializedProperty shootSoundProp;
    private SerializedProperty recoilUpProp;
    private SerializedProperty recoilSideProp;
    private SerializedProperty hasPenetrationProp;

    // Scope Settings
    private SerializedProperty zoomMultiplierProp;
    private SerializedProperty fogStartDistanceProp;
    private SerializedProperty fogEndDistanceProp;
    private SerializedProperty batteryEfficiencyMultiplierProp;

    // AggroAmmo Settings
    private SerializedProperty effectDelayProp;
    private SerializedProperty smokeDelayProp;
    private SerializedProperty effectDurationProp;
    private SerializedProperty smokeFadeOutDurationProp;
    private SerializedProperty aggroRadiusProp;
    private SerializedProperty circleRadiusProp;
    private SerializedProperty luredWalkSpeedProp;
    private SerializedProperty explosionSFXProp;
    private SerializedProperty explosionVolumeProp;
    private SerializedProperty explosionParticlePrefabProp;
    private SerializedProperty explosionScaleProp;
    private SerializedProperty smokeSFXProp;
    private SerializedProperty smokeVolumeProp;
    private SerializedProperty smokeParticlePrefabProp;
    private SerializedProperty smokeScaleProp;

    private void OnEnable()
    {
        // Item Basic Info
        itemNameProp = serializedObject.FindProperty("itemName");
        categoryProp = serializedObject.FindProperty("category");
        itemIconProp = serializedObject.FindProperty("itemIcon");
        inGameAmmoIconProp = serializedObject.FindProperty("inGameAmmoIcon");
        iconSizeProp = serializedObject.FindProperty("iconSize");
        itemPriceProp = serializedObject.FindProperty("itemPrice");

        // Item Details
        itemDescriptionProp = serializedObject.FindProperty("itemDescription");

        // Gun Settings
        gunDamageProp = serializedObject.FindProperty("gunDamage");
        shootSoundProp = serializedObject.FindProperty("shootSound");
        recoilUpProp = serializedObject.FindProperty("recoilUp");
        recoilSideProp = serializedObject.FindProperty("recoilSide");
        hasPenetrationProp = serializedObject.FindProperty("hasPenetration");

        // Scope Settings
        zoomMultiplierProp = serializedObject.FindProperty("zoomMultiplier");
        fogStartDistanceProp = serializedObject.FindProperty("fogStartDistance");
        fogEndDistanceProp = serializedObject.FindProperty("fogEndDistance");
        batteryEfficiencyMultiplierProp = serializedObject.FindProperty("batteryEfficiencyMultiplier");

        // AggroAmmo Settings
        effectDelayProp              = serializedObject.FindProperty("effectDelay");
        smokeDelayProp               = serializedObject.FindProperty("smokeDelay");
        effectDurationProp           = serializedObject.FindProperty("effectDuration");
        smokeFadeOutDurationProp     = serializedObject.FindProperty("smokeFadeOutDuration");
        aggroRadiusProp              = serializedObject.FindProperty("aggroRadius");
        circleRadiusProp             = serializedObject.FindProperty("circleRadius");
        luredWalkSpeedProp           = serializedObject.FindProperty("luredWalkSpeed");
        explosionSFXProp             = serializedObject.FindProperty("explosionSFX");
        explosionVolumeProp          = serializedObject.FindProperty("explosionVolume");
        explosionParticlePrefabProp  = serializedObject.FindProperty("explosionParticlePrefab");
        explosionScaleProp           = serializedObject.FindProperty("explosionScale");
        smokeSFXProp                 = serializedObject.FindProperty("smokeSFX");
        smokeVolumeProp              = serializedObject.FindProperty("smokeVolume");
        smokeParticlePrefabProp      = serializedObject.FindProperty("smokeParticlePrefab");
        smokeScaleProp               = serializedObject.FindProperty("smokeScale");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // --- Item Basic Info ---
        EditorGUILayout.LabelField("Item Basic Info", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(itemNameProp);
        EditorGUILayout.PropertyField(categoryProp);
        EditorGUILayout.PropertyField(itemIconProp);
        EditorGUILayout.PropertyField(inGameAmmoIconProp);
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
            EditorGUILayout.PropertyField(recoilUpProp);
            EditorGUILayout.PropertyField(recoilSideProp);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Special Abilities", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(hasPenetrationProp, new GUIContent("Penetration (관통탄)",
                "체크 시 탄이 뮤턴트/몬스터를 관통하여 뒤에 있는 적들도 연속으로 타격합니다."));
        }

        // --- Scope Settings ---
        if (categoryProp.enumValueIndex == (int)ItemCategory.Scope)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scope Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(zoomMultiplierProp);
            EditorGUILayout.PropertyField(fogStartDistanceProp);
            EditorGUILayout.PropertyField(fogEndDistanceProp);
            EditorGUILayout.PropertyField(batteryEfficiencyMultiplierProp);
        }

        // --- AggroAmmo Settings ---
        if (categoryProp.enumValueIndex == (int)ItemCategory.AggroAmmo)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Aggro Ammo Settings", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Timing", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(effectDelayProp,          new GUIContent("Explosion Delay (s)", "착탄 후 폭발까지 대기 시간"));
            EditorGUILayout.PropertyField(smokeDelayProp,           new GUIContent("Smoke Delay (s)",     "폭발 후 연막 시작까지 추가 대기 시간"));
            EditorGUILayout.PropertyField(effectDurationProp,       new GUIContent("Effect Duration (s)", "연막 지속 시간"));
            EditorGUILayout.PropertyField(smokeFadeOutDurationProp, new GUIContent("Smoke Fade Out (s)",  "연막 서서히 사라지는 시간"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Aggro Range", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(aggroRadiusProp,      new GUIContent("Aggro Radius (m)",       "어그로가 끽리는 반경"));
            EditorGUILayout.PropertyField(circleRadiusProp,     new GUIContent("Circle Radius (m)",      "도착 후 순찰 반경"));
            EditorGUILayout.PropertyField(luredWalkSpeedProp,   new GUIContent("Lured Walk Speed",       "접근 속도"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Explosion (1-shot at Explosion Delay)", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(explosionSFXProp,            new GUIContent("Explosion SFX"));
            EditorGUILayout.PropertyField(explosionVolumeProp,         new GUIContent("Explosion Volume",  "볼륨 (0~1)"));
            EditorGUILayout.PropertyField(explosionParticlePrefabProp, new GUIContent("Explosion Particle"));
            EditorGUILayout.PropertyField(explosionScaleProp,          new GUIContent("Explosion Scale",   "파티클 크기 배율 (1=원본, 2=두배)"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Smoke (loop, starts at Explosion+Smoke Delay)", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(smokeSFXProp,            new GUIContent("Smoke SFX"));
            EditorGUILayout.PropertyField(smokeVolumeProp,         new GUIContent("Smoke Volume",      "볼륨 (0~1)"));
            EditorGUILayout.PropertyField(smokeParticlePrefabProp, new GUIContent("Smoke Particle"));
            EditorGUILayout.PropertyField(smokeScaleProp,          new GUIContent("Smoke Scale",       "파티클 크기 배율 (1=원본, 2=두배)"));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
