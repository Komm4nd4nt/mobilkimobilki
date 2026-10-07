using UnityEngine;

namespace RacingMobile.UI.MainMenu
{
    /// <summary>
    /// Slowly rotates the showcase vehicle in the main menu showroom.
    /// </summary>
    public class ShowroomRotator : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 12f;
        [SerializeField] private Vector3 rotationAxis = Vector3.up;

        private void Update()
        {
            transform.Rotate(rotationAxis * (rotationSpeed * Time.deltaTime), Space.World);
        }
    }
}
