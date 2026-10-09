using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    [Header("Voice Guide Controls")]
    [SerializeField] private Button voiceGuideButton;
    [SerializeField] private TMP_Text voiceGuideButtonLabel;
    [SerializeField] private Button startButton;
    [SerializeField] private Button restartButton;

    private enum VoiceSequence
    {
        None,
        Explore,
        Completion
    }

    private bool startupVoicePlayed;
    private bool clusteringCompleteVoicePlayed;
    private Coroutine exploreVoiceSequence;
    private Coroutine completionVoiceSequence;
    private VoiceSequence lastVoiceSequence;

    private void Awake()
    {
        Instance = this;

        if (voiceSource == null)
            voiceSource = GetComponent<AudioSource>();

        voiceSource.playOnAwake = false;
        voiceSource.loop = false;

        if (voiceGuideButton != null)
            voiceGuideButton.onClick.AddListener(HandleVoiceGuideButtonClicked);

        if (voiceGuideButtonLabel == null && voiceGuideButton != null)
            voiceGuideButtonLabel = voiceGuideButton.GetComponentInChildren<TMP_Text>();

        if (voiceGuideButton == null || startButton == null || restartButton == null)
            Debug.LogWarning("[AudioController] Assign the Voice Guide, Start, and Restart buttons in the Inspector.", this);

        UpdateVoiceGuideButton();
    }

    private void OnDestroy()
    {
        if (voiceGuideButton != null)
            voiceGuideButton.onClick.RemoveListener(HandleVoiceGuideButtonClicked);

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

        lastVoiceSequence = VoiceSequence.Explore;
        SetSequenceControlsLocked(true, false);
        exploreVoiceSequence = StartCoroutine(PlayExploreVoiceSequence());
        UpdateVoiceGuideButton();
    }

    public void PlayClusteringCompleteVoice()
    {
        if (clusteringCompleteVoicePlayed || clusteringCompleteVoice == null)
            return;

        clusteringCompleteVoicePlayed = true;
        CancelVoiceSequences();
        lastVoiceSequence = VoiceSequence.Completion;
        SetSequenceControlsLocked(false, true);
        completionVoiceSequence = StartCoroutine(PlayCompletionVoiceSequence());
        UpdateVoiceGuideButton();
    }

    public void PlayVoice(AudioClip voice)
    {
        CancelVoiceSequences();
        SetSequenceControlsLocked(false, false);
        UpdateVoiceGuideButton();

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
        SetSequenceControlsLocked(false, false);
        UpdateVoiceGuideButton();
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
        SetSequenceControlsLocked(false, false);
        UpdateVoiceGuideButton();
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

    public void SkipVoiceGuide()
    {
        if (!IsVoiceSequencePlaying())
            return;

        CancelVoiceSequences();
        if (voiceSource != null)
            voiceSource.Stop();

        SetSequenceControlsLocked(false, false);
        UpdateVoiceGuideButton();
    }

    public void ReplayVoiceGuide()
    {
        if (IsVoiceSequencePlaying())
            return;

        switch (lastVoiceSequence)
        {
            case VoiceSequence.Explore:
                PlayExploreVoice();
                break;
            case VoiceSequence.Completion:
                PlayClusteringCompleteVoiceSequence();
                break;
        }
    }

    private void PlayClusteringCompleteVoiceSequence()
    {
        if (voiceSource == null)
            return;

        CancelVoiceSequences();
        lastVoiceSequence = VoiceSequence.Completion;
        SetSequenceControlsLocked(false, true);
        completionVoiceSequence = StartCoroutine(PlayCompletionVoiceSequence());
        UpdateVoiceGuideButton();
    }

    private bool IsVoiceSequencePlaying()
    {
        return exploreVoiceSequence != null || completionVoiceSequence != null;
    }

    private void HandleVoiceGuideButtonClicked()
    {
        if (IsVoiceSequencePlaying())
            SkipVoiceGuide();
        else
            ReplayVoiceGuide();
    }

    private void SetSequenceControlsLocked(bool lockStart, bool lockRestart)
    {
        if (startButton != null)
            startButton.interactable = !lockStart;

        if (restartButton != null)
            restartButton.interactable = !lockRestart;
    }

    private void UpdateVoiceGuideButton()
    {
        if (voiceGuideButton != null)
            voiceGuideButton.interactable = IsVoiceSequencePlaying() || lastVoiceSequence != VoiceSequence.None;

        if (voiceGuideButtonLabel == null)
            return;

        if (IsVoiceSequencePlaying())
            voiceGuideButtonLabel.text = "Skip Voice Guide";
        else if (lastVoiceSequence != VoiceSequence.None)
            voiceGuideButtonLabel.text = "Replay Voice Guide";
        else
            voiceGuideButtonLabel.text = "Voice Guide";
    }
}