using UnityEngine;

public class GrowTest : MonoBehaviour
{
    [SerializeField] float secondsToFull = 60f;   // bao nhiêu giây thì to hết cỡ
    float timer;                                   // đồng hồ đếm giây, bắt đầu từ 0

    void Update()   // chạy lặp lại MỖI KHUNG HÌNH
    {
        timer += Time.deltaTime;                              // cộng thêm thời gian trôi qua
        float t = Mathf.Clamp01(timer / secondsToFull);       // đổi thành số từ 0 đến 1
        transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, t);   // cỡ từ 0.2 đến 1
    }
}
