using UnityEngine;

[CreateAssetMenu(menuName = "BGM_SE Settings")]
public class BGM_SE_Setting : ScriptableObject
{
    [System.Serializable]
    public class AudioData
    {
        public AudioClip value;
        public string key;
    }
    //-----------------------------------BGM----------------------------------------------
    /// <summary>
    /// フェードインとフェードアウトの実行を重ねる割合
    /// </summary>
    [SerializeField, Range(0f, 1f)] public float CrossFadeRatio = 0.5f;
    /// <summary>
    /// BGM
    /// </summary>
    [SerializeField] public AudioData[] BGM;
    //-----------------------------------SE----------------------------------------------
    /// <summary>
    /// SE用のAudioSourceの数
    /// </summary>
    [SerializeField, Range(0f, 20f)] public int SEAudioSourceSize;
    /// <summary>
    /// SE
    /// </summary>
    [SerializeField] public AudioData[] SE;
}
