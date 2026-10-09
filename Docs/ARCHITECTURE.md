# Kiến trúc: mô-đun cho từng cơ chế

Mỗi sinh vật / cơ chế (rêu, cây leo, con vật, mưa...) là một **mô-đun** độc lập để dùng lại cho map khác. Mô-đun không gọi thẳng nhau.

## Cấu trúc thư mục

```
Assets/script/
  Garden/
    Core/                 Garden.Core    khuôn mô-đun, trạng thái, phiên làm việc, sự kiện, click (không biết mô-đun nào)
    Modules/
      Moss/               Garden.Moss    mô-đun rêu (logic, dữ liệu, hiển thị, shader)
      (Vines/, Animals/, Rain/ ... sau này)
    ProceduralBlob.cs ...                sinh hình bằng code (đá, bụi cây, cỏ)
  Shell/                  Garden.Shell   logic thuần của cửa sổ PIP (vùng chuột)
  Pip*.cs                                cửa sổ PIP, thanh tiêu đề, ControlPanel (phần "vỏ" của app)
Assets/Tests/EditMode/    Garden.Tests   test cho Core, Moss, Shell
```

Quy tắc phụ thuộc (do Unity ép bằng `.asmdef`): `Garden.Moss` -> `Garden.Core`. Mô-đun không tham chiếu mô-đun khác. `Garden.Core` không tham chiếu ai.

## Một mô-đun gồm những gì
Xem `Modules/Moss/` làm mẫu:
1. **Logic thuần** (không dùng Unity, có test): ví dụ `MossSimulation`.
2. **Phần dữ liệu lưu** (`[Serializable] struct`, ví dụ `MossSection`) nằm trong file lưu dưới tên mô-đun (`"moss"`).
3. **Component `IGardenModule`** (ví dụ `MossModule`): `Load` đọc dữ liệu, `Tick(ngày)` cho thời gian trôi qua, `Save` ghi dữ liệu.
4. Phần hiển thị (ví dụ `MossGrowth` + shader `Garden/RockMoss`).
5. Tuỳ chọn: `IAgePreviewable` (xem thử ở tuổi bất kỳ), `IGardenClickHandler` (phản ứng khi click vào vườn).

### Thêm một mô-đun mới
1. Tạo thư mục `Modules/<Tên>/` với `Garden.<Tên>.asmdef` (tham chiếu `Garden.Core`).
2. Viết logic thuần + test trước (RED), rồi cài đặt.
3. Viết struct dữ liệu và component `IGardenModule` với `Id` duy nhất.
4. Gắn component vào một object nằm dưới `Garden` trong cảnh (thêm vào `GardenSceneBuilder`). `GardenHost` tự tìm.
5. Muốn mô-đun này phản ứng với mô-đun khác: dùng `GardenEventBus` (xem `session.Events`), không tham chiếu trực tiếp.
6. Thêm `Garden.<Tên>` vào `references` của `Garden.Tests.asmdef`.

## Trạng thái và file lưu
- `GardenSession` là nơi **duy nhất** đọc/ghi file lưu (`%LOCALAPPDATA%\GardenChill\garden_state.json`; Editor dùng `garden_state.editor.json`). Mỗi mô-đun chỉ đụng tới phần của mình nên không ghi đè nhau.
- Phần của mô-đun chưa nạp được **giữ nguyên** khi lưu (không mất dữ liệu của mô-đun khác).
- Một mô-đun lỗi (ném exception) không làm hỏng các mô-đun còn lại: lỗi được ghi kèm tên mô-đun.
- Thời gian trôi qua (kể cả lúc tắt app, tối đa 365 ngày) được chia cho mọi mô-đun qua `Tick`. Đồng hồ chỉnh lùi thì bỏ qua.

### Đổi định dạng file lưu (QUAN TRỌNG)
Không bao giờ coi file bản cũ là "hỏng". Khi cần đổi:
1. Tăng `GardenState.CurrentVersion`.
2. Thêm một nhánh nâng cấp trong `GardenStateMigrator` cho bản trước và test cho nó (có sẵn mẫu cho bản 1).
3. `GardenStateStore` tự giữ bản sao `.pre-v2.bak` (hoặc tương tự) trước khi nâng cấp, `.bad` cho file hỏng, `.newer` cho file do app mới hơn tạo ra.

Thêm dữ liệu vào phần riêng của mô-đun (field mới trong struct) **không cần** tăng phiên bản: field thiếu sẽ nhận giá trị mặc định.

## Click
`PipHitTest` (thuần, có test) quyết định điểm chuột thuộc vùng nào: ngoài, vườn, thanh tiêu đề (kéo), nút, hoặc mép (đổi cỡ). `PipWindow` và `GardenClickRouter` đều dùng nó nên không có chuyện hai chỗ cùng nhận một click.
`GardenClickRouter` chỉ nhận click **trái** trong vùng vườn, bắn tia từ camera (đá có `MeshCollider`) rồi `GardenHost.DispatchClick` báo cho mọi `IGardenClickHandler`. Chuột phải để trống.

## Công cụ dev
- Menu `Tools/Garden/Dev/...`: xem thử ở ngày N, xoá file lưu của Editor.
- Dòng lệnh bản build: `-gardenAgeDays N` (xem thử, không ghi file), `-gardenStateFile <đường dẫn>` (dùng file lưu khác để thử nâng cấp mà không đụng vườn thật).
- Test: Window > General > Test Runner > EditMode (assembly `Garden.Tests`).
- Khi thử bản build có mở cửa sổ vườn: kiểm tra trước là chưa có Aquarium nào đang chạy, vì lệnh hiện/ẩn/tắt đi qua một cổng UDP chung (47321).
