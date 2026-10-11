# Phát hành tự động Windows

Workflow: `.github/workflows/release.yml`.

- PR vào `main`: Windows runner chạy `dotnet test VChibi.sln -c Release`, publish theo `Windows.pubxml`, đóng ZIP và lưu artifact `windows-release` trong 7 ngày. Không tạo tag/Release.
- Push vào `main` (bao gồm merge PR): cùng quy trình kiểm tra, sau đó tạo GitHub Release chính thức kèm ZIP.
- Tag tự động: `v0.1.<github.run_number>`. Đây là số lần chạy workflow, không phải số merge; PR và lần chạy thất bại cũng tiêu thụ số. Chạy lại cùng run giữ nguyên tag.
- Version assembly/package được đặt thành `0.1.<github.run_number>` khi publish. Chuỗi này độc lập với tiêu đề phiên bản lịch sử trong changelog.
- Tag trỏ đúng `github.sha` đã build, không phải HEAD mới nhất nếu có merge khác trong lúc build.
- Build/test/publish/đóng gói lỗi: không chạy job phát hành. Tag chỉ được tạo khi tạo bản nháp Release sau khi ZIP đã sẵn sàng. Upload lỗi có thể để lại tag/bản nháp; rerun dùng lại bản đó và upload lại ZIP trước khi công khai.
- Quyền ghi `contents: write` chỉ cấp cho job release trên `main`; PR chỉ có quyền đọc. Dùng `GITHUB_TOKEN`, không cần PAT hay secret riêng.

## Tải và sử dụng

Vào GitHub → Releases → tải `VChibi-v0.1.<số>-win-x64.zip`. Giải nén toàn bộ rồi chạy `VChibi.exe`. ZIP chứa runtime .NET, `Assets/DefaultPet` và `NOTICES.md`; không chỉ riêng EXE. Không chứa pet cá nhân trong AppData hoặc ảnh nguồn `assets/congb`.

## Vận hành

Workflow bắt đầu hoạt động khi được push lên nhánh có PR; phát hành chính thức lần đầu khi workflow được merge vào `main`. Không merge chỉ để thử phát hành.

Trong Actions → Windows CI and Release, kiểm tra hai job. Khi thất bại, sửa lỗi hoặc dùng Re-run failed jobs. Không tạo tag thủ công trùng `v0.1.<số>`.

Trigger `push` cũng nhận push trực tiếp vào `main`. Muốn chỉ phát hành qua PR: cấu hình branch protection/ruleset yêu cầu PR và check `Build, test and package Windows`; workflow không tự thay đổi ruleset.

CI xanh chứng minh test/build/package đạt, không thay thế kiểm thử GUI, DPI, click-through, tray, Sleep hoặc máy Windows sạch.

Tham khảo: [GITHUB_TOKEN và quyền workflow](https://docs.github.com/en/actions/tutorials/authenticate-with-github_token).
