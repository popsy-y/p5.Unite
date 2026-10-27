using UnityEngine;

namespace Unite.Canvas
{
    public partial class UCanvas
    {
        // Editor only in effect. The field stays in players as well so that the serialized
        // layout of UCanvas is the same in the Editor and in builds.
        [Tooltip("Editor only: draws into the Scene View camera instead of the target camera after the Scene View was focused, until the Game view is focused.")]
        [SerializeField] private bool _drawInSceneView = true;

        private Camera _targetCamera;
        private bool _fallbackWarned;

        /// <summary>
        /// Sets the camera this canvas draws to. Pass null to follow <see cref="Camera.main"/>.
        /// </summary>
        /// <remarks>
        /// When the camera is destroyed, the canvas falls back to <see cref="Camera.main"/>,
        /// and draws nothing when there is no main camera either.
        /// </remarks>
        public void SetCamera(Camera cam)
        {
            _targetCamera = cam;
            _fallbackWarned = false;
        }

        /// <summary>The camera the canvas draws to, or null when there is none.</summary>
        internal Camera ActiveCamera => ResolveCamera();

        private Camera ResolveCamera()
        {
#if UNITY_EDITOR
            var sceneView = FocusedSceneView();
            if (_drawInSceneView && sceneView)
                return sceneView.camera;
#endif
            if (_targetCamera)
                return _targetCamera;

            var mainCamera = Camera.main;

            // A target that compares equal to null without being a null reference was destroyed.
            // Following Camera.main because no camera was set is not a fallback.
            if (!ReferenceEquals(_targetCamera, null) && mainCamera && !_fallbackWarned)
            {
                Debug.LogWarning(
                    $"p5.Unite: The target camera of '{name}' was destroyed. Falling back to Camera.main ('{mainCamera.name}')."
                );
                _fallbackWarned = true;
            }

            return mainCamera;
        }

#if UNITY_EDITOR
        // The Scene View focused last, or null after the Game view was focused. Focusing any
        // other window (Hierarchy, Inspector, ...) keeps it, so the Scene View can be watched
        // while working in other panels.
        private static UnityEditor.SceneView _focusedSceneView;

        private static UnityEditor.SceneView FocusedSceneView()
        {
            var window = UnityEditor.EditorWindow.focusedWindow;
            if (window is UnityEditor.SceneView sceneView)
                _focusedSceneView = sceneView;
            else if (window != null && window.GetType().Name == "GameView") // GameView is internal
                _focusedSceneView = null;

            return _focusedSceneView;
        }
#endif
    }
}
