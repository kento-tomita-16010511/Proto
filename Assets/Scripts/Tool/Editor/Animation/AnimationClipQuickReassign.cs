using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class AnimationClipQuickReassign
{
    [MenuItem("Assets/Animation/再割り当て - 同名クリップを割り当て")]
    private static void QuickReassignAnimationClips()
    {
        AnimatorController controller = Selection.activeObject as AnimatorController;

        if (controller == null)
        {
            EditorUtility.DisplayDialog("エラー", "Animator Controllerを選択してください", "OK");
            return;
        }

        int reassignedCount = 0;
        int notFoundCount = 0;
        int skipCount = 0;

        // 全てのレイヤーを走査
        foreach (var layer in controller.layers)
        {
            var stateMachine = layer.stateMachine;
            ProcessStateMachine(stateMachine, ref reassignedCount, ref notFoundCount, ref skipCount);
        }

        string message = $"処理完了！\n\n" +
                        $"✓ 再割り当て: {reassignedCount}個\n" +
                        $"✗ 見つからなかった: {notFoundCount}個\n" +
                        $"⊘ スキップ: {skipCount}個";

        EditorUtility.DisplayDialog("アニメーション再割り当て", message, "OK");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AnimationClipReassign] {message.Replace("\n", " ")}");
    }

    [MenuItem("Assets/Animation/手動割り当て - 選択クリップを全ステートに割り当て")]
    private static void ManualAssignToAllStates()
    {
        AnimationClip selectedClip = Selection.activeObject as AnimationClip;
        if (selectedClip == null)
        {
            EditorUtility.DisplayDialog("エラー", "アニメーションクリップを選択してください", "OK");
            return;
        }

        // Animator Controllerを別途選択する必要があるので、シーン内から探す
        Animator animator = Object.FindFirstObjectByType<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            EditorUtility.DisplayDialog("エラー", "シーン内にAnimatorを持つオブジェクトがありません", "OK");
            return;
        }

        AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null) return;

        int count = 0;
        foreach (var layer in controller.layers)
        {
            var stateMachine = layer.stateMachine;
            AssignClipToAllStates(stateMachine, selectedClip, ref count);
        }

        EditorUtility.DisplayDialog("完了", $"{count}個のステートに '{selectedClip.name}' を割り当てました", "OK");
    }

    private static void ProcessStateMachine(
        AnimatorStateMachine stateMachine,
        ref int reassignedCount,
        ref int notFoundCount,
        ref int skipCount)
    {
        foreach (var state in stateMachine.states)
        {
            // 既にクリップが割り当てられている場合はスキップ
            if (state.state.motion is AnimationClip existingClip && existingClip != null)
            {
                // 同じ名前のクリップが既に割り当てられていればスキップ
                if (existingClip.name == state.state.name)
                {
                    skipCount++;
                    continue;
                }
            }

            string stateName = state.state.name;
            AnimationClip foundClip = FindAnimationClipByName(stateName);

            if (foundClip != null)
            {
                state.state.motion = foundClip;
                reassignedCount++;
                Debug.Log($"✓ State '{stateName}' → Clip '{foundClip.name}'");
            }
            else
            {
                notFoundCount++;
                Debug.LogWarning($"✗ Clip not found for state '{stateName}'");
            }
        }

        // サブステートマシンを再帰処理
        foreach (var subMachine in stateMachine.stateMachines)
        {
            ProcessStateMachine(subMachine.stateMachine, ref reassignedCount, ref notFoundCount, ref skipCount);
        }
    }

    private static void AssignClipToAllStates(AnimatorStateMachine stateMachine, AnimationClip clip, ref int count)
    {
        foreach (var state in stateMachine.states)
        {
            state.state.motion = clip;
            count++;
        }

        foreach (var subMachine in stateMachine.stateMachines)
        {
            AssignClipToAllStates(subMachine.stateMachine, clip, ref count);
        }
    }

    private static AnimationClip FindAnimationClipByName(string clipName)
    {
        string[] guids = AssetDatabase.FindAssets($"{clipName} t:AnimationClip");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (clip != null && clip.name == clipName)
            {
                return clip;
            }
        }

        return null;
    }
}