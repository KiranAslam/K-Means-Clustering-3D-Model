using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class UIButtonHoverFix : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private TextMeshProUGUI[] allButtonTexts;

    private List<Color> originalVertexColors = new List<Color>();

    [Header("🎨 Cyberpunk Dynamic Colors")]
    public Color hoverColor = new Color(0f, 1f, 0f);
    public Color pressedColor = new Color(0f, 0.4f, 0f);

    void Awake()
    {
        allButtonTexts = GetComponentsInChildren<TextMeshProUGUI>(true);

        if (allButtonTexts != null)
        {
            foreach (var txt in allButtonTexts)
            {
                originalVertexColors.Add(txt.color);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (allButtonTexts == null) return;

        foreach (var txt in allButtonTexts)
        {
            txt.color = hoverColor;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (allButtonTexts == null) return;

        for (int i = 0; i < allButtonTexts.Length; i++)
        {
            if (allButtonTexts[i] != null)
            {
                allButtonTexts[i].color = originalVertexColors[i];
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (allButtonTexts == null) return;

        foreach (var txt in allButtonTexts)
        {
            txt.color = pressedColor;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (allButtonTexts == null) return;

        bool isOver = eventData.pointerCurrentRaycast.gameObject == gameObject ||
                      (eventData.pointerCurrentRaycast.gameObject != null &&
                       eventData.pointerCurrentRaycast.gameObject.transform.IsChildOf(transform));

        for (int i = 0; i < allButtonTexts.Length; i++)
        {
            if (allButtonTexts[i] != null)
            {
                allButtonTexts[i].color = isOver ? hoverColor : originalVertexColors[i];
            }
        }
    }
}
