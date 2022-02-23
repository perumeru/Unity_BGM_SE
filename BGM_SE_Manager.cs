using DG.Tweening;
using Singleton;
using UnityEngine;

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
    [SerializeField] public BGM_SE_Setting bgm_se_setting;

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
    /// <summary>
    /// BGM
    /// </summary>
    [SerializeField] public AudioClip[] BGM { get => bgm_se_setting.BGM; }
    protected override void Awake()
    {
        base.Awake();
        //シングルトンのためのコード
        BGMInit();
        SEInit();
        DontDestroyOnLoad(this.gameObject);
    }

    void BGMInit()
    {
        //AudioSourceを２つ用意。クロスフェード時に同時再生するために２つ用意する。
        this.AudioSources = new AudioSource[2];

        for (int i = 0; i < 2; i++)
        {
            this.AudioSources[i] = (this.gameObject.AddComponent<AudioSource>());
            AudioSource s = this.AudioSources[i];
            s.playOnAwake = false;
            s.volume = 0f;
            s.loop = true;
        }
    }
    /// <summary>
    /// BGMを再生します。
    /// </summary>
    /// <param name="BGMID">BGMID</param>
    /// <param name="redo">再生中のbgmを再度再生するかどうか</param>
    /// <param name="TimeToFade">フェードにかかる時間</param>
    public void PlayBGM(in int BGMID, bool redo, float TimeToFade)
    {
        if (BGM.Length > BGMID)
            PlayBGM(BGM[BGMID], redo, TimeToFade);
    }
    /// <summary>
    /// BGMを再生します。
    /// </summary>
    /// <param name="bgmName">BGM名</param>
    /// <param name="redo">再生中のbgmを再度再生するかどうか</param>
    /// <param name="TimeToFade">フェードにかかる時間</param>
    public void PlayBGM(in AudioClip clip, bool redo, float TimeToFade)
    {
        if (!clip)
        {
            Debug.LogError("BGMを指定してください");
            return;
        }
        else
        if (!redo)
        {
            if ((this.CurrentAudioSource != null)
                && (this.CurrentAudioSource.clip == clip))
            {
                //すでに指定されたBGMを再生中
                return;
            }
        }

        float fadeInStartDelay = TimeToFade * (1.0f - this.CrossFadeRatio);
        //再生中のBGMをフェードアウト開始
        this.StopBGM(this.CurrentAudioSource,fadeInStartDelay);

        //BGM再生開始
        this.CurrentAudioSource = this.SubAudioSource;
        this.CurrentAudioSource.clip = clip;
        if (this.CurrentAudioSource.clip == null) return;

        this.CurrentAudioSource.PlayDelayed(fadeInStartDelay);
        this.CurrentAudioSource.DOFade(endValue: 1f, duration: TimeToFade).SetDelay(fadeInStartDelay);
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
    public void StopBGM(AudioSource OldAudioSource,float TimeToFade)
    {
        if (OldAudioSource != null)
        {
            OldAudioSource.DOFade(endValue: 0f, duration: TimeToFade).onComplete = () => OldAudioSource.Stop();
        }
    }
    /// <summary>
    /// 全体ボリューム設定
    /// </summary>
    /// <param name="SEVolume">設定ボリューム</param>
    public void BGMVolumeAllConfig(float SEVolume)
    {
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
    //-----------------------------------SE----------------------------------------------
    bool SEMute;
    /// <summary>
    /// SE用のAudioSourceの数
    /// </summary>
    int SEAudioSourceSize { get => bgm_se_setting.SEAudioSourceSize; }
    AudioSource[] SEsources;
    /// <summary>
    /// SE
    /// </summary>
    public AudioClip[] SE { get => bgm_se_setting.SE; }

    [Range(0f, 1f)]
    float Basevolume;

    readonly object lockSE = new object();
    void SEInit()
    {
        SEsources = new AudioSource[SEAudioSourceSize];
        Basevolume = 1f;
        //SE AudioSource
        for (int count = 0; count < SEsources.Length; count++)
        {
            SEsources[count] = gameObject.AddComponent<AudioSource>();
        }
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
        if (SEVolume > 1)
        {
            SEVolume = 1;
        }

        if (SEsources == null)
        {
            Debug.LogError("AudioSouceが存在しません");
            return;
        }

        Basevolume = SEVolume;
        for (int i = 0; i < SEsources.Length; i++)
            SEsources[i].volume = Basevolume;

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

        SEsources[index].volume = SEVolume;
    }

    /// <summary>
    /// SE再生
    /// </summary>
    /// <param name="SEIndex">再生中のオーディオ番号</param>
    /// <param name="loop">ループ判定</param>
    /// <returns></returns>
    public int PlaySE(int SEIndex, bool loop)
    {
        if (SEIndex < 0 || SE.Length <= SEIndex)
        {
            return -1;//エラー
        }

        //再生中で無いAudioSouceで鳴らす
        for (int num = 0; num < SEsources.Length; num++)
        {
            AudioSource source = SEsources[num];
            if (!source.isPlaying)
            {
                lock (lockSE)
                {
                    if (source.volume < 0.1f)
                        source.volume = Basevolume;
                    source.clip = SE[SEIndex];
                    if (loop) source.loop = true;
                    else source.loop = false;
                    source.Play();
                    return num;
                }
            }
        }
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
        //再生中で無いAudioSouceで鳴らす
        for (int num = 0; num < SEsources.Length; num++)
        {
            AudioSource source = SEsources[num];
            if (!source.isPlaying)
            {
                lock (lockSE)
                {
                    if (source.volume < 0.1f)
                        source.volume = Basevolume;
                    source.clip = SE;
                    if (loop) source.loop = true;
                    else source.loop = false;
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
    public int stopSE(int index)
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

