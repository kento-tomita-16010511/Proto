# AGENTS.md — Codex CLI 向け作業ガイド

本ファイルは Codex CLI(および他のAIエージェント)がこのリポジトリで作業する際の指示書です。
Claude Code と Codex CLI は本ファイルと `Docs/MapDesign.md` を介して連携します。

---

## 必読ドキュメント

| ファイル | 内容 |
|---|---|
| `CLAUDE.md` | プロジェクト全体のコーディング規約。**全エージェント必須遵守** |
| `Docs/MapDesign.md` | 廃病院マップの設計書。マップ関連の作業はこれが正 |

## コーディング規約の要点(詳細は CLAUDE.md)

- MVPパターン(Model=ScriptableObject / View / Presenter)。依存方向厳守
- `~~Controller` / `~~Service` 命名は禁止。ロジックは `~~Presenter`、staticクラスは `~~Utility`、シングルトンは `~~Manager`
- `~~Utility` は状態・データ構造の保持禁止(引数と返り値でやり取り)
- 非同期は UniTask + CancellationToken 必須(`Task` / `Coroutine` 禁止)
- `Update` / `FixedUpdate` 禁止。UniRx(`ReactiveProperty` / `Observable.EveryUpdate`)で実装
- ネストは最大2階層。ガード節・メソッド分割で解消。1メソッド20行以内目安
- すべてのクラス・メソッド・フィールドに `/// <summary>` コメント(日本語)
- フィールドは `[SerializeField] private`。クラス内メンバーの記述順序は CLAUDE.md 参照
- Unity標準UIコンポーネントの直接使用禁止(Common ラッパー経由)

---

## マップ生成タスクの役割分担

### Claude Code(リモート)側が担当済み

- マップ設計書 `Docs/MapDesign.md` の作成
- Editor拡張ツール `Assets/Scripts/Tool/Editor/MapGenerator/` の実装
  - `HospitalMapConfigModel` … 生成設定(ScriptableObject)
  - `HospitalMapLayoutUtility` … レイアウト定義・検証(設計書と1対1同期)
  - `HospitalMapGeneratorUtility` … GameObject生成ロジック
  - `PlaceholderPrefabUtility` … 仮Prefab(床・壁・ドア・天井)の自動生成
  - `HospitalMapGeneratorWindow` … ツールウィンドウ(Tools > Hospital Map Generator)

### Codex CLI(ローカル・Unity実行環境あり)側のタスク

1. **動作確認**: Unityで `Tools > Hospital Map Generator` を開き、設計書 4章の手順でマップを生成する
2. **Terrain置き換えの完了**: MainScene で生成→Terrain非アクティブ化→NavMesh再ベイク→動作確認→問題なければTerrain削除
3. **レイアウト調整**: 遊んでみて距離感・動線を調整する場合は、設計書 5章の手順で
   `Docs/MapDesign.md` と `HospitalMapLayoutUtility.cs` を**必ず両方**更新する
4. **見た目のクオリティアップ**: 仮Prefabを本番アセット(壁・床・ドアのモデル)に差し替える。
   生成ロジックは変更せず、`HospitalMapConfig.asset` のPrefab参照差し替えで対応する
5. **プレイヤー・敵配置の調整**: スポーン位置をマップ内(廊下・部屋)に移動する

### 禁止事項(マップ作業)

- `Docs/MapDesign.md` 1-5 の設計禁止事項(正方形だけの建物・一直線の廊下・左右対称・部屋同士の直結など)
- 設計書とレイアウトコードの片方だけを変更すること(必ず同期)
- ツールの「レイアウトを検証」でエラーが出る状態でのコミット

---

## ブランチ運用

- マップ生成関連の開発ブランチ: `claude/codex-map-reproducibility-qvwg63`
- コミットメッセージは日本語可。変更内容が分かる粒度でコミットすること
