using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "BGM_SE Settings")]
public class BGM_SE_Setting : ScriptableObject
{
    //-----------------------------------BGM----------------------------------------------
    /// <summary>
    /// �t�F�[�h�C���ƃt�F�[�h�A�E�g�̎��s���d�˂銄��
    /// </summary>
    [SerializeField, Range(0f, 1f)] public float CrossFadeRatio = 0.5f;
    /// <summary>
    /// BGM
    /// </summary>
    [SerializeField] public AudioClip[] BGM;
    //-----------------------------------SE----------------------------------------------
    /// <summary>
    /// SE�p��AudioSource�̐�
    /// </summary>
    [SerializeField, Range(0f, 20f)] public int SEAudioSourceSize;
    /// <summary>
    /// SE
    /// </summary>
    [SerializeField] public AudioClip[] SE;


}