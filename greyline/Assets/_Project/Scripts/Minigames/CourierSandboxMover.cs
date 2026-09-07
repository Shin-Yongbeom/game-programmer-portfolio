using UnityEngine;
using UnityEngine.InputSystem;

namespace Greyline.Minigames
{
    /// <summary>Sandbox-only movement primitive. It owns no production player state or locomotion contract.</summary>
    public sealed class CourierSandboxMover : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 3.2f;
        [SerializeField] private float sneakSpeed = 1.6f;
        [SerializeField] private CharacterController characterController;

        public bool IsCrouching { get; private set; }

        public void Configure(float walk, float sneak)
        {
            walkSpeed = Mathf.Max(.1f, walk); sneakSpeed = Mathf.Max(.1f, sneak);
            if (characterController == null) characterController = GetComponent<CharacterController>();
        }

        public void Tick(bool sneak)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) { IsCrouching = false; return; }
            Vector2 input = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            if (input.sqrMagnitude > 1f) input.Normalize();
            IsCrouching = sneak && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
            Vector3 delta = new Vector3(input.x, 0f, input.y) * (IsCrouching ? sneakSpeed : walkSpeed) * Time.deltaTime;
            if (characterController != null) characterController.Move(delta);
            else transform.position += delta;
        }
    }
}
