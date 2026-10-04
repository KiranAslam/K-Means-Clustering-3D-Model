using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UICardBorderHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private Image cardImage;
    private Color originalBorderColor;

    [Header("🎨 Cyberpunk Border Colors")]
    public Color hoverColor = new Color(0f, 1f, 0f);
    public Color pressedColor = new Color(0f, 0.4f, 0f);

    void Awake()
    {
        cardImage = GetComponent<Image>();
        if (cardImage != null) originalBorderColor = cardImage.color;
    }

    public void OnPointerEnter(PointerEventData eventData) { if (cardImage != null) cardImage.color = hoverColor; }
    public void OnPointerExit(PointerEventData eventData) { if (cardImage != null) cardImage.color = originalBorderColor; }
    public void OnPointerDown(PointerEventData eventData) { if (cardImage != null) cardImage.color = pressedColor; }
    public void OnPointerUp(PointerEventData eventData) { if (cardImage != null) cardImage.color = eventData.pointerCurrentRaycast.gameObject == gameObject ? hoverColor : originalBorderColor; }
}
