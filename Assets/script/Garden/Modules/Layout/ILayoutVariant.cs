namespace Garden.Layout
{
    // Vật có thể đổi hình theo một "hạt giống" (ví dụ tảng đá sinh bằng code). Cài đặt ở assembly của vật đó.
    public interface ILayoutVariant
    {
        void SetVariantSeed(int seed);
    }
}
