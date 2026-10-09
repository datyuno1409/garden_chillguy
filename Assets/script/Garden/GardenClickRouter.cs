using Garden.Core;
using UnityEngine;

// Biến click chuột trái trong cửa sổ thành "click vào vườn": chỉ nhận khi PipHitTest xác định điểm chuột nằm trong vùng vườn
// (không phải thanh tiêu đề, nút bấm hay mép đổi kích thước), bắn tia từ camera, rồi báo cho các mô-đun qua GardenHost.
// Chuột phải được để trống.
public sealed class GardenClickRouter : MonoBehaviour
{
    [SerializeField] GardenHost host;
    [SerializeField] float maxDistance = 100f;

    PipWindow pip;

    void Start()
    {
        if (host == null) host = GetComponent<GardenHost>();
        pip = FindAnyObjectByType<PipWindow>();
    }

    void Update()
    {
        if (host == null || !Input.GetMouseButtonDown(0)) return;
        if (pip != null && !pip.IsGardenClick) return;

        Camera view = Camera.main;
        if (view == null) return;

        Ray ray = view.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance)) return;

        host.DispatchClick(new GardenClick(hit.point, hit.normal, hit.collider));
    }
}
