using UnityEngine;
using UnityEngine.EventSystems;

namespace RacingMobile.MobileUI
{
    /// <summary>
    /// UI Button that tracks continuous press/hold state for touch controls (Gas, Brake, Steer).
    /// Works with touch screens and mouse in Editor.
    /// </summary>
    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private bool isPressed;

        public bool IsPressed => isPressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // If finger slides off the button bounds, release the press
            isPressed = false;
        }

        private void OnDisable()
        {
            isPressed = false;
        }
    }
}
