using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DatasetInfoPanel : MonoBehaviour
{
    [Header("References")]
    public DatasetVisualizer datasetVisualizer;

    [Tooltip("Complete dataset information panel GameObject")]
    public GameObject infoPanel;

    [Tooltip("TMP text inside information panel")]
    public TMP_Text infoText;


    [Header("Display Settings")]
    [Tooltip("Duration on first application launch")]
    public float firstLaunchDuration = 120f;

    [Tooltip("Duration after changing dataset")]
    public float datasetChangeDuration = 20f;


    [Header("Text Settings")]
    public float titleFontSize = 25f;
    public float descriptionFontSize = 22f;

    [Tooltip("Line spacing between title and description")]
    public float lineSpacing = 3f;


    [Header("Colors")]
    public string datasetNameColor = "73E7CF";


    private Coroutine hideRoutine;
    private bool firstDisplayDone = false;


    private readonly Dictionary<DatasetVisualizer.DatasetType, string> descriptions =
        new Dictionary<DatasetVisualizer.DatasetType, string>()
    {
        {
            DatasetVisualizer.DatasetType.Moons,
            "Synthetic dataset with two curved clusters. Useful for observing non-linear patterns."
        },

        {
            DatasetVisualizer.DatasetType.Circles,
            "Synthetic dataset containing circular groups. Useful for studying cluster separation."
        },

        {
            DatasetVisualizer.DatasetType.AnisotropicBlobs,
            "Synthetic dataset with stretched clusters. Shows how cluster shape affects grouping."
        },

        {
            DatasetVisualizer.DatasetType.VaryingDensityBlobs,
            "Synthetic dataset with different cluster densities. Helps explore uneven grouping."
        },

        {
            DatasetVisualizer.DatasetType.CustomerSegmentation,
            "Real customer data based on income and spending behavior. Explore grouping patterns."
        },

        {
            DatasetVisualizer.DatasetType.ImageColorQuantization,
            "Groups similar pixel colors to demonstrate clustering-based image reduction."
        },

        {
            DatasetVisualizer.DatasetType.Iris,
            "Real-world flower dataset containing measurements of iris species."
        }
    };


    private void Awake()
    {
        if (datasetVisualizer != null)
        {
            datasetVisualizer.OnPointsReady += ShowDatasetInfo;
        }
    }


    private void Start()
    {
        HidePanel();

        // Initial Moons dataset information
        Invoke(nameof(ShowInitialDatasetInfo), 0.5f);
    }


    private void OnDestroy()
    {
        if (datasetVisualizer != null)
        {
            datasetVisualizer.OnPointsReady -= ShowDatasetInfo;
        }


        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }
    }


    private void ShowInitialDatasetInfo()
    {
        ShowDatasetInfo(true);
    }


    private void ShowDatasetInfo()
    {
        ShowDatasetInfo(false);
    }


    private void ShowDatasetInfo(bool isFirstLaunch)
    {
        if (datasetVisualizer == null ||
            datasetVisualizer.datasetDropdown == null ||
            infoPanel == null ||
            infoText == null)
        {
            return;
        }


        DatasetVisualizer.DatasetType currentDataset =
            (DatasetVisualizer.DatasetType)datasetVisualizer.datasetDropdown.value;


        string description = "Dataset information unavailable.";


        if (descriptions.TryGetValue(currentDataset, out string datasetDescription))
        {
            description = datasetDescription;
        }


        string datasetName = currentDataset.ToString();


        infoText.richText = true;


        infoText.text =
    $"<color=#{datasetNameColor}><size={titleFontSize}>{datasetName}</size></color>\n" +
    $"<color=#FFFFFF><size={descriptionFontSize}>{description}</size></color>";


        infoPanel.SetActive(true);


        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }


        float duration = isFirstLaunch
            ? firstLaunchDuration
            : datasetChangeDuration;


        hideRoutine = StartCoroutine(HideAfterTime(duration));


        firstDisplayDone = true;
    }


    private IEnumerator HideAfterTime(float duration)
    {
        yield return new WaitForSeconds(duration);

        HidePanel();
    }


    private void HidePanel()
    {
        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }
    }
}