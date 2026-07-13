using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoisonArea : MonoBehaviour
{
    [Header("Pool Key")]
    [SerializeField] private string areaKey = "PoisonArea";

    [Header("Settings")]
    [SerializeField] private float duration = 5f;
    [SerializeField] private int damagePerTick = 5;
    [SerializeField] private float tickInterval = 0.5f;

    private List<PlayerStatus> playersInArea = new List<PlayerStatus>();
    private float tickTimer = 0f;
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
    }

    private void OnEnable()
    {
        playersInArea.Clear();
        tickTimer = 0f;
        StartCoroutine(AutoReturnRoutine());

        // 오브젝트 풀 생성 시(초기화) 사운드 재생 방지
        if (transform.parent != null && transform.parent.GetComponent<PoolingManager>() != null)
        {
            return;
        }

        PlayPoisonAreaSound();
    }

    private void OnDisable()
    {
        StopPoisonAreaSound();
    }

    private void Update()
    {
        if (playersInArea.Count == 0) return;

        tickTimer += Time.deltaTime;
        if (tickTimer >= tickInterval)
        {
            tickTimer = 0f;
            foreach (var player in playersInArea)
            {
                if (player != null)
                {
                    player.TakeDamage(damagePerTick);
                }
            }
        }
    }

    private IEnumerator AutoReturnRoutine()
    {
        yield return new WaitForSeconds(duration - 0.3f);

        Animator anim = GetComponentInChildren<Animator>();

        yield return new WaitForSeconds(0.3f);

        PoolingManager.Instance.Return(areaKey, gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerStatus player = collision.GetComponent<PlayerStatus>();
            if (player != null && !playersInArea.Contains(player))
            {
                playersInArea.Add(player);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerStatus player = collision.GetComponent<PlayerStatus>();
            if (player != null && playersInArea.Contains(player))
            {
                playersInArea.Remove(player);
            }
        }
    }

    #region Audio Management
    private void PlayPoisonAreaSound()
    {
        if (AudioManager.instance == null || audioSource == null) return;

        int sfxIndex = (int)AudioManager.SFX.RGB_PoisonArea;
        if (AudioManager.instance.sfxClips == null || sfxIndex < 0 || sfxIndex >= AudioManager.instance.sfxClips.Length)
        {
            Debug.LogWarning($"[PoisonArea] RGB_PoisonArea SFX index {sfxIndex} is out of bounds. Please assign it in the AudioManager Inspector.");
            return;
        }

        var sfxData = AudioManager.instance.sfxClips[sfxIndex];
        audioSource.clip = sfxData.clip;
        float globalVol = GameManager.instance != null ? GameManager.instance.sfxVolume : AudioManager.instance.sfxVolume;
        audioSource.volume = globalVol * sfxData.volumeScale;
        audioSource.Play();
    }

    private void StopPoisonAreaSound()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
    #endregion
}