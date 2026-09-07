using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Greyline.Interaction
{
    /// <summary>
    /// Thin production input adapter: turns the shared <c>Player/Interact</c> action into a plain
    /// C# event / UnityEvent. Gameplay code (an interaction driver, a sandbox tester, a future
    /// interaction router) subscribes to <see cref="Interacted"/> and never names a key.
    ///
    /// Deliberately not an interaction framework: it resolves no targets, holds no interaction
    /// state, and knows nothing about <see cref="Interactable"/> or GameFlags.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionInput : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string actionName = "Interact";
        [SerializeField] private UnityEvent onInteract = new();

        private InputAction interactAction;

        /// <summary>Raised once on the frame the Interact action is pressed.</summary>
        public event Action Interacted;

        /// <summary>
        /// True when the configured action resolves from the assigned asset. Safe to call in the
        /// Editor before Awake (used by the sandbox validator): it resolves on demand.
        /// </summary>
        public bool HasResolvedAction
        {
            get
            {
                if (interactAction == null)
                {
                    ResolveAction();
                }

                return interactAction != null;
            }
        }

        /// <summary>Editor-tool / scene wiring entry point so scene setup stays code-owned.</summary>
        public void Configure(InputActionAsset actions)
        {
            inputActions = actions;
            ResolveAction();
        }

        private void Awake()
        {
            ResolveAction();
        }

        private void OnEnable()
        {
            if (interactAction != null && !interactAction.enabled)
            {
                interactAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (interactAction != null && interactAction.enabled)
            {
                interactAction.Disable();
            }
        }

        private void Update()
        {
            if (Progression.ProductionSession.GameplayBlocked) return;
            if (interactAction != null && interactAction.WasPressedThisFrame())
            {
                Interacted?.Invoke();
                onInteract?.Invoke();
            }
        }

        private void ResolveAction()
        {
            interactAction = inputActions != null
                ? inputActions.FindAction($"{actionMapName}/{actionName}", false)
                : null;
        }
    }
}
