# Printf89 GameKit

Unity 用の汎用ゲーム基盤ライブラリ。プロジェクト非依存のコンポーネントを集約する。

## インストール（他プロジェクトから）

Package Manager → **Add package from git URL** に、このパッケージを切り出した Git リポジトリの URL を指定する。

```
https://github.com/<user>/com.printf89.gamekit.git
```

## 前提パッケージ

利用側プロジェクトに以下が導入されている必要がある（asmdef が名前参照しているため）。

| 依存 | 入手元 | asmdef 名 |
|------|--------|-----------|
| UniTask | `com.cysharp.unitask` (git) | `UniTask` |
| UniRx | Assets 直置き / OpenUPM | `UniRx` |
| uGUI | `com.unity.ugui` | `UnityEngine.UI` |

## 構成（予定）

```
Runtime/
├── Popup/      … PopupBase / PopupManager
├── Sound/      … SoundManager / BGMEnum / SEEnum
├── Effects/    … PlayerDeathEffectView 等
└── UI/         … CommonButton 等
Editor/
└── Tools/      … LocalTextureGenerator 等
```

> 注: DOTween を使うコンポーネント（例 CountdownView）は、DOTween に asmdef が無い環境では参照できない。移設する場合は DOTween 側へ asmdef を付与する必要がある。

## 名前空間

- Runtime: `Printf89.GameKit`
- Editor: `Printf89.GameKit.Editor`
