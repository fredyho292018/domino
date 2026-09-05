using UnityEngine;

namespace Domino.UI
{
    public readonly struct TilePose
    {
        public readonly Vector2 Position;
        public readonly float Angle;
        public readonly float Scale;
        public TilePose(float x, float y, float angle = 0, float scale = 1) { Position = new Vector2(x, y); Angle = angle; Scale = scale; }
    }
    public static class BoardLayout
    {
        public const float TileGap = 3f;
        public static Vector2 HalfSize(TilePose pose) =>
            (pose.Angle % 180 == 90 ? new Vector2(23, 48) : new Vector2(48, 23)) * pose.Scale;

        /// <summary>Rebuild the whole chain using actual tile widths, then fit and center it on the felt.</summary>
        public static TilePose[] Arrange(int count, System.Func<int, bool> isDouble)
        {
            var poses = new TilePose[count];
            if (count == 0) return poses;
            int direction = 1;
            float rowWidth = 0;
            bool afterBend = false;
            for (int i = 0; i < count; i++)
            {
                bool crosswise = isDouble != null && isDouble(i);
                float angle = (direction == 1 ? 0 : 180) + (crosswise ? 90 : 0);
                float scale = crosswise ? .684f : .76f;
                var shape = new TilePose(0, 0, angle, scale);
                var half = HalfSize(shape);
                Vector2 position = Vector2.zero;
                if (i == 0) rowWidth = half.x * 2;
                else
                {
                    var previous = poses[i - 1];
                    var previousHalf = HalfSize(previous);
                    if (afterBend)
                    {
                        // Align the new row with the lower end of the vertical corner tile.
                        position = previous.Position + new Vector2(direction * (previousHalf.x + half.x + TileGap), -previousHalf.y);
                        rowWidth = half.x * 2;
                        afterBend = false;
                    }
                    else if (rowWidth + TileGap + half.x * 2 <= 650
                        || crosswise
                        || (isDouble != null && isDouble(i - 1))
                        || (i + 1 < count && isDouble != null && isDouble(i + 1)))
                    {
                        position = previous.Position + Vector2.right * direction * (previousHalf.x + half.x + TileGap);
                        rowWidth += TileGap + half.x * 2;
                    }
                    else
                    {
                        // A bend uses a normal tile between two horizontal normal tiles.
                        // Keep doubles on the straight run instead of packing them into the corner.
                        angle = 270;
                        half = HalfSize(new TilePose(0, 0, angle, scale));
                        position = previous.Position + new Vector2(direction * (previousHalf.x + half.x + TileGap), -half.y);
                        direction = -direction;
                        afterBend = true;
                    }
                }
                poses[i] = new TilePose(position.x, position.y, angle, scale);
            }
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var pose in poses)
            {
                min = Vector2.Min(min, pose.Position - HalfSize(pose));
                max = Vector2.Max(max, pose.Position + HalfSize(pose));
            }
            var center = (min + max) * .5f;
            float fit = Mathf.Min(1, Mathf.Min(920 / (max.x - min.x), 360 / (max.y - min.y)));
            for (int i = 0; i < count; i++)
            {
                var p = poses[i]; var position = (p.Position - center) * fit;
                poses[i] = new TilePose(position.x, position.y, p.Angle, p.Scale * fit);
            }
            return poses;
        }
        public static Vector2 CenterOffset(int count, System.Func<int, bool> isDouble = null)
        {
            if (count == 0) return Vector2.zero;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < count; i++)
            {
                var pose = Slot(i, isDouble != null && isDouble(i));
                var half = (pose.Angle % 180 == 90 ? new Vector2(23, 48) : new Vector2(48, 23)) * pose.Scale;
                min = Vector2.Min(min, pose.Position - half);
                max = Vector2.Max(max, pose.Position + half);
            }
            return (min + max) * .5f;
        }
        public static TilePose Slot(int index, bool isDouble)
        {
            var pose = Slot(index);
            // Leave clearance between adjacent rows when doubles stand crosswise.
            return isDouble ? new TilePose(pose.Position.x, pose.Position.y, pose.Angle + 90, .684f) : pose;
        }
        public static TilePose Slot(int index)
        {
            // Five rows fit all forty dealt tiles; bends continue downward.
            int row = index / 9, column = index % 9;
            if (column == 8) return new TilePose(row % 2 == 0 ? 320 : -320, 105 - row * 70, 270, .76f);
            float x = -266 + column * 76;
            if (row % 2 == 1) x = -x;
            return new TilePose(x, 140 - row * 70, row % 2 == 1 ? 180 : 0, .76f);
        }
    }
}
