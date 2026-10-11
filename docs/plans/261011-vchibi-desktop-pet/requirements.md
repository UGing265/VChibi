# VChibi — yêu cầu sản phẩm và tiêu chí duyệt

## Nguồn quyết định

Tài liệu tổng hợp trao đổi với người dùng ngày 2026-10-11. Là đầu vào bắt buộc cho mọi phase; không thay thế yêu cầu bằng bản thu hẹp. Chỉ lập kế hoạch trong phiên này, chưa triển khai app.

## 1. Mục đích đã thống nhất

- App quản lý desktop pet dạng idol, lấy cảm hứng Desktop Goose và VPET Simulator.
- Người dùng ngắm idol trong lúc làm việc trên máy; idol xuất hiện trực tiếp trên desktop, không bị giới hạn trong cửa sổ quản lý.
- Thư viện có nhiều pet để chọn; chỉ một pet được hiện trên desktop cùng lúc.
- Ưu tiên app đơn giản, mở lên dùng được; không nền tảng online, backend hoặc hệ thống plugin.
- Tạo/sửa pet và chỉnh cài đặt bằng UI. Người dùng không viết JSON, không phải lập trình.

## 2. Quyết định đã chốt

| Mục | Quyết định |
|---|---|
| Hệ điều hành | Windows trước |
| Công nghệ | C# / .NET / WPF; dùng WPF UI của lepoco |
| Kiến trúc | Một project app, một process, MVVM, folder theo loại |
| Nhân vật | Sprite 2D, chuỗi ảnh PNG nền trong suốt |
| Số pet hiện | Một |
| Màn hình | Một màn hình; lấy màn hình chính làm phạm vi |
| Di chuyển | Đi lung tung/ngẫu nhiên khắp vùng làm việc màn hình |
| Tỷ lệ vùng | Không dùng quy tắc 70/30; không nhận diện vùng ứng dụng đang làm việc |
| Kéo pet | Không làm |
| Múa/nói/hát | Không làm trong kế hoạch này; để sau |
| Theme UI | Sáng |
| Màu nhấn | Lavender |
| Font | Theo diện mạo mặc định của WPF UI/Windows, không thêm font thương hiệu |
| Tổ chức tài liệu | Mọi tài liệu agent dưới docs/ |

Không chuyển sprite sang Live2D/3D. Không nhận diện IDE, trình duyệt, Word hoặc đọc nội dung cửa sổ.

## 3. Các lựa chọn kỹ thuật bảo thủ để thực thi không phải hỏi lại

Đây là lựa chọn triển khai của kế hoạch, không giả mạo lời xác nhận của người dùng:

- .NET 10, target net10.0-windows; SDK 10.0.300 và WindowsDesktop runtime 10.0.8 đã quan sát trên máy.
- Import nhận thư mục gói hoàn chỉnh chứa pet.json và PNG; chưa làm ZIP/export. Tạo pet qua UI vẫn là luồng đầy đủ độc lập.
- Gói chỉ chứa dữ liệu/PNG, không chạy script/code/plugin: chính sách triển khai bảo thủ, không coi là yêu cầu được người dùng phát biểu riêng.
- Cửa sổ pet không tương tác: click xuyên toàn cửa sổ, không nhận focus. Mọi điều khiển qua UI/system tray. Không thêm click reaction.
- Vùng di chuyển là work area màn hình chính, loại taskbar; pet có thể đi đè lên cửa sổ ứng dụng thông thường.
- Đứng và đi là hai animation bắt buộc; mỗi animation có ít nhất một PNG. Một frame là hợp lệ nhưng chỉ là hình tĩnh khi phát.
- Lật ngang khi đổi hướng; UI preview thể hiện hướng để người tạo biết chi tiết bất đối xứng/chữ sẽ bị lật. Chưa làm bộ frame trái/phải riêng.
- Tốc độ animation mặc định 8 FPS; tốc độ đi mặc định 40 DIP/giây; đây là giá trị ban đầu có thể chỉnh, không phải tiêu chí cứng về thẩm mỹ.
- Màu lavender ứng viên #7862B8; kiểm tra tương phản chữ trắng/đen và các trạng thái trước khi dùng. Không dùng lavender nhạt cho chữ trên nền trắng.
- Không dựng lớp/interface rỗng theo cây folder. Tách lớp khi trách nhiệm thật cần thiết.
- Một instance sở hữu data root để mở exe lần hai không thêm pet hoặc tranh commit; lựa chọn bảo đảm tính nhất quán, không thêm IPC.

## 4. Luồng mở app và sử dụng thường ngày

1. Lần đầu mở: thấy cửa sổ quản lý và pet mẫu thật có animation đứng/đi trên desktop.
2. Người dùng chọn pet trong thư viện để xem trước; chưa đổi pet đang chạy.
3. Bấm Thả ra desktop mới kích hoạt/thay pet; luôn chỉ một pet hiện.
4. Chỉnh kích thước, tốc độ đi; áp dụng ngay cho pet đang chạy.
5. Ẩn pet: pet biến mất, dừng scheduler của pet; app và tray còn hoạt động.
6. Hiện pet: dùng pet đang chọn; nếu khác active thì nạp/kích hoạt selected trước, không chỉ Show lại active cũ. Load lỗi giữ hidden và báo rõ. Vị trí phải hợp lệ.
7. Đóng cửa sổ quản lý: app vẫn ở tray, pet đang hiện tiếp tục hoạt động.
8. Mở lại bằng tray: cùng cửa sổ quản lý, không tạo cửa sổ/phiên pet trùng.
9. Thoát: đóng mọi cửa sổ, dừng animation/movement/preview, dispose tray, process kết thúc.
10. Mở lần sau: phục hồi pet đã chọn, cài đặt theo pet, trạng thái hiện/ẩn. Nếu không có lựa chọn trước, dùng pet mẫu.

Pet bị mất/hỏng không âm thầm thay bằng mẫu. Settings sai cú pháp hoặc giá trị không hợp lệ báo rõ: Reset được xác nhận mới ghi cài đặt mới; Cancel thoát không ghi, giữ nguyên file hỏng để phục hồi.

## 5. Luồng tạo/sửa pet bằng UI

1. Bấm Tạo pet hoặc Sửa pet từ màn hình quản lý.
2. Nhập tên hiển thị; tên không dùng làm tên thư mục lưu.
3. Chọn động tác Đứng hoặc Đi.
4. Chọn nhiều PNG qua hộp thoại Windows; app hiển thị thumbnail, tên file và thứ tự frame.
5. Có nút Trước/Sau/Bỏ ảnh cho frame đang chọn; không bắt kéo thả và không cần timeline editor.
6. Chỉnh FPS cho từng động tác bằng số/nút tăng giảm.
7. Preview phát animation đang chọn, có phát/tạm dừng và xem hướng trái/phải.
8. Chuyển tab động tác không mất bản chỉnh sửa.
9. Lưu: kiểm tra dữ liệu, chép asset, tự ghi cấu hình, cập nhật thư viện; không tự thả pet mới nếu chỉ bấm Lưu.
10. Lưu và thả ra desktop: lưu thành công trước, rồi kích hoạt pet.
11. Hủy: không thay bản đã lưu; nếu có thay đổi chưa lưu thì hỏi trước khi bỏ.
12. Khi sửa pet đang chạy, chỉ thay asset/runtime sau khi lưu và nạp bản mới thành công. Lỗi lưu giữ bản cũ tiếp tục chạy.

App không vẽ ảnh, không sinh animation, không có AI tạo asset. Xóa/di chuyển ảnh gốc sau khi lưu không làm pet đã lưu hỏng.

## 6. Luồng import

- Nút Import gói mở hộp thoại chọn thư mục chứa gói hoàn chỉnh.
- Người dùng không chọn JSON riêng, không gom lại từng ảnh.
- App kiểm tra schema, animation đứng/đi, PNG, thứ tự, FPS, giới hạn kích thước/bộ nhớ và đường dẫn.
- Import hợp lệ: tạo ID cục bộ mới, chép asset vào kho app, thêm vào thư viện; không tự thay pet đang chạy.
- Import cùng gói lần nữa tạo bản riêng, không ghi đè pet đang có; tên hiển thị trùng được phép vì ID khác nhau.
- Import lỗi: báo nguyên nhân và cách sửa, không tạo mục thư viện nửa vời, không làm mất pet cũ.
- Chỉ dữ liệu và PNG; không chạy script/code/plugin trong gói.

## 7. Di chuyển và animation

- Hai trạng thái đầu tiên: đứng và đi.
- Đứng nghỉ một khoảng, chọn đích ngẫu nhiên, đi tới rồi nghỉ; chọn cả tọa độ X và Y, không chỉ chạy dọc đáy màn hình.
- Không chia vùng 70/30; không theo chuột; không kéo pet; không tương tác ứng dụng đang mở.
- Giữ toàn bộ khung pet trong work area, không cắt đầu/chân ở biên và không đè taskbar.
- Khi đổi kích thước hoặc work area/DPI thay đổi, tính lại giới hạn và clamp tọa độ.
- Tính di chuyển theo elapsed time; frame rate thấp không làm đổi tốc độ dự định hoặc vượt biên sau khi resume.
- Ảnh nạp một lần khi kích hoạt/preview, không đọc đĩa mỗi frame; không tạo bitmap mới mỗi tick.
- Pet/preview có playback state riêng; dùng chung thuật toán phát frame, không giành cursor animation của nhau.
- Scheduler ngừng khi đối tượng ẩn/đóng; không để callback, timer hoặc event subscription chạy sót.

## 8. Giao diện đã bàn

### Pet của tôi

- Header: VChibi, Import gói, Tạo pet.
- Cột trái: thư viện pet, tên và trạng thái đang hiện/chưa hiện.
- Vùng giữa: preview và chọn Đứng/Đi.
- Cột phải: tên pet, trạng thái, kích thước, tốc độ di chuyển.
- Hành động: Thả ra desktop/Ẩn khỏi desktop, Sửa pet, Thoát.
- Chọn pet chỉ thay selection/preview, không thay active pet.

### Tạo/sửa pet

- Quay lại, tên pet, tab Đứng/Đi.
- Chọn PNG, danh sách thumbnail/frame, Trước/Sau/Bỏ.
- FPS từng động tác.
- Preview, phát/tạm dừng, hướng preview.
- Hủy, Lưu, Lưu và thả ra desktop.

### Quy tắc UI

- WPF UI cho control/theme/icon; không tự thêm navigation/sidebar nhiều tầng chỉ vì toolkit có.
- Light theme cố định trong phạm vi này; không thêm màn hình chọn theme/dark mode.
- Lavender cho nút chính, selection và slider; nền trắng/xám nhạt, chữ tối.
- Font chữ theo Windows/WPF UI; phân biệt font chữ với font icon, không đóng gói font Microsoft tùy tiện.
- Có label, keyboard Tab/focus, thông báo lỗi tại trường liên quan và trạng thái đang nạp/lưu.
- Không thể hiện trạng thái chỉ bằng màu; dùng chữ.
- UI không block lâu khi đọc/chép/giải mã asset.
- Không theme nền cửa sổ pet: pet vẫn alpha trong suốt, không Mica/Acrylic.

## 9. Lưu dữ liệu

- Kho người dùng: %LOCALAPPDATA%/VChibi/Pets/<pet-id>/.
- Cài đặt: %LOCALAPPDATA%/VChibi/settings.json.
- Gói mẫu đi kèm app: Assets/DefaultPet/; khởi tạo bản quản lý cục bộ khi chưa có.
- Không ghi dữ liệu người dùng vào Program Files, thư mục exe hoặc source.
- ID ổn định khi đổi tên/chỉnh nội dung pet.
- Tốc độ phát animation thuộc pet; kích thước/tốc độ di chuyển là cài đặt sử dụng theo ID pet.
- Lưu/import có staging và commit rõ; lỗi không công bố dữ liệu một phần.
- Path trong gói phải tương đối, nằm trong gói; từ chối đường dẫn tuyệt đối, traversal và symlink/reparse escape.

## 10. Ngoài phạm vi

- Live2D, 3D, GIF, video, sprite sheet.
- Múa, nói, hát, trạng thái cảm xúc phức tạp.
- AI/chat/voice synthesis, sinh ảnh/animation.
- Plugin, script, code tùy ý, marketplace.
- Backend, database, login, cloud sync, telemetry.
- Nhiều pet đồng thời, nhiều màn hình, pet kéo thả.
- Theo dõi/đọc cửa sổ làm việc, chia vùng 70/30.
- Vẽ animation, timeline editor đầy đủ.
- ZIP/export, tự cập nhật, tự chạy cùng Windows, installer/MSIX ở bước này.

## 11. Ma trận nghiệm thu toàn bộ

| ID | Tình huống | Kết quả bắt buộc | Phase |
|---|---|---|---|
| A01 | Mở lần đầu | UI light lavender, pet mẫu thật hiện và đi | 1–4, 6 |
| A02 | Quan sát di chuyển | Đi ngẫu nhiên X/Y, đứng/đi đúng animation, không ra ngoài work area | 3, 6 |
| A03 | Click/gõ vào app phía dưới pet | Click xuyên, không giành focus, không chặn làm việc | 3, 6 |
| A04 | Chọn pet khác | Preview đổi, active pet chưa đổi | 4, 6 |
| A05 | Thả pet khác | Đổi pet thành công, luôn một cửa sổ pet | 4, 6 |
| A06 | Chỉnh scale/tốc độ | Áp dụng ngay, clamp đúng, không thay FPS gói | 3–4, 6 |
| A07 | Tạo pet từ PNG | Đứng/đi, frame order, FPS và tên lưu đúng, không viết JSON | 5–6 |
| A08 | Xóa ảnh nguồn sau khi tạo | Pet đã lưu vẫn chạy | 2, 5–6 |
| A09 | Sửa rồi hủy | Bản lưu/active pet không đổi | 5–6 |
| A10 | Sửa rồi lưu pet đang chạy | Bản mới cập nhật, lỗi giữ bản cũ | 3, 5–6 |
| A11 | Import gói hợp lệ | Thêm thư viện, ID mới, chưa đổi active pet | 2, 5–6 |
| A12 | Import hỏng/độc hại | Báo rõ, không crash/traversal/mục nửa vời | 2, 5–6 |
| A13 | Ẩn/hiện và đóng/mở quản lý | Đúng trạng thái, không timer/window/tray trùng | 3–4, 6 |
| A14 | Thoát rồi mở lại | Process hết; phục hồi pet, cài đặt, trạng thái hiện/ẩn | 4, 6 |
| A15 | Scale/DPI/taskbar thay đổi | Khung pet trong work area, tốc độ DIP nhất quán | 3–4, 6 |
| A16 | Chạy bản publish ngoài repo | Asset mẫu đủ, lần đầu dùng được, không cần SDK | 6 |
| A17 | Gói/settings đã lưu bị hỏng | Báo rõ và có đường phục hồi, không âm thầm giả thành công | 2, 4–6 |

## 12. Handoff

Đọc tiếp [kiến trúc](./architecture.md), [quy tắc thực thi liên tục](./execution-contract.md) và [kế hoạch tổng](./plan.md). Các lựa chọn kỹ thuật có thể điều chỉnh để sửa lỗi thực tế, nhưng không tự bỏ yêu cầu hoặc thêm phạm vi ngoài bảng này.
