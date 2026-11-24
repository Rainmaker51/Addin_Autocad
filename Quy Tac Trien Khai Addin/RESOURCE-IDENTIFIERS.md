# Mã định danh tài nguyên thư viện

Khi viết add-in mới, đọc thêm [quy định dùng kho](LIBRARY-CONSUMER-STANDARD.md). Các ghi nhận ban đầu về cấp mã dưới đây không thay thế yêu cầu tích hợp xóa/gộp và nạp lại cấu hình theo QA 1.8.1.

Ngày: 20/11/2026. Phạm vi hiện tại: Block, Layer, Dim, Text và Hatch của nguồn `ISO_QUANGANH` đã được đồng bộ. Không triển khai tab tổng hợp ai đang sử dụng kho.

## Mã và ID

| Loại | Mã ngắn |
|---|---|
| Block | `BLK-0001`… |
| Layer | `LAY-0001`… |
| Dim | `DIM-0001`… |
| Text | `TXT-0001`… |
| Hatch | `HAT-0001`… |

Mỗi mã ngắn là bí danh của bộ khóa **SourceId + Kind + Id**. `Id` lấy từ định danh CAD đã lưu trong trạng thái đồng bộ và thẻ `AICAD_SYNC_RESOURCE` của đối tượng DWG sau khi áp dụng đồng bộ. Mã không được tính từ tên, đường dẫn, hash nội dung hoặc số version.

Đổi tên, đổi nhóm hoặc chỉnh thông số không tự đổi mã nếu vẫn là cùng ID. Khi đổi tên trong CAD, lưu DWG và thực hiện luồng đồng bộ hiện có để app nhận lại đúng ID đã gắn. Trước khi đồng bộ xong, mục chưa khớp hiện “Mã chờ đồng bộ”; app không suy đoán đối tượng mới chỉ từ tên.

Các asset cấu hình/biến thể trong app cùng tham chiếu một block nguồn dùng chung mã của block nguồn đó. `ResourceId` của asset hiện tại vẫn được giữ riêng để phân biệt cấu hình. Mã nguồn không thay thế các tham số biến thể hoặc metadata cũ.

## Nơi lưu và bảo toàn

Sổ mã nằm trong kho: `Catalog/resource-codes.json`. Hai máy phải đồng bộ cùng file này; không xóa để tạo lại số hoặc sửa tay. Các mã đã cấp được giữ lại kể cả khi tài nguyên không còn trong danh mục hoạt động. Một ID mới nhận mã tiếp theo, không tái sử dụng số đã cấp cho ID khác.

```json
{
  "SchemaVersion": 1,
  "Revision": 1,
  "Items": [
    {
      "SourceId": "ISO_QUANGANH",
      "Kind": "blocks",
      "Id": "375171a1639e99c107748ca2be57c29c",
      "Code": "BLK-0005",
      "Name": "QA_KT_KHUNG_TEN"
    }
  ]
}
```

Ví dụ chỉ minh họa cấu trúc; không dùng để ghi đè sổ mã. `Name` phục vụ hiển thị, không phải khóa tham chiếu. Giao tiếp không có trạng thái dùng của add-in/skill, lịch sử vẽ hay cấu hình chọn tài nguyên.

Sổ mã có khóa ghi giữa các tiến trình cùng hệ thống tệp. Với kho đồng bộ qua cloud, chỉ một máy cấp/cập nhật mã tại một thời điểm và chờ đồng bộ đủ trước khi mở app trên máy kia; khóa file không phải khóa phân tán.

## API và giao diện

- `POST /api/library-identifiers` (API cục bộ có xác thực): cấp bổ sung mã cho ID đã xác nhận, giữ mã cũ và trả ánh xạ danh mục đang hoạt động. Không quét hoặc ghi DWG, không sửa tên/nhóm, phiên bản thư viện hay gói add-in.
- Kết quả có `revision`, `sourceId`, `items`. Mỗi mục có `Kind`, `SourceId`, `ResourceId` (khóa metadata hiện tại), `Id` (đồng bộ), `Code`, `Name`, `Status`.
- `IDENTIFIED`: đã ánh xạ được đúng một ID. `PENDING_SYNC`: chưa xác nhận được ID. `AMBIGUOUS`: có nhiều ID khớp, không tự chọn.
- App hiển thị mã trên danh sách và trong hồ sơ từng loại, cùng nút sao chép mã/đọc lại mã. Có thể tìm tài nguyên bằng mã trong bộ lọc hiện có.

Add-in/skill tích hợp sau này phải tra sổ mã hoặc giao tiếp app để phân giải mã thành đúng bộ khóa và tài nguyên nguồn. Việc cấp mã chưa tự sửa cách Tạo trục chọn dim; phần chọn tài nguyên trong add-in/skill là bước triển khai riêng.

## Kiểm thử

`tests/ResourceIdentifiers.Tests` kiểm tra đủ năm tiền tố, đổi tên giữ ID/mã, không tái dùng mã, chống trùng/sai cấu trúc/đường dẫn, ánh xạ metadata cũ và đổi tên CAD sau đồng bộ. Kiểm tra giao diện Chrome xác nhận mã và nút sao chép ở đủ năm loại và không có tab tổng hợp đã hủy.
