# CLAUDE.md — Unity プロジェクト コーディング規約

このファイルはプロジェクト全体に適用されるルールです。
新規ファイル作成・既存ファイル修正のいずれの場合も必ず従ってください。

---

## アーキテクチャ：MVPパターン

すべての機能は Model  View  Presenter の3層に分けて実装すること。

### 責務

 層  役割  禁止事項 
---------
 Model  データ・状態の保持（ScriptableObject 推奨）  MonoBehaviour への依存、表示操作 
 View  UI表示・入力受付・イベント公開のみ  ゲームロジック、シーン遷移判断 
 Presenter  ロジックの実行・ViewとModelの仲介  `UnityEngine.UI` 型への直接参照（View経由のみ） 

### 依存の方向（厳守）

```
Presenter → View（メソッド呼び出し）
View      → Presenter（UniRx Subject  Observable で通知）
Presenter → Model（読み書き）
Model     ← Presenter のみ（View は Model を知らない）
```

### ディレクトリ構成

機能ごとにフォルダを切り、その中に Model  View  Presenter をまとめること。

```
AssetsScripts
├── Title
│   ├── Model       TitleState.cs など
│   ├── View        TitleScreenView.cs など
│   └── Presenter   TitlePresenter.cs など
├── Player
│   ├── Model       PlayerState.cs など
│   ├── View        PlayerView.cs など
│   └── Presenter   PlayerPresenter.cs など
├── Enemy
│   ├── Model
│   ├── View
│   └── Presenter
└── Common          複数機能で共有するクラス（SceneLoader など）
    ├── Model
    ├── View
    └── Presenter
```

新しい機能を追加する際は必ず対応する機能フォルダを作成し、既存フォルダに混在させないこと。

### ViewのイベントはUniRxで公開する

購読（イベント通知）が必要な場合は `System.Action` ではなく UniRx の `Subject`  `ReactiveProperty` を使うこと。

```csharp
using UniRx;

 ✅ 正しい：ボタンは AsObservable() で購読する
startButton.onClick
    .AsObservable()
    .Subscribe(_ = HandleStart())
    .AddTo(this);

 ✅ View側でイベントを公開する場合は Subject を使う
public IObservableUnit OnStartButtonClicked = _onStartButtonClicked;
private readonly SubjectUnit _onStartButtonClicked = new SubjectUnit();

void Start()
{
     ボタンの購読も AsObservable() 経由で Subject に流す
    startButton.onClick
        .AsObservable()
        .Subscribe(_ = _onStartButtonClicked.OnNext(Unit.Default))
        .AddTo(this);
}

 Presenter は Subscribe で購読し、AddTo(this) でライフサイクルを紐付ける
view.OnStartButtonClicked
    .Subscribe(_ = HandleStart())
    .AddTo(this);

 ✅ 値の変化を監視する場合は ReactiveProperty
public IReadOnlyReactivePropertyScenePhase CurrentPhase = _currentPhase;
private readonly ReactivePropertyScenePhase _currentPhase = new ReactivePropertyScenePhase(ScenePhase.Title);

 ❌ 禁止：AddListener を直接使う
startButton.onClick.AddListener(() = HandleStart());

 ❌ 禁止：Action イベントを新規で使う
public event Action OnStartButtonClicked;

 ❌ 禁止：View 内でシーン遷移などのロジックを直接呼ぶ
startButton.onClick.AsObservable().Subscribe(_ = SceneManager.LoadScene(Main));
```

#### UniRx のリソース管理

購読は必ず `AddTo(this)` または `CompositeDisposable` で破棄すること。

```csharp
 ✅ MonoBehaviour の場合は AddTo(this)
view.OnStartButtonClicked
    .Subscribe(_ = HandleStart())
    .AddTo(this);

 ✅ 複数購読をまとめる場合は CompositeDisposable
private readonly CompositeDisposable _disposables = new CompositeDisposable();

void Start()
{
    view.OnStartButtonClicked
        .Subscribe(_ = HandleStart())
        .AddTo(_disposables);
}

void OnDestroy() = _disposables.Dispose();

 ❌ 禁止：AddTo  Dispose なしの購読（メモリリーク）
view.OnStartButtonClicked.Subscribe(_ = HandleStart());
```

---

## アクセス修飾子：SerializeField を使うこと

外部スクリプトから参照しないフィールドは `public` にせず `[SerializeField] private` を使うこと。

```csharp
 ✅ 正しい
[SerializeField] private Button startButton;
[SerializeField] private CanvasGroup logoGroup;

 ❌ 禁止
public Button startButton;
public CanvasGroup logoGroup;
```

外部参照が必要な場合は `public` または `internal` を使い、その理由をコメントに明記すること。

```csharp
 外部の Presenter から参照するため public
public event Action OnStartButtonClicked;
```

---

## コメント：変数・関数・クラスすべてに記載すること

### クラス

```csharp
 summary
 タイトル画面のUI表示を担当するViewクラス。
 ロジックは持たず、イベントの通知と表示操作のみを行う。
 summary
public class TitleScreenView  MonoBehaviour
```

### フィールド・変数

```csharp
 summaryロゴのフェードイン・アウトを制御するCanvasGroupsummary
[SerializeField] private CanvasGroup logoGroup;

 summary遷移中の連打を防ぐフラグsummary
private bool _isTransitioning = false;
```

### メソッド

```csharp
 summary
 ロゴをフェードインさせる。
 summary
 param name=durationフェードにかける秒数param
public IEnumerator FadeLogoIn(float duration)
```

### コメントの省略禁止

Unity のコールバック（`Awake`  `Start`  `Update` など）にも必ずコメントを付けること。

```csharp
 summary初期化：ボタンイベントを登録し、初期表示を非表示にするsummary
void Start()
```

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

\`\`\`csharp
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
\`\`\`

### 機能ごとに必ずメソッド化する

1つのメソッドは「1つの責務」のみを持つこと。
処理が複数の役割を持つ場合は、役割ごとにメソッドに分割すること。
目安として、1メソッドは20行以内に収めること。

\`\`\`csharp
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
\`\`\`

### LINQでコレクション処理をフラットに書く

`foreach` + `if` のネストは LINQ に置き換えてフラットにすること。

\`\`\`csharp
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
\`\`\`

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