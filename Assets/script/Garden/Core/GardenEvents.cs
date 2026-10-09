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
}
