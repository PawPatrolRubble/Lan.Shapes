using System;

namespace Lan.Shapes.Scaling
{
    /// <summary>
    /// Immutable per-instance configuration for <see cref="ViewportScalingService"/>.
    /// Use this instead of the static defaults to support multiple concurrent <c>SketchBoard</c>
    /// instances with independent zoom levels.
    /// </summary>
    public sealed class ViewportScalingOptions
    {
        /// <summary>Stroke thickness at scale 1.0 (default: 1.0).</summary>
        public double BaseStrokeThickness { get; }

        /// <summary>Drag handle size at scale 1.0 (default: 8.0).</summary>
        public double BaseDragHandleSize { get; }

        public ViewportScalingOptions(double baseStrokeThickness = 1.0, double baseDragHandleSize = 8.0)
        {
            BaseStrokeThickness = baseStrokeThickness;
            BaseDragHandleSize  = baseDragHandleSize;
        }

        public static readonly ViewportScalingOptions Default = new ViewportScalingOptions();
    }

    /// <summary>
    /// Provides centralized services for adjusting visual elements based on viewport scaling.
    /// This ensures consistent appearance of shapes regardless of zoom level.
    /// <para>
    /// <b>Usage note:</b> The static base values are intentionally read-only process-wide defaults.
    /// If you run multiple <c>SketchBoard</c> instances simultaneously, pass a per-instance
    /// <see cref="ViewportScalingOptions"/> to the overloaded instance helpers instead.
    /// </para>
    /// </summary>
    public static class ViewportScalingService
    {
        // Read-only process-wide defaults. For per-instance configuration use ViewportScalingOptions.
        public static readonly double BaseStrokeThickness = 1.0;
        public static readonly double BaseDragHandleSize  = 8.0;

        /// <summary>
        /// Calculates the appropriate stroke thickness based on the current scale factor.
        /// Formula: <c>Base / scale</c> — keeps the on-screen pixel width constant as the
        /// viewport zooms in or out.
        /// </summary>
        /// <param name="scale">The current viewport scale factor (must be &gt; 0).</param>
        public static double CalculateStrokeThickness(double scale)
        {
            if (scale <= 0) scale = 1.0;
            return BaseStrokeThickness / scale;
        }

        /// <summary>
        /// Calculates the appropriate drag handle size based on the current scale factor.
        /// Same linear formula as <see cref="CalculateStrokeThickness"/>.
        /// </summary>
        /// <param name="scale">The current viewport scale factor (must be &gt; 0).</param>
        public static double CalculateDragHandleSize(double scale)
        {
            if (scale <= 0) scale = 1.0;
            return BaseDragHandleSize / scale;
        }

        /// <summary>
        /// Seed-only helper for stroke thickness from viewport pixel dimensions when
        /// an explicit zoom scale is not yet available (e.g. pre-host configuration).
        /// <para>
        /// <b>Not a live driver after the board is attached.</b> Runtime stroke/handle
        /// sizing must use <see cref="CalculateStrokeThickness(double)"/> /
        /// <see cref="CalculateStrokeThickness(double, ViewportScalingOptions)"/> with
        /// the viewer <c>LocalScale</c>.
        /// </para>
        /// <para>
        /// <b>Formula derivation:</b> <c>1.8 ^ (log2(w+h) - 10)</c>.
        /// At the reference viewport of 1024×768 (w+h ≈ 1792, log2 ≈ 10.8) this yields ~1.5 px.
        /// Base 1.8 gives gentle exponential growth: large viewports get proportionally thicker
        /// lines without becoming visually dominant on small canvases.
        /// Edge case: at w+h ≤ 0 the method falls back to <see cref="BaseStrokeThickness"/>.
        /// </para>
        /// </summary>
        [Obsolete("Seed-only. Prefer CalculateStrokeThickness(LocalScale, options) as the live scale authority.")]
        public static double CalculateStrokeThicknessFromViewportSize(double viewportWidth, double viewportHeight)
        {
            var sum = viewportWidth + viewportHeight;
            if (sum <= 0) return BaseStrokeThickness;
            return Math.Pow(1.8, Math.Log2(sum) - 10);
        }

        // ── Per-instance overloads ────────────────────────────────────────────────

        /// <inheritdoc cref="CalculateStrokeThickness(double)"/>
        public static double CalculateStrokeThickness(double scale, ViewportScalingOptions options)
        {
            if (scale <= 0) scale = 1.0;
            return options.BaseStrokeThickness / scale;
        }

        /// <inheritdoc cref="CalculateDragHandleSize(double)"/>
        public static double CalculateDragHandleSize(double scale, ViewportScalingOptions options)
        {
            if (scale <= 0) scale = 1.0;
            return options.BaseDragHandleSize / scale;
        }
    }
}
