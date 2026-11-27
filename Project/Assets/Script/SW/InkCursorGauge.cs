using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 실제 마우스 포인터 우측 상단을 항상 따라다니며,
/// 좌클릭(일반 잉크 / 스킬 시 특수 잉크) 또는 우클릭(발판 특수 잉크) 홀드 중
/// 현재 사용 중인 잉크 잔량을 도넛(Radial360) 게이지로 표시합니다.
/// </summary>
public class InkCursorGauge : MonoBehaviour
{
    [Header("UI 참조")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image fillImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("위치 설정 (마우스 포인터 기준)")]
    [Tooltip("마우스 포인터 기준 UI 오프셋 (기본: 정확히 우측)")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(40f, 0f);
    [SerializeField] private float fadeSpeed = 14f;

    [Header("색상 설정")]
    [SerializeField] private bool useStanceColorForNormalInk = true;
    [SerializeField] private Color normalInkFallbackColor = Color.white;
    [SerializeField] private Color specialInkColor = new Color(0.45f, 0.92f, 1f, 1f);
    [SerializeField] private Color lowInkColor = new Color(0.95f, 0.32f, 0.32f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.65f);

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private RectTransform parentRect;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
            backgroundImage.raycastTarget = false;
        }

        if (fillImage != null)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillOrigin = (int)Image.Origin360.Top;
            fillImage.fillClockwise = true;
            fillImage.raycastTarget = false;
        }

        CacheCanvasReference();
    }

    private void OnTransformParentChanged()
    {
        CacheCanvasReference();
    }

    private void LateUpdate()
    {
        UpdateCursorPosition();
        UpdateGaugeVisual();
    }

    private void CacheCanvasReference()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        parentRect = transform.parent as RectTransform;
        if (parentRect == null && parentCanvas != null)
        {
            parentRect = parentCanvas.transform as RectTransform;
        }
    }

    private void UpdateCursorPosition()
    {
        if (rectTransform == null) return;

        Vector2 screenPos = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;

        if (parentRect == null)
        {
            CacheCanvasReference();
        }

        if (parentRect != null)
        {
            Camera uiCam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? parentCanvas.worldCamera
                : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, uiCam, out Vector2 localPoint))
            {
                rectTransform.anchoredPosition = localPoint + cursorOffset;
            }
        }
        else
        {
            rectTransform.position = screenPos + cursorOffset;
        }
    }

    private void UpdateGaugeVisual()
    {
        Player player = Player.instance;
        PlayerStatus status = player != null ? player.status : null;

        bool blocked = status == null
            || status.HP <= 0
            || (GameManager.instance != null && GameManager.instance.isSetting);

        bool shouldShow = false;

        if (!blocked)
        {
            bool pointerOverUI = PlayerInputScript.IsPointerOverInteractiveUI();
            bool leftHeld = (!pointerOverUI && (Mouse.current != null ? Mouse.current.leftButton.isPressed : Input.GetMouseButton(0)))
                || (player.cursor != null && player.cursor.isMove);
            bool rightHeld = (!pointerOverUI && (Mouse.current != null ? Mouse.current.rightButton.isPressed : Input.GetMouseButton(1)))
                || (player.groundCursor != null && player.groundCursor.isMove);

            // 발판(우클릭) 또는 스킬 모드 공격(좌클릭 + isSkill)은 specialInk 사용
            bool useSpecialInk = rightHeld || (leftHeld && player.isSkill);
            bool useNormalInk = leftHeld && !player.isSkill && !rightHeld;

            if (useSpecialInk)
            {
                shouldShow = true;
                float ratio = status.maxSpecialInk > 0f ? status.specialInk / status.maxSpecialInk : 0f;
                bool isLow = status.specialInk <= status.minSpecialInk;
                ApplyFill(ratio, isLow ? lowInkColor : specialInkColor);
            }
            else if (useNormalInk)
            {
                shouldShow = true;
                float ratio = status.maxInk > 0f ? status.ink / status.maxInk : 0f;
                bool isLow = status.ink <= status.minInk;
                ApplyFill(ratio, isLow ? lowInkColor : normalInkFallbackColor);
            }
        }

        if (canvasGroup != null)
        {
            float targetAlpha = shouldShow ? 1f : 0f;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);
        }
    }

    private void ApplyFill(float ratio, Color color)
    {
        if (fillImage == null) return;

        fillImage.fillAmount = Mathf.Clamp01(ratio);
        fillImage.color = color;
    }
}
