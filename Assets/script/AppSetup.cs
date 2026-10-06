using UnityEngine;

public class AppSetup : MonoBehaviour
{
    void Awake()
    {
        Application.runInBackground = true;   // game không đứng hình khi bạn bấm sang app khác
        QualitySettings.vSyncCount = 0;        // tắt vSync, nếu không dòng dưới sẽ không có tác dụng
        Application.targetFrameRate = 30;      // giới hạn 30 khung hình/giây để đỡ tốn máy
    }
}
