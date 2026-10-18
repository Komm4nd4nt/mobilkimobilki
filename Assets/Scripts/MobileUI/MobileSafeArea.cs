using UnityEngine;

namespace RacingMobile.MobileUI
{
    /// <summary>
    /// Adjusts the RectTransform to respect the mobile device's safe area
    /// (e.g. camera notches, curved screen corners, and home indicator bars).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MobileSafeArea : MonoBehaviour
    {
        private RectTransform panelRect;
        private Rect lastSafeArea = Rect.zero;
        private Vector2Int lastScreenSize = Vector2Int.zero;
        private ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;

        private void Awake()
        {
            panelRect = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void Update()
        {
            if (lastSafeArea != Screen.safeArea || 
                lastScreenSize.x != Screen.width || 
                lastScreenSize.y != Screen.height || 
                lastOrientation != Screen.orientation)
            {
                ApplySafeArea();
            }
        }

        private void ApplySafeArea()
        {
            if (panelRect == null) return;

            Rect safeArea = Screen.safeArea;
            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            lastOrientation = Screen.orientation;

            // Convert safe area rectangle from absolute pixels to normalized anchor coordinates
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            // Safety check against zero/invalid values
            if (anchorMin.x >= 0 && anchorMin.y >= 0 && anchorMax.x <= 1 && anchorMax.y <= 1)
            {
                panelRect.anchorMin = anchorMin;
                panelRect.anchorMax = anchorMax;
            }
        }
    }
}
