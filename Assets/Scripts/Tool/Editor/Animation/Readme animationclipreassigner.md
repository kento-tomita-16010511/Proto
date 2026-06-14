# Unity アニメーションクリップ 自動再割り当てツール

モデル更新時にAnimator Controllerから外れたアニメーションクリップを自動的に再割り当てするツールです。

---

## 📦 ファイル構成

### 1. **AnimationClipReassigner.cs** （メインツール）
エディタウィンドウから操作できる管理ツール。

**使い方：**
```
メニュー → Tools → Animation Clip Reassigner
```

**手順：**
1. ウィンドウが開く
2. 「Animator Controller」フィールドに対象のコントローラーをドラッグ＆ドロップ
3. 「アニメーションクリップを自動割り当て」ボタンをクリック
4. 同名のアニメーションクリップが自動的に割り当てられます

**条件：** クリップ名 = ステート名

---

### 2. **AnimationClipQuickReassign.cs** （高速ツール）
右クリックメニューから直接実行できる便利ツール。

**使い方：**
```
Project → Animator Controller を右クリック
→ Animation → 再割り当て - 同名クリップを割り当て
```

**機能：**
- ✅ 既に割り当てられているクリップを検出（スキップ）
- ✅ 1クリックで完了
- ✅ ログで詳細を確認可能

---

## 🎯 実行フロー

```
モデルを新しくする
  ↓
Animator Controllerをインポート
  ↓
アニメーションクリップを配置
  ↓
このツールを実行 ← ここで自動割り当て
  ↓
完了！
```

---

## ⚙️ 重要な注意点

### ✅ クリップが正しく割り当てられるには：

```
ステート名 = アニメーションクリップ名
```

**例：**
- ステート名: `Idle`
- クリップ名: `Idle` ✓ OK
- クリップ名: `idle` ✗ NG（大文字小文字を区別）

### ⚠️ よくあるトラブル

| 問題 | 原因 | 解決方法 |
|------|------|--------|
| クリップが見つからない | 名前が違う | ステート名とクリップ名を統一 |
| スキップされた | 既に割り当てられている | 古いクリップを削除して実行 |
| 何も起こらない | Controllerが選択されてない | Assets フォルダから選択 |

---

## 🔧 カスタマイズ

### クリップの検索ロジックを変更したい場合

`FindAnimationClipByName()` メソッドを編集：

```csharp
// 現在：完全一致検索
if (clip != null && clip.name == clipName)

// 変更例：部分一致検索
if (clip != null && clip.name.Contains(clipName))
```

### 複数のモデル構成に対応

サブレイヤーごとにコントローラーが分かれている場合も自動で走査します。
（`ProcessStateMachine()` が再帰的に処理）

---

## 📊 ログ出力

実行時のログ例：

```
✓ State 'Idle' → Clip 'Idle'
✓ State 'Run' → Clip 'Run'
✓ State 'Jump' → Clip 'Jump'
✗ Clip not found for state 'Attack'

処理完了！
✓ 再割り当て: 3個
✗ 見つからなかった: 1個
⊘ スキップ: 0個
```

---

## 🚀 推奨ワークフロー

```
1. アニメーションクリップをフォルダに整理
   Assets/Animations/
   ├── Idle.anim
   ├── Run.anim
   ├── Jump.anim
   └── Attack.anim

2. Animator Controllerを作成・設定
   Assets/Controllers/
   └── PlayerController.controller
      ├── Idle (ステート)
      ├── Run (ステート)
      ├── Jump (ステート)
      └── Attack (ステート)

3. このツールで一括割り当て
   → 自動的に全てのステートに対応クリップが割り当てられます
```

---

## 💡 Tips

- **複数モデルを一度に処理したい？**
  → 「AnimationClipReassigner」のウィンドウを複数開いて、異なるControllerを割り当てれば可能

- **特定のレイヤーだけ処理したい？**
  → スクリプトの `controller.layers` ループを修正して、特定レイヤーのみ処理

- **バッチ処理したい？**
  → エディタメニューを追加してスクリプトを拡張可能

---

## 🐛 トラブルシューティング

### Q: 「Animator Controllerを選択してください」と表示される
**A:** Project ウィンドウから `.controller` ファイルを直接選択してください

### Q: ステート名が複数あり、複数のクリップが見つかる
**A:** 最初にヒットしたクリップが割り当てられます。
クリップを `Rename` してから実行してください

### Q: アンドゥ（Undo）したい
**A:** Ctrl+Z（Cmd+Z）で直前の処理を戻せます

---

**作成日:** 2026年
**対応Unity:** 2020.3 以上