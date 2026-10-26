# Báo cáo kiểm tra (audit) dự án Garden Chill

- **Phạm vi:** toàn bộ repo ở commit `f6fef63` (nhánh `claude/blissful-cori-j989un`). Đã đọc hết 61 file C# (~4.500 dòng code, ~2.500 dòng test), 4 shader, 7 script Python, `.gitignore`, `.gitattributes`, `Packages/manifest.json`, scene và lịch sử Git.
- **Không sửa bất kỳ file code nào.** Báo cáo chỉ ghi nhận và đề xuất.
- **Thang điểm:** **Tốt** / **Cần cải thiện** / **Nghiêm trọng**.
- **Cách đọc vị trí:** `đường/dẫn/File.cs:12-15` là file và số dòng.

> **Lưu ý quan trọng:** đây là game thư giãn "khu vườn trên màn hình", **không có** nhân vật, máu, tấn công, sát thương, vật phẩm, tiền tệ hay chơi online.
> Những câu hỏi về các thứ đó được trả lời theo phần tương đương trong dự án (các "mô-đun" rêu, mưa, bố cục), hoặc ghi rõ là không áp dụng.

## Bảng điểm nhanh

| Mục | Điểm | Một câu tóm tắt |
|---|---|---|
| A. Code lặp lại | **Cần cải thiện** (nhẹ) | Kiến trúc dùng chung tốt; có ~10 cụm code chép lại ở phần "vỏ" app và công cụ Editor. |
| B. Cấu trúc module | **Cần cải thiện** (nhẹ) | Không có phụ thuộc vòng tròn (Unity ép bằng `.asmdef`). Có 2 file quá lớn; nhiều thông số viết cứng trong code. |
| C. Hiệu năng | **Tốt** | Giới hạn 30 FPS, 5 FPS khi ẩn; không tạo/huỷ object liên tục. Chỉ vài chỗ lãng phí nhỏ. |
| D. Lỗi tiềm ẩn | **Cần cải thiện**, có **1 điểm Nghiêm trọng** | Có đường mất dữ liệu vườn khi đọc file lỗi tạm thời; 2 lỗi logic về cài đặt; vài chỗ có thể null; 2 file code thừa. |
| E. Bảo mật & gian lận | **Tốt** (gần như không áp dụng) | Không online, không tiền tệ. Không có khoá bí mật trong code hay lịch sử Git. |
| F. Kiểm thử | **Cần cải thiện** | 184 test cho logic thuần rất tốt, nhưng không có CI, không có test tích hợp, các lỗi ở mục D chưa có test. |
| G. Git & tài nguyên | **Cần cải thiện** (nhẹ) | Git LFS dùng đúng, không có file lớn trong lịch sử. Thiếu vài dòng `.gitignore`; texture đá có thể có phần do AI tạo. |

**Về build:** môi trường kiểm tra (container Linux trên cloud) **không có Unity Editor hay .NET SDK**, nên **không chạy được build hay test C#**. Ghi chú trong commit `f6fef63` nói "EditMode tests: 184 passed", và số test đếm được trong mã nguồn cũng đúng là 184, nhưng kết quả này chưa được kiểm chứng lại trong lần audit này. 7 script Python đều qua kiểm tra cú pháp (`ast.parse`).

---

## A. Code lặp lại — **Cần cải thiện** (nhẹ)

### A1. Các "nhân vật" có dùng chung code không?
Không có nhân vật. Phần tương đương là các **mô-đun** (rêu, mưa, bố cục), và chúng **dùng chung khung rất tốt** → **Tốt**:
- Mọi mô-đun cài cùng interface `IGardenModule` (`Assets/script/Garden/Core/GardenModule.cs:36-48`): `Load` / `Save` / `Tick`.
- Lưu/tải, đếm thời gian, bắt lỗi từng mô-đun đều nằm **một chỗ** ở `GardenSession.cs`. Mô-đun không tự viết lại.
- Cách "mỗi mô-đun một phần JSON" dùng chung cho cả file lưu lẫn file cài đặt qua `GardenSections.cs:9-57`.
- Bắt lỗi "một thành phần lỗi không kéo sập cái khác" dùng chung: `ClickDispatcher.cs`, `ConfigurableApplier.cs`, `GardenEventBus.cs:34-44`.

### A2. Các đoạn code giống hoặc gần giống nhau

| # | Đoạn lặp | Vị trí | Rủi ro |
|---|---|---|---|
| 1 | Bật 30 FPS + chạy nền | `Assets/script/AppSetup.cs:7-9` và `Assets/script/ControlPanel.cs:41-43` | Đổi FPS một nơi, quên nơi kia. |
| 2 | Hàm bật một file `.exe` (kiểm tra tồn tại, `Process.Start`, bắt lỗi) | `Assets/script/ControlPanel.cs:105-129` (`LaunchAquarium`) và `Assets/script/PipWindow.cs:384-406` (`LaunchControlPanel`) | Sửa lỗi một bên, bên kia vẫn lỗi. |
| 3 | **Công thức độ cao mô đất** chép lại bằng số "ma thuật" | `Assets/Editor/PaintedBoulderSetup.cs:28-33` chép từ `Assets/Editor/GardenSceneBuilder.cs:161-170` | **Đáng chú ý nhất:** đổi `MoundCenter`/`MoundSize` trong SceneBuilder thì tảng đá vẽ tay sẽ lơ lửng hoặc chìm, không có cảnh báo. |
| 4 | Mở scene "additive" nếu chưa mở | `Assets/Editor/GardenSceneBuilder.cs:492-497`, `Assets/Editor/PipBuild.cs:74-79` (giống hệt), `Assets/Editor/PaintedBoulderSetup.cs:61-63` | Thấp. |
| 5 | Đường dẫn scene `"Assets/cozy garden.unity"` viết lại 3 lần | `GardenSceneBuilder.cs:18`, `PaintedBoulderSetup.cs:17`, `PipBuild.cs:12` | Đổi tên scene thì phải sửa 3 chỗ. |
| 6 | Đường dẫn model/texture tảng đá | `PaintedBoulderSetup.cs:18-19` và `RockPreview.cs:12-13` | Thấp. |
| 7 | Bọc "chạy batch, lỗi thì thoát mã 1" | `PaintedBoulderSetup.cs:38-51` và `RockPreview.cs:25-38` | Thấp. |
| 8 | Vòng đời mesh tự sinh (OnEnable/OnValidate/OnDisable + huỷ mesh) | `Assets/script/Garden/GrassTuft.cs:39-41, 96-102`, `BlobScatter.cs:57-59, 118-124`, `ProceduralBlob.cs:68-70, 116-120` | Thấp; có thể gom vào một lớp cha. |
| 9 | "Thay phần tử cùng id, không có thì thêm" | `Assets/script/Garden/Modules/Layout/LayoutSettings.cs:34-41` lặp lại logic của `Assets/script/Garden/Core/GardenSections.cs:34-43` | Thấp. |
| 10 | Struct `VersionProbe` và hậu tố `.bad` / `.newer` | `Core/GardenSettings.cs:38-41` và `Core/GardenStateMigrator.cs:20`, `Core/GardenStateStore.cs:24-25` | Thấp. |
| 11 | Chuỗi `"UnityWndClass"`: đã có hằng số nhưng vẫn viết lại chuỗi | Hằng số ở `Assets/script/PipWindow.cs:75`, chuỗi viết tay ở `PipWindow.cs:266` | Thấp. |

---

## B. Cấu trúc module — **Cần cải thiện** (nhẹ)

### B1. Module quá lớn (> 300 dòng) hoặc làm quá nhiều việc

| File | Số dòng | Nhận xét | Điểm |
|---|---|---|---|
| `Assets/Editor/GardenSceneBuilder.cs` | 521 | Một lớp làm 9 việc: vật liệu, mặt đất, đá, nấm/cỏ, mưa, lá, ánh sáng, camera, hậu kỳ, render pipeline. Chỉ chạy trong Editor nên rủi ro thấp, nhưng khó đọc và khó sửa. | Cần cải thiện |
| `Assets/script/PipWindow.cs` | 428 | Gộp: khai báo Win32 (~65 dòng), kéo/đổi cỡ, đặt vị trí ban đầu, ẩn/hiện, chặn tắt app, mở Control Panel, kiểm tra con trỏ. Nên tách phần Win32 và phần "mở app khác" ra. | Cần cải thiện |
| `Art/blender_scripts/rock_uv.py` (354), `rock_edgeflow.py` (349), `rock_paint.py` (340), `rock_paint2.py` (301) | | Công cụ làm art chạy một lần, không chạy trong game. Chấp nhận được. | Tốt |
| `Assets/Tests/EditMode/*Tests.cs` (300-432) | | File test dài là bình thường. | Tốt |

Mọi file code chạy trong game khác đều dưới 200 dòng và mỗi file một nhiệm vụ rõ ràng. **Tốt**.

### B2. Phụ thuộc vòng tròn — **Tốt**
- Không có. Quy tắc được **Unity ép khi biên dịch** qua các file `.asmdef`: `Garden.Moss`, `Garden.Rain`, `Garden.Layout` chỉ tham chiếu `Garden.Core`; `Garden.Core` và `Garden.Shell` không tham chiếu ai (xem sơ đồ trong `ARCHITECTURE.md`).
- Mô-đun nói chuyện qua `GardenEventBus`, không gọi trực tiếp nhau.
- **Một ngoại lệ nhỏ:** `Garden.Core` biết tên mô-đun rêu (`"moss"`) và cấu trúc dữ liệu cũ của nó để nâng cấp file lưu bản 1: `Assets/script/Garden/Core/GardenStateMigrator.cs:33-36`. Đã có test kiểm tra hai bên khớp nhau, nên chấp nhận được.

### B3. Dữ liệu viết cứng (hard-code) trong code — **Cần cải thiện** (nhẹ)

| Dữ liệu | Vị trí |
|---|---|
| Thông số rêu mặc định (14 ngày, 0.1 → 0.5, bán kính 0.45, lành 2 ngày) | `Assets/script/Garden/Modules/Moss/MossSimulation.cs:14-21` |
| 3 mức tốc độ rêu (28/14/7 ngày) | `Assets/script/Garden/Modules/Moss/MossDesignerPanel.cs:10-11` |
| Xác suất mưa 12/25/45%, ô 6 giờ, trận 20-90 phút | `Assets/script/Garden/Modules/Rain/RainSchedule.cs:44-49` |
| Thời lượng "Mưa ngay" 30 phút | `Assets/script/Garden/Modules/Rain/RainDesignerPanel.cs:11` |
| Vị trí/kích thước/seed của 5 tảng đá, 3 cây nấm, 5 bụi cỏ, 4 chiếc lá | `Assets/Editor/GardenSceneBuilder.cs:185-192, 232, 244, 331-337` |
| Màu vật liệu, ánh sáng, camera, hậu kỳ | `Assets/Editor/GardenSceneBuilder.cs:84-91, 354-366, 379-390, 402-422` |
| Kích thước và độ lún tảng đá vẽ tay | `Assets/Editor/PaintedBoulderSetup.cs:24-25` |
| Cổng UDP 47321 | `Assets/script/PipProtocol.cs:4` |

Với dự án nhỏ một người làm, mức này **chấp nhận được**: nhiều giá trị được Unity chép vào scene thành field chỉnh được trong Inspector (ví dụ `MossModule.settings`, `RainModule.growthBoostFactor`). Nhưng khi muốn cho người làm game design chỉnh mà không cần lập trình viên (cân bằng tốc độ rêu, tần suất mưa), nên đưa ra `ScriptableObject`.

---

## C. Hiệu năng — **Tốt**

Điểm mạnh: app tự giới hạn **30 FPS** (`AppSetup.cs:9`) và **5 FPS khi ẩn** (`PipWindow.cs:25, 343`); việc nặng (cho rêu già đi, lưu file) chạy theo chu kỳ 30 giây / 5 phút chứ không phải mỗi khung hình (`GardenHost.cs:13-15, 47-75`).

### C1. Code chạy mỗi khung hình không cần thiết — chỉ có lãng phí nhỏ

| Vị trí | Việc | Mức |
|---|---|---|
| `Assets/script/Garden/Modules/Rain/RainView.cs:37-41, 51-66` | Mỗi khung hình đặt lại tốc độ phát hạt mưa và độ sáng/màu đèn, kể cả khi không mưa và giá trị không đổi. | Rất nhẹ. Có thể bỏ qua khi `Intensity` không đổi. |
| `Assets/script/PipWindow.cs:181` và `Assets/script/PipTitleBar.cs:34` | Cả hai cùng gọi `IsCursorOver` mỗi khung hình. Mỗi lần gọi là 2 lời gọi Win32 (`PipWindow.cs:359-360`), tức 4 lời gọi Win32 mỗi khung hình. | Rất nhẹ. |
| `Assets/script/DesignerBoard.cs:51` + `Modules/Layout/LayoutDesignerPanel.cs:39, 49`, `Modules/Rain/RainDesignerPanel.cs:45`, `Modules/Moss/MossDesignerPanel.cs:28` | Mỗi sự kiện IMGUI (nhiều lần mỗi khung hình) đều tạo mảng mới và chuyển sang JSON (`GardenSections.cs:36-37`) dù người chơi không chỉnh gì. Chỉ ở Control Panel. | Nhẹ: sinh rác bộ nhớ (GC) liên tục khi mở tab "Thiết kế". |
| `Assets/script/Garden/Core/GardenHost.cs:96-101` | Kiểm tra thời điểm ghi của file cài đặt mỗi giây. | Chấp nhận được, rất rẻ. |

### C2. Tạo/huỷ object liên tục, object pooling — **Tốt**
- Không có chỗ nào tạo/huỷ object trong lúc chơi. Mưa dùng **một** `ParticleSystem` (đã tự tái sử dụng hạt), chỉ đổi tốc độ phát (`RainView.cs:55-58`).
- Ẩn/hiện bố cục dùng `SetActive` (`GardenLayout.cs:21`); mesh chỉ dựng lại khi đổi seed (`GardenLayout.cs:23-28`).
- Sỏi/hoa/cỏ được gộp vào một mesh để chỉ tốn một draw call (`BlobMeshBuilder.cs:7`).

### C3. Tìm kiếm tốn kém (Find, GetComponent) trong vòng lặp — **Tốt**
- `GardenHost` tìm mô-đun **một lần** lúc `Start` (`GardenHost.cs:30-32`); `GardenClickRouter` tìm `PipWindow` một lần (`GardenClickRouter.cs:17`).
- `Camera.main` chỉ gọi khi có click (`GardenClickRouter.cs:25`).
- Một chỗ lãng phí nhỏ: `MossGrowth.Apply` gọi `GetComponentsInChildren<Renderer>()` (cấp phát mảng mới, `MossGrowth.cs:71`), và mỗi lần `MossModule.Apply` chạy thì hàm này bị gọi **2 lần** (setter `Seed` và setter `Coverage`, `MossGrowth.cs:31-33, 41-44` qua `MossModule.cs:125-126`). Chỉ xảy ra mỗi 30 giây và khi click, nên chưa đáng kể.

---

## D. Lỗi tiềm ẩn — **Cần cải thiện**, có **1 điểm Nghiêm trọng**

### D1. Lỗi logic về lưu/tải và cài đặt (quan trọng nhất trong báo cáo)

**D1.1 — Nghiêm trọng (ảnh hưởng lớn, xác suất thấp): lỗi đọc file tạm thời có thể xoá vườn thật**
- `Assets/script/Garden/Core/GardenStateStore.cs:37-41`: khi **đọc** file lưu bị lỗi `IOException`/`UnauthorizedAccessException` (file đang bị phần mềm diệt virus, OneDrive/Dropbox hay chương trình backup giữ tạm), hàm trả về **vườn mới** với kết quả `Corrupt`, **không sao lưu** file gốc (khác nhánh file hỏng ở dòng 58-61, có sao lưu `.bad`).
- Ngay sau đó `Assets/script/Garden/Core/GardenSession.cs:55` gọi `Save()` → **ghi đè** `garden_state.json` bằng vườn mới. Nếu lần ghi này lỗi thì lần lưu định kỳ sau 5 phút (`GardenHost.cs:70-74`) vẫn ghi đè.
- **Hậu quả:** người chơi mất toàn bộ tiến trình (rêu đã mọc nhiều ngày) mà không có bản sao lưu.
- Thêm vào đó, nếu lần `Save()` ở dòng 55 ném lỗi IO, lỗi sẽ thoát ra khỏi `GardenHost.Start` vì `GardenHost.cs:37` không có `try/catch`. Khi đó `ReloadSettings()` ở `GardenHost.cs:40` không chạy, nên vườn chạy với cài đặt mặc định.

**D1.2 — Cần cải thiện: mưa lúc tắt app được tính theo cài đặt mặc định, không theo lựa chọn của người chơi**
- `GardenHost.cs:36-40`: `session.Start(...)` (dòng 37) cho các mô-đun `Tick` phần thời gian lúc tắt app, **rồi mới** `ReloadSettings()` (dòng 40).
- Trong lúc `Tick` đó, `RainModule` vẫn đang dùng `RainSettings.Default` (`Assets/script/Garden/Modules/Rain/RainModule.cs:19`, dùng ở dòng 44-47).
- **Hậu quả:** người chơi **tắt** mưa tự nhiên, hoặc chọn tần suất "Nhiều", nhưng phần thời gian app đóng vẫn tính theo "mưa bật, tần suất Vừa". Rêu mọc nhanh/chậm sai so với lựa chọn. "Mưa ngay" mà người chơi gọi trước khi tắt app cũng không được tính cho phần lúc app đóng.
- Test hiện có (`Assets/Tests/EditMode/RainTests.cs:317-338`) gọi `ApplySettings` **trước** `Tick`, nên không bắt được lỗi thứ tự này ở `GardenHost`.

**D1.3 — Cần cải thiện: lỗi đọc file cài đặt tạm thời làm vườn quay về mặc định**
- `Assets/script/Garden/Core/GardenSettings.cs:64-71`: lỗi `IOException` khi đọc (ví dụ đúng lúc Control Panel đang thay file) được xử lý **giống file hỏng**. Hàm sao lưu `.bad` (ghi đè bản `.bad` cũ) rồi trả về **cài đặt mặc định**.
- `GardenHost.cs:105` đã ghi nhận thời điểm ghi file **trước khi** đọc, nên lần kiểm tra sau coi như file chưa đổi và **không đọc lại**. Bố cục, mưa, tốc độ rêu bị trả về mặc định cho tới khi người chơi chỉnh lại.
- `Assets/script/Garden/Core/SafeFile.cs:30` chỉ bắt `IOException`. Nếu sao lưu bị `UnauthorizedAccessException` (thư mục chỉ đọc) thì lỗi thoát ra:
  - Ở cửa sổ vườn: `GardenHost.Update` → `PollSettings` ném lỗi mỗi giây.
  - Ở Control Panel: lỗi thoát ra khỏi constructor `DesignerBoard` (`DesignerBoard.cs:31`) trong `ControlPanel.Awake` (`ControlPanel.cs:45`). `board` bị null, nên `ControlPanel.cs:62` (`board.Update()`, không kiểm tra null, trong khi dòng 59 thì có `board?.`) ném `NullReferenceException` **mỗi khung hình**.

### D2. Chỗ có thể null / object không tồn tại

| Vị trí | Tình huống | Mức |
|---|---|---|
| `Assets/script/ControlPanel.cs:62` | `board` null nếu `Awake` lỗi (xem D1.3) → NRE mỗi khung hình. | Cần cải thiện |
| `Assets/Editor/PaintedBoulderSetup.cs:82-83` | `GetComponentInParent<MossGrowth>()` có thể null → NRE (chỉ ở chế độ batch có chụp ảnh). | Thấp (Editor) |
| `Assets/Editor/PaintedBoulderSetup.cs:102` → `:66` | Vị trí "rock-main" không có `ProceduralBlob` → `new SerializedObject(null)` ném lỗi khó hiểu. | Thấp (Editor) |
| `Assets/Editor/RockPreview.cs:125` | `renderers[0]` khi model không có Renderer → `IndexOutOfRangeException`. | Thấp (Editor) |
| `Assets/Editor/GardenSceneBuilder.cs:79-80, 103, 129` | `Shader.Find` trả null (shader lỗi biên dịch) → `new Material(null)` ném lỗi giữa chừng, scene bị dựng dở. | Thấp (Editor) |
| `Assets/Editor/GardenSceneBuilder.cs:320, 351` | `FindAnyObjectByType<Light>()` lấy **bất kỳ** đèn nào trong mọi scene đang mở, không giới hạn trong scene vườn. | Thấp (Editor) |
| `Assets/script/Garden/Modules/Moss/MossModule.cs:94` | `OnDisable` huỷ đăng ký sự kiện mưa nhưng không có `OnEnable` đăng ký lại. Nếu object `Rocks` bị tắt rồi bật, mưa không còn làm rêu mọc nhanh cho tới lần mở app sau. Hiện chưa có code nào tắt object này. | Thấp (lỗi ngầm) |
| `Assets/script/ControlPanel.cs:49-60` | Tắt rồi bật lại component sẽ tạo luồng hỏi trạng thái mới trong khi luồng cũ có thể chưa thoát (đang `Sleep`), nên có 2 luồng cùng ghi `status` trong chốc lát. | Thấp |

Phần lớn code game đã kiểm tra null cẩn thận (ví dụ `MossModule.cs:87`, `PipCommandServer.cs:23, 87, 93`, `GardenClickRouter.cs:22-26`). **Tốt**.

### D3. Code bỏ dở, TODO, hàm không dùng, file thừa

- **TODO/FIXME/HACK:** không có (đã tìm trong `.cs`, `.shader`, `.hlsl`, `.py`). **Tốt**.
- **File thừa:**
  - `Assets/script/GrowTest.cs` (14 dòng): script thử nghiệm, **không gắn vào scene nào** (đã kiểm tra GUID trong cả 2 scene).
  - `Assets/script/Garden/BlobScatter.cs` (125 dòng, cả `ScatterSettings`, `ScatterArea`): **không được dùng ở đâu** (không có trong scene, không có code nào gọi).
- **Object thừa trong scene:** một GameObject rỗng tên `GameObject` ở gốc scene, `Assets/cozy garden.unity:2566-2596` (vị trí `-0.59, 0, 2.13`, chỉ có Transform; được liệt kê trong danh sách object gốc ở dòng 5968).
- **Dữ liệu chưa ai dùng:**
  - `GardenClick.normal` (`Core/GardenModule.cs:22`) được gán nhưng chưa có handler nào đọc.
  - Sự kiện `GardenDisturbance` (`MossModule.cs:91`) chưa có ai nghe ngoài test. Theo tài liệu thì để dành cho tính năng "con vật" sau này, nên chấp nhận được.
- **Gói thử nghiệm/chưa phát hành chính thức trong dự án:** `com.unity.ai.assistant` `2.20.0-pre.1` (`Packages/manifest.json:3`) và `com.unity.pipeline` `0.8.0-exp.1` (`Packages/manifest.json:6`). Gói `-exp` có thể đổi API hoặc bị bỏ bất kỳ lúc nào. Nếu chỉ dùng để hỗ trợ lúc phát triển, nên cân nhắc gỡ khỏi bản build.
- **Cài đặt chưa đổi tên:** `ProjectSettings/ProjectSettings.asset:15-16` vẫn là `companyName: DefaultCompany`, `productName: My1`. `PipBuild` đổi `productName` lúc build, nhưng `companyName` thì không, nên `PlayerPrefs` nằm ở `HKCU\Software\DefaultCompany\Aquarium`.

### D4. Cảnh báo/lỗi khi build
- **Không chạy được** trong lần audit này: không có Unity Editor 6000.6.4f1 và không có .NET SDK trong môi trường kiểm tra.
- Theo commit `f6fef63`: "EditMode tests: 184 passed" (chưa kiểm chứng lại).
- Script Python: 7/7 qua kiểm tra cú pháp. Không chạy thật được vì cần Blender (`bpy`), numpy, và GPU (Stable Diffusion).
- **Đề xuất:** chạy `Unity -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod PipBuild.BuildBoth -logFile build.log` và `-runTests -testPlatform EditMode` trên máy dev hoặc CI, rồi lưu log.

---

## E. Bảo mật & gian lận — **Tốt** (phần lớn không áp dụng)

### E1. Điểm, tiền, vật phẩm do client tự quyết?
**Không áp dụng:** game không có online, bảng xếp hạng, tiền tệ hay nạp tiền. Toàn bộ dữ liệu là JSON trên máy người chơi. Người chơi có thể tự sửa file để rêu mọc ngay, nhưng việc này không ảnh hưởng ai khác. Code vẫn kiểm tra dữ liệu đọc vào cẩn thận (`GardenState.IsValid`, `MossDamage.Load` ở `MossDamage.cs:82-96`, `MossSimulation.SetAge`).

Một điểm nhỏ để biết: kênh lệnh UDP **không xác thực** (`Assets/script/PipCommandServer.cs:30, 64-73`). Server chỉ nghe trên `127.0.0.1` và chỉ nhận 3 lệnh `show/hide/quit`, nên chỉ chương trình khác **trên cùng máy** mới gửi được, và tối đa chỉ ẩn/tắt được cửa sổ vườn. Rủi ro **thấp**.

### E2. API key, mật khẩu, token trong code hoặc lịch sử Git
- Đã tìm trong code hiện tại và **toàn bộ lịch sử Git** (`git log --all -p`) các mẫu `api key`, `secret`, `password`, `token`, `sk-…`, `AKIA…`, `ghp_…`, `xoxb-…`, `private key`: **không có**. Hai kết quả duy nhất là field trống mặc định của Unity (`ps4NPTitleSecret`, `metroCertificatePassword`) trong `ProjectSettings.asset`.
- Unity Cloud tắt (`ProjectSettings/UnityConnectSettings.asset:7`), `cloudProjectId`/`organizationId` trống.
- Để biết: email tác giả commit (`licute1489@gmail.com`) công khai trong lịch sử Git. Đây là chuyện bình thường của Git, chỉ lưu ý nếu repo được để public.

---

## F. Kiểm thử — **Cần cải thiện**

### Đã có (điểm mạnh lớn của dự án)
**184 test EditMode** trong `Assets/Tests/EditMode/` (assembly `Garden.Tests`):

| File | Số test | Kiểm tra gì |
|---|---|---|
| `GardenSessionTests.cs` | 22 | Lưu/tải nhiều mô-đun không ghi đè nhau, giữ phần mô-đun lạ, tính thời gian lúc tắt app, đồng hồ chỉnh lùi, chế độ xem thử không ghi file, mô-đun lỗi không kéo sập cả vườn, nâng cấp file bản 1; EventBus; ClickDispatcher. |
| `GardenSettingsTests.cs` | 33 | TimeWindow, file cài đặt (hỏng / bản mới hơn / thiếu version), ghi trễ và thử lại khi lỗi, GardenArgs. |
| `GardenStateTests.cs` | 29 | Dữ liệu file lưu, nâng cấp phiên bản, đọc/ghi, sao lưu file hỏng. |
| `MossDamageTests.cs` | 25 | Bóc rêu, gộp vết, giới hạn 16 vết, lành dần, dữ liệu xấu. |
| `MossAndShellTests.cs` | 24 | Đường cong mọc rêu, MossModule, chia vùng chuột `PipHitTest`. |
| `RainTests.cs` | 28 | Lịch mưa xác định, tần suất, mưa gọi tay, mưa làm rêu mọc nhanh, thứ tự cập nhật không quan trọng. |
| `LayoutTests.cs` | 22 | Danh mục vị trí, cài đặt bố cục, ẩn/hiện/đổi hình. |
| `ProjectRulesTests.cs` | 1 | Mỗi component nằm trong file trùng tên (tránh lỗi "missing script"). |

Phần **save/load** (quan trọng nhất của game này) được test kỹ.

### Phần quan trọng còn thiếu

| Thiếu | Vì sao quan trọng |
|---|---|
| **Không có CI** (không có thư mục `.github/`) | Test chỉ chạy khi có người mở Test Runner bằng tay. Không có gì chặn một commit làm hỏng test. |
| Test cho **lỗi đọc file tạm thời** (file bị khoá) cho cả file lưu và file cài đặt | Đúng các lỗi D1.1 và D1.3. Hiện chỉ có test lỗi **ghi** (`GardenSettingsTests.cs:344`). |
| Test tích hợp `GardenHost` (PlayMode): thứ tự `Start` → nạp cài đặt → `Tick` | Đúng lỗi D1.2. |
| Kênh lệnh UDP (`PipCommandServer` ↔ `ControlPanel`) | Chưa có test nào cho giao thức ping/pong và lệnh. |
| `PipBounds.TryLoad` (đọc chuỗi `"x,y,w,h"`, kiểm tra cửa sổ còn trong màn hình) | Logic thuần, dễ test, đang chưa có. |
| `DesignerBoard`: chỉ ghi khi `GUI.changed` | Logic quan trọng để không ghi file liên tục, chưa có test. |
| Khớp `MossDamage.Capacity = 16` với `#define MOSS_MAX_HITS 16` trong shader | Chỉ có lời nhắc trong comment (`MossDamage.cs:36`, `GhibliRockMoss.shader:113`). Lệch nhau thì vết bóc thứ 17+ không hiện, hoặc shader đọc ngoài mảng. Có thể viết test đọc file shader và so sánh. |
| Sát thương, tiền tệ | Không áp dụng (game không có). |

---

## G. Git & tài nguyên — **Cần cải thiện** (nhẹ)

### G1. `.gitignore`, file rác, file build, file lớn
- `.gitignore` **đúng chuẩn Unity** cho phần chính: `Library/`, `Temp/`, `Obj/`, `Build(s)/`, `Logs/`, `UserSettings/`, `MemoryCaptures/`, file IDE (`*.csproj`, `*.sln`, `.vs/`, `.idea/`), file hệ điều hành. **Tốt**.
- **Git LFS** được cấu hình đúng (`.gitattributes:21-46`) và đang dùng thật: 6 file nhị phân (`Art/boulder.blend`, `Boulder.fbx`, 4 PNG) đều là con trỏ LFS (`git lfs ls-files`). Lịch sử Git **không** có blob nào trên 500 KB. **Tốt**.
- Mọi file trong `Assets/` đều có `.meta` đi kèm, không có `.meta` mồ côi. **Tốt**.
- Không có file build hay file rác nào bị commit.
- **Thiếu:**
  - `*.blend1` (file sao lưu Blender tự tạo cạnh `Art/boulder.blend`) **chưa bị ignore** và không đi qua LFS (`.gitattributes` chỉ có `*.blend`). Lỡ `git add .` sẽ commit file vài MB vào Git thường. Đã kiểm tra bằng `git check-ignore`.
  - `Assets/Garden/Materials/BoulderPreview.mat` do menu "Preview painted boulder" tạo ra (`Assets/Editor/RockPreview.cs:14, 116`) nằm trong `Assets/` và **không bị ignore**. Chạy menu xong sẽ thấy file lạ trong `git status`.
  - Nên thêm các mục chuẩn của template Unity mà file đang thiếu: `[Rr]ecordings/`, `*.pidb.meta`, `*.pdb.meta`, `*.mdb.meta`, `/[Aa]ssets/[Ss]treaming[Aa]ssets/aa*`.

### G2. Hình ảnh, âm thanh, font và nguồn gốc

| Tài nguyên | Nguồn gốc (xác định từ code/lịch sử) |
|---|---|
| `Assets/Garden/Textures/rock_albedo.png`, `moss_albedo.png`, `moss_mask.png` | **Tự tạo bằng code**: sinh bằng noise trong `Assets/Editor/GardenTextureGenerator.cs:73-104`. |
| `Assets/Garden/Textures/boulder_albedo.png` | **Tự tạo bằng script** `Art/blender_scripts/rock_paint.py` / `rock_paint2.py` (tô màu theo hình khối 3D). Script **có tuỳ chọn** trộn thêm "tile nét cọ" do **AI tạo** (`rock_paint.py:125-150`). Các tile đó do `Art/blender_scripts/gen_paint_tiles.py` sinh bằng **Stable Diffusion 1.5 + IP-Adapter**, lấy **phong cách từ một bức tranh tham chiếu** (ảnh tham chiếu nằm ở `Docs/reference/`, bị ignore, ghi là "của bên thứ ba" trong `.gitignore`). **Không xác định được** bản PNG đã commit có dùng tile AI hay không (nội dung LFS không được tải về trong môi trường audit). |
| `Assets/Garden/Models/Boulder.fbx`, `Art/boulder.blend` | **Làm trong Blender**: retopo/UV bằng script trong `Art/blender_scripts/` từ một mô hình điêu khắc `Boulder_HiPoly`. Không xác định được mô hình điêu khắc gốc do ai làm. |
| Các mesh đá, cỏ, nấm, lá trong scene | **Sinh bằng code** lúc chạy (`ProceduralBlob`, `GrassTuft`). |
| Shader (`GhibliToon`, `GhibliRockMoss`, `GardenRainStreak`) | **Tự viết** trong repo. |
| Con trỏ đổi kích thước, icon thanh tiêu đề | **Vẽ bằng code** (`PipResizeCursors.cs`, `PipTitleBar.cs:71-101`). |
| Âm thanh | **Không có**. |
| Font | **Không có** (dùng font mặc định của IMGUI). |
| Mã nguồn | 13/14 commit có dòng `Co-Authored-By: Claude` → phần lớn code được viết **với trợ giúp của AI**. |

**Lưu ý bản quyền:** trước khi phát hành thương mại, nên ghi lại (a) bản `boulder_albedo.png` có dùng tile AI không, và (b) bức tranh tham chiếu ở `Docs/reference/` là của ai, giấy phép gì. Dùng phong cách tác phẩm của người khác làm đầu vào cho AI là vùng pháp lý còn chưa rõ ràng.

---

## Kết luận

### 1. Tóm tắt

**Điểm tổng thể: 7.5 / 10 — Khá tốt.**

Với một dự án nhỏ, code sạch và có kỷ luật hơn mức thường gặp. Các vấn đề tìm thấy phần lớn là lỗi ở "đường biên" (lỗi đọc file, thứ tự khởi tạo) và dọn dẹp nhỏ, không phải lỗi thiết kế.

**3 điểm mạnh:**
1. **Kiến trúc mô-đun rõ ràng, được trình biên dịch ép buộc.** Các assembly `.asmdef` cấm mô-đun gọi chéo nhau, mô-đun nói chuyện qua EventBus, thêm mô-đun mới chỉ cần thêm một thư mục và một dòng (`DesignerBoard.DefaultPanels`).
2. **Lưu/tải được thiết kế cẩn thận:** ghi file nguyên tử (`SafeFile.WriteAtomically`), có phiên bản và nâng cấp file cũ, sao lưu file hỏng/mới hơn, giữ dữ liệu của mô-đun lạ, chống đồng hồ chỉnh lùi, giới hạn 365 ngày, mô-đun lỗi không kéo sập cả vườn.
3. **Có 184 test tự động** cho toàn bộ logic thuần (rêu, mưa, bố cục, lưu/tải, chia vùng chuột), cộng một test quy tắc dự án. Logic được tách khỏi Unity (`MossSimulation`, `RainSchedule`, `PipHitTest`, `GardenSession` dùng đồng hồ giả) nên test chạy nhanh, kết quả ổn định.

### 2. Top 5 vấn đề cần sửa trước (chỉ đề xuất, chưa sửa)

| # | Mức | Vấn đề | Đề xuất cách sửa |
|---|---|---|---|
| 1 | **Nghiêm trọng** | **Có thể mất vườn khi đọc file lưu lỗi tạm thời** (D1.1): `GardenStateStore.cs:37-41` trả vườn mới, không sao lưu; `GardenSession.cs:55` ghi đè ngay. | Thêm kết quả riêng `LoadOutcome.Unreadable` (khác `Corrupt`). Khi gặp: **không ghi** file (cho session vào chế độ chỉ đọc như preview), thử đọc lại sau vài giây, và chỉ cho phép lưu khi đã đọc được. Ở nhánh nào cũng nên sao lưu file gốc trước khi ghi đè. Bọc `session.Start` trong `GardenHost.cs:37` bằng `try/catch`. Thêm test giả lập file bị khoá (mở bằng `FileShare.None`). |
| 2 | Cần cải thiện | **Mưa lúc tắt app tính theo cài đặt mặc định** (D1.2): `GardenHost.cs:36-40` nạp cài đặt **sau** khi đã `Tick` phần thời gian lúc app đóng. | Đọc `GardenSettingsStore.Load` và `ConfigurableApplier.Apply` **trước** `session.Start` (mô-đun cần được `ApplySettings` trước `Load`/`Tick`), hoặc tách `Start` thành `Load` → áp cài đặt → `CatchUpOfflineTime`. Thêm test ở mức `GardenSession`/`GardenHost` với mưa tắt. |
| 3 | Cần cải thiện | **Lỗi đọc file cài đặt làm vườn về mặc định, và có thể gây lỗi lặp mỗi khung hình** (D1.3): `GardenSettings.cs:64-71`, `GardenHost.cs:105`, `SafeFile.cs:30`, `ControlPanel.cs:62`. | Khi lỗi IO: **giữ cài đặt đang dùng**, không sao lưu `.bad`, không cập nhật `lastSettingsWriteUtc` để lần sau đọc lại. `SafeFile.Backup` bắt thêm `UnauthorizedAccessException`. `ControlPanel.Update` dùng `board?.Update()` giống `OnDisable`. |
| 4 | Cần cải thiện | **Không có CI; thiếu test cho các lỗi trên và cho phần "vỏ" app** (mục F). | Thêm GitHub Actions dùng `game-ci/unity-test-runner` (cần Unity license dạng secret) chạy 184 test EditMode mỗi lần push/PR. Viết test cho 3 lỗi D1, `PipBounds.TryLoad`, và test đối chiếu `MossDamage.Capacity` với `MOSS_MAX_HITS` trong shader. |
| 5 | Cần cải thiện (nhẹ) | **Code thừa, code chép lại dễ lệch, `.gitignore` thiếu** (A2, D3, G1): `GrowTest.cs`, `BlobScatter.cs`, GameObject rỗng trong scene; công thức mô đất chép ở `PaintedBoulderSetup.cs:28-33`; 2 hàm bật `.exe`; thiếu `*.blend1`, `BoulderPreview.mat`. | Xoá 2 file thừa và object rỗng. Đưa `MoundHeight` + `MoundCenter/MoundSize` thành `public static` dùng chung (hoặc một class `GardenSceneLayout`). Gộp hàm bật `.exe` vào một helper (ví dụ `AppLauncher.TryStart(path, out error)`). Thêm `*.blend1` và `Assets/Garden/Materials/BoulderPreview.mat*` vào `.gitignore` (hoặc chuyển vật liệu xem thử ra ngoài `Assets/`). Tách `PipWindow.cs` (Win32 interop riêng) và `GardenSceneBuilder.cs` (theo từng phần: vật liệu / cảnh / ánh sáng). |

### 3. Ước lượng

**Mức: "sửa nhỏ là ổn".**

Kiến trúc nền đúng hướng và có test bảo vệ; không có phụ thuộc vòng tròn hay module "ôm đồm" ở phần chạy trong game. Cả 5 vấn đề trên đều sửa được cục bộ, mỗi cái từ vài chục tới khoảng một trăm dòng, không cần tái cấu trúc. Nên sửa vấn đề #1 trước khi đưa app cho người chơi thật dùng lâu dài, vì đó là chỗ duy nhất có thể làm người chơi mất dữ liệu.
