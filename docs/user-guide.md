# Hướng dẫn sử dụng VChibi (User Guide)

VChibi là ứng dụng desktop idol pet dành cho Windows (10/11 x64), cho phép bạn hiển thị nhân vật 2D anime/chibi di chuyển và biểu cảm trực tiếp trên màn hình máy tính với độ mượt mà cao, không che khuất hay cản trở thao tác làm việc của bạn.

---

## 1. Yêu cầu hệ thống & Cài đặt

- **Hệ điều hành**: Windows 10 (bản 1809 trở lên) hoặc Windows 11, kiến trúc 64-bit (x64).
- **Runtime**: Bản phân phối `win-x64` là **Self-Contained**, đã tích hợp sẵn toàn bộ runtime cần thiết bên trong thư mục ứng dụng. Bạn không cần cài đặt thêm .NET SDK hay runtime bên ngoài.
- **Khởi chạy**: Nhấp đúp vào tệp tin `VChibi.exe` bên trong thư mục ứng dụng.

---

## 2. Giao diện chính & Quản lý thư viện Pet

Khi khởi chạy lần đầu, VChibi tự động nạp nhân vật mẫu **Chibi Idol** và hiển thị cửa sổ quản lý thư viện (`LibraryWindow`):

### 2.1. Danh sách nhân vật
- Cột bên trái hiển thị toàn bộ các idol pet đang có trong thư viện của bạn.
- Bạn có thể nhấp chọn bất kỳ pet nào để xem thông tin chi tiết và tùy chỉnh thông số riêng cho pet đó.
- **Lưu ý**: Việc nhấp chọn pet trong danh sách chỉ để xem và chỉnh sửa, **không** tự động thay đổi pet đang chạy trên màn hình desktop cho đến khi bạn bấm **"Thả ra desktop"**.

### 2.2. Điều chỉnh kích thước & Tốc độ
- **Tỉ lệ hiển thị (Scale)**: Điều chỉnh từ `25%` đến `400%` kích thước gốc của khung hình.
- **Tốc độ di chuyển (Speed)**: Điều chỉnh từ `5` đến `200` DIP/s (Device Independent Pixels / giây).
- Các thông số này được lưu trữ độc lập cho từng pet. Bạn có thể đặt Pet A to 200% nhưng Pet B chỉ 100%.

### 2.3. Thả ra desktop & Ẩn desktop pet
- **Thả ra desktop (`ReleaseToDesktopButton`)**: Kích hoạt pet đang chọn trở thành nhân vật đại diện hiển thị trên màn hình. Pet cũ sẽ được thay thế an toàn.
- **Ẩn khỏi desktop (`HideFromDesktopButton`)**: Tạm thời ẩn nhân vật trên desktop khi bạn cần tập trung làm việc hoặc quay màn hình. Bấm lại "Thả ra desktop" để nhân vật xuất hiện trở lại.

---

## 3. Tạo pet mới & Chỉnh sửa nhân vật (Pet Editor)

VChibi tích hợp sẵn bộ công cụ tạo và chỉnh sửa pet hoàn toàn trực quan, không yêu cầu bạn phải viết hay chỉnh sửa tệp JSON thủ công.

### 3.1. Tạo pet mới từ ảnh PNG
1. Bấm nút **"+ Tạo pet mới"** ở góc trên cửa sổ quản lý.
2. **Đặt tên nhân vật**: Nhập tên cho idol của bạn tại ô "Tên nhân vật".
3. **Thêm khung hình động tác Đứng (Idle)**:
   - Tại tab **"Động tác đứng (Idle)"**, bấm **"Chọn ảnh PNG..."**.
   - Hộp thoại tệp tin xuất hiện, bạn chọn một hoặc nhiều tệp ảnh PNG động tác đứng.
   - Các khung hình sẽ xuất hiện trong danh sách bên dưới kèm theo tên tệp và chỉ số thứ tự.
4. **Sắp xếp thứ tự khung hình**:
   - Chọn một khung hình trong danh sách.
   - Bấm **"Lên"** hoặc **"Xuống"** để đổi vị trí phát animation.
   - Bấm **"Xóa"** nếu muốn loại bỏ khung hình đã chọn.
5. **Cài đặt tốc độ khung hình (FPS)**:
   - Kéo thanh trượt FPS để chọn tốc độ phát (từ `1` đến `60` FPS).
   - Khung xem trước (Preview) ở bên phải sẽ phát thử chuyển động theo đúng tốc độ bạn vừa chọn.
6. **Thêm khung hình động tác Đi (Walk)**:
   - Chuyển sang tab **"Động tác đi (Walk)"**.
   - Thao tác tương tự để thêm ảnh PNG, sắp xếp thứ tự và đặt tốc độ FPS cho động tác đi lại.
7. **Lưu nhân vật**:
   - Bấm **"Lưu thay đổi"**: Lưu pet vào thư viện để sử dụng sau.
   - Hoặc bấm **"Lưu và thả ra desktop"**: Lưu pet vào thư viện và ngay lập tức kích hoạt nhân vật chạy trên màn hình desktop chỉ với một thao tác.

### 3.2. Chỉnh sửa pet hiện có
1. Chọn một pet trong danh sách thư viện.
2. Bấm nút **"Chỉnh sửa"**.
3. Bạn có thể sửa tên, thêm/bớt khung hình, thay đổi thứ tự và điều chỉnh lại FPS.
4. Nếu bạn đổi ý và bấm **"Hủy"** khi đã có chỉnh sửa, ứng dụng sẽ hiện hộp thoại xác nhận. Nếu bạn đồng ý hủy, mọi thay đổi chưa lưu sẽ được loại bỏ và dữ liệu gốc được giữ nguyên vẹn.
5. Nếu bạn chỉnh sửa chính nhân vật đang chạy trên desktop và bấm "Lưu thay đổi", ứng dụng sẽ tự động nạp lại (hot reload) chuyển động mới mà không làm gián đoạn phiên hoạt động.

### 3.3. Xóa pet
- Chọn pet trong danh sách (ngoại trừ pet mặc định của hệ thống) và bấm **"Xóa pet"**.
- Ứng dụng sẽ xóa thư mục dữ liệu của pet đó khỏi hệ thống an toàn.

---

## 4. Nhập gói Pet từ thư mục (Import Package)

Bạn có thể tải về các gói pet do cộng đồng đóng gói sẵn và nhập vào VChibi cực kỳ đơn giản:

1. Một gói pet hợp lệ là một thư mục chứa tệp cấu hình `pet.json` cùng các tệp ảnh PNG (thường nằm trong các thư mục con `idle/` và `walk/`).
2. Trên cửa sổ VChibi, bấm nút **"Nhập gói pet"**.
3. Chọn thư mục chứa gói pet cần nhập.
4. VChibi sẽ kiểm tra tính toàn vẹn của tệp tin (chuẩn PNG, kích thước ảnh, kiểm tra an toàn bảo mật).
5. Nếu gói hợp lệ, nhân vật sẽ được tự động sao chép vào kho lưu trữ nội bộ và xuất hiện trong danh sách thư viện.
6. **Nhập lặp lại**: Nếu bạn nhập cùng một gói thư mục nhiều lần, VChibi tự động cấp mã định danh mới (`-2`, `-3`, ...) để tránh ghi đè dữ liệu cũ.

---

## 5. Hành vi Desktop Pet & Click-Through

- **Nhân vật hoàn toàn không cản trở thao tác**: VChibi sử dụng cơ chế cửa sổ Win32 trong suốt chuẩn hệ điều hành (`WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`).
  - Bạn có thể nhấp chuột, bôi đen văn bản, cuộn chuột hoặc thao tác trên bất kỳ ứng dụng nào nằm ngay bên dưới nhân vật mà không bị chặn lại.
  - Nhân vật không cướp focus bàn phím, không hiện biểu tượng thừa trên thanh Taskbar hay danh sách Alt+Tab.
- **Quy luật di chuyển thông minh**:
  - Nhân vật chuyển đổi tự nhiên giữa trạng thái đứng yên ngắm nhìn và đi dạo.
  - Nhân vật đi dạo ngẫu nhiên đều khắp vùng làm việc màn hình chính (không áp dụng quy tắc 70/30, không theo dõi cửa sổ ứng dụng hay chuột).
  - Hướng quay đầu (trái/phải) tự động lật tự nhiên theo hướng bước đi.
  - Hệ thống tự động giới hạn nhân vật luôn nằm trong ranh giới màn hình, không bị kẹt hay biến mất ra ngoài rìa.
  - Khi tiến trình bị tạm dừng (freeze/lag giật hoặc sau khoảng ngắt thời gian dài), nhân vật tự động kẹp thời gian di chuyển (delta clamping tối đa 0.2s) để tránh bị tua nhanh hay nhảy vọt bất thường (lưu ý: chế độ Sleep/Hibernate phần cứng của hệ điều hành là giới hạn vật lý chưa kích hoạt kiểm thử trực tiếp).

---

## 6. Khay hệ thống (System Tray) & Thoát ứng dụng

- **Đóng cửa sổ (`X`)**:
  - Khi bạn bấm nút đóng (`X`) trên cửa sổ quản lý `LibraryWindow`, cửa sổ sẽ **ẩn về khay hệ thống** (System Tray).
  - Nhân vật desktop pet vẫn tiếp tục chạy bình thường trên màn hình.
  - Để mở lại cửa sổ quản lý: Nhấp đúp chuột vào biểu tượng VChibi trên khay hệ thống cạnh đồng hồ Windows.
- **Thoát hoàn toàn ứng dụng**:
  - Trên cửa sổ quản lý, bấm nút **"Thoát ứng dụng"** ở góc dưới cùng bên trái.
  - Toàn bộ nhân vật và tiến trình nền sẽ được đóng an toàn, giải phóng tệp khóa hệ thống.

---

## 7. Lưu trữ dữ liệu & An toàn tập tin

- **Vị trí dữ liệu**: Toàn bộ dữ liệu người dùng, cài đặt và các gói pet đã lưu được đặt tại thư mục:
  `%LOCALAPPDATA%\VChibi\`
  - `settings.json`: Lưu thông số tỉ lệ, tốc độ và trạng thái nhân vật đang chạy.
  - `pets/`: Thư mục chứa các gói pet nội bộ. Khi bạn tạo pet từ ảnh PNG bên ngoài, VChibi tự động sao chép các tệp ảnh vào kho này, nên bạn có thể thoải mái xóa hoặc di chuyển các tệp ảnh gốc mà không làm mất pet trong ứng dụng.
- **Bảo vệ chống hỏng dữ liệu (Data Recovery Guard)**:
  - Nếu tệp `settings.json` bị mất điện đột ngột hoặc hỏng cú pháp, VChibi sẽ hiển thị thông báo khôi phục và tạm thời tắt tính năng tự động ghi đè để bảo vệ dữ liệu.
  - Người dùng có thể bấm "Thoát không lưu" để bảo toàn 100% tệp tin lỗi và kiểm tra lại mà không lo bị ghi đè dữ liệu rỗng.
- **Chống mở trùng lặp (Single-Instance Guard)**:
  - VChibi chỉ cho phép một phiên bản ứng dụng chạy tại một thời điểm bằng cơ chế khóa tệp độc quyền (`.instance.lock`). Nếu bạn bấm chạy thêm lần nữa, ứng dụng sẽ hiện thông báo nhắc nhở và tự động đóng tiến trình thừa.
