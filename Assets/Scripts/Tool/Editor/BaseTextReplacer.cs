#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BaseTextReplacer
{
    private const string BasePrefabPath  = "Assets/Prefab/Common/BaseText.prefab";
    private const string BaseTextGuid    = "b24306b239e55fd43affb3181b1162cb";

    private static readonly string[] TargetScenes =
    {
        "Assets/Scenes/TitleScene.unity",
        "Assets/Scenes/MainScene.unity",
        "Assets/Scenes/ResultScene.unity",
    };

    [MenuItem("Tools/Replace All Texts with BaseText")]
    public static void ReplaceAll()
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
        if (basePrefab == null) { Debug.LogError("BaseText prefab not found: " + BasePrefabPath); return; }

        foreach (var sp in TargetScenes)
        {
            var scene = EditorSceneManager.OpenScene(sp, OpenSceneMode.Single);
            bool modified = false;

            // スナップショット取得（反復中に破棄されないよう List にコピー）
            var allTexts = new List<TextMeshProUGUI>(
                Object.FindObjectsOfType<TextMeshProUGUI>(true));

            foreach (var t in allTexts)
            {
                if (t == null || EditorUtility.IsPersistent(t)) continue;
                if (IsBaseTextInstance(t)) continue;

                modified |= ReplaceOne(t, basePrefab, scene.name);
            }

            // MainScene 専用: TimerController.timerDisplay を新 TimerText に再アサイン
            if (scene.name == "MainScene")
                modified |= AssignTimerDisplay();

            if (modified)
            {
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[BaseTextReplacer] Saved: " + scene.name);
            }
        }

        Debug.Log("[BaseTextReplacer] Done.");
    }

    private static bool ReplaceOne(TextMeshProUGUI t, GameObject basePrefab, string sceneName)
    {
        // --- 元 GO のプロパティを記録 ---
        var parent      = t.transform.parent;
        int sibIdx      = t.transform.GetSiblingIndex();
        string goName   = t.gameObject.name;
        bool goActive   = t.gameObject.activeSelf;

        string text             = t.text;
        float  fontSize         = t.fontSize;
        FontStyles fontStyle    = t.fontStyle;
        Color  color            = t.color;
        var    alignment        = t.alignment;
        var    overflow         = t.overflowMode;
        bool   autoSize         = t.enableAutoSizing;
        float  fontSizeMin      = t.fontSizeMin;
        float  fontSizeMax      = t.fontSizeMax;
        bool   wordWrap         = t.enableWordWrapping;

        var rt              = t.GetComponent<RectTransform>();
        var anchorMin       = rt.anchorMin;
        var anchorMax       = rt.anchorMax;
        var pivot           = rt.pivot;
        var anchoredPos     = rt.anchoredPosition;
        var sizeDelta       = rt.sizeDelta;

        bool parentHasLayout = parent != null && parent.GetComponent<LayoutGroup>() != null;

        // --- このテキストを参照している MonoBehaviour フィールドを収集 ---
        var refs = CollectReferences(t);

        // --- BaseText をインスタンス化して元 GO の場所に配置 ---
        var newGO  = PrefabUtility.InstantiatePrefab(basePrefab, parent) as GameObject;
        newGO.name = goName;
        newGO.SetActive(goActive);
        newGO.transform.SetSiblingIndex(sibIdx);

        // TMP プロパティをオーバーライド
        var newTMP = newGO.GetComponent<TextMeshProUGUI>();
        newTMP.text               = text;
        newTMP.fontSize           = fontSize;
        newTMP.fontStyle          = fontStyle;
        newTMP.color              = color;
        newTMP.alignment          = alignment;
        newTMP.overflowMode       = overflow;
        newTMP.enableAutoSizing   = autoSize;
        newTMP.fontSizeMin        = fontSizeMin;
        newTMP.fontSizeMax        = fontSizeMax;
        newTMP.enableWordWrapping = wordWrap;
        EditorUtility.SetDirty(newTMP);

        // RectTransform をオーバーライド
        var newRT          = newGO.GetComponent<RectTransform>();
        newRT.anchorMin    = anchorMin;
        newRT.anchorMax    = anchorMax;
        newRT.pivot        = pivot;
        newRT.anchoredPosition = anchoredPos;
        newRT.sizeDelta    = sizeDelta;
        EditorUtility.SetDirty(newRT);

        // LayoutGroup が無い親なら LayoutElement を無効化
        var le = newGO.GetComponent<LayoutElement>();
        if (le != null && !parentHasLayout)
        {
            le.ignoreLayout = true;
            EditorUtility.SetDirty(le);
        }

        // 参照を新 TMP に更新
        foreach (var (so, path) in refs)
        {
            so.FindProperty(path).objectReferenceValue = newTMP;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
        }

        Object.DestroyImmediate(t.gameObject);

        Debug.Log($"[{sceneName}] Replaced '{goName}' refs={refs.Count}");
        return true;
    }

    private static bool AssignTimerDisplay()
    {
        // CountdownPresenter GO にある TimerController の timerDisplay を TimerText に紐付け
        var cp = Object.FindObjectOfType<CountdownPresenter>(true);
        if (cp == null) { Debug.LogWarning("CountdownPresenter not found"); return false; }

        var tc = cp.GetComponent<TimerController>();
        if (tc == null) { Debug.LogWarning("TimerController not found on CountdownPresenter"); return false; }

        TextMeshProUGUI timerTMP = null;
        foreach (var tmp in Object.FindObjectsOfType<TextMeshProUGUI>(true))
        {
            if (tmp.gameObject.name == "TimerText") { timerTMP = tmp; break; }
        }
        if (timerTMP == null) { Debug.LogWarning("TimerText not found"); return false; }

        var so = new SerializedObject(tc);
        so.FindProperty("timerDisplay").objectReferenceValue = timerTMP;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(tc);
        Debug.Log("[MainScene] TimerController.timerDisplay → " + timerTMP.gameObject.name);
        return true;
    }

    private static List<(SerializedObject so, string path)> CollectReferences(TextMeshProUGUI target)
    {
        var result = new List<(SerializedObject, string)>();
        foreach (var mb in Object.FindObjectsOfType<MonoBehaviour>(true))
        {
            if (mb == null || EditorUtility.IsPersistent(mb)) continue;
            var so   = new SerializedObject(mb);
            var prop = so.GetIterator();
            prop.Next(true);
            while (prop.NextVisible(false))
            {
                if (prop.propertyType == SerializedPropertyType.ObjectReference
                    && prop.objectReferenceValue == (Object)target)
                {
                    result.Add((so, prop.propertyPath));
                }
            }
        }
        return result;
    }

    private static bool IsBaseTextInstance(TextMeshProUGUI t)
    {
        var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject);
        if (src == null) return false;
        string ap   = AssetDatabase.GetAssetPath(src);
        string guid = AssetDatabase.AssetPathToGUID(ap);
        return guid == BaseTextGuid;
    }
}
#endif
