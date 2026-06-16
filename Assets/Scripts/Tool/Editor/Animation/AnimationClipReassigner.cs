using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

public class AnimationClipReassigner : EditorWindow
{
    private AnimatorController animatorController;
    private AnimationClip[] animationClips;
    private Vector2 scrollPosition;

    [MenuItem("Tools/Animation Clip Reassigner")]
    public static void ShowWindow()
    {
        GetWindow<AnimationClipReassigner>("Animation Reassigner");
    }

    private void OnGUI()
    {
        GUILayout.Label("アニメーションクリップ 再割り当てツール", EditorStyles.largeLabel);
        EditorGUILayout.Space();

        // Animator Controllerの選択
        animatorController = EditorGUILayout.ObjectField(
            "Animator Controller",
            animatorController,
            typeof(AnimatorController),
            false
        ) as AnimatorController;

        EditorGUILayout.Space();

        if (GUILayout.Button("アニメーションクリップを自動割り当て", GUILayout.Height(40)))
        {
            if (animatorController == null)
            {
                EditorUtility.DisplayDialog("エラー", "Animator Controllerを選択してください", "OK");
                return;
            }

            ReassignAnimationClips();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space();

        if (GUILayout.Button("フォルダから全クリップをスキャン", GUILayout.Height(40)))
        {
            if (animatorController == null)
            {
                EditorUtility.DisplayDialog("エラー", "Animator Controllerを選択してください", "OK");
                return;
            }

            ScanAnimationClips();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("検出されたアニメーションクリップ", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

        if (animationClips != null && animationClips.Length > 0)
        {
            foreach (var clip in animationClips)
            {
                EditorGUILayout.LabelField($"  • {clip.name}", EditorStyles.label);
            }
        }
        else
        {
            EditorGUILayout.LabelField("  (クリップが検出されていません)", EditorStyles.helpBox);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "使い方：\n" +
            "1. Animator Controllerを選択\n" +
            "2. 「自動割り当て」ボタンをクリック\n" +
            "3. 同名のアニメーションクリップが自動的に割り当てられます\n\n" +
            "※ クリップ名がステート名と同じである必要があります",
            MessageType.Info
        );
    }

    private void ReassignAnimationClips()
    {
        if (animatorController == null) return;

        int reassignedCount = 0;
        int notFoundCount = 0;

        // 全てのレイヤーを走査
        foreach (var layer in animatorController.layers)
        {
            var stateMachine = layer.stateMachine;
            ReassignInStateMachine(stateMachine, ref reassignedCount, ref notFoundCount);
        }

        EditorUtility.DisplayDialog(
            "完了",
            $"処理完了\n\n再割り当て: {reassignedCount}個\n見つからなかった: {notFoundCount}個",
            "OK"
        );

        EditorUtility.SetDirty(animatorController);
        AssetDatabase.SaveAssets();
    }

    private void ReassignInStateMachine(AnimatorStateMachine stateMachine, ref int reassignedCount, ref int notFoundCount)
    {
        // 各ステートを処理
        foreach (var state in stateMachine.states)
        {
            string stateName = state.state.name;

            // 同名のアニメーションクリップを検索
            AnimationClip foundClip = FindAnimationClipByName(stateName);

            if (foundClip != null)
            {
                state.state.motion = foundClip;
                reassignedCount++;
                Debug.Log($"✓ '{stateName}' → '{foundClip.name}' を割り当てました");
            }
            else
            {
                notFoundCount++;
                Debug.LogWarning($"✗ '{stateName}' に対応するクリップが見つかりません");
            }
        }

        // サブステートマシンを再帰的に処理
        foreach (var subMachine in stateMachine.stateMachines)
        {
            ReassignInStateMachine(subMachine.stateMachine, ref reassignedCount, ref notFoundCount);
        }
    }

    private AnimationClip FindAnimationClipByName(string clipName)
    {
        // プロジェクト内の全アニメーションクリップを検索
        string[] guids = AssetDatabase.FindAssets($"{clipName} t:AnimationClip");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // FBX内のサブアセットも含めて検索するため、LoadAllAssetsAtPath を使用
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip && clip.name == clipName)
                {
                    return clip;
                }
            }
        }

        return null;
    }

    private void ScanAnimationClips()
    {
        List<AnimationClip> clips = new List<AnimationClip>();
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
        HashSet<string> processedPaths = new HashSet<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!processedPaths.Add(path)) continue;

            // FBXなどのファイル内に含まれる全クリップを取得するため、LoadAllAssetsAtPath を使用
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip)
                {
                    clips.Add(clip);
                }
            }
        }

        animationClips = clips.ToArray();
        Debug.Log($"{clips.Count}個のアニメーションクリップが見つかりました");
    }
}