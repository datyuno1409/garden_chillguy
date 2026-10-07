# Quy trình art: đá, rêu, texture, Maya

Cảnh góc vườn (tảng đá phủ rêu) dựng bằng code và shader; phần mỹ thuật (texture, mô hình) là của bạn chỉnh.

## Dựng lại cảnh
Menu **Tools/Garden/3. Build garden scene**. Chạy lại bao nhiêu lần cũng được: nó xóa nhóm `Garden` cũ và dựng lại.
Nó **không ghi đè** texture trong `Assets/Garden/Textures` và không đổi vật liệu `RockMoss`, `MossGround` nếu đã tồn tại.

## Chỉnh texture
Ba file mẫu lặp liền mạch (512x512) nằm ở `Assets/Garden/Textures/`. Mở ra vẽ đè (giữ tên file, hoặc gán file khác vào vật liệu):

| File | Dùng cho | Ghi chú |
|---|---|---|
| `rock_albedo.png` | Màu đá | Phải lặp liền mạch (tileable) |
| `moss_albedo.png` | Màu rêu | Phải lặp liền mạch |
| `moss_mask.png` | Chỗ nào có rêu trước | Kênh **R**: mảng rêu lớn. Kênh **G**: hạt mịn tạo mép rêu xù. Không phải ảnh màu, đừng bật sRGB |

Vật liệu: `Assets/Garden/Materials/RockMoss.mat` (đá) và `MossGround.mat` (mô đất rêu luôn phủ kín).
Các thông số chính trong vật liệu:
- **Moss Coverage**: độ phủ rêu 0..1 (0 = đá trần, 1 = phủ kín). Sau này nối với số ngày.
- **Up Bias**: rêu thích bám mặt hướng lên bao nhiêu.
- **Moss Fuzzy Edge / Edge Softness**: độ xù và mềm của mép rêu.
- **Rock / Moss / Mask Tiling**: số lần lặp mỗi mét.
- **Use UV**: tắt = texture chiếu theo 3 mặt phẳng (mesh chưa cần UV). Bật khi mô hình từ Maya đã có UV tử tế.

Xem rêu mọc: chọn object `Garden/Rocks`, kéo thanh **Coverage** của component `Moss Growth`.

## Mô hình từ Maya
1. **Tools/Garden/Export rocks to OBJ (Maya)**: xuất từng tảng đá ra `Exports/Rocks/*.obj` (cạnh thư mục `Assets`, không vào git).
   OBJ ở kích thước thật (mét) và đã đổi hệ toạ độ trái sang phải. Maya mặc định là cm: chỉnh đơn vị khi nhập.
2. Sửa trong Maya, xuất **FBX** vào `Assets/Garden/Models/`.
3. Chọn tảng đá tương ứng (`Garden/Rocks/Rock_<seed>`), kéo FBX vào ô **Override Mesh** của component `Procedural Blob`. Muốn quay về mesh sinh bằng code thì xóa ô đó.
4. Nếu mesh có UV, bật **Use UV** trong vật liệu.

Lưu ý: menu số 3 xóa và dựng lại cả nhóm `Garden`. Nếu có tảng đá đang dùng mô hình bạn gán (Override Mesh), nó **từ chối chạy** và báo trong Console, để mô hình của bạn không bị mất. Muốn dựng lại thì gỡ mô hình đó ra khỏi nhóm `Garden` trước.

## Lá tạm
Nhóm `Garden/Placeholders` là mấy tấm lá tạm ở rìa khung chỉ để có chiều sâu. Xóa hoặc thay bằng lá của bạn.

## Camera
`Main Camera`: ống kính tele (FOV 30), đứng xa 11 m, nhìn chếch xuống 12 độ, kết hợp làm mờ phía gần và xa (Depth of Field trong `Assets/Garden/GardenVolume.asset`) để cảnh trông như mô hình thu nhỏ. Khung hình vuông, khớp cửa sổ PIP 400x400.
