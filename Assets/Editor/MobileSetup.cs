using UnityEditor;
using UnityEngine;

// Telefon uchun sozlamalar: vertikal ekran, IL2CPP/ARM64 va URP Lit shaderini build'ga qo'shish.
// Loyiha ochilganda bir marta o'zi ishlaydi; qayta ishlatish: menyu Royal Run > Mobil sozlamalar.
[InitializeOnLoad]
public static class MobileSetup
{
    const string Flag = "RoyalRunMobileSetupDone2";

    static MobileSetup()
    {
        if (!SessionState.GetBool(Flag, false) && PlayerPrefs.GetInt(Flag, 0) == 0)
            EditorApplication.delayCall += Apply;
    }

    [MenuItem("Royal Run/Mobil sozlamalar")]
    static void Apply()
    {
        PlayerPrefs.SetInt(Flag, 1);
        SessionState.SetBool(Flag, true);

        PlayerSettings.productName = "Royal Run";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.royalrun.game");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        // build'da Shader.Find ishlashi uchun URP Lit shaderi har doim kiritilgan bo'lsin
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
        if (shader != null && gs != null)
        {
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            bool has = false;
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader) has = true;
            if (!has)
            {
                arr.arraySize++;
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = shader;
                so.ApplyModifiedProperties();
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Royal Run: mobil sozlamalar qo'llandi (vertikal ekran, IL2CPP, ARM64, URP Lit shader).");
    }
}
