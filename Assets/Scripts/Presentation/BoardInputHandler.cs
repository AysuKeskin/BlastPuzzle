using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlastPuzzle.Presentation
{
    // Turns a pointer press into a logical BoardPosition and hands it to the controller.
    //
    // This is the only class in the project that knows what a pixel is. Everything
    // downstream of it deals in rows and columns.
    //
    // No Update: the Input System raises an event when a press actually happens, so
    // nothing runs on the frames where the player is not touching the screen.
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

        private InputAction pointAction;
        private InputAction pressAction;

        // How far the board plane sits in front of the camera. Computed rather than
        // hardcoded so moving either object does not silently break the conversion.
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

        // Unsubscribing matters: the InputAction lives on the asset, which outlives this
        // component. A handler left attached would keep firing into a destroyed object.
        private void OnDisable()
        {
            pressAction.performed -= OnPressPerformed;
            pointAction.Disable();
            pressAction.Disable();
        }

        private void OnPressPerformed(InputAction.CallbackContext context)
        {
            // Where the pointer is at the moment of the press. Mouse, pen and finger all
            // report through <Pointer>, so this one read covers every platform.
            Vector2 screenPosition = pointAction.ReadValue<Vector2>();

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
