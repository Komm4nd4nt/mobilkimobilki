using UnityEngine;
using UnityEngine.EventSystems;

namespace RacingMobile.MobileUI
{
    /// <summary>
    /// Virtual on-screen joystick for mobile analog steering.
    /// Supports clamped handle dragging and returns normalized horizontal axis (-1 to +1).
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("UI RectTransforms")]
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;

        [Header("Settings")]
        [SerializeField] private float handleRange = 60f;
        [SerializeField] private bool horizontalOnly = true;

        private Vector2 inputVector;

        public Vector2 InputVector => inputVector;
        public float Horizontal => inputVector.x;
        public float Vertical => inputVector.y;

        private void Start()
        {
            if (background == null) background = GetComponent<RectTransform>();
            if (handle == null && transform.childCount > 0)
            {
                handle = transform.GetChild(0).GetComponent<RectTransform>();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (background == null || handle == null) return;

            Vector2 position = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, background.position);
            Vector2 radius = background.sizeDelta / 2f;

            Vector2 delta = (eventData.position - position) / (radius.magnitude > 0f ? radius.x : handleRange);

            if (horizontalOnly)
            {
                delta.y = 0f;
            }

            inputVector = delta.magnitude > 1.0f ? delta.normalized : delta;
            handle.anchoredPosition = new Vector2(inputVector.x * handleRange, horizontalOnly ? 0f : inputVector.y * handleRange);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            inputVector = Vector2.zero;
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }
        }

        private void OnDisable()
        {
            inputVector = Vector2.zero;
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }
        }
    }
}
