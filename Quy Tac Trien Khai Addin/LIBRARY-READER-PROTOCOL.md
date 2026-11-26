# Giao thức đọc kho V1

Ngày 25/11/2026. Thành phần `Rainmaker.Library.Reader` 1.0.0, Manager 0.10.0 và Axis 1.9.0.

## Phân công và triển khai

QA quản lý DWG, quét, xác nhận và xuất kho. DLL Reader chỉ đọc bộ đã xuất; không sửa nguồn, danh mục, mã, nhóm, cấu hình add-in hoặc hàng chờ. Không cần Host, HTTP hoặc QA đang mở.

Reader là DLL hạ tầng riêng, không phải một công cụ Ribbon. Manager 0.10.0 mang một bản Reader dùng chung và phân giải phụ thuộc cho add-in. Không nhập Reader bằng nút Thêm file; không kèm bản Reader khác trong mỗi gói add-in. Gói Reader trong `_Development` dùng để bàn giao/tham chiếu phát triển; bản chạy nằm cạnh DLL Manager.

Add-in tham chiếu `Rainmaker.Library.Reader` với `Private=false`. API công khai chỉ dùng kiểu .NET tiêu chuẩn, không đưa kiểu Json.NET, AutoCAD, CAD.Contracts hay QA qua biên giao tiếp. Add-in vẫn có thể dùng các API vẽ hiện tại của Manager; đây là hai giao tiếp riêng.

## API ổn định

```csharp
using Rainmaker.Library.Reader;
ILibraryReaderV1 reader = LibraryReader.Open(applicationRoot);
LibrarySnapshotV1 snapshot = reader.Read();
var choices = snapshot.ListResources("hatches");
var chosen = snapshot.FindByCode("HAT-0022");
// Lưu chosen.Key.SourceId, Kind, Id vào cấu hình RIÊNG của add-in.
var ready = snapshot.ResolveResources(new[] { chosen.Key });
// Vẽ bằng ParametersJsonV1 / ProfileJsonV1 và SourceFile đã xác minh.
```

`applicationRoot` là ContentRoot của ứng dụng trên máy hiện tại, chứa `warehouse.local.json`. Cũng hỗ trợ trỏ trực tiếp vào kho cho công cụ kiểm thử. Nguồn hiện hỗ trợ là `ISO_QUANGANH`; không tự coi các nguồn tùy ý đã tích hợp QA.

| API | Ý nghĩa |
|---|---|
| `Read()` | Tạo snapshot nhất quán của bản đã xuất hiện hành. |
| `GetCatalogRevision()` | Đọc revision hiện tại; không phải một bộ đếm số nguyên hoặc khóa phân tán. |
| `ListResources(kind)` / `SearchResources(text, kind)` | Chỉ trả tài nguyên đang được phép chọn. Chuỗi rỗng nghĩa là cả 5 loại. |
| `GetResource(key)` / `FindByCode(code)` | Có thể trả cả dấu vết đã loại để giải thích liên kết cũ. Luôn kiểm tra `Available`. |
| `ResolveResources(keys)` | Chặn ID mất/loại, kiểm tra snapshot trước/sau và hash asset được chọn. Thực hiện trước khi ghi CAD. |
| `VerifyUnchanged()` | Phát hiện file, con trỏ hoặc metadata đã đổi từ lúc đọc. |
| `ResolveAssetFile(applicationRoot, hash)` | Phân giải asset theo cấu trúc kho hiện tại và kiểm tra SHA256, phục vụ engine vẽ. |

Khóa là `SourceId + Kind + Id`; Kind: `blocks`, `layers`, `hatches`, `texts`, `dims`. Đổi tên/nhóm không thay khóa. Không lưu đường dẫn vật lý làm khóa. Snapshot không được dùng để ghi đè cấu hình đã đổi ở lần nạp sau.

`ResourceV1` trả mã, tên CAD, tên hiển thị, đường dẫn nhóm, công dụng, ghi chú, trạng thái duyệt, version, SourceHash/SourceFile và hai payload V1. `Available` chỉ đúng khi `Availability == AVAILABLE`; các trạng thái còn lại là `RETIRED`, `DRAFT`, `UNPUBLISHED`, `TRASH`. Chưa duyệt không tự có nghĩa bị cấm sử dụng; yêu cầu nghiệp vụ của công cụ kiểm tra riêng.

## Schema payload V1

Các JSON này là giao tiếp V1 cho consumer, không phải lời hứa giữ nguyên JSON vật lý trong kho. Adapter của Reader chịu trách nhiệm chuyển cấu trúc mới về schema này. Trường tên phân biệt hoa/thường. Trường bổ sung phải được consumer bỏ qua; thiếu trường bắt buộc cho vai trò thì báo chọn/cấu hình lại.

| Kind | `ParametersJsonV1` |
|---|---|
| layers | `color`, `rgb`, `lineWeight`, `lineType`, `plottable`. LineWeight là số phần trăm mm theo AutoCAD, RGB có thể null. |
| hatches | `pattern`, `type`, `scale`, `angle` (độ), `layer`, `color`, `rgb`, `spacing`, `doublePattern`. |
| texts | `font`, `bigFont`, `height`, `widthFactor`, `obliqueAngle`, `vertical`. |
| dims | `textStyle`, `textHeight`, `arrowSize`, `scale`, `linearFactor`, `precision`, `extensionOffset`, `extensionBeyond`, `baselineSpacing`, `textGap`, `rounding`, `unitFormat`, `angularPrecision`, `zeroSuppression`, `annotative`. |
| blocks | `dynamic` và `attributes` cùng thông tin định nghĩa đã xác nhận; mỗi ATT có `tag`, `constant` và thuộc tính mẫu; dùng đúng tag người dùng chọn. |

`ProfileJsonV1` giữ schema render hiện tại: block có `Id`, `Geometry`, `NativeSource` (FileHash, BlockName, LabelAttribute, Min/Max và thông tin chèn), `Usage`; layer có `Name`, `Color`, `Appearance` (Rgb, LineWeight, LineType, Plottable, SourceHash). Text/Dim/Hatch có các trường tham số ở trên cùng `id`, `name`/`handle` khi có. Các thuộc tính render bổ sung giữ kiểu tương thích với `BlockAsset`/`LayerAsset` hiện tại; consumer không dùng Profile để tìm đường dẫn lưu danh mục.

`TemplateJsonV1` có `Id`, `Name`, `Version`, `Units`, `Font`, `TextHeight`, `ArrowSize`, `Presets`, `Blocks`, `Layers`; chỉ chứa block/layer còn hoạt động. Axis dùng schema này cho engine vẽ cũ. Không dùng `Usage` của Skill để ghi đè lựa chọn riêng của Axis.

Adapter json-v1 hỗ trợ các style export cũ chưa có `annotative`: lấy giá trị từ trạng thái đồng bộ đã xác nhận. Khi vẽ, vẫn clone Dim thật từ DWG đã xuất, giữ tên và xử lý annotative theo engine hiện hành. Không suy đoán lại kiểu Dim từ tên.

## Thay đổi cấu trúc kho

Mặc định Reader ghép các bản active của Template/Styles/Hatches, trạng thái ID, sổ mã và metadata. Chỉ Reader được biết cách ghép này. Consumer mới không đọc lại các file đó.

Tùy chọn `Catalog/library-reader.json` cho phép adapter đổi vị trí bên trong kho, ví dụ:

```json
{
  "schemaVersion": 1,
  "format": "json-v1",
  "paths": {
    "state": "Catalog/sync/state.json",
    "codes": "Catalog/resource-codes.json",
    "templates": "Versions/templates",
    "styles": "Styles",
    "hatches": "Hatches",
    "management": "Catalog/management",
    "assets": "CAD/Assets"
  }
}
```

Không cần tạo file này cho kho hiện tại. Nó chỉ là mô tả cho Reader, không tự di chuyển dữ liệu hay làm QA hỗ trợ đường dẫn mới. Một thay đổi storage phải cập nhật cả writer QA và adapter Reader, giữ API/payload V1, kiểm thử với consumer đã biên dịch trước đó. Nếu đổi định dạng dữ liệu, bổ sung adapter thay vì bắt mọi add-in đọc thêm file.

Giữ AssemblyVersion `1.0.0.0` cho cập nhật V1 tương thích; tăng FileVersion/package version. Không xóa/đổi tên/chữ ký public hoặc đổi kiểu/ý nghĩa trường V1. Thay đổi phá vỡ phải có giao thức major mới và kế hoạch chuyển đổi, không đổi getter dưới cùng assembly version. Việc triển khai Reader mới qua Manager cần mở lại AutoCAD; dữ liệu kho mới được nhận ở lần `Read()` tiếp theo, không cần build lại Reader mỗi khi thêm tài nguyên.

## Lỗi và giới hạn

`LibraryReadException.Code` phân biệt `LIBRARY_PENDING`, `CATALOG_CHANGED`, `INTEGRITY_MISMATCH`, `RESOURCE_NOT_FOUND`, `RESOURCE_UNAVAILABLE`, `ASSET_MISSING`, `ASSET_CHANGED`, `WAREHOUSE_UNAVAILABLE`, `STORAGE_UNSUPPORTED`, `CATALOG_INVALID` và lỗi ID/đường dẫn/nhóm. Khi gặp lỗi, dừng triển khai, báo người dùng xử lý tại QA hoặc nạp lại; không tự dùng cache cũ hay tạo kho thay thế.

Reader phát hiện cả hàng chờ sync cũ và journal QA, xác minh checksum danh mục và hash asset trước khi sử dụng. Đây không phải chữ ký chống giả mạo hoặc khóa cloud phân tán. Chỉ một máy xuất kho tại một thời điểm; đợi đồng bộ đủ. Snapshot có thể phát hiện thay đổi nhưng không giữ khóa writer trong toàn bộ transaction CAD.

Không triển khai registry vai trò, dịch vụ nền, tab tổng hợp ai đang dùng, hay tự chuyển đổi các add-in/skill khác. Axis 1.9.0 là consumer được chuyển lần này; cấu hình vẫn ở `Catalog/addin-settings/rainmaker-axis.json` để QA kiểm tra phụ thuộc.
