# Quản lý dữ liệu kiểm thử và dọn sau bàn giao

Cập nhật: 24/11/2026. Bắt buộc khi build/kiểm thử/bàn giao CAD.Manager và mọi add-in. Mục tiêu: lưu đủ bằng chứng để tái hiện kết quả, không tích lũy bản sao runtime và profile AutoCAD trong kho đồng bộ.

## 1. Nơi lưu và vòng đời

| Loại dữ liệu | Nơi lưu | Khi kết thúc |
|---|---|---|
| Source, kịch bản và fixture nhỏ có thể tái tạo | Repo Git | Giữ trong Git theo quy tắc dự án |
| Build tạm | `_Development/<TênAddin>/working/<run-id>/` | Dọn sau khi bản bàn giao đã kiểm tra đầy đủ |
| Profile Core Console, runtime-cache, DWG sinh ra và bản sao Host phục vụ test | `.tools/test-runs/<run-id>/` trên máy hiện tại | Dọn khi tiến trình kiểm thử đã thoát; không đồng bộ vào kho |
| DLL, manifest, source commit/patch và bằng chứng chọn lọc | `versions/<version>/build-<fingerprint>/` | Giữ bất biến |
| Hồ sơ dọn dữ liệu tạm chưa gắn với build | `_Development/<TênAddin>/cleanup-records/<run-id>/` | Chỉ giữ báo cáo nhỏ, không sao lưu lại toàn bộ dữ liệu vừa dọn |

Ngoại lệ `.tools/test-runs` chỉ dành cho dữ liệu chạy thử cục bộ, không thay vị trí build/bàn giao trong `working` và `versions`. Nếu Core cần đường dẫn ngắn, dùng ổ ánh xạ tạm cho thư mục của phiên; tháo ánh xạ trong `finally`.

## 2. Trước khi chạy

1. Đọc catalog để xác định bản đang dùng và đọc hướng dẫn của add-in. Không dùng DWG công trình thật làm fixture.
2. Tạo run ID riêng. Ghi marker sở hữu, ví dụ `test-run.json`, gồm run ID, tên component, source commit, thời điểm UTC, mục đích và các thư mục của phiên. Không ghi token, mật khẩu hoặc dữ liệu đăng nhập.
3. Đo dung lượng/số tệp ban đầu của `working`. Tái sử dụng một bộ runtime thử nghiệm trong phiên khi an toàn; không sao chép cả Host/profile cho từng thao tác hoặc từng test case.
4. Ghi PID/đường dẫn tiến trình do phiên tạo, dùng dữ liệu và cổng biệt lập. Không tự đóng AutoCAD của người dùng để chạy hoặc dọn test.

## 3. Giữ gì làm bằng chứng

Trước khi niêm phong build, chọn các tệp thực sự chứng minh thay đổi:

- Kết quả kiểm thử có trạng thái đạt/lỗi và phạm vi đã kiểm tra, lỗi/stack trace liên quan.
- Log ngắn hoặc đoạn log liên quan; ảnh giao diện/preview cần thiết.
- Hash của DLL và fixture đầu vào, tham số/lệnh chạy, version AutoCAD và .NET.
- Fixture đầu ra nhỏ nếu cần để tái hiện một lỗi hình học hoặc lưu/mở DWG. Ghi lý do nếu giữ DWG lớn.
- Source commit và patch đủ để dựng lại bản bàn giao.

Không mang cả profile AutoCAD, runtime-cache, bộ Host trùng lặp, `bin/obj`, tài khoản hoặc file `host.json`/session có token vào gói bằng chứng. Không tạo ZIP toàn bộ thư mục test rồi coi là đã dọn.

Không ghi đè bằng chứng trùng tên: dùng thư mục theo loại test hoặc tên như `native-data.json`, `http.json`, `axis-resources.json`. Đối chiếu từng báo cáo và hash sau khi sao chép; không chỉ kiểm tra tệp có tồn tại.

## 4. Điều kiện được dọn

Với dữ liệu do chính phiên hiện tại tạo, dọn là một bước của công việc build/kiểm thử; không cần chờ người dùng nhắc lại. Chỉ thực hiện sau khi:

1. Build đã được chốt tại `versions`, DLL/source patch và bằng chứng cần giữ đã được kiểm tra hash. Nếu chưa chốt, giữ source/patch và ghi rõ phần đang làm dở.
2. Tất cả tiến trình dùng thư mục đích do phiên tạo đã thoát; tháo ổ ánh xạ tạm. Nếu file bị khóa, báo phần chưa dọn, không đóng cưỡng bức AutoCAD người dùng.
3. Đối chiếu Registry loader, cấu hình Host, catalog và cấu hình phiên liên quan để bảo đảm không có đường dẫn đang sử dụng nằm trong mục định xóa. Không dọn build đang chờ cài nếu chưa có gói bàn giao thay thế đã kiểm tra.
4. Giải quyết đường dẫn tuyệt đối của từng mục, xác nhận nó nằm dưới đúng thư mục tạm/working của phiên. Từ chối gốc kho, gốc repo, `.git`, đường dẫn vượt phạm vi và junction/symlink/reparse point không được kiểm chứng. Không xóa hàng loạt bằng wildcard hoặc chỉ dựa vào ngày cũ.

Dữ liệu tạm của phiên khác hoặc không rõ chủ sở hữu phải được kiểm tra và xác nhận phạm vi trước khi dọn; không dùng quy tắc này để tự xóa lịch sử người khác. Không xóa/sửa `versions`, `_Installed`, catalog, nguồn CAD hoặc cấu hình add-in bằng bước dọn kiểm thử.

## 5. Kết thúc mỗi phiên

1. Ghi manifest dọn: thư mục tuyệt đối đã kiểm tra, run ID, dung lượng/số tệp trước dọn, danh sách bằng chứng đã giữ và SHA256.
2. Dọn thư mục runtime/profile/DWG tạm và build tạm đã đủ điều kiện; dùng thao tác một shell, đúng đường dẫn literal đã kiểm tra.
3. Xác nhận đích đã biến mất, kiểm tra bản bàn giao và con trỏ đang dùng không thay đổi.
4. Đo dung lượng còn lại. Báo số MB đã thu hồi, bằng chứng giữ ở đâu và mục nào chưa dọn cùng lý do. Phiên có test lỗi vẫn phải dọn phần không cần; chỉ giữ bộ tái hiện nhỏ nhất và đánh dấu đang giữ.

Ngưỡng để rà soát: khi `working` của một component vượt **300 MB** hoặc tổng `working` của các component vượt **1 GB**, thống kê trước khi tạo thêm bản sao. Đây là ngưỡng cảnh báo, không phải lệnh tự xóa; nếu kiểm thử cần vượt ngưỡng, ghi lý do và kế hoạch dọn. Không tích lũy hết lần chạy này đến lần chạy khác.

## 6. Tích hợp vào script

Script kiểm thử mới/sửa phải dùng `try/finally` để kết thúc đúng tiến trình của fixture và tháo ổ ánh xạ. Bước giữ bằng chứng chạy trước bước dọn; lỗi kiểm thử vẫn giữ kết quả thất bại. Nếu dọn không thành công, ghi đường dẫn còn lại thay vì báo đã dọn.

Không thêm dịch vụ nền hay tác vụ hẹn giờ tự xóa kho. Script cũ chưa có bước dọn phải được người thực hiện phiên chạy bổ sung bước kết thúc theo mục 5; việc có tài liệu này không có nghĩa các script cũ đã tự động dọn.
