using System;

namespace Garden.Shell
{
    // Mép hoặc góc nào của cửa sổ PIP đang được chỉ vào
    [Flags]
    public enum PipEdge { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }

    // Điểm chuột đang nằm ở vùng nào của cửa sổ PIP. Mỗi vùng có đúng một chủ: không thể vừa là nút bấm vừa là vùng click vườn.
    public enum PipRegion
    {
        Outside,           // ngoài cửa sổ
        Garden,            // trong vườn: click ở đây là tương tác với cây cỏ
        TitleBarDrag,      // thanh tiêu đề (phần bên trái các nút): giữ chuột trái để kéo cửa sổ
        TitleBarButtons,   // vùng 3 nút Ẩn / Về Control Panel / Tắt
        ResizeEdge,        // mép hoặc góc: kéo để đổi kích thước
    }

    public readonly struct PipLayout
    {
        public readonly float edgeSize;          // bề dày vùng mép để bắt chuột đổi kích thước (pixel)
        public readonly float titleBarHeight;    // 0 = chưa có thanh tiêu đề
        public readonly float buttonsWidth;      // bề rộng vùng nút, tính từ mép phải

        public PipLayout(float edgeSize, float titleBarHeight, float buttonsWidth)
        {
            this.edgeSize = edgeSize;
            this.titleBarHeight = titleBarHeight;
            this.buttonsWidth = buttonsWidth;
        }
    }

    public readonly struct PipHit
    {
        public readonly PipRegion region;
        public readonly PipEdge edges;   // chỉ có nghĩa khi region = ResizeEdge

        public PipHit(PipRegion region, PipEdge edges = PipEdge.None)
        {
            this.region = region;
            this.edges = edges;
        }
    }

    // Logic thuần quyết định một điểm chuột thuộc vùng nào, để mọi chỗ xử lý chuột (kéo, đổi cỡ, nút, click vườn)
    // dùng chung một nguồn sự thật thay vì mỗi chỗ tự tính.
    // Toạ độ theo kiểu Unity: gốc ở góc dưới-trái của cửa sổ, y hướng lên.
    public static class PipHitTest
    {
        public static PipHit Classify(float x, float y, float width, float height, PipLayout layout)
        {
            if (x < 0f || y < 0f || x >= width || y >= height) return new PipHit(PipRegion.Outside);

            bool hasTitleBar = layout.titleBarHeight > 0f;
            bool inTitleBar = hasTitleBar && y >= height - layout.titleBarHeight;

            // Vùng nút được ưu tiên hơn mép: bấm nút ở sát mép không bị hiểu thành đổi kích thước
            if (inTitleBar && layout.buttonsWidth > 0f && x >= width - layout.buttonsWidth)
                return new PipHit(PipRegion.TitleBarButtons);

            PipEdge edges = PipEdge.None;
            if (x < layout.edgeSize) edges |= PipEdge.Left;
            else if (x >= width - layout.edgeSize) edges |= PipEdge.Right;
            if (y < layout.edgeSize) edges |= PipEdge.Bottom;
            else if (y >= height - layout.edgeSize) edges |= PipEdge.Top;
            if (edges != PipEdge.None) return new PipHit(PipRegion.ResizeEdge, edges);

            return new PipHit(inTitleBar ? PipRegion.TitleBarDrag : PipRegion.Garden);
        }
    }
}
