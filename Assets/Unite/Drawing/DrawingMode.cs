namespace Unite.Drawing
{
    public enum DrawingMode
    {
        /// <summary>
        /// Uses pixel screen coordinates and renders as an overlay.
        /// </summary>
        ScreenOverlay,

        /// <summary>
        /// Uses pixel screen coordinates with depth.
        /// </summary>
        Screen,

        /// <summary>
        /// Uses normalized screen coordinates in the 0..1 range and renders as an overlay.
        /// </summary>
        NormalizedScreenOverlay,

        /// <summary>
        /// Uses normalized screen coordinates in the 0..1 range with depth.
        /// </summary>
        NormalizedScreen,

        /// <summary>
        /// Uses world coordinates.
        /// </summary>
        World,

        /// <summary>
        /// Uses coordinates local to the UCanvas GameObject.
        /// </summary>
        CanvasLocal
    }
}
