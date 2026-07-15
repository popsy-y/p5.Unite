namespace Unite.Drawing
{
    /// <summary>
    /// Selects the GPU submission path used for shape meshes.
    /// </summary>
    public enum DrawingExecutionMode
    {
        /// <summary>Submits all shapes in one indirect instanced draw.</summary>
        Indirect,

        /// <summary>Submits one mesh per shape for rendering diagnostics.</summary>
        Sequential
    }
}
