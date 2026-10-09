using UnityEngine;

namespace Garden.Core
{
    // Người chơi vừa làm phiền khu vườn tại một điểm (ví dụ click phá rêu).
    // Mô-đun gây ra sự kiện chỉ việc phát đi; mô-đun khác (ví dụ con vật bỏ chạy) tự nghe, không cần biết nhau.
    public readonly struct GardenDisturbance
    {
        public readonly Vector3 point;
        public readonly float amount;     // mức làm phiền 0..1
        public readonly string source;    // Id của mô-đun gây ra

        public GardenDisturbance(Vector3 point, float amount, string source)
        {
            this.point = point;
            this.amount = amount;
            this.source = source;
        }
    }

    // Môi trường giúp cây cỏ mọc nhanh hơn: thêm số "ngày mọc" bổ sung (ví dụ mưa rơi 1 giờ thì thêm 3 giờ mọc).
    // Cộng dồn nên thứ tự các mô-đun được cập nhật không quan trọng.
    public readonly struct GrowthBoost
    {
        public readonly double extraDays;
        public readonly string source;

        public GrowthBoost(double extraDays, string source)
        {
            this.extraDays = extraDays;
            this.source = source;
        }
    }
}
