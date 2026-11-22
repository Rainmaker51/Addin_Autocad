# Quy tắc làm việc với add-in AutoCAD

Khi phát triển, sửa lỗi, build hoặc bàn giao add-in .NET của dự án:

Đọc và tuân thủ `docs/ADDIN-MANAGER-STANDARD.md`: mỗi add-in mới có bảng điều khiển riêng và đúng một nút Ribbon để mở bảng. Manager quản lý tab/nhóm/nút; add-in không tự tạo Ribbon hoặc tự đăng ký nạp riêng. Đối chiếu giao tiếp Manager thực tế, giữ cấu hình và dữ liệu khi cập nhật. Không tự chuyển đổi add-in cũ hoặc coi khả năng dự kiến trong tiêu chuẩn là tính năng Manager đã có.

1. Đọc `docs/ADDIN-DEVELOPMENT-WORKFLOW.md`. Nếu có `warehouse.local.json`, lấy trường `path` rồi đọc thêm `Tools/dotnet/QUY_TRINH_PHAT_TRIEN_ADDIN.md` và README của add-in trong `_Development` ở kho đó. Không hardcode đường dẫn kho của máy khác.
2. Đối chiếu `Tools/catalog.json` để biết gói Manager đang chọn; không suy ra từ version cao nhất hoặc ngày tệp.
3. Sửa mã nguồn trong repo. Gói bàn giao và các version phát triển đặt trong `Tools/dotnet/_Development/<TênAddin>/versions/<version>/build-<fingerprint>/`; mỗi build có `package/`, manifest và ghi chú/kiểm thử. Build tạm đặt dưới `working/` của đúng add-in. Không trả gói cuối ra Desktop, Temp hoặc nhiều thư mục tùy ý.
4. Gói đang dùng nằm trong `Tools/dotnet/_Installed/<TênAddin>/<version>-<fingerprint>/`; chỉ giữ một gói cho mỗi add-in. Không sửa tay gói cài hoặc catalog. Manager tự lưu lịch sử, sao chép/kiểm tra gói mới, đổi con trỏ rồi dọn bản cũ. Khi được yêu cầu chuyển cấu trúc, dùng `tools/Addin.Storage` hoặc installer `-MigrateInstalled`; không sửa JSON/di chuyển thủ công. Cập nhật cùng một phiên bản Manager trên cả hai máy.
5. Không ghi đè build đã bàn giao, không tự xóa bản cũ. Bàn giao phải chỉ rõ source commit/patch, version, hash, kiểm thử và tình trạng đã/chưa được chọn trong catalog.

Nếu kho không truy cập được, tiếp tục công việc độc lập trong repo theo bản quy trình tại `docs/`; nêu rõ chưa thể bàn giao vào kho. Không tự tạo kho thay thế hay sửa đường dẫn kho để vượt qua tình trạng đó.
