using System.ComponentModel;

namespace MenUI
{
    /// <summary>How a menu too tall for the window is fitted.</summary>
    internal enum FitMode
    {
        /// <summary>Shrink it, keeping its top where it is, down to the smallest size; then move it up as well.</summary>
        [Description("Shrink, then move up")]
        ShrinkThenMoveUp,

        /// <summary>Move it up as far as allowed; then shrink it as well.</summary>
        [Description("Move up, then shrink")]
        MoveUpThenShrink,

        /// <summary>Only shrink it, keeping its top where it is, however small that makes it.</summary>
        [Description("Shrink only")]
        ShrinkOnly,
    }
}
