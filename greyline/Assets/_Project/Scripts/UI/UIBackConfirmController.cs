using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Greyline.UI
{
    /// Maps the existing Input System actions to consistent UI semantics. It never pauses gameplay.
    public sealed class UIBackConfirmController : MonoBehaviour
    {
        [SerializeField] private InputActionReference backAction;
        [SerializeField] private InputActionReference confirmAction;
        [SerializeField] private UnityEvent backRequested;
        [SerializeField] private UnityEvent confirmRequested;

        private void OnEnable()
        {
            Register(backAction, OnBackPerformed);
            Register(confirmAction, OnConfirmPerformed);
        }

        private void OnDisable()
        {
            Unregister(backAction, OnBackPerformed);
            Unregister(confirmAction, OnConfirmPerformed);
        }

        private static void Register(InputActionReference reference, System.Action<InputAction.CallbackContext> callback)
        {
            if (reference == null || reference.action == null) return;
            reference.action.performed += callback;
        }

        private static void Unregister(InputActionReference reference, System.Action<InputAction.CallbackContext> callback)
        {
            if (reference == null || reference.action == null) return;
            reference.action.performed -= callback;
        }

        private void OnBackPerformed(InputAction.CallbackContext context) => backRequested?.Invoke();
        private void OnConfirmPerformed(InputAction.CallbackContext context) => confirmRequested?.Invoke();

        public void RequestBack() => backRequested?.Invoke();
        public void RequestConfirm() => confirmRequested?.Invoke();
    }
}
