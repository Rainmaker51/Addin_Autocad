# Quy tắc làm việc với add-in AutoCAD

Khi phát triển, sửa lỗi, build hoặc bàn giao add-in .NET của dự án:

Đọc và tuân thủ `docs/ADDIN-MANAGER-STANDARD.md`: mỗi add-in mới có bảng điều khiển riêng và đúng một nút Ribbon để mở bảng. Manager quản lý tab/nhóm/nút; add-in không tự tạo Ribbon hoặc tự đăng ký nạp riêng. Đối chiếu giao tiếp Manager thực tế, giữ cấu hình và dữ liệu khi cập nhật. Không tự chuyển đổi add-in cũ hoặc coi khả năng dự kiến trong tiêu chuẩn là tính năng Manager đã có.

Giao diện add-in mới phải theo Axis, gồm cả màu sắc, bố cục, kiểu điều khiển và tương tác; đọc `docs/ADDIN-UI-STANDARD.md`. Dùng theme dark hiện có, bảng thông số gọn, nhãn rõ, nút chính ở cuối bảng; nếu có preview thì dùng bố cục hai vùng co kéo và nhớ tỷ lệ theo máy. Không tự thiết kế một phong cách khác hoặc đổi giao diện add-in cũ khi chưa được yêu cầu.

Add-in mới dùng Block, Layer, Hatch, Text hoặc Dim của kho phải đọc `docs/LIBRARY-CONSUMER-STANDARD.md` và `docs/RESOURCE-IDENTIFIERS.md`. QA quản lý thư viện; add-in tiêu thụ chỉ đọc bản đã xuất và ghi lựa chọn của chính nó theo ID ổn định. Cấu hình liên kết phải được QA nhận diện để kiểm tra xóa/gộp; nạp lại lựa chọn ở lần mở/triển khai sau, không phục hồi ID đã loại từ cache. Đối chiếu giới hạn hiện tại về phụ thuộc, journal và skill; không tự giả định API chung hoặc tự chuyển đổi add-in cũ.

1. Đọc `docs/ADDIN-DEVELOPMENT-WORKFLOW.md`. Nếu có `warehouse.local.json`, lấy trường `path` rồi đọc thêm `Tools/dotnet/QUY_TRINH_PHAT_TRIEN_ADDIN.md` và README của add-in trong `_Development` ở kho đó. Không hardcode đường dẫn kho của máy khác.
2. Đối chiếu `Tools/catalog.json` để biết gói Manager đang chọn; không suy ra từ version cao nhất hoặc ngày tệp.
3. Sửa mã nguồn trong repo. Gói bàn giao và các version phát triển đặt trong `Tools/dotnet/_Development/<TênAddin>/versions/<version>/build-<fingerprint>/`; mỗi build có `package/`, manifest và ghi chú/kiểm thử. Build tạm đặt dưới `working/` của đúng add-in. Không trả gói cuối ra Desktop, Temp hoặc nhiều thư mục tùy ý.
4. Gói đang dùng nằm trong `Tools/dotnet/_Installed/<TênAddin>/<version>-<fingerprint>/`; chỉ giữ một gói cho mỗi add-in. Không sửa tay gói cài hoặc catalog. Manager tự lưu lịch sử, sao chép/kiểm tra gói mới, đổi con trỏ rồi dọn bản cũ. Khi được yêu cầu chuyển cấu trúc, dùng `tools/Addin.Storage` hoặc installer `-MigrateInstalled`; không sửa JSON/di chuyển thủ công. Cập nhật cùng một phiên bản Manager trên cả hai máy.
5. Không ghi đè build đã bàn giao, không tự xóa bản cũ. Bàn giao phải chỉ rõ source commit/patch, version, hash, kiểm thử và tình trạng đã/chưa được chọn trong catalog.
6. Đọc `docs/TEST-ARTIFACT-LIFECYCLE.md`. Sau kiểm thử phải giữ bằng chứng chọn lọc, xác minh bản bàn giao rồi dọn dữ liệu tạm thuộc phiên; báo dung lượng trước/sau và mục còn giữ. Profile Core/runtime thử nghiệm nằm cục bộ ở `.tools/test-runs/<run-id>`, không nhân bản vào kho đồng bộ. Không dọn phiên khác hoặc bản đang sử dụng chỉ vì thư mục lớn/cũ.

Nếu kho không truy cập được, tiếp tục công việc độc lập trong repo theo bản quy trình tại `docs/`; nêu rõ chưa thể bàn giao vào kho. Không tự tạo kho thay thế hay sửa đường dẫn kho để vượt qua tình trạng đó.
