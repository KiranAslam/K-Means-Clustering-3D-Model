using UnityEngine;
using UnityEngine.Serialization;

public class SceneSwitcher : MonoBehaviour
{
    public static SceneSwitcher Instance { get; private set; }

    [Header("Panels (inside Dashboard_Canvas > Main_Layout_Wrapper)")]
    public GameObject panelMainMenu;
    public GameObject panelKMC;

    [Header("K-Means Model")]
    public GameObject kmcModel;

    [Header("Main Menu Objects")]
    [Tooltip("Objects that should only be visible on the Main Menu.")]
    [FormerlySerializedAs("robotObjects")]
    public GameObject[] mainMenuObjects;

    [Header("K-Means Camera")]
    public IntegratedCameraController modelCamera;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ShowMainMenu();
    }

    private void SetPanel(GameObject target)
    {
        if (panelMainMenu != null) panelMainMenu.SetActive(panelMainMenu == target);
        if (panelKMC != null) panelKMC.SetActive(panelKMC == target);
    }

    private void SetKMCModel(bool isVisible)
    {
        if (kmcModel != null) kmcModel.SetActive(isVisible);
    }

    private void SetMainMenuObjects(bool isVisible)
    {
        if (mainMenuObjects == null) return;
        foreach (GameObject mainMenuObject in mainMenuObjects)
            if (mainMenuObject != null) mainMenuObject.SetActive(isVisible);
    }

    public void ShowMainMenu()
    {
        SetPanel(panelMainMenu);
        SetKMCModel(false);
        SetMainMenuObjects(true);
    }

    public void ShowKMCModel()
    {
        SetPanel(panelKMC);
        SetKMCModel(true);
        SetMainMenuObjects(false);

        if (modelCamera != null && kmcModel != null)
        {
            Transform origin = kmcModel.transform.Find("NetworkOrigin");
            modelCamera.targetFocus = origin != null ? origin : kmcModel.transform;
            modelCamera.ResetToDefaultView();
        }
    }

    public void ShowModelSelection() => ShowKMCModel();
    public void ShowDNNModel() => ShowKMCModel();
    public void ShowLLMModel() => ShowKMCModel();
}
