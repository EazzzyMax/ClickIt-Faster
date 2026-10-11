namespace ClickItMax.Features.Area
{
    internal static class BlockedAreaHitTestEngine
    {
        internal static bool PointInAnyBlockedUiRectangle(Vector2 point, IReadOnlyList<RectangleF> rectangles, RectangleF fullScreenRectangle)
        {
            for (int i = 0; i < rectangles.Count; i++)
                if (PointInBlockedUiRectangle(point, rectangles[i], fullScreenRectangle))
                    return true;


            return false;
        }

        internal static bool PointInBlockedUiRectangle(Vector2 point, RectangleF rect, RectangleF fullScreenRectangle)
        {
            if (BlockedAreaGeometryEngine.PointInUiRectangle(point, rect))
            {
                ClickPointDiagnosticTrace.UiHit(point, point, rect, "direct");
                return true;
            }

            Vector2 windowTopLeft = new(fullScreenRectangle.X, fullScreenRectangle.Y);
            if (windowTopLeft.X == 0f && windowTopLeft.Y == 0f)
                return false;

            Vector2 clientPoint = point - windowTopLeft;
            bool hit = BlockedAreaGeometryEngine.PointInUiRectangle(clientPoint, rect);
            if (hit) ClickPointDiagnosticTrace.UiHit(point, clientPoint, rect, "client-translated");
            return hit;
        }
    }
}
