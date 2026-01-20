namespace Unite.Drawing
{
    public enum DrawingMode
    {
        // Use Pixel coordinate. Any drawings will overlay.
        ScreenSpaceOverlay,
        // Use Pixel coordinate. Drawings have Z coordinate. 
        ScreenSpace,
        // Use normalized screen coordinate. Any drawings will overlay.
        ViewSpaceOverlay,
        // Use normalized screen coordinate. Drawings have Z coordinate.
        ViewSpace,
        // Use World coordinate.
        WorldSpace
    }
}