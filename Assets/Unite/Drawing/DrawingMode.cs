namespace Unite.Drawing
{
    public enum DrawingMode
    {
        // Use Pixel coordinate. Any drawings will overlay.
        ScreenSpaceOverlay,
        // Use Pixel coordinate. Drawings have Z coordinate. 
        ScreenSpace,
        // Use normalized screen coordinate. Any drawings will overlay.
        ClipSpaceOverlay,
        // Use normalized screen coordinate. Drawings have Z coordinate.
        ClipSpace,
        // Use World coordinate.
        WorldSpace
    }
}