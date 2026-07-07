using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

/// <summary>
/// ゲーム全体の音響（BGM・SE）を一括管理するシングルトンクラスです。
/// </summary>
public class SoundManager : MonoBehaviour
{
    [Serializable]
    public class BGMSoundData
    {
        public BGMEnum bgmEnum;
        public AudioClip clip;
    }

    [Serializable]
    public class SESoundData
    {
        public SEEnum seEnum;
        public AudioClip clip;
    }

    public static SoundManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource _bgmSource;
    [SerializeField] private AudioSource _seSource;

    [Header("Sound Lists")]
    [SerializeField] private List<BGMSoundData> _bgmList = new List<BGMSoundData>();
    [SerializeField] private List<SESoundData> _seList = new List<SESoundData>();

    private Dictionary<BGMEnum, AudioClip> _bgmDictionary = new Dictionary<BGMEnum, AudioClip>();
    private Dictionary<SEEnum, AudioClip> _seDictionary = new Dictionary<SEEnum, AudioClip>();

    private const string BgmVolumeKey = "BGMVolume";
    private const string SeVolumeKey = "SEVolume";

    /// <summary>現在の BGM 音量（0〜1）。</summary>
    public float BGMVolume => _bgmSource != null ? _bgmSource.volume : 1f;

    /// <summary>現在の SE 音量（0〜1）。</summary>
    public float SEVolume => _seSource != null ? _seSource.volume : 1f;

    private void Awake()
    {
        // 常駐は PersistentScene への配置で実現する（DontDestroyOnLoad は使用禁止 / CLAUDE.md 規約）。
        // PersistentScene は起動時に一度だけ読み込まれるため、重複生成ガードのみ残す。
        if (Instance == null)
        {
            Instance = this;
            InitializeDictionaries();
            LoadVolumes();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>PlayerPrefs から保存済み音量を読み込んで AudioSource に適用する。</summary>
    private void LoadVolumes()
    {
        if (_bgmSource != null) _bgmSource.volume = PlayerPrefs.GetFloat(BgmVolumeKey, 1f);
        if (_seSource != null) _seSource.volume = PlayerPrefs.GetFloat(SeVolumeKey, 1f);
    }

    /// <summary>BGM 音量を設定して PlayerPrefs に保存する。</summary>
    public void SetBGMVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        if (_bgmSource != null) _bgmSource.volume = volume;
        PlayerPrefs.SetFloat(BgmVolumeKey, volume);
        PlayerPrefs.Save();
    }

    /// <summary>SE 音量を設定して PlayerPrefs に保存する。</summary>
    public void SetSEVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        if (_seSource != null) _seSource.volume = volume;
        PlayerPrefs.SetFloat(SeVolumeKey, volume);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// インスペクターで設定されたリストを検索用の辞書に変換します。
    /// </summary>
    private void InitializeDictionaries()
    {
        _bgmDictionary = BuildClipDictionary(_bgmList);
        _seDictionary = BuildClipDictionary(_seList);
    }

    /// <summary>有効なエントリのみを辞書化する（キー重複は後勝ち）。</summary>
    private static Dictionary<BGMEnum, AudioClip> BuildClipDictionary(List<BGMSoundData> list) =>
        list.Where(d => d.clip != null)
            .GroupBy(d => d.bgmEnum)
            .ToDictionary(g => g.Key, g => g.Last().clip);

    /// <summary>有効なエントリのみを辞書化する（キー重複は後勝ち）。</summary>
    private static Dictionary<SEEnum, AudioClip> BuildClipDictionary(List<SESoundData> list) =>
        list.Where(d => d.clip != null)
            .GroupBy(d => d.seEnum)
            .ToDictionary(g => g.Key, g => g.Last().clip);

    /// <summary>
    /// 指定されたキーのBGMを再生します。
    /// </summary>
    /// <param name="key">登録されたBGMのキー</param>
    /// <param name="loop">ループ再生するかどうか</param>
    public void PlayBGM(BGMEnum key, bool loop = true)
    {
        if (_bgmDictionary.TryGetValue(key, out AudioClip clip))
        {
            if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;

            _bgmSource.clip = clip;
            _bgmSource.loop = loop;
            _bgmSource.Play();
        }
        else
        {
            Debug.LogWarning($"BGM Key: {key} が見つかりません。");
        }
    }

    /// <summary>
    /// BGMの再生を停止します。
    /// </summary>
    public void StopBGM()
    {
        _bgmSource.Stop();
        _bgmSource.clip = null;
    }

    /// <summary>
    /// 指定されたキーのSEを再生します（PlayOneShotにより複数同時再生が可能）。
    /// </summary>
    /// <param name="key">登録されたSEのキー</param>
    /// <param name="volume">音量倍率 (0.0 - 1.0)</param>
    public void PlaySE(SEEnum key, float volume = 1.0f)
    {
        if (_seDictionary.TryGetValue(key, out AudioClip clip))
        {
            _seSource.PlayOneShot(clip, volume);
        }
        else
        {
            Debug.LogWarning($"SE Key: {key} が見つかりません。");
        }
    }

    /// <summary>
    /// SEをピッチを少しランダムに変えて再生する
    /// </summary>
    /// <param name="key">音源のキー（例: "かみつき"）</param>
    /// <param name="pitchRandomRange">ランダムにずらす幅（0.05なら 0.95 〜 1.05 の間で変化）</param>
    public void PlaySEWithRandomPitch(SEEnum key, float pitchRandomRange = 0.05f)
    {
        if (_seDictionary.TryGetValue(key, out AudioClip clip))
        {
            // 基準のピッチ（1.0）から、指定された幅でランダムにずらす
            // UnityEngine と System の両方に Random クラスが存在するため、明示的に指定してエラーを回避
            float randomPitch = UnityEngine.Random.Range(1.0f - pitchRandomRange, 1.0f + pitchRandomRange);

            // スピーカー（AudioSource）にピッチをセット
            _seSource.pitch = randomPitch;

            // 再生！
            _seSource.PlayOneShot(clip);

            // 他の再生（通常の PlaySE など）に影響しないよう、ピッチを標準（1.0）に戻しておく
            _seSource.pitch = 1.0f;
        }
        else
        {
            Debug.LogWarning($"SE Key: {key} が見つかりません。");
        }
    }

    /// <summary>
    /// SEの再生をすべて停止します。
    /// </summary>
    public void StopAllSE()
    {
        _seSource.Stop();
    }
}