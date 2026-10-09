# Kiến trúc: mô-đun cho từng cơ chế

Mỗi sinh vật / cơ chế (rêu, mưa, bố cục, sau này là cây leo, con vật...) là một **mô-đun** độc lập để dùng lại cho map khác. Mô-đun không gọi thẳng nhau.

## Cấu trúc thư mục

```text
Assets/script/
  Garden/
    Core/                 Garden.Core    khuôn mô-đun, trạng thái, cài đặt, phiên làm việc, sự kiện, click (không biết mô-đun nào)
    Modules/
      Moss/               Garden.Moss    rêu: mọc theo thời gian, bị bóc khi click, tự lành
      Rain/               Garden.Rain    mưa: lịch xác định theo seed, tăng tốc mọc, hạt mưa và ánh sáng
      Layout/             Garden.Layout  bố cục: đổi hình đá, ẩn/hiện nấm, cỏ, lá
      (Vines/, Animals/ ... sau này)
    ProceduralBlob.cs ...                sinh hình bằng code (đá, bụi cây, cỏ)
  Shell/                  Garden.Shell   logic thuần của cửa sổ PIP (vùng chuột)
  Pip*.cs, ControlPanel.cs, DesignerBoard.cs   phần "vỏ" của app
Assets/Tests/EditMode/    Garden.Tests   test cho Core, các mô-đun, Shell, và quy tắc dự án
```

Quy tắc phụ thuộc (do Unity ép bằng `.asmdef`): mỗi mô-đun chỉ tham chiếu `Garden.Core`. Mô-đun không tham chiếu mô-đun khác. `Garden.Core` không tham chiếu ai.

## Một mô-đun gồm những gì
Xem `Modules/Moss/` và `Modules/Rain/` làm mẫu:
1. **Logic thuần** (không dùng Unity, có test): ví dụ `MossSimulation`, `RainSchedule`.
2. **Dữ liệu lưu** (`[Serializable] struct`, ví dụ `MossSection`) nằm trong file lưu dưới tên mô-đun. Mô-đun không có gì để lưu (như mưa) thì `Save` trả trạng thái nguyên vẹn.
3. **Component `IGardenModule`**: `Load` đọc dữ liệu, `Tick(TimeWindow)` nhận khoảng thời gian thật đã trôi qua (kể cả lúc tắt app), `Save` ghi dữ liệu.
4. Phần hiển thị (shader, `RainView`...).
5. Tuỳ chọn, mỗi thứ một interface nhỏ: `IAgePreviewable` (xem thử ở tuổi bất kỳ), `IGardenClickHandler` (phản ứng khi click vào vườn), `IGardenConfigurable` (nhận cài đặt từ bảng thiết kế), `IGardenDesignerPanel` (bảng điều khiển của mô-đun trong bảng thiết kế).

### Thêm một mô-đun mới
1. Tạo thư mục `Modules/<Tên>/` với `Garden.<Tên>.asmdef` (tham chiếu `Garden.Core`).
2. Viết logic thuần + test trước (RED), rồi cài đặt.
3. Viết struct dữ liệu và component `IGardenModule` với `Id` duy nhất.
4. Gắn component vào một object nằm dưới `Garden` trong cảnh (thêm vào `GardenSceneBuilder`). `GardenHost` tự tìm.
5. Muốn mô-đun này tác động lên mô-đun khác: phát sự kiện qua `GardenEventBus` (xem bên dưới), không tham chiếu trực tiếp.
6. Muốn người chơi chỉnh được: viết `XxxDesignerPanel : IGardenDesignerPanel` trong thư mục mô-đun, nhận cài đặt ở `IGardenConfigurable.ApplySettings`, và thêm MỘT dòng vào `DesignerBoard.DefaultPanels()`.
7. Thêm `Garden.<Tên>` vào `references` của `Garden.Tests.asmdef`.
8. **Mỗi component (MonoBehaviour) phải nằm trong file riêng trùng tên class** (`ProjectRulesTests` kiểm tra). Sai thì scene lưu ra báo "missing script" dù Editor vẫn chạy.

## Sự kiện giữa các mô-đun
`GardenEventBus` (trong `session.Events`, đưa cho mô-đun qua `GardenContext`). Hiện có:
- `GardenDisturbance`: người chơi làm phiền vườn tại một điểm (rêu phát khi bị click). Con vật sau này nghe để bỏ chạy.
- `GrowthBoost`: môi trường làm cây mọc nhanh hơn, thêm `extraDays` ngày mọc (mưa phát). Rêu nghe và cộng vào tuổi. Cộng dồn nên **thứ tự cập nhật các mô-đun không quan trọng**, và mưa rơi lúc đóng app vẫn được tính vì mô-đun nhận `TimeWindow` chứ không chỉ số ngày.

## Trạng thái và file lưu
- `GardenSession` là nơi **duy nhất** đọc/ghi file lưu (`%LOCALAPPDATA%\GardenChill\garden_state.json`; Editor dùng `garden_state.editor.json`). Mỗi mô-đun chỉ đụng tới phần của mình nên không ghi đè nhau.
- Phần của mô-đun chưa nạp được **giữ nguyên** khi lưu (không mất dữ liệu của mô-đun khác).
- Một mô-đun lỗi (ném exception) không làm hỏng các mô-đun còn lại: lỗi được ghi kèm tên mô-đun.
- Thời gian trôi qua (tối đa 365 ngày cho một lần tắt app) được chia cho mọi mô-đun qua `Tick(TimeWindow)`. Đồng hồ chỉnh lùi thì bỏ qua.

### Đổi định dạng file lưu (QUAN TRỌNG)
Không bao giờ coi file bản cũ là "hỏng". Khi cần đổi:
1. Tăng `GardenState.CurrentVersion`.
2. Thêm một nhánh nâng cấp trong `GardenStateMigrator` cho bản trước và test cho nó (có sẵn mẫu cho bản 1).
3. `GardenStateStore` tự giữ bản sao `.pre-v2.bak` (hoặc tương tự) trước khi nâng cấp, `.bad` cho file hỏng, `.newer` cho file do app mới hơn tạo ra.

Thêm dữ liệu vào phần riêng của mô-đun (field mới trong struct) **không cần** tăng phiên bản: field thiếu sẽ nhận giá trị mặc định.

## Cài đặt và bảng thiết kế
Control Panel (tab "Thiết kế") là nơi người chơi thiết kế vườn; cửa sổ vườn chỉ để ngắm và click.
- Cài đặt nằm ở file riêng `%LOCALAPPDATA%\GardenChill\garden_settings.json`, cùng cách chia phần theo mô-đun (`GardenSettings`). Khác file lưu: đây là lựa chọn của người chơi, không phải thứ do thời gian thay đổi.
- `DesignerBoard` vẽ bảng của từng mô-đun. Chỉ nhận kết quả của một bảng khi người chơi thật sự chỉnh (`GUI.changed`) nên chỉ mở bảng ra thì không ghi gì. `DebouncedSettingsWriter` gộp các lần kéo thanh trượt thành một lần ghi, ghi ngay khi đóng, thử lại khi lỗi.
- `GardenHost` kiểm tra file mỗi giây (so thời điểm ghi) và đưa cài đặt cho mọi `IGardenConfigurable` ngay, không cần khởi động lại cửa sổ vườn. Mỗi thành phần chỉ đọc phần của mình.
- Các vị trí tuỳ chỉnh được của bố cục nằm ở **một nơi duy nhất**, `LayoutCatalog`, cho cả công cụ dựng cảnh và bảng thiết kế. Thêm vật tuỳ chỉnh mới: thêm một dòng ở đó và gắn `LayoutSlot` với id đó.

## Click
`PipHitTest` (thuần, có test) quyết định điểm chuột thuộc vùng nào: ngoài, vườn, thanh tiêu đề (kéo), nút, hoặc mép (đổi cỡ). `PipWindow` và `GardenClickRouter` đều dùng nó nên không có chuyện hai chỗ cùng nhận một click.
`GardenClickRouter` chỉ nhận click **trái** trong vùng vườn, bắn tia từ camera (đá có `MeshCollider`) rồi `GardenHost.DispatchClick` báo cho mọi `IGardenClickHandler`. Chuột phải để trống.

### Ví dụ: click phá rêu
Click vào đá -> `MossModule.OnGardenClick` thêm một vết bóc (`MossDamage`, tối đa 16 vết, click lặp thì vết rộng ra tới 2 lần, rêu lành dần sau `healDays`). Vết được lưu trong phần `moss` của file lưu, đẩy sang shader qua mảng toàn cục (`MossGrowth.SetHits`), và mô-đun phát `GardenDisturbance`. Vật liệu có `_MossDamageable = 0` (mô đất nền) không bị bóc.

### Ví dụ: mưa
`RainSchedule` chia thời gian thành ô 6 giờ; mỗi ô có thể có một trận 20-90 phút, vị trí và độ dài xác định theo (seed vườn, ô). Không lưu lịch sử mưa mà tính lại được, kể cả cho lúc đóng app. Tần suất cao chỉ **thêm** ô mưa (không xáo lịch cũ). Mưa người chơi gọi (nút "Mưa ngay") gộp với mưa tự nhiên, phần chồng không tính hai lần.

## Chặn chạy hai bản
`SingleInstance` dùng một Mutex theo tên app: bản chạy sau tự thoát, và với cửa sổ vườn thì nhờ bản đang chạy hiện lên (lệnh `show`). Cần thiết vì các lệnh hiện/ẩn/tắt đi qua một cổng UDP chung (47321), hai bản cùng chạy sẽ làm lệnh đến nhầm bản.

## Công cụ dev
- Menu `Tools/Garden/Dev/...`: xem thử rêu ở ngày N, xem thử mưa, xoá file lưu của Editor.
- Dòng lệnh bản build (xem `GardenArgs`): `-gardenAgeDays N` (xem thử, không ghi file), `-gardenStateFile`, `-gardenSettingsFile` (dùng file khác để thử mà không đụng dữ liệu thật), `-gardenScreenshot <path>` + `-gardenScreenshotDelay N` (tự chụp màn hình để kiểm tra bản build), `-gardenTab N` (Control Panel).
- Test: Window > General > Test Runner > EditMode (assembly `Garden.Tests`).
- Khi thử bản build có mở cửa sổ vườn: kiểm tra trước là chưa có Aquarium nào đang chạy (`Get-Process Aquarium`), vì lệnh hiện/ẩn/tắt đi qua một cổng UDP chung; nên dùng file lưu/cài đặt tạm.
