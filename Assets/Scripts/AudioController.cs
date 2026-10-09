using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioController : MonoBehaviour
{
    public static AudioController Instance { get; private set; }

    [Header("Startup Audio")]
    [SerializeField] private AudioClip startupVoice;
    [SerializeField] private AudioClip exploreVoice;
    [SerializeField] private AudioClip datasetVoice;
    [SerializeField] private AudioClip kValueVoice;
    [SerializeField] private AudioClip initializerVoice;
    [SerializeField] private AudioClip distanceMetricVoice;
    [SerializeField] private AudioClip startVoice;
    [SerializeField] private AudioClip clusteringCompleteVoice;
    [SerializeField] private AudioClip whyThisClusterVoice;
    [SerializeField] private AudioClip silhouetteVoice;
    [SerializeField] private AudioClip showTrueClusterVoice;
    [SerializeField] private AudioClip endVoice;
    [SerializeField] private AudioSource voiceSource;

    private bool startupVoicePlayed;
    private bool clusteringCompleteVoicePlayed;
    private Coroutine exploreVoiceSequence;
    private Coroutine completionVoiceSequence;

    private void Awake()
    {
        Instance = this;

        if (voiceSource == null)
            voiceSource = GetComponent<AudioSource>();

        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        PlayStartupVoiceOnce();
    }

    public void PlayStartupVoiceOnce()
    {
        if (startupVoicePlayed || startupVoice == null)
            return;

        startupVoicePlayed = true;
        PlayVoice(startupVoice);
    }

    public void PlayExploreVoice()
    {
        CancelVoiceSequences();

        if (voiceSource == null)
            return;

        exploreVoiceSequence = StartCoroutine(PlayExploreVoiceSequence());
    }

    public void PlayClusteringCompleteVoice()
    {
        if (clusteringCompleteVoicePlayed || clusteringCompleteVoice == null)
            return;

        clusteringCompleteVoicePlayed = true;
        CancelVoiceSequences();
        completionVoiceSequence = StartCoroutine(PlayCompletionVoiceSequence());
    }

    public void PlayVoice(AudioClip voice)
    {
        CancelVoiceSequences();

        if (voice == null || voiceSource == null)
            return;

        PlayClip(voice);
    }

    private void PlayClip(AudioClip voice)
    {
        voiceSource.Stop();
        voiceSource.loop = false;
        voiceSource.clip = voice;
        voiceSource.Play();
    }

    private IEnumerator PlayExploreVoiceSequence()
    {
        yield return PlayClipToCompletion(exploreVoice);
        yield return new WaitForSecondsRealtime(1f);
        yield return PlayClipToCompletion(datasetVoice);
        yield return new WaitForSecondsRealtime(1f);
        yield return PlayClipToCompletion(kValueVoice);
        yield return new WaitForSecondsRealtime(1f);
        yield return PlayClipToCompletion(initializerVoice);
        yield return new WaitForSecondsRealtime(1f);
        yield return PlayClipToCompletion(distanceMetricVoice);
        yield return new WaitForSecondsRealtime(1f);
        yield return PlayClipToCompletion(startVoice);
        exploreVoiceSequence = null;
    }

    private IEnumerator PlayCompletionVoiceSequence()
    {
        yield return PlayClipToCompletion(clusteringCompleteVoice);
        yield return new WaitForSecondsRealtime(1f);
        yield return PlayClipToCompletion(whyThisClusterVoice);
        yield return new WaitForSecondsRealtime(1f);
        yield return PlayClipToCompletion(silhouetteVoice);
        yield return new WaitForSecondsRealtime(1f);
        yield return PlayClipToCompletion(showTrueClusterVoice);
        yield return new WaitForSecondsRealtime(1f);
        yield return PlayClipToCompletion(endVoice);
        completionVoiceSequence = null;
    }

    private IEnumerator PlayClipToCompletion(AudioClip clip)
    {
        if (clip == null || voiceSource == null)
            yield break;

        PlayClip(clip);
        while (voiceSource != null && voiceSource.isPlaying)
            yield return null;
    }

    private void CancelVoiceSequences()
    {
        if (exploreVoiceSequence != null)
        {
            StopCoroutine(exploreVoiceSequence);
            exploreVoiceSequence = null;
        }

        if (completionVoiceSequence != null)
        {
            StopCoroutine(completionVoiceSequence);
            completionVoiceSequence = null;
        }
    }
}