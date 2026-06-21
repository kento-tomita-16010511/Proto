# CLAUDE.md — Unity プロジェクト コーディング規約

このファイルはプロジェクト全体に適用されるルールです。
新規ファイル作成・既存ファイル修正のいずれの場合も必ず従ってください。

---

## アーキテクチャ：MVPパターン

すべての機能は Model / View / Presenter の3層に分けて実装すること。

### 責務

| 層 | 役割 | 禁止事項 |
|------|------|---------|
| Model | データ・状態の保持（ScriptableObject 推奨） | MonoBehaviour への依存、表示操作 |
| View | UI表示・入力受付・イベント公開のみ | ゲームロジック、シーン遷移判断 |
| Presenter | ロジックの実行・ViewとModelの仲介 | `UnityEngine.UI` 型への直接参照（View経由のみ） |

### 依存の方向（厳守）

```
Presenter → View（メソッド呼び出し）
View      → Presenter（UniRx Subject / Observable で通知）
Presenter → Model（読み書き）
Model     ← Presenter のみ（View は Model を知らない）
```

### ディレクトリ構成

機能ごとにフォルダを切り、その中に Model / View / Presenter をまとめること。

```
Assets/Scripts/
├── Title/
│   ├── Model/       TitleState.cs など
│   ├── View/        TitleScreenView.cs など
│   └── Presenter/   TitlePresenter.cs など
├── Player/
│   ├── Model/       PlayerState.cs など
│   ├── View/        PlayerView.cs など
│   └── Presenter/   PlayerPresenter.cs など
├── Enemy/
│   ├── Model/
│   ├── View/
│   └── Presenter/
└── Common/          複数機能で共有するクラス（SceneLoader など）
    ├── Model/
    ├── View/
    └── Presenter/
```

新しい機能を追加する際は必ず対応する機能フォルダを作成し、既存フォルダに混在させないこと。

### ViewのイベントはUniRxで公開する

購読（イベント通知）が必要な場合は `System.Action` ではなく UniRx の `Subject` / `ReactiveProperty` を使うこと。

```csharp
using UniRx;

// ✅ 正しい：ボタンは AsObservable() で購読する
startButton.onClick
    .AsObservable()
    .Subscribe(_ => HandleStart())
    .AddTo(this);

// ✅ View側でイベントを公開する場合は Subject を使う
public IObservable<Unit> OnStartButtonClicked => _onStartButtonClicked;
private readonly Subject<Unit> _onStartButtonClicked = new Subject<Unit>();

void Start()
{
    // ボタンの購読も AsObservable() 経由で Subject に流す
    startButton.onClick
        .AsObservable()
        .Subscribe(_ => _onStartButtonClicked.OnNext(Unit.Default))
        .AddTo(this);
}

// Presenter は Subscribe で購読し、AddTo(this) でライフサイクルを紐付ける
view.OnStartButtonClicked
    .Subscribe(_ => HandleStart())
    .AddTo(this);

// ✅ 値の変化を監視する場合は ReactiveProperty
public IReadOnlyReactiveProperty<ScenePhase> CurrentPhase => _currentPhase;
private readonly ReactiveProperty<ScenePhase> _currentPhase = new ReactiveProperty<ScenePhase>(ScenePhase.Title);

// ❌ 禁止：AddListener を直接使う
startButton.onClick.AddListener(() => HandleStart());

// ❌ 禁止：Action イベントを新規で使う
public event Action OnStartButtonClicked;

// ❌ 禁止：View 内でシーン遷移などのロジックを直接呼ぶ
startButton.onClick.AsObservable().Subscribe(_ => SceneManager.LoadScene("Main"));
```

#### UniRx のリソース管理

購読は必ず `AddTo(this)` または `CompositeDisposable` で破棄すること。

```csharp
// ✅ MonoBehaviour の場合は AddTo(this)
view.OnStartButtonClicked
    .Subscribe(_ => HandleStart())
    .AddTo(this);

// ✅ 複数購読をまとめる場合は CompositeDisposable
private readonly CompositeDisposable _disposables = new CompositeDisposable();

void Start()
{
    view.OnStartButtonClicked
        .Subscribe(_ => HandleStart())
        .AddTo(_disposables);
}

void OnDestroy() => _disposables.Dispose();

// ❌ 禁止：AddTo / Dispose なしの購読（メモリリーク）
view.OnStartButtonClicked.Subscribe(_ => HandleStart());
```

---

## クラス種別の使い分け

### 命名と責務の対応

| 命名 | 構成 | 用途 | 禁止事項 |
|------|------|------|---------|
| `~~Manager` | シングルトン | 単機能・アプリ全体で1つだけ存在するもの | 複数責務を持つこと |
| `~~Utility` | staticクラス | メソッドの提供のみ。状態・データ構造を持たない | フィールド・プロパティでの状態保持 |
| `~~Model` | ScriptableObject | データ・状態の保持 | ロジック・表示操作 |
| `~~View` | MonoBehaviour | UI表示・入力受付 | ロジック・シーン遷移判断 |
| `~~Presenter` | MonoBehaviour | ロジック・View/Model仲介 | UnityEngine.UI への直接参照 |
| `~~Controller` | 禁止 | ロジックは `~~Presenter`、単機能シングルトンは `~~Manager` に寄せるため使わない | - |
| `~~Service` | 禁止 | `~~Manager` か `~~Utility` に統一するため使わない | - |

### Controller命名の禁止

`~~Controller` という命名のクラスは作成禁止とする。
MVP構成では Controller の役割が曖昧になり、Presenter と責務が重複するため。

```
やりたいこと → 使うクラス
├── ロジック・演出の制御         → ~~Presenter
├── 単機能・シングルトンの制御   → ~~Manager
└── 状態を持たない処理           → ~~Utility
```

```csharp
// ❌ 禁止：Controller命名
public class DissolveController : MonoBehaviour { ... }
public class PlayerController : MonoBehaviour { ... }

// ✅ 正しい：ロジックはPresenterに寄せる
public class EnemyPresenter : MonoBehaviour { /* Dissolve演出もここで制御 */ }
public class PlayerPresenter : MonoBehaviour { /* プレイヤー制御もここ */ }
```

### Manager（シングルトン）

単機能かつアプリ全体で1つだけ存在するものに使う。
複数の責務を持ち始めたら MVP パターンへの分割を検討すること。

```csharp
// ✅ 正しい：単機能のシングルトンとして実装
/// <summary>
/// サウンド再生を管理するシングルトンクラス。
/// BGM・SE の再生・停止のみを担当する。
/// </summary>
public class AudioManager : MonoBehaviour
{
    /// <summary>シングルトンインスタンス</summary>
    public static AudioManager Instance { get; private set; }

    /// <summary>BGM用AudioSource</summary>
    [SerializeField] private AudioSource _bgmSource;

    /// <summary>SE用AudioSource</summary>
    [SerializeField] private AudioSource _seSource;

    /// <summary>初期化：シングルトンの設定</summary>
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>SEを再生する</summary>
    public void PlaySE(AudioClip clip) => _seSource.PlayOneShot(clip);

    /// <summary>BGMを再生する</summary>
    public void PlayBGM(AudioClip clip)
    {
        _bgmSource.clip = clip;
        _bgmSource.Play();
    }

    /// <summary>BGMを停止する</summary>
    public void StopBGM() => _bgmSource.Stop();
}

// Presenterから呼ぶ
AudioManager.Instance.PlaySE(_defeatSE);

// ❌ 禁止：Managerに複数の責務を持たせる
public class GameManager : MonoBehaviour
{
    // サウンド・スコア・シーン遷移を1つに詰め込む → MVP化すること
}
```

### Utility（staticクラス）

メソッドの提供のみを目的とするクラス。
フィールド・プロパティでの状態・データ構造の保持は禁止。
データのやり取りは必ず引数と返り値で行うこと。

```csharp
// ✅ 正しい：引数・返り値でデータを受け渡す
/// <summary>
/// スコア計算に関するユーティリティクラス。
/// 状態を持たず、メソッドのみを提供する。
/// </summary>
public static class ScoreUtility
{
    /// <summary>敵の種類に応じたスコアを計算して返す</summary>
    /// <param name="enemyType">倒した敵の種類</param>
    /// <param name="comboCount">現在のコンボ数</param>
    /// <returns>加算するスコア</returns>
    public static int Calculate(EnemyType enemyType, int comboCount)
    {
        var baseScore = enemyType == EnemyType.Boss ? 100 : 10;
        return baseScore * comboCount;
    }
}

// ❌ 禁止：Utilityクラスがデータ・状態を保持する
public static class ScoreUtility
{
    private static int _totalScore = 0;      // ❌ 状態の保持
    public static List<int> ScoreLog = ...;  // ❌ データ構造の保持

    public static void AddScore(int amount)
    {
        _totalScore += amount;               // ❌ 内部状態を変更している
    }
}
```

### 既存のManagerクラスについて

動作中の既存Managerはすぐに削除しなくてよい。
ただし修正・機能追加の際は以下の基準で判断すること。

```
単機能のままか？
├── Yes → Managerとして維持してOK
└── No（複数責務が増えた）→ MVPパターンへ移行する
```

### 例外

サードパーティライブラリが提供するManager・Controllerクラスは本規約の対象外とする。
自分たちで新規作成するクラスにのみ適用する。

---

## アクセス修飾子：SerializeField を使うこと

外部スクリプトから参照しないフィールドは `public` にせず `[SerializeField] private` を使うこと。

```csharp
// ✅ 正しい
[SerializeField] private Button startButton;
[SerializeField] private CanvasGroup logoGroup;

// ❌ 禁止
public Button startButton;
public CanvasGroup logoGroup;
```

外部参照が必要な場合は `public` または `internal` を使い、その理由をコメントに明記すること。

```csharp
// 外部の Presenter から参照するため public
public IObservable<Unit> OnStartButtonClicked => _onStartButtonClicked;
```

---

## コメント：変数・関数・クラスすべてに記載すること

### クラス

```csharp
/// <summary>
/// タイトル画面のUI表示を担当するViewクラス。
/// ロジックは持たず、イベントの通知と表示操作のみを行う。
/// </summary>
public class TitleScreenView : MonoBehaviour
```

### フィールド・変数

```csharp
/// <summary>ロゴのフェードイン・アウトを制御するCanvasGroup</summary>
[SerializeField] private CanvasGroup logoGroup;

/// <summary>遷移中の連打を防ぐフラグ</summary>
private bool _isTransitioning = false;
```

### メソッド

```csharp
/// <summary>
/// ロゴをフェードインさせる。
/// </summary>
/// <param name="duration">フェードにかける秒数</param>
public async UniTask FadeLogoInAsync(float duration, CancellationToken ct)
```

### コメントの省略禁止

Unity のコールバック（`Awake` / `Start` など）にも必ずコメントを付けること。

```csharp
/// <summary>初期化：ボタンイベントを登録し、初期表示を非表示にする</summary>
void Start()
```

---


---

## 非同期処理：UniTask を使うこと

`Task`  `async Task` は使用禁止。非同期処理はすべて UniTask で実装すること。
`IEnumerator`  `Coroutine` も原則 UniTask に置き換えること。

```csharp
using Cysharp.Threading.Tasks;

 ✅ 正しい：UniTask で非同期メソッドを定義
public async UniTask FadeLogoInAsync(float duration, CancellationToken ct)
{
    float elapsed = 0f;
    while (elapsed  duration)
    {
        elapsed += Time.deltaTime;
        logoGroup.alpha = Mathf.Clamp01(elapsed  duration);
        await UniTask.Yield(PlayerLoopTiming.Update, ct);
    }
    logoGroup.alpha = 1f;
}

 ✅ シーンロードも UniTask で待機
await SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Additive)
    .ToUniTask(cancellationToken ct);

 ❌ 禁止
private async Task FadeAsync() { ... }    Task
private IEnumerator FadeCo() { ... }      Coroutine（新規実装）
```

### CancellationToken の扱い

UniTask メソッドには必ず `CancellationToken` を引数で受け取り、
MonoBehaviour の破棄時に自動キャンセルされるよう `this.GetCancellationTokenOnDestroy()` を渡すこと。

```csharp
 ✅ 呼び出し側
await view.FadeLogoInAsync(duration, this.GetCancellationTokenOnDestroy());

 ❌ CancellationToken なしは禁止（シーン破棄後に処理が継続する危険がある）
await view.FadeLogoInAsync(duration);
```

---

## コードスタイル：ネストの禁止とメソッド分割

### ネストは最大2階層まで

`if` / `for` / `foreach` などのネストは最大2階層までとする。
3階層以上になる場合は、早期リターン（ガード節）またはメソッド分割で解消すること。

```csharp
// ❌ 禁止：深いネスト
void HandleAttack()
{
    if (isAlive)
    {
        if (hasTarget)
        {
            if (isInRange)
            {
                DealDamage();
            }
        }
    }
}

// ✅ 正しい：ガード節で早期リターン
void HandleAttack()
{
    if (!isAlive) return;
    if (!hasTarget) return;
    if (!isInRange) return;

    DealDamage();
}
```

### 機能ごとに必ずメソッド化する

1つのメソッドは「1つの責務」のみを持つこと。
処理が複数の役割を持つ場合は、役割ごとにメソッドに分割すること。
目安として、1メソッドは20行以内に収めること。

```csharp
// ❌ 禁止：1メソッドに複数の処理を詰め込む
void OnEnemyDefeated()
{
    // スコア加算
    _score.Value += 10;
    scoreText.text = _score.Value.ToString();

    // エフェクト再生
    Instantiate(defeatEffect, enemy.transform.position, Quaternion.identity);
    AudioManager.Play(defeatSE);

    // 敵の削除
    enemy.gameObject.SetActive(false);
    _enemyList.Remove(enemy);
}

// ✅ 正しい：役割ごとにメソッドを分割する
void OnEnemyDefeated(Enemy enemy)
{
    AddScore(10);
    PlayDefeatEffect(enemy.transform.position);
    RemoveEnemy(enemy);
}

/// <summary>スコアを加算し、表示を更新する</summary>
private void AddScore(int amount)
{
    _score.Value += amount;
}

/// <summary>撃破エフェクトとSEを再生する</summary>
private void PlayDefeatEffect(Vector3 position)
{
    Instantiate(defeatEffect, position, Quaternion.identity);
    AudioManager.Play(defeatSE);
}

/// <summary>敵をリストから除外し、非アクティブにする</summary>
private void RemoveEnemy(Enemy enemy)
{
    _enemyList.Remove(enemy);
    enemy.gameObject.SetActive(false);
}
```

### LINQでコレクション処理をフラットに書く

`foreach` + `if` のネストは LINQ に置き換えてフラットにすること。

```csharp
// ❌ 禁止：foreach + if のネスト
foreach (var enemy in _enemyList)
{
    if (enemy.IsAlive)
    {
        enemy.TakeDamage(10);
    }
}

// ✅ 正しい：LINQ でフラットに書く
_enemyList
    .Where(e => e.IsAlive)
    .ToList()
    .ForEach(e => e.TakeDamage(10));
```

---

## その他の規約

- `Time.timeScale` はグローバルに影響するため原則使用禁止。停止が必要な場合は `Behaviour.enabled`  `Rigidbody.isKinematic` で個別に制御する
- `DontDestroyOnLoad` は使用禁止。常駐オブジェクトは `PersistentScene` に配置する
- シーンロードは必ず `LoadSceneAsync` を使用し、同期版 `LoadScene` は使わない
- `#if UNITY_EDITOR` で囲まない `UnityEditor` 名前空間の参照は禁止

## 更新処理：Update / FixedUpdate は使用禁止

`Update()` および `FixedUpdate()` は原則使用禁止とする。
フレームごとの状態監視・値の変化検知は UniRx の `ReactiveProperty` および `ObserveEveryValueChanged` で実装すること。

### 理由

- `Update` は毎フレーム全インスタンスで実行されパフォーマンスに影響する
- UniRx による変化検知は「変化があった時だけ処理が走る」ため無駄がない
- 処理の流れが宣言的になり、MVPパターンとの整合性が高まる

### 実装例

```csharp
// ✅ 正しい：ReactiveProperty で値の変化を監視する
private readonly ReactiveProperty<int> _score = new ReactiveProperty<int>(0);

void Start()
{
    // 値が変わった時だけ処理が走る
    _score
        .Subscribe(score => view.UpdateScoreText(score))
        .AddTo(this);
}

// ✅ 外部から値を変える（Presenterが呼ぶ）
public void AddScore(int amount) => _score.Value += amount;

// ✅ ObserveEveryValueChanged：外部オブジェクトの変化を監視する場合
enemy.ObserveEveryValueChanged(e => e.IsDead)
    .Where(isDead => isDead)
    .Subscribe(_ => HandleEnemyDead())
    .AddTo(this);

// ✅ 時間経過など「毎フレーム処理が必要な場合」は Observable.EveryUpdate を使う
Observable.EveryUpdate()
    .Where(_ => Input.GetKeyDown(KeyCode.Space))
    .Subscribe(_ => HandleJump())
    .AddTo(this);

// ❌ 禁止
void Update()
{
    if (Input.GetKeyDown(KeyCode.Space)) HandleJump();
}

// ❌ 禁止
void FixedUpdate()
{
    rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime);
}
```

### 例外（物理演算）

`Rigidbody` を使った物理演算（移動・力の付与）は `Observable.EveryFixedUpdate()` を使うこと。

```csharp
// ✅ 物理演算が必要な場合
Observable.EveryFixedUpdate()
    .Subscribe(_ => rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime))
    .AddTo(this);

// ❌ 禁止
void FixedUpdate()
{
    rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime);
}
```

---

## シーン管理規約

### 概要

すべてのシーンには必ず `~~Scene` という名前（例: TitleScene, MainScene, ResultScene）の
GameObjectを1つ配置し、BaseScene を継承した起点クラスをアタッチすること。
各シーンの実装は MVPパターンに従い、以下の3クラスに分割する。

- `~~SceneModel`     … シーンの状態・データ管理
- `~~SceneView`      … UI表示・入力受付・イベント公開
- `~~ScenePresenter` … ロジック・ViewとModelの仲介・シーン遷移の実行

---

### ディレクトリ構成

```
Assets/Scripts/
├── Title/
│   ├── Model/      TitleSceneModel.cs
│   ├── View/       TitleSceneView.cs
│   └── Presenter/  TitleScenePresenter.cs
├── Main/
│   ├── Model/
│   ├── View/
│   └── Presenter/
├── Result/
│   ├── Model/
│   ├── View/
│   └── Presenter/
└── Common/
    ├── BaseScene.cs
    ├── SceneLoader.cs
    ├── SceneType.cs
    └── ISceneLifecycle.cs
```

---

### SceneType（シーン列挙体）

シーン遷移の指定には文字列ではなく、必ず SceneType Enum を使用すること。
シーン追加時はここに追記し、BuildSettings にも必ず追加すること。

```csharp
/// <summary>
/// プロジェクト内の全シーンを定義するEnum。
/// </summary>
public enum SceneType
{
    Title,
    Main,
    Result,
}
```

---

### ISceneLifecycle インターフェース

フェードイン・フェードアウトのタイミングで処理を挟むため、
~~ScenePresenter は必ず ISceneLifecycle を実装すること。

```csharp
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// シーンのライフサイクルイベントを定義するインターフェース。
/// ~~ScenePresenter に実装し、BaseScene から委譲で呼ばれる。
/// </summary>
public interface ISceneLifecycle
{
    /// <summary>
    /// シーンフェードイン完了後に呼ばれる。
    /// BGM再生・初期アニメーションの開始などに使用する。
    /// </summary>
    UniTask OnAfterFadeInAsync(CancellationToken ct);

    /// <summary>
    /// シーンフェードアウト開始前に呼ばれる。
    /// SEの停止・後処理などに使用する。
    /// </summary>
    UniTask OnBeforeFadeOutAsync(CancellationToken ct);
}
```

---

### BaseScene（起点オブジェクト）

ロジックを持たず、Presenter への参照を保持して委譲するだけの薄いクラス。

```csharp
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

/// <summary>
/// 全シーンの起点となる基底クラス。
/// 各シーンに1つだけ存在する ~~Scene オブジェクトにアタッチする。
/// ロジックは持たず、~~ScenePresenter に委譲する。
/// </summary>
public abstract class BaseScene : MonoBehaviour
{
    /// <summary>このシーンのPresenter参照。Inspector から設定する</summary>
    [SerializeField] private MonoBehaviour _presenterBehaviour;

    /// <summary>ISceneLifecycle として Presenter を参照するプロパティ</summary>
    private ISceneLifecycle Presenter => _presenterBehaviour as ISceneLifecycle;

    /// <summary>
    /// フェードイン完了後に Presenter へ委譲する。
    /// </summary>
    public async UniTask OnAfterFadeInAsync(CancellationToken ct)
    {
        if (Presenter == null) return;
        await Presenter.OnAfterFadeInAsync(ct);
    }

    /// <summary>
    /// フェードアウト前に Presenter へ委譲する。
    /// </summary>
    public async UniTask OnBeforeFadeOutAsync(CancellationToken ct)
    {
        if (Presenter == null) return;
        await Presenter.OnBeforeFadeOutAsync(ct);
    }
}
```

---

### SceneLoader（シーン遷移ユーティリティ）

直接 SceneManager を呼ばず、必ずこのクラス経由で遷移すること。

```csharp
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.SceneManagement;

/// <summary>
/// シーン遷移を一元管理するユーティリティクラス。
/// </summary>
public static class SceneLoader
{
    /// <summary>
    /// 指定した SceneType のシーンを非同期でロードする。
    /// </summary>
    /// <param name="sceneType">遷移先シーン</param>
    /// <param name="ct">キャンセルトークン</param>
    public static async UniTask LoadAsync(SceneType sceneType, CancellationToken ct)
    {
        var sceneName = sceneType.ToString();
        await SceneManager.LoadSceneAsync(sceneName)
            .ToUniTask(cancellationToken: ct);
    }
}
```

---

### 各クラスの責務

| クラス | 基底 | 責務 | 禁止事項 |
|--------|------|------|---------|
| ~~SceneModel | ScriptableObject | データ・状態を ReactiveProperty で保持 | ロジック・表示操作 |
| ~~SceneView | MonoBehaviour | UI表示・イベントをSubjectで公開 | ロジック・シーン遷移判断 |
| ~~ScenePresenter | MonoBehaviour + ISceneLifecycle | ロジック実行・View/Model仲介・シーン遷移 | UnityEngine.UI への直接参照 |
| BaseScene | MonoBehaviour | Presenterへの委譲のみ | ロジックを自身で持つこと |

---

### TitleScene 実装例

#### TitleSceneModel.cs

```csharp
using UniRx;
using UnityEngine;

/// <summary>
/// タイトルシーンのデータ・状態を管理するModelクラス。
/// </summary>
[CreateAssetMenu(fileName = "TitleSceneModel", menuName = "Model/TitleSceneModel")]
public class TitleSceneModel : ScriptableObject
{
    /// <summary>ロゴのフェード完了フラグ</summary>
    public IReadOnlyReactiveProperty<bool> IsLogoVisible => _isLogoVisible;
    private readonly ReactiveProperty<bool> _isLogoVisible = new ReactiveProperty<bool>(false);

    /// <summary>ロゴ表示状態を設定する</summary>
    public void SetLogoVisible(bool visible) => _isLogoVisible.Value = visible;
}
```

#### TitleSceneView.cs

```csharp
using UniRx;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// タイトルシーンのUI表示・入力受付を担当するViewクラス。
/// ロジックは持たず、イベント通知と表示操作のみ行う。
/// </summary>
public class TitleSceneView : MonoBehaviour
{
    /// <summary>ゲームスタートボタン</summary>
    [SerializeField] private Button _startButton;

    /// <summary>ロゴのフェードを制御するCanvasGroup</summary>
    [SerializeField] private CanvasGroup _logoGroup;

    /// <summary>スタートボタン押下イベント</summary>
    public IObservable<Unit> OnStartButtonClicked => _onStartButtonClicked;
    private readonly Subject<Unit> _onStartButtonClicked = new Subject<Unit>();

    /// <summary>初期化：ボタンイベントを Subject に流す</summary>
    private void Start()
    {
        _startButton.onClick
            .AsObservable()
            .Subscribe(_ => _onStartButtonClicked.OnNext(Unit.Default))
            .AddTo(this);
    }

    /// <summary>ロゴのアルファ値を設定する</summary>
    public void SetLogoAlpha(float alpha) => _logoGroup.alpha = alpha;

    /// <summary>スタートボタンの表示を切り替える</summary>
    public void SetStartButtonVisible(bool visible) => _startButton.gameObject.SetActive(visible);
}
```

#### TitleScenePresenter.cs

```csharp
using Cysharp.Threading.Tasks;
using System.Threading;
using UniRx;
using UnityEngine;

/// <summary>
/// タイトルシーンのロジックを担当するPresenterクラス。
/// ISceneLifecycle を実装し、フェードイン・アウトのタイミングで処理を行う。
/// </summary>
public class TitleScenePresenter : MonoBehaviour, ISceneLifecycle
{
    /// <summary>タイトルシーンのView参照</summary>
    [SerializeField] private TitleSceneView _view;

    /// <summary>タイトルシーンのModel参照</summary>
    [SerializeField] private TitleSceneModel _model;

    /// <summary>初期化：ViewのイベントをSubscribeしてロジックに繋ぐ</summary>
    private void Start()
    {
        _view.OnStartButtonClicked
            .Subscribe(_ => OnStartButtonClickedAsync(this.GetCancellationTokenOnDestroy()).Forget())
            .AddTo(this);
    }

    /// <summary>
    /// フェードイン完了後の処理：ロゴを表示してスタートボタンを有効化する。
    /// </summary>
    public async UniTask OnAfterFadeInAsync(CancellationToken ct)
    {
        _view.SetLogoAlpha(1f);
        _view.SetStartButtonVisible(true);
        _model.SetLogoVisible(true);
        await UniTask.CompletedTask;
    }

    /// <summary>
    /// フェードアウト前の処理：ボタンを非表示にして後処理を行う。
    /// </summary>
    public async UniTask OnBeforeFadeOutAsync(CancellationToken ct)
    {
        _view.SetStartButtonVisible(false);
        await UniTask.CompletedTask;
    }

    /// <summary>
    /// スタートボタン押下時の処理：Mainシーンへ遷移する。
    /// </summary>
    private async UniTask OnStartButtonClickedAsync(CancellationToken ct)
    {
        await OnBeforeFadeOutAsync(ct);
        await SceneLoader.LoadAsync(SceneType.Main, ct);
    }
}
```

#### TitleScene.cs（BaseScene継承）

```csharp
using UnityEngine;

/// <summary>
/// タイトルシーンの起点オブジェクト。
/// TitleScene GameObject にアタッチし、Presenterへの参照を保持する。
/// ロジックは TitleScenePresenter に委譲する。
/// </summary>
public class TitleScene : BaseScene
{
}
```

---

### 規約まとめ

- 各シーンに `~~Scene` という名前の GameObject を **必ず1つだけ** 配置する
- `BaseScene` を継承したクラスをアタッチし、Inspector で `~~ScenePresenter` を参照させる
- ロジック・シーン遷移は `~~ScenePresenter` に集約し、`BaseScene` はロジックを持たない
- `ISceneLifecycle` は `~~ScenePresenter` が実装し、`BaseScene` は委譲するだけにする
- シーン遷移は `SceneLoader.LoadAsync(SceneType, ct)` 経由のみとし、直接 `SceneManager` を呼ばない
- `SceneType` Enum にシーンを追加した場合は BuildSettings にも必ず追加すること
- すべての非同期処理は UniTask + CancellationToken 必須
- 購読は必ず AddTo(this) または CompositeDisposable で破棄すること

---

## 共有アクション：Player/Enemy間で同じ振る舞いを使い回す

### 目的

「ジャンプ」「攻撃」「ダッシュ」など、複数のキャラクター種別（Player / Enemy / NPC 等）で
共通して使える振る舞いは、種別ごとに個別実装せず、**インターフェース1枚を実装するだけで
共通の Presenter / Model がそのまま使い回せる形**にすること。

新しいキャラクター種別を追加する際も、対応する `~~View` インターフェースを実装するだけで
既存の共有Presenterが流用でき、ロジックの重複・実装漏れを防ぐ。

### 構成

| 層 | 役割 | 備考 |
|------|------|------|
| `I~~ableModel`     | 能力（ジャンプ等）のロジック・状態を保持 | MonoBehaviour非依存、ScriptableObjectにしない（キャラ毎にインスタンスが必要なため） |
| `I~~View`           | 入力・トリガーをUniRxで公開し、結果の反映口を提供 | PlayerView・EnemyViewなど複数のViewが実装する |
| `~~Presenter`（共有） | Viewのイベントを購読し、Modelのメソッドを呼ぶ | Player/Enemy専用に分けず、同じ実装をどちらにも使う |

依存の方向は通常のMVP規約と同じ（`Presenter → View` 呼び出し、`View → Presenter` はUniRxで通知、`Presenter → Model` 読み書き）。

### 実装例：Jump機能をPlayer/Enemy共通にする

#### IJumpable.cs（Model側インターフェース）

```csharp
/// <summary>
/// ジャンプ能力を持つことを示すインターフェース。
/// Player・Enemyなど、ジャンプするキャラクターのModelはこれを実装する。
/// </summary>
public interface IJumpable
{
    /// <summary>ジャンプ処理を実行する</summary>
    void Jump();
}
```

#### JumpModel.cs

```csharp
using UniRx;
using UnityEngine;

/// <summary>
/// ジャンプの能力・パラメータを保持するModelクラス。
/// MonoBehaviourに依存せず、キャラクターごとにインスタンスを生成して使う。
/// </summary>
public class JumpModel : IJumpable
{
    /// <summary>ジャンプ力</summary>
    private readonly float _jumpPower;

    /// <summary>Viewへ力を伝える購読用Subject</summary>
    private readonly Subject<Vector3> _onForceApplied = new Subject<Vector3>();

    /// <summary>Viewが購読する力の発生通知</summary>
    public IObservable<Vector3> OnForceApplied => _onForceApplied;

    /// <summary>
    /// JumpModelを生成する。
    /// </summary>
    /// <param name="jumpPower">付与するジャンプ力</param>
    public JumpModel(float jumpPower)
    {
        _jumpPower = jumpPower;
    }

    /// <summary>ジャンプ処理を実行し、力の発生をViewへ通知する</summary>
    public void Jump()
    {
        _onForceApplied.OnNext(Vector3.up * _jumpPower);
    }
}
```

#### IJumpView.cs（View側インターフェース）

```csharp
using UniRx;
using UnityEngine;

/// <summary>
/// ジャンプ機能に必要なView側の公開イベントと操作口を定義するインターフェース。
/// PlayerView・EnemyViewなど、ジャンプ機能を持つViewはこれを実装する。
/// </summary>
public interface IJumpView
{
    /// <summary>ジャンプ実行のトリガー（入力・AI判断などが発行する）</summary>
    IObservable<Unit> OnJumpRequested { get; }

    /// <summary>Rigidbodyへ力を加える</summary>
    void ApplyJumpForce(Vector3 force);
}
```

#### JumpPresenter.cs（Player/Enemyで共有する）

```csharp
using UniRx;
using UnityEngine;

/// <summary>
/// ジャンプ機能のロジックを担当する共有Presenterクラス。
/// IJumpViewを実装したView（PlayerView・EnemyViewなど）であれば
/// キャラクター種別を問わずそのまま使い回せる。
/// </summary>
public class JumpPresenter
{
    /// <summary>紐付け対象のView</summary>
    private readonly IJumpView _view;

    /// <summary>ジャンプ能力を持つModel</summary>
    private readonly JumpModel _model;

    /// <summary>購読の破棄管理</summary>
    private readonly CompositeDisposable _disposables = new CompositeDisposable();

    /// <summary>
    /// JumpPresenterを生成し、Viewのイベントを購読する。
    /// </summary>
    /// <param name="view">紐付けるView</param>
    /// <param name="jumpPower">このキャラクターのジャンプ力</param>
    public JumpPresenter(IJumpView view, float jumpPower)
    {
        _view = view;
        _model = new JumpModel(jumpPower);

        // Viewからのトリガー → Modelのメソッドを呼ぶ（誰のViewでも同じ1行で済む）
        _view.OnJumpRequested
            .Subscribe(_ => _model.Jump())
            .AddTo(_disposables);

        // Modelの結果 → Viewへ反映
        _model.OnForceApplied
            .Subscribe(force => _view.ApplyJumpForce(force))
            .AddTo(_disposables);
    }

    /// <summary>購読を破棄する。生成元MonoBehaviourのOnDestroyから呼ぶこと</summary>
    public void Dispose() => _disposables.Dispose();
}
```

#### PlayerView.cs（入力がトリガー）

```csharp
using UniRx;
using UnityEngine;

/// <summary>
/// プレイヤーのUI表示・入力受付を担当するViewクラス。
/// ジャンプ機能はIJumpViewを実装することでJumpPresenterと連携する。
/// </summary>
public class PlayerView : MonoBehaviour, IJumpView
{
    /// <summary>ジャンプ力（Inspectorから設定）</summary>
    [SerializeField] private float _jumpPower = 5f;

    /// <summary>移動・ジャンプに使うRigidbody</summary>
    [SerializeField] private Rigidbody _rigidbody;

    /// <summary>ジャンプ実行のトリガー（スペースキー入力）</summary>
    public IObservable<Unit> OnJumpRequested => _onJumpRequested;
    private readonly Subject<Unit> _onJumpRequested = new Subject<Unit>();

    /// <summary>このViewに紐付くJumpPresenter</summary>
    private JumpPresenter _jumpPresenter;

    /// <summary>初期化：JumpPresenterを生成し、入力購読を開始する</summary>
    private void Start()
    {
        _jumpPresenter = new JumpPresenter(this, _jumpPower);

        Observable.EveryUpdate()
            .Where(_ => Input.GetKeyDown(KeyCode.Space))
            .Subscribe(_ => _onJumpRequested.OnNext(Unit.Default))
            .AddTo(this);
    }

    /// <summary>Rigidbodyへジャンプ力を加える</summary>
    public void ApplyJumpForce(Vector3 force)
    {
        Observable.EveryFixedUpdate()
            .Take(1)
            .Subscribe(_ => _rigidbody.AddForce(force, ForceMode.Impulse))
            .AddTo(this);
    }

    /// <summary>破棄時にPresenterの購読も解放する</summary>
    private void OnDestroy() => _jumpPresenter?.Dispose();
}
```

#### EnemyView.cs（AI判断がトリガー）

```csharp
using UniRx;
using UnityEngine;

/// <summary>
/// 敵のUI表示・AI判断結果の反映を担当するViewクラス。
/// PlayerViewと同じJumpPresenterを使い回し、トリガーのみAI判断に差し替える。
/// </summary>
public class EnemyView : MonoBehaviour, IJumpView
{
    /// <summary>ジャンプ力（Inspectorから設定、Playerと異なる値でよい）</summary>
    [SerializeField] private float _jumpPower = 3f;

    /// <summary>移動・ジャンプに使うRigidbody</summary>
    [SerializeField] private Rigidbody _rigidbody;

    /// <summary>ジャンプ実行のトリガー（AI判断が発行）</summary>
    public IObservable<Unit> OnJumpRequested => _onJumpRequested;
    private readonly Subject<Unit> _onJumpRequested = new Subject<Unit>();

    /// <summary>このViewに紐付くJumpPresenter</summary>
    private JumpPresenter _jumpPresenter;

    /// <summary>初期化：JumpPresenterを生成する</summary>
    private void Start()
    {
        _jumpPresenter = new JumpPresenter(this, _jumpPower);
    }

    /// <summary>AIロジック側から呼ばれ、ジャンプ要求を発行する</summary>
    public void RequestJump() => _onJumpRequested.OnNext(Unit.Default);

    /// <summary>Rigidbodyへジャンプ力を加える</summary>
    public void ApplyJumpForce(Vector3 force)
    {
        Observable.EveryFixedUpdate()
            .Take(1)
            .Subscribe(_ => _rigidbody.AddForce(force, ForceMode.Impulse))
            .AddTo(this);
    }

    /// <summary>破棄時にPresenterの購読も解放する</summary>
    private void OnDestroy() => _jumpPresenter?.Dispose();
}
```

### この形が規約に沿っている理由

- View → Presenter の通知は `Action` ではなく `IObservable<Unit>` / `Subject` で統一（UniRx規約に準拠）
- `Update` を直接使わず `Observable.EveryUpdate()`、物理演算は `Observable.EveryFixedUpdate()` を使用
- `JumpPresenter` は通常Presenterと異なりMonoBehaviourではなく**通常クラス**として実装し、PlayerView・EnemyViewそれぞれの `Start()` から生成する。これにより同一クラスをキャラクター種別を問わず使い回せる
- 購読は `CompositeDisposable` で管理し、生成元の `OnDestroy()` から `Dispose()` する
- 新しいキャラクター種別（Boss・NPC等）を追加する場合も、`IJumpView` を実装するだけで `JumpPresenter` がそのまま流用できる

### 他のアクションを追加する場合の命名規則

同じパターンで機能を増やす場合、以下の命名で揃えること。

```
I~~able      … Modelが実装する能力インターフェース（例: IAttackable, IDashable）
~~Model      … 能力のロジック・状態（例: AttackModel, DashModel）
I~~View      … Viewが実装するインターフェース（例: IAttackView, IDashView）
~~Presenter  … Player/Enemy等で共有する仲介役（例: AttackPresenter, DashPresenter）
```

ディレクトリは機能ごとの専用フォルダではなく `Common/` 配下にまとめ、
Player・Enemyなど複数の種別から参照される共有アクションであることを明示すること。

```
Assets/Scripts/
└── Common/
    └── Actions/
        ├── Jump/
        │   ├── IJumpable.cs
        │   ├── JumpModel.cs
        │   ├── IJumpView.cs
        │   └── JumpPresenter.cs
        └── Attack/
            ├── IAttackable.cs
            ├── AttackModel.cs
            ├── IAttackView.cs
            └── AttackPresenter.cs
```

---

## UIコンポーネント：必ずラッパーを使うこと

### 原則

Unity標準のUIコンポーネントを直接使用することを禁止する。
必ず対応するラッパークラス・Prefabを使うこと。
これはゲーム・アプリケーション問わず、本プロジェクトを元に作るすべての
プロダクトに適用する共通規約とする。

### コンポーネント対応表

| Unity標準 | ラッパー | 形式 | 主な役割 |
|-----------|---------|------|---------|
| `Button` | `CommonButton` | Prefab✅ | クリック音・連打防止・無効化スタイル統一 |
| `Image` | `CommonImage` | スクリプト | フェードイン/アウト・スプライト差し替えを統一 |
| `TextMeshProUGUI` | `CommonText` | スクリプト | フォント・カラー・サイズの統一管理 |
| `Slider` | `CommonSlider` | Prefab✅ | HP・音量バーなど共通スタイル |
| `CanvasGroup` | `CommonFade` | スクリプト | フェード処理を毎回書かずに済む |
| `Animator` | `CommonAnimator` | スクリプト | パラメータ名のタイポ防止・共通操作を統一 |
| `ScrollRect` | `CommonScrollView` | Prefab✅ | スクロール位置リセット・慣性設定を統一 |
| `Toggle` | `CommonToggle` | Prefab✅ | ON/OFFスタイル統一 |
| `InputField` | `CommonInputField` | Prefab✅ | バリデーション・プレースホルダー統一 |
| `AudioSource` | `AudioManager` | シングルトン | 直接触らずManager経由に統一 |

### Prefabの使い方

Prefab化されているものは、Hierarchyへの配置時に
必ずPrefabから生成すること。スクリプトから直接インスタンス化する場合も同様。

```csharp
// ✅ 正しい：PrefabをSerializeFieldで参照してInstantiate
/// <summary>CommonButtonのPrefab参照</summary>
[SerializeField] private CommonButton _buttonPrefab;

var button = Instantiate(_buttonPrefab, _parent);

// ❌ 禁止：Unity標準コンポーネントを直接生成・参照する
var button = new GameObject().AddComponent<Button>();
[SerializeField] private Button _button; // CommonButtonを使うこと
```

### スクリプトのみのラッパーの使い方

Prefab化されていないものは、GameObjectにアタッチして使うこと。

```csharp
// ✅ 正しい：Commonラッパーをアタッチして参照する
[SerializeField] private CommonImage _icon;
[SerializeField] private CommonText _scoreText;
[SerializeField] private CommonFade _fadePanel;
[SerializeField] private CommonAnimator _playerAnimator;

// ❌ 禁止：Unity標準を直接参照する
[SerializeField] private Image _icon;
[SerializeField] private TextMeshProUGUI _scoreText;
[SerializeField] private CanvasGroup _fadePanel;
[SerializeField] private Animator _playerAnimator;
```

### Commonに新しいラッパーを追加するタイミング

Unity標準コンポーネントを新たに使いたくなった場合は、
直接使わずに先にラッパーをCommonに追加してから使うこと。

```
新しいUIコンポーネントが必要になった
        ↓
Common にラッパークラスを作成する
        ↓
必要に応じてPrefab化する
        ↓
CLAUDE.md のコンポーネント対応表に追記する
        ↓
以降はラッパー経由で使う
```

---

## 新規プロダクト作成時のフロー

本プロジェクトを元に新しいゲーム・アプリケーションを作る際は、
以下のフローに従って進めること。

```
STEP 1｜Commonコンポーネントの移植
├── 以下をそのまま新プロジェクトにコピーする
│   ├── Assets/Scripts/Common/     （BaseScene・SceneLoader・SceneType等）
│   ├── Assets/Prefabs/Common/     （CommonButton・CommonSlider等のPrefab）
│   └── Assets/Scripts/UI/Common/  （CommonImage・CommonText・CommonFade等）
└── CLAUDE.md もコピーして規約を引き継ぐ

STEP 2｜SceneTypeの定義
├── Common/SceneType.cs を新プロダクト用に書き換える
│   例: Title / Main / Result → Title / Home / Game / Setting
└── BuildSettings にも同じシーンを追加する

STEP 3｜シーンの作成（1シーンずつ）
├── シーンファイルを作成する
├── ~~Scene という名前のGameObjectを1つ配置する
├── MVPの判断フローで構成を決める
│   ├── 状態・ロジック・表示が絡む → MVP化（Model / View / Presenter）
│   ├── 単機能・シングルトン       → ~~Manager
│   └── 状態を持たない処理         → ~~Utility（staticクラス）
└── ディレクトリを機能ごとに切る
    例: Assets/Scripts/Title/Model・View・Presenter

STEP 4｜UIの実装
├── Unity標準コンポーネントは直接使わない
├── Prefabがあるもの     → PrefabをHierarchyに配置またはInstantiate
├── スクリプトのみのもの → GameObjectにアタッチして[SerializeField]で参照
└── 新たに必要なUIが出たら Common に追加してから使う

STEP 5｜シーン遷移の確認
├── SceneLoader.LoadAsync(SceneType.~~, ct) 経由のみを使う
├── 各シーンに BaseScene 継承クラスが配置されているか確認する
└── ISceneLifecycle（OnAfterFadeInAsync / OnBeforeFadeOutAsync）が実装されているか確認する

STEP 6｜規約チェック（実装完了後に必ず確認）
├── Update / FixedUpdate が使われていないか
├── Manager命名で複数責務を持つクラスがないか
├── Controller / Service 命名のクラスを作っていないか
├── Utilityクラスが状態・データ構造を持っていないか
├── ネストが3階層以上になっていないか
├── Unity標準UIコンポーネントが直接参照されていないか
└── すべてのクラス・メソッド・フィールドにコメントがあるか
```