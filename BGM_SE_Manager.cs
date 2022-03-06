using DG.Tweening;
using Singleton;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(BGM_SE_Manager))]
public class BGM_SE_ManagerEdit : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!Application.isPlaying)
        {
            if (BGM_SE_Manager.Instance.bgm_se_setting == null) return;
            GUILayout.Space(20);
            var audioClipBGM = BGM_SE_Manager.Instance.bgm_se_setting.BGM;

            for (int i = 0; i < audioClipBGM.Length; i++)
            {
                if (audioClipBGM[i] == null || audioClipBGM[i].value == null) continue;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(audioClipBGM[i].value.name,  EditorStyles.label);
                GUILayout.TextField(audioClipBGM[i].key, EditorStyles.textField);
                EditorGUILayout.EndHorizontal();
            }
            var audioClipSE = BGM_SE_Manager.Instance.bgm_se_setting.SE;
            for (int i = 0; i < audioClipSE.Length; i++)
            {
                if (audioClipSE[i] == null || audioClipSE[i].value == null) continue;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(audioClipSE[i].value.name, EditorStyles.label);
                GUILayout.TextField(audioClipSE[i].key, EditorStyles.textField);
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
#endif
public static class BGM_SE_Manager_Util
{
    const int SAMPLE_RATE = 48000;
    const float BaseDB = 20.0f;
    public static float AnalyzeSound(this AudioSource ac, in int qSamples, in float threshold)
    {
        float[] spectrum = new float[qSamples];
        ac.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
        float maxV = 0;
        int maxN = 0;
        //最大値（ピッチ）を見つける。
        for (int i = 0; i < qSamples; i++)
        {
            if (spectrum[i] > maxV)
            {
                maxV = spectrum[i];
                maxN = i;
            }
        }
        if (maxV < threshold)
            return 0.0f;

        float freqN = maxN;
        if (maxN > 0 && maxN < qSamples - 1)
        {
            float dL = spectrum[maxN - 1] / maxV;
            float dR = spectrum[maxN + 1] / maxV;
            freqN += 0.5f * (dR * dR - dL * dL);
        }

        float pitchValue = freqN * (AudioSettings.outputSampleRate / 2) / qSamples;
        return pitchValue;
    }
    public static float DeSound(this AudioSource MicSources, in int qSamples)
    {
        if (MicSources.isPlaying)
        {
            float[] data = new float[qSamples];
            MicSources.GetOutputData(data, 0);
            float aveAmp = 0;
            for (int i = 0; i < qSamples; i++)
            {
                aveAmp += Mathf.Abs(data[i]);
            }
            float dB = BaseDB * Mathf.Log10(aveAmp / qSamples);
            return dB;
        }
        return 0.0f;
    }
    /// <summary>
    /// マイクを出力します。
    /// </summary>
    /// <param name="MicSources">マイクのClipをセットする為のAudioSource</param>
    public static void MicStart(this AudioSource MicSources)
    {
        //AudioSourceのClipにマイクデバイスをセット(サンプリング周波数48000)
        MicSources.clip = Microphone.Start(null, true, 1, SAMPLE_RATE);

        //マイクデバイスの準備ができるまで待つ
        while (!(Microphone.GetPosition("") > 0)) { }

        //AudioSouceからの出力を開始
        MicSources.Play();
    }
}
public class BGM_SE_Manager : SingletonMonoBehaviour<BGM_SE_Manager>
{
    [SerializeField] BGM_SE_Setting _bgm_se_setting;
    readonly static Dictionary<string, AudioClip> AudioDictionary = new Dictionary<string, AudioClip>();
    public BGM_SE_Setting bgm_se_setting
    {
        get { return _bgm_se_setting; }
        set
        {
            if (_bgm_se_setting != value) _bgm_se_setting = value;
            if (_bgm_se_setting != null)
            {
                AudioDictionary.Clear();
                if (_bgm_se_setting.BGM != null)
                {
                    foreach (var bgm in _bgm_se_setting.BGM)
                    {
                        if (bgm == null || bgm.value == null) continue;
                        if (bgm.key == "") bgm.key = bgm.value.name;
                        AudioDictionary.Add(bgm.key, bgm.value);
                    }
                }
                if (_bgm_se_setting.SE != null)
                {
                    foreach (var se in _bgm_se_setting.SE)
                    {
                        if (se == null || se.value == null) continue;
                        if (se.key == "") se.key = se.value.name;
                        AudioDictionary.Add(se.key, se.value);
                    }
                }
            }
        }
    }

    //-----------------------------------BGM----------------------------------------------
    /// <summary>
    /// フェードインとフェードアウトの実行を重ねる割合
    /// </summary>
    float CrossFadeRatio { get => bgm_se_setting.CrossFadeRatio; }
    /// <summary>
    /// 現在再生中のAudioSource
    /// FadeOut中のものは除く
    /// </summary>
    public AudioSource CurrentAudioSource { get; private set; }
    /// <summary>
    /// FadeOut中、もしくは再生待機中のAudioSource
    /// </summary>
    AudioSource SubAudioSource
    {
        get
        {
            //bgmSourcesのうち、CurrentAudioSourceでない方を返す
            if (this.AudioSources == null)
                return null;
            for (int i = 0; i < this.AudioSources.Length; i++)
            {
                AudioSource s = this.AudioSources[i];
                if (s != this.CurrentAudioSource)
                {
                    return s;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// BGMを再生するためのAudioSource
    /// </summary>
    AudioSource[] AudioSources = null;

    [Range(0f, 1f)]
    public float BaseBGMVolume = 1f;

    //-----------------------------------SE----------------------------------------------
    bool SEMute;
    /// <summary>
    /// SE用のAudioSourceの数
    /// </summary>
    int SEAudioSourceSize { get => bgm_se_setting.SEAudioSourceSize; }
    AudioSource[] SEsources;
    [Range(0f, 1f)]
    public float BaseSEVolume = 1f;
    readonly object lockSE = new object();

    //---------------------------------------------------------------------------------
    protected override void Awake()
    {
        base.Awake();
        Init();
        SEsources = new AudioSource[SEAudioSourceSize];
        //SE AudioSource
        for (int count = 0; count < SEsources.Length; count++)
        {
            SEsources[count] = gameObject.AddComponent<AudioSource>();
        }
        
        DontDestroyOnLoad(this.gameObject);
    }
    public void Init()
    {
        this.AudioSources = gameObject.GetComponents<AudioSource>();
        if(this.AudioSources == null || this.AudioSources.Length < 2)
        {
            //AudioSourceを２つ用意。クロスフェード時に同時再生するために２つ用意する。
            System.Array.Resize(ref this.AudioSources, 2);
            for (int i = 0; i < 2 ; i++)
            {
                if(this.AudioSources[i] == null)
                this.AudioSources[i] = (this.gameObject.AddComponent<AudioSource>());
                AudioSource s = this.AudioSources[i];
                s.playOnAwake = false;
                s.volume = 0f;
                s.loop = true;
            }
        }
        this.CurrentAudioSource = this.SubAudioSource;
        bgm_se_setting = _bgm_se_setting;
    }
    /// <summary>
    /// BGMを再生します。
    /// </summary>
    /// <param name="BGMID">BGMID</param>
    /// <param name="redo">再生中のbgmを再度再生するかどうか</param>
    /// <param name="TimeToFade">フェードにかかる時間</param>
    public static void PlayBGM(in string BGMname)
    {
        if(AudioDictionary.ContainsKey(BGMname))
        Instance.PlayBGM(AudioDictionary[BGMname], false, 1.0f);
    }
    /// <summary>
    /// BGMを再生します。
    /// </summary>
    /// <param name="bgmName">BGM名</param>
    /// <param name="redo">再生中のbgmを再度再生するかどうか</param>
    /// <param name="TimeToFade">フェードにかかる時間</param>
    public void PlayBGM(in AudioClip clip, bool redo, float TimeToFade)
    {
        if (clip == null)
        {
            return;
        }
        if ((!redo) && (this.CurrentAudioSource != null) && (this.CurrentAudioSource.clip == clip))
        {
            return;
        }

        float fadeInStartDelay = TimeToFade * (1.0f - this.CrossFadeRatio);
        //再生中のBGMをフェードアウト開始
        this.StopBGM(this.CurrentAudioSource, fadeInStartDelay);

        //BGM再生開始
        this.CurrentAudioSource = this.SubAudioSource;
        this.CurrentAudioSource.clip = clip;
        if (this.CurrentAudioSource.clip == null) return;

        this.CurrentAudioSource.PlayDelayed(fadeInStartDelay);
        this.CurrentAudioSource.DOFade(endValue: BaseBGMVolume, duration: TimeToFade).SetDelay(fadeInStartDelay);
    }

    /// <summary>
    /// BGMのポーズを行います。解除を行います。
    /// </summary>
    public void PauseBGM(bool Pause)
    {
        if (this.CurrentAudioSource != null)
        {
            if (Pause)
                this.CurrentAudioSource.Pause();
            else
                this.CurrentAudioSource.UnPause();
        }
    }

    /// <summary>
    /// BGMを停止します。
    /// </summary>
    /// <param name="TimeToFade">フェードアウトにかかる時間</param>
    public void StopBGM(AudioSource OldAudioSource, float TimeToFade)
    {
        if (OldAudioSource != null)
        {
            OldAudioSource.DOFade(endValue: 0f, duration: TimeToFade).onComplete = () =>
            {
                if(this.CurrentAudioSource.isPlaying)
                OldAudioSource.Stop();
            };
        }
    }
    /// <summary>
    /// 全体ボリューム設定
    /// </summary>
    /// <param name="SEVolume">設定ボリューム</param>
    public void BGMVolumeAllConfig(float SEVolume)
    {
        BaseBGMVolume = SEVolume;
        for (int i = 0; i < 2; i++)
        {
            this.AudioSources[i].volume = SEVolume;
        }
    }
    /// <summary>
    /// BGMをただちに停止します。
    /// </summary>
    public void StopImmediately()
    {
        if (this.CurrentAudioSource != null)
        {
            this.CurrentAudioSource.Stop();
            this.CurrentAudioSource = null;
        }
    }

    public float AnalyzeBGMSound()
    {
        if (this.CurrentAudioSource != null)
        {
            return this.CurrentAudioSource.AnalyzeSound(1024, 0.3f);
        }
        return 0.0f;
    }

    /// <summary>
    /// SEミュート設定又解除
    /// </summary>
    /// <param name="Mute">ミュートを行うかどうか</param>
    public void MuteConfig(bool Mute)
    {
        if (Mute) SEMute = false;
        else SEMute = true;

        for (int i = 0; i < SEsources.Length; i++)
        {
            AudioSource source = SEsources[i];
            source.mute = SEMute;
        }
    }

    /// <summary>
    /// 全体ボリューム設定
    /// </summary>
    /// <param name="SEVolume">設定ボリューム</param>
    public void SEVolumeAllConfig(float SEVolume)
    {
        if (SEsources == null)
        {
            Debug.LogError("AudioSouceが存在しません");
            return;
        }
        if (SEVolume > 1)
        {
            SEVolume = 1;
        }

        BaseSEVolume = SEVolume;
        for (int i = 0; i < SEsources.Length; i++)
            SEsources[i].volume = SEVolume;

    }

    /// <summary>
    /// 特定のボリューム変更
    /// </summary>
    /// <param name="index">配列のindex</param>
    /// <param name="SEVolume">設定ボリューム</param>
    public void SEVolumeConfig(int index, float SEVolume)
    {
        if (SEsources.Length <= index)
        {
            return;//エラー
        }

        if (SEsources[index] == null)
        {
            Debug.LogError("指定したindexのAudioSouceが存在しません");
            return;
        }

        if (SEVolume > 1)
        {
            SEVolume = 1;
        }
        BaseSEVolume = SEVolume;
        SEsources[index].volume = SEVolume;
    }

    /// <summary>
    /// SE再生
    /// </summary>
    /// <param name="SEIndex">再生中のオーディオ番号</param>
    /// <param name="loop">ループ判定</param>
    /// <returns></returns>
    public static int PlaySE(string SEname, bool loop)
    {
        if (AudioDictionary.ContainsKey(SEname))
            return Instance.PlaySE(AudioDictionary[SEname], loop);
        return -1;//再生エラー
    }
    /// <summary>
    /// SE再生
    /// </summary>
    /// <param name="SEIndex">再生中のオーディオ番号</param>
    /// <param name="loop">ループ判定</param>
    /// <returns></returns>
    public int PlaySE(in AudioClip SE, bool loop)
    {
        if (SE == null) return -1;//再生エラー
        //再生中で無いAudioSouceで鳴らす
        for (int num = 0; num < SEsources.Length; num++)
        {
            AudioSource source = SEsources[num];
            if (!source.isPlaying)
            {
                lock (lockSE)
                {
                    source.clip = SE;
                    source.loop = loop;
                    source.Play();
                    return num;
                }
            }
        }
        return -1;//再生エラー
    }
    /// <summary>
    /// SE停止
    /// </summary>
    /// <param name="index">停止するオーディオ番号を指定</param>
    /// <returns></returns>
    public int StopSE(int index)
    {
        if (SEsources.Length <= index)
        {
            return -1;//エラー
        }
        //指定した番号のオーディオを停止する
        AudioSource source = SEsources[index];
        if (source.isPlaying)
        {
            source.Stop();
            return 0;//停止成功
        }

        return -1;//停止エラー
    }

    /// <summary>
    /// 全SE停止
    /// </summary>
    public void AllstopSE()
    {
        //SE用のAudioSouceを全停止する
        for (int i = 0; i < SEsources.Length; i++)
        {
            AudioSource source = SEsources[i];
            if (source.isPlaying)
            {
                source.Stop();
                source.clip = null;
            }
        }
    }

    /// <summary>
    /// 全SEを一時停止する
    /// </summary>
    /// <param name="Pause">一時停止するか解除するか</param>
    public void AllPauseSE(bool Pause)
    {
        for (int i = 0; i < SEsources.Length; i++)
        {
            AudioSource source = SEsources[i];
            if (source.isPlaying && Pause)
            {
                source.Pause();
                return;
            }
            else if (!Pause)
            {
                source.UnPause();
            }
        }
    }

    public static void AllPause(bool Pause)
    {
        Instance.PauseBGM(Pause);
        Instance.AllPauseSE(Pause);
    }

    public float AnalyzeSESound(int index)
    {
        if (SEsources.Length <= index)
        {
            return 0.0f;//エラー
        }
        return this.SEsources[index].AnalyzeSound(1024, 0.3f);
    }

}

