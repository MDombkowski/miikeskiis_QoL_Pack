namespace PowerWheel
{
    /// <summary>
    /// The padlock on a locked power's icon, drawn in code (the game's fonts have no padlock, and no picture of one is
    /// known in its files): a light body with a dark keyhole under a light shackle, ringed with a dark outline so it reads
    /// over any icon. Pure arithmetic, so a check outside the game can draw it too.
    /// </summary>
    internal static class Padlock
    {
        internal const int Width = 24;
        internal const int Height = 30;

        internal const byte Clear = 0;
        internal const byte Light = 1;
        internal const byte Dark = 2;

        /// <summary>One value per pixel, row by row from the bottom (as Unity's textures count), Clear, Light or Dark.</summary>
        internal static byte[] Mask() => Mask(1);

        /// <summary>The same padlock drawn <paramref name="scale"/> times larger (Width * scale by Height * scale), its
        /// outline as much thicker (a power's whole icon, card B06).</summary>
        internal static byte[] Mask(int scale)
        {
            int width = Width * scale;
            int height = Height * scale;
            var shape = new byte[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    shape[y * width + x] = Shape((x + 0.5f) / scale, (y + 0.5f) / scale);
                }
            }
            // The outline: every clear pixel within the scale of a light one turns dark.
            var mask = (byte[])shape.Clone();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (shape[y * width + x] == Clear && TouchesLight(shape, width, height, x, y, scale))
                    {
                        mask[y * width + x] = Dark;
                    }
                }
            }
            return mask;
        }

        // The body, x 3 to 21 and y 1 to 17 with its keyhole (a round hole and a slot under it), and the shackle, half a
        // ring around (12, 17) between radius 5.5 and 8.5.
        private static byte Shape(float x, float y)
        {
            if (x >= 3f && x <= 21f && y >= 1f && y <= 17f)
            {
                float dx = x - 12f;
                float dy = y - 11f;
                bool hole = dx * dx + dy * dy <= 2.6f * 2.6f || (dx >= -1.1f && dx <= 1.1f && y >= 5f && y <= 11f);
                return hole ? Dark : Light;
            }
            float sx = x - 12f;
            float sy = y - 17f;
            float r2 = sx * sx + sy * sy;
            return sy >= 0f && r2 >= 5.5f * 5.5f && r2 <= 8.5f * 8.5f ? Light : Clear;
        }

        private static bool TouchesLight(byte[] shape, int width, int height, int x, int y, int reach)
        {
            for (int dy = -reach; dy <= reach; dy++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    // Round at a larger scale (the shackle's outline stays round); the 8 neighbours at 1, as drawn before.
                    bool near = reach == 1 || dx * dx + dy * dy <= reach * reach + reach;
                    if (near && (dx != 0 || dy != 0) && nx >= 0 && nx < width && ny >= 0 && ny < height && shape[ny * width + nx] == Light)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
