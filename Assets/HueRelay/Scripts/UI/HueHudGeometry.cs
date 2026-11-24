using UnityEngine;

namespace HueRelay
{
    /// <summary>Header-local logical rectangles; stack primary stats directly below the toolbar metadata.</summary>
    public struct HueHudGeometry
    {
        public Rect head, level, score, moves, best, progress;

        public static HueHudGeometry Calculate(float width, float boardWidth, float boardTop,
            float minimumTop, float cardHeight, float gap, float progressHeight)
        {
            float contentWidth = Mathf.Min(width - 8, Mathf.Max(240, boardWidth + 16));
            float height = cardHeight;
            float x = (width - contentWidth) * .5f;
            float y = minimumTop;
            float cardWidth = (contentWidth - 12) * .5f;
            // Metadata occupies only the center of the toolbar; the rightmost 112 px stay clear.
            float metadataWidth = Mathf.Min(140, width - 224);
            float metadataX = (contentWidth - metadataWidth) * .5f;
            return new HueHudGeometry
            {
                head = new Rect(x, y, contentWidth, height),
                level = new Rect(metadataX, 6 - y, metadataWidth, 32),
                score = new Rect(0, 0, cardWidth, cardHeight),
                moves = new Rect(cardWidth + 12, 0, cardWidth, cardHeight),
                best = new Rect(metadataX, 42 - y, metadataWidth, 28),
                progress = new Rect(x + 8, y + height + gap, contentWidth - 16, progressHeight)
            };
        }
    }
}
