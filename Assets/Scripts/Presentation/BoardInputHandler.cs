using System.Collections.Generic;
using UnityEngine.EventSystems;
using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlastPuzzle.Presentation
{
    // Turns a screen tap into a board cell. The only class that knows about pixels.
    public sealed class BoardInputHandler : MonoBehaviour
    {
        private const string GameplayMapName = "Gameplay";
        private const string PointActionName = "Point";
        private const string PressActionName = "Press";

        [SerializeField]
        private InputActionAsset inputActions;

        [SerializeField]
        private Camera gameplayCamera;

        [SerializeField]
        private BoardView boardView;

        [SerializeField]
        private GameplayController gameplayController;

        public bool InputBlocked { get; set; }

        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private PointerEventData pointerEvent;
        private EventSystem pointerEventSystem;

        public bool IsOverUI(Vector2 position)
        {
            var events = EventSystem.current;
            if (events == null) return false;
            if (pointerEvent == null || pointerEventSystem != events)
            {
                pointerEventSystem = events;
                pointerEvent = new PointerEventData(events);
            }
            pointerEvent.Reset();
            pointerEvent.position = position;
            uiHits.Clear();
            // Raycast now: cached IsPointerOverGameObject can describe the previous
            // frame when queried from an InputAction callback.
            events.RaycastAll(pointerEvent, uiHits);
            foreach (var hit in uiHits)
                if (hit.module is UnityEngine.UI.GraphicRaycaster) return true;
            return false;
        }

        private InputAction pointAction;
        private InputAction pressAction;
        private float DistanceToBoardPlane =>
            Mathf.Abs(boardView.transform.position.z - gameplayCamera.transform.position.z);

        private void Awake()
        {
            InputActionMap gameplayMap = inputActions.FindActionMap(GameplayMapName, throwIfNotFound: true);
            pointAction = gameplayMap.FindAction(PointActionName, throwIfNotFound: true);
            pressAction = gameplayMap.FindAction(PressActionName, throwIfNotFound: true);
        }

        private void OnEnable()
        {
            pressAction.performed += OnPressPerformed;
            pointAction.Enable();
            pressAction.Enable();
        }
        private void OnDisable()
        {
            pressAction.performed -= OnPressPerformed;
            pointAction.Disable();
            pressAction.Disable();
        }

        private void OnPressPerformed(InputAction.CallbackContext context)
        {
            if (InputBlocked) return;

            Vector2 screenPosition = pointAction.ReadValue<Vector2>();

            if (IsOverUI(screenPosition)) return;

            Vector3 worldPosition = gameplayCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, DistanceToBoardPlane));

            // Off the board entirely: not an error, just nothing to do.
            if (!boardView.TryGetBoardPosition(worldPosition, out BoardPosition position))
            {
                return;
            }

            gameplayController.HandleBlockSelected(position);
        }
    }
}
