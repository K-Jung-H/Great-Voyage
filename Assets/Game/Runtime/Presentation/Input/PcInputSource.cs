using System;
using GreatVoyage.Application;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GreatVoyage.Presentation
{
    public sealed class PcInputSource : IDisposable
    {
        private readonly InputActionAsset instance;
        private readonly InputAction move, interact, back;
        private readonly IGameActions actions;

        public PcInputSource(InputActionAsset asset, IGameActions actions)
        {
            this.actions = actions;
            instance = UnityEngine.Object.Instantiate(asset);
            move = instance.FindAction("World/Move", true);
            interact = instance.FindAction("World/Interact", true);
            back = instance.FindAction("UI/Back", true);
            interact.performed += OnInteract;
            back.performed += OnBack;
            instance.Enable();
        }

        public void Poll()
        {
            var value = move.ReadValue<Vector2>();
            actions.Move(value.x, value.y);
        }

        private void OnInteract(InputAction.CallbackContext context) => actions.Interact();
        private void OnBack(InputAction.CallbackContext context) => actions.Back();
        public void Dispose()
        {
            instance.Disable();
            interact.performed -= OnInteract;
            back.performed -= OnBack;
            UnityEngine.Object.Destroy(instance);
        }
    }
}
