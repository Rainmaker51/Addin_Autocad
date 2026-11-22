# KẾ HOẠCH XÂY DỰNG ADD-IN ARCHITECTURAL SUITE

**Tài liệu:** `PLAN_Architectural_Suite_v0.1.md`  
**Phiên bản:** 0.1 – Đề xuất kiến trúc và kế hoạch triển khai  
**Ngày:** 21/11/2026  
**Nền tảng mục tiêu:** AutoCAD 2021, C#/.NET Framework 4.8, x64  
**Căn cứ:** `PLAN_Autocad_v0.2.md` và bộ tài liệu trong `Quy Tac Trien Khai Addin`  
**Trạng thái ngày 21/11/2026.** Manager đã chọn Architectural Suite 0.2.0, build `90655f7a4b06`, với một nút `QAARCHOPEN`. Palette bố trí thao tác theo nhóm, có chọn vùng nguồn, kiểm kê, duyệt gợi ý, đăng ký tường/cửa, tra cứu và định vị, chọn tài nguyên kho, tạo hai mặt bằng, bảng thống kê, xem trước và duyệt cập nhật, xử lý xung đột, xuất CSV/JSON và dựng lại chỉ mục SQLite. Kiểm thử Core Console trên DWG tổng hợp đã kiểm tra liên kết, diện tích trừ cửa, sửa rộng cửa, bảo vệ nét chỉnh tay, lưu/mở lại, Undo/Redo và nhập tài nguyên kho. Đã mở Palette hai lần trong AutoCAD đồ họa và xác nhận một instance. Cần người triển khai duyệt quy tắc nhận diện, điểm neo cửa, bố trí ký hiệu và tỷ lệ trên mặt bằng công trình đại diện. Hướng dẫn sử dụng ở `src/Arch.Suite.Host/README.md`.

**Bổ sung đã thống nhất ngày 21/11/2026:** tên Architectural Suite, ưu tiên đăng ký hình có sẵn, duyệt trước khi đồng bộ, bảo vệ chỉnh sửa tay và hai tiện ích kiểm tra MVP tại mục 2.1–2.3.

---

## 1. Mục tiêu sản phẩm

Xây dựng **một add-in duy nhất mang tên Architectural Suite** được cài và quản lý bằng CAD Tool Manager, có một nút Ribbon để mở một Palette WPF. Bên trong add-in chứa các module chức năng có thể phát triển, kiểm thử và bảo trì độc lập:

1. Drawing Analyzer – phân tích hồ sơ DWG hiện hữu.
2. Object Manager – quản lý cấu kiện kiến trúc có ID và thuộc tính.
3. Link Manager – quản lý các quan hệ dữ liệu, hình biểu diễn và bản vẽ.
4. Sync Engine – phát hiện thay đổi và đồng bộ an toàn.
5. Drawing Generator – sinh và tái cập nhật bản vẽ triển khai.
6. Quantity & QA – thống kê, trích xuất dữ liệu và kiểm tra lỗi.

**Kết quả mong muốn:** sửa một đối tượng nguồn đúng quyền sở hữu thì hệ thống nhận diện các hình và bảng chịu ảnh hưởng, cho người thiết kế duyệt và cập nhật phần cần thiết; không xóa hoặc ghi đè những nét/ghi chú thủ công ngoài phạm vi quản lý.

### Các nguyên tắc bất biến

- Một cấu kiện logic có **một GUID nghiệp vụ ổn định**, có thể có nhiều hình biểu diễn CAD.
- Một thuộc tính có **nguồn có thẩm quyền**; không đồng bộ hai chiều tự do trong MVP.
- Dữ liệu nghiệp vụ và các nét/hatch/dim do generator tạo phải phân biệt rõ.
- Chỉ cập nhật đối tượng bị ảnh hưởng; chạy lại Sync không sinh trùng.
- Không tự động thay đổi DWG ngay khi nạp add-in, quét hồ sơ hoặc mở Palette.
- Mọi cập nhật có trạng thái, revision, kiểm tra xung đột và khả năng phục hồi.

## 2. Phạm vi phiên bản đầu tiên (MVP)

### Phạm vi phải làm

- AutoCAD 2021; **một DWG có thể chứa nhiều vùng bản vẽ trong Model Space**.
- Chọn và xác nhận ít nhất một vùng mặt bằng nguồn, một tầng.
- Nhập/đăng ký **Wall thẳng 2D/2.5D** và **Door gắn Wall**, bao gồm các nét đã vẽ.
- Gắn GUID và lưu dữ liệu cốt lõi trong DWG; dựng lại index khi mở lại.
- Thiết lập quan hệ Door → Wall → View/biểu diễn/bảng.
- Tạo **mặt bằng xây tường** (`PLAN_MASONRY`) và **mặt bằng định vị cửa** (`PLAN_DOOR`).
- Tạo **thống kê cửa** (`DOOR_SCHEDULE`) và **thống kê tường** (`WALL_SCHEDULE`).
- Phát hiện Move/Stretch/Resize/Delete, Undo/Redo; xem trước tác động và cho phép duyệt đồng bộ.
- Xuất dữ liệu cơ bản CSV/JSON; bảng số liệu truy vết về đối tượng nguồn.
- Giao diện Palette cho cây hồ sơ, thuộc tính, liên kết, đồng bộ, xuất dữ liệu, lỗi.
- Tra cứu hai chiều giữa Palette và đối tượng CAD bằng chọn, zoom/highlight và xem thuộc tính.
- Kiểm tra sẵn sàng trước Generate/Apply; báo điều kiện thiếu và dẫn tới đối tượng lỗi.

### Chưa thuộc MVP

- Tự động nhận diện hoàn hảo toàn bộ hồ sơ DWG cũ.
- Bộ công cụ vẽ tường/cửa mới từ đầu; MVP ưu tiên đăng ký nét/block có sẵn và chỉnh dữ liệu nguồn đã đăng ký.
- Đồng bộ hai chiều tự do; chỉnh nét nào trong bản vẽ con cũng thay đổi mô hình nguồn.
- Đồng bộ nhiều DWG/Xref theo thời gian thực; cộng tác ghi đồng thời nhiều máy.
- Sinh hoàn chỉnh mặt đứng, mặt cắt, trần, sàn, WC, cầu thang, nội thất và bộ hồ sơ in ấn.
- IFC/BIM 3D/AI nhận diện tự động, phát hành PDF hàng loạt.

Các hạng mục này chỉ triển khai sau khi MVP đạt kiểm thử và có dữ liệu thực tế để xác định quy tắc.

### 2.1. Các quyết định nghiệp vụ đã thống nhất

- **Tên sản phẩm:** Architectural Suite; tên nút Ribbon và Palette đồng nhất. Assembly chính `Arch.Suite.Host.dll`; Architectural Data Core là lõi dữ liệu bên trong. Lệnh mở dự kiến `QAARCHOPEN`, kiểm tra trùng với Manager trước khi cố định; GUID PaletteSet được cấp một lần khi triển khai và giữ ổn định.
- **Đầu vào ưu tiên:** người dùng chọn nét/block hiện có, xác nhận phân loại rồi đăng ký Wall/Door. Không yêu cầu vẽ lại hồ sơ. Công cụ vẽ cấu kiện mới bổ sung sau MVP.
- **Đồng bộ MVP:** nguồn thay đổi thì đánh dấu đầu ra cần cập nhật, hiển thị tác động theo luồng **Preview → Apply/Skip**. Không tự ghi cập nhật chỉ vì phát hiện thay đổi; kiểm tra lại revision trước Apply.
- **Chỉnh tay hình được sinh:** giữ nguyên hình đã chỉnh, đánh dấu `CONFLICT` và chặn ghi đè phần xung đột. Người dùng có thể giữ chỉnh sửa để xử lý sau hoặc chủ động duyệt tái sinh từ nguồn sau khi xem preview; lựa chọn phải được ghi nhận. Không tự suy ngược chỉnh sửa nét thành dữ liệu nguồn. Ghi chú thủ công ngoài phạm vi managed luôn được bảo toàn.

### 2.2. Hai tiện ích bắt buộc trong MVP

**Tra cứu Palette ↔ CAD**

- Chọn một mục Wall/Door/biểu diễn trong Palette để zoom/highlight đúng thực thể trong DWG hiện hành.
- Chọn thực thể CAD đã đăng ký để mở dữ liệu và quan hệ tương ứng trong Object Inspector. Thực thể chưa đăng ký phải hiển thị đúng trạng thái, không tự đăng ký.
- Đối tượng có nhiều biểu diễn phải chỉ rõ View và cho chọn biểu diễn cần tìm. Khi entity bị xóa hoặc người dùng chuyển DWG, báo liên kết mất hoặc làm mới ngữ cảnh; không dùng handle để tìm nhầm tài liệu.
- Zoom/highlight và xem thuộc tính không sửa dữ liệu DWG; dọn highlight khi đổi lựa chọn hoặc đóng bảng.

**Kiểm tra sẵn sàng trước khi chạy**

- Trước Generate/Apply, kiểm tra tài liệu đích, đơn vị, tầng/View, dữ liệu Wall/Door bắt buộc, host tường, liên kết, revision/xung đột và tài nguyên theo vai trò mà thao tác cần dùng.
- Hiển thị danh sách lỗi/cảnh báo, nguyên nhân và hướng khắc phục; lỗi bắt buộc chặn thao tác phụ thuộc. Không tự chọn tài nguyên thay thế hay tự sửa dữ liệu thiếu.
- Có nút đi tới đối tượng lỗi bằng zoom/highlight; lỗi không có entity đích dẫn tới mục cấu hình phù hợp.
- Kiểm tra lại sau khi người dùng sửa lỗi và ngay trước khi ghi. Kết quả kiểm tra chỉ phản ánh trạng thái tại thời điểm kiểm tra.

### 2.3. Những đầu vào còn phải xác minh tại Gate 0

- Đường dẫn repo AI-CAD-MCP/Manager, contract thực tế, `warehouse.local.json`, catalog và hash gói đang dùng; môi trường AutoCAD 2021 và công cụ build.
- Đã tìm thấy hai DWG trong thư mục FILE MAU, cấu hình kho ở repo AI-CAD-MCP và catalog revision 99. Kiểm kê trực tiếp được 24.781 thực thể trong Nhà Công Vụ và 3.873 trong Nhà Cô Hà. Hai file khai báo đơn vị mm, có Model và Layout1. Cần đối chiếu hình vẽ khi xác nhận tầng, phạm vi mặt bằng và vai trò của từng nhóm thực thể.
- Sau khảo sát mới chốt đơn vị, vùng bản vẽ, kiểu thể hiện tường/cửa, nguồn chiều cao và quy tắc neo cửa khi tường dịch chuyển/đổi chiều dài. Không khóa schema dựa trên giả định chưa kiểm chứng.

## 3. Luồng nghiệp vụ minh chứng

1. Người dùng mở **Architectural Suite** từ một nút Ribbon.
2. Trong **Drawing Analyzer**, chọn vùng mặt bằng gốc, xác nhận mã view, tầng, đơn vị và cách tổ chức trong Model Space.
3. Chọn một nhóm hai Line tường và một Block/nét cửa; xác nhận phân loại.
4. **Object Manager** đăng ký Wall/ Door, tạo ID và thuộc tính; không làm lệch hình học gốc.
5. **Link Manager** gắn cửa với tường, tạo quan hệ nguồn → hình biểu diễn → bảng thống kê.
6. **Drawing Generator** sinh hai bản vẽ phụ thuộc và bảng cửa/tường trong vùng đầu ra được chỉ định.
7. Người dùng đổi cửa rộng **900 → 1000 mm** ở dữ liệu nguồn.
8. **Sync Engine** báo các ô mở, ký hiệu, kích thước và dòng thống kê chịu ảnh hưởng; người dùng chọn **Preview → Apply**.
9. **Quantity & QA** so sánh số liệu, phát hiện liên kết hỏng và ghi trạng thái.
10. Save/Close/Open lại DWG: danh tính, liên kết, revision và trạng thái vẫn được khôi phục.

**Demo đạt khi:** thay đổi một cửa cập nhật đúng các bản vẽ liên quan; các ghi chú không do hệ thống quản lý vẫn giữ nguyên; chạy Sync lần nữa không sinh đối tượng trùng.

## 4. Kiến trúc phần mềm

### 4.1. Một Add-in, nhiều module

```text
CAD Tool Manager
    |
    |-- 1 nút Ribbon: "Architectural Suite"
    v
Arch.Suite.Host.dll
    |-- WPF PaletteSet: Architectural Suite
    |-- Command router / lifecycle
    |
    +-- Drawing Analyzer
    +-- Object Manager
    +-- Link Manager
    +-- Sync Engine
    +-- Drawing Generator
    +-- Quantity & QA
    |
    +-- Architectural Data Core (dùng chung)
          |-- Domain objects / schemas / GUID
          |-- Property ownership / ChangeSet
          |-- View & representation mapping
          |-- Dependency graph
          |-- Resource roles
          |-- DWG persistence + SQLite index
```

Mỗi module có thể là project/DLL kỹ thuật riêng, nhưng **không phải một add-in có nút Ribbon riêng**. Các lệnh nghiệp vụ có thể được Manager điều phối mà không phải ghim nhiều nút.

### 4.2. Cấu trúc solution đề xuất

```text
ArchitecturalSuite/
  src/
    Arch.Suite.Host/          # IExtensionApplication, commands, PaletteSet
    Arch.Core/                # model nghiệp vụ, interfaces, rules
    Arch.CadAdapter.A2021/    # AutoCAD .NET API, transactions, metadata
    Arch.Analyzer/
    Arch.ObjectManager/
    Arch.LinkManager/
    Arch.SyncEngine/
    Arch.DrawingGenerator/
    Arch.QuantityQA/
    Arch.Modules.Wall/
    Arch.Modules.Door/
    Arch.ResourceIntegration/
    Arch.UI/
  tests/
    Arch.Core.Tests/
    Arch.Geometry.Tests/
    Arch.Dependency.Tests/
    Arch.Sync.Tests/
    Arch.Integration.AutoCAD/
  schemas/                    # schema version và migration
  rules/                      # view templates, rules, roles
  samples/                    # bản sao fixture DWG, không chứa dữ liệu bí mật
  docs/                       # ADR, README, UAT, changelog
```

**Lưu ý:** đây là đích tổ chức code dự kiến. Trước khi tạo solution phải đối chiếu repo AI-CAD-MCP hiện có, assembly/contracts và cơ chế build của CAD Tool Manager, tránh tạo trùng hạ tầng.

### 4.3. Hợp đồng dữ liệu v1 cần chốt

**Đối tượng nguồn:** `GlobalGuid`, `Type`, `SchemaVersion`, `LevelId`, `Geometry`, `Properties`, `StyleId`, `Revision`, `Relations`, `PropertyOwners`.

**Biểu diễn:** `RepresentationGuid`, `SourceGlobalGuid`, `ViewId`, `RendererVersion`, `SourceRevision`, `ManagedEntityHandles`, `ManualEditPolicy`.

**Quan hệ:** `SourceGuid`, `TargetGuid`, `RelationType`, `PropertyDependencies`, `SourceRevision`, `TargetRevision`, `Status`.

**Bản vẽ/View:** `DocumentGuid`, `ViewId`, `ViewType`, `LevelId`, `Bounds`, `Transform`, `Scale`, `Owner`, `Revision`, `SyncStatus`.

**Wall MVP:** đường chuẩn, điểm đầu/cuối, bề dày, cao độ đáy/đỉnh, tầng, loại/vật liệu, danh sách CAD entities.

**Door MVP:** tường chủ, điểm neo/khoảng cách tương đối, rộng/cao, chiều mở, loại cửa, cao độ và danh sách CAD entities.

Nguồn dữ liệu có thẩm quyền lưu cùng metadata trong DWG bằng Extension Dictionary/Xrecord và cấu trúc cấp tài liệu thích hợp; SQLite chủ yếu phục vụ index, graph truy vấn nhanh, cache, snapshot và audit. **Mất SQLite cache không được làm mất danh tính các đối tượng nguồn đã đăng ký**. Cần thiết kế rõ giao dịch/phục hồi khi DWG và SQLite lệch revision.

Không dùng `ObjectId` phiên AutoCAD làm khóa vĩnh viễn; `Handle` chỉ có nghĩa khi đi cùng định danh DWG. Khi copy, paste, explode hoặc ghép bản vẽ phải xử lý trùng GUID có kiểm soát.

## 5. Thiết kế Palette chính

**Một lệnh mở bảng ổn định**, ví dụ `QAARCHOPEN`; tên thực tế chốt sau khi đối chiếu Manager để tránh trùng lệnh. Một instance Palette WPF duy nhất, có thể dock phải/trái, dark mode và hỗ trợ thu nhỏ.

| Mục giao diện | Chức năng |
|---|---|
| **Tổng quan** | Dự án/tầng, cây bản vẽ, số đối tượng, link, cảnh báo, thao tác nhanh |
| **Drawing Analyzer** | Quét DWG, vùng bản vẽ, layer/block inventory, nhận diện, duyệt phân loại |
| **Object Manager** | Danh sách Wall/Door; Property Inspector, Style, Register, Validate |
| **Link Manager** | Xem graph, trace nguồn/đích, impact, gỡ/sửa liên kết |
| **Sync Engine** | Danh sách thay đổi, Preview, Apply, Skip, Conflict, nhật ký |
| **Drawing Generator** | Chọn View/template, vùng đầu ra, sinh/cập nhật bản vẽ và bảng |
| **Quantity & QA** | Bảng khối lượng, bộ lọc, Export, kiểm tra GUID/link/đơn vị |
| **Dự án & Dữ liệu** | Project/Level/View, quy tắc thuộc tính, trạng thái, revision |
| **Cài đặt** | Cấu hình kho, mã nguồn tài nguyên, lựa chọn Layer/Block/Dim/Text/Hatch |

Palette không trực tiếp sửa Database từ luồng nền; các lệnh cần ngữ cảnh AutoCAD được dispatch qua cơ chế Manager đã kiểm chứng.

## 6. Lộ trình phát triển và điều kiện chuyển bước

### Gate 0 – Chuẩn bị & khảo sát thực tế

**Công việc**
- Đọc/đối chiếu `AGENTS.md`, `ADDIN-MANAGER-STANDARD.md`, `ADDIN-DEVELOPMENT-WORKFLOW.md`, `RESOURCE-IDENTIFIERS.md`.
- Kiểm tra source repo, `warehouse.local.json`, `Tools/catalog.json`, Manager contract/DLL được chọn, môi trường AutoCAD 2021.
- Kiểm kê **hai DWG mẫu** thành `DWG_INVENTORY_01/02`, `SHEET_CATALOG`, `ENTITY_CLASSIFICATION`, `DEPENDENCY_MATRIX`, `GAP_REPORT`.
- Xác thực đơn vị, nguồn dữ liệu thuộc tính, vùng Model Space, kiểu tường/cửa; chọn fixture và quyết định phạm vi MVP.
- Viết ADR về chiến lược GUID, quyền sở hữu thuộc tính, DWG/SQLite và hình sinh ra.

**Gate đạt:** có báo cáo có bằng chứng (layout/handle/layer/block/extent); không chỉnh sửa bản vẽ gốc; xác định được một mặt bằng và bộ Wall/Door mẫu.

### Bước 1 – Drawing Analyzer (P0)

- `ARC_SCAN`: inventory không ghi DWG.
- `ARC_SHEETS`: khoanh vùng View, tầng, tỷ lệ, tọa độ.
- `ARC_CLASSIFY`: đề xuất nhóm Wall/Door theo layer, block, hình học, topology.
- `ARC_REVIEW`: xác nhận/gộp/tách các gợi ý thủ công.
- `ARC_REPORT`: xuất `drawing_inventory.json`, `view_catalog.json`, `classification_candidates.json`.

**Gate đạt:** quét lặp lại cho kết quả ổn định; đánh dấu được tối thiểu một vùng mặt bằng và một tập tường/cửa; kết quả có evidence, độ tin cậy, phần chưa nhận diện; không đổi DWG.

### Bước 2 – Object Manager + Architectural Core (P0)

- Thiết kế `ObjectRegistry`, `IdentityService`, `PropertyStore`, `SchemaRegistry`, `GeometryAdapter`.
- Đăng ký Wall/Door từ thực thể CAD đang có, gắn GUID và xử lý clone/copy.
- `ARC_REGISTER_WALL`, `ARC_REGISTER_DOOR`, `ARC_PROPERTIES`, `ARC_VALIDATE`, `ARC_FIND_OBJECT`.
- Thiết lập quy tắc Door neo theo Wall; đọc/ghi Xrecord; migration tối thiểu.

**Gate đạt:** save/reopen vẫn nhận đúng GUID, dữ liệu, hình; copy/paste không tạo trùng danh tính âm thầm; không làm sai hình học khi register.

### Bước 3 – Link Manager (P0)

- Tạo graph quan hệ `HOSTED_BY`, `DERIVED_FROM`, `DISPLAYED_IN`, `REPRESENTED_BY`, `TAGGED_BY`, `COUNTED_IN`, `USES_STYLE`.
- Xây truy vấn `ARC_LINK`, `ARC_UNLINK`, `ARC_IMPACT`, `ARC_TRACE`, `ARC_REPAIR_LINK`.
- Phụ thuộc theo **thuộc tính**, không chỉ theo đối tượng.
- Phát hiện link gãy, vòng lặp, stale revision; có rebuild index.

**Gate đạt:** từ một Door truy đúng Wall chủ, các biểu diễn dự kiến và dòng bảng; mất biểu diễn được báo `BROKEN`, không tự sinh bừa.

### Bước 4 – Sync Engine (P0)

- Thu thập `ChangeSet` sau thao tác; event handler chỉ ghi nhận, không chỉnh CAD phức tạp ngay trong callback.
- Xác định `ChangedProperties`, tính ảnh hưởng, dựng `UpdatePlan`.
- Giao diện **Preview → Apply/Skip**; gắn trạng thái `UPDATED`, `STALE/OUTDATED`, `CONFLICT`, `BROKEN/MISSING_LINK`, `SYNC_FAILED`.
- Có `jobId`, `sourceRevision`, chống tái nhập/vòng lặp, kiểm tra revision trước khi ghi.
- Thực thi trong ngữ cảnh lệnh/DocumentLock/Transaction phù hợp; Undo/Redo và recover khi lỗi.

**Gate đạt:** Move/Stretch/Resize/Delete Door/Wall và Undo/Redo xác định đúng đối tượng bị ảnh hưởng; không ghi đè chỉnh tay, không lặp sync vô hạn.

### Bước 5 – Drawing Generator (P0)

- View renderer theo `ViewId`, `RendererVersion`, resource roles, rule/template.
- `PLAN_MASONRY`: tường, ô mở, hatch/kích thước theo cấu hình.
- `PLAN_DOOR`: block/ký hiệu cửa, mã, kích thước, vị trí định vị.
- `DOOR_SCHEDULE`, `WALL_SCHEDULE`: bảng dẫn xuất từ dữ liệu Core.
- Generator chỉ sở hữu entity đã đánh dấu managed; giữ nguyên đối tượng thủ công.
- Một DWG nhiều vùng View trước; cross-DWG phát triển sau.

**Gate đạt:** generate hai lần không trùng đối tượng; sửa cửa/tường chỉ cập nhật biểu diễn bị ảnh hưởng; bảng khớp dữ liệu Core.

### Bước 6 – Quantity & QA + hoàn thiện MVP

- Xuất CSV/JSON và bảng trong AutoCAD; nguồn số liệu truy ngược tới GUID, công thức, đơn vị và revision.
- Kiểm tra trùng GUID, thiếu host, missing links, sai units, view cũ, lỗi geometry.
- Bộ lọc theo tầng/loại, báo cáo thay đổi, lệnh validate/rebuild/repair.
- Hoàn thiện Dashboard, logs, xử lý lỗi, UAT và tài liệu hướng dẫn.

**Gate đạt:** tất cả kịch bản MVP được kiểm thử trên AutoCAD 2021; các báo cáo có nguồn truy vết; người triển khai dùng được qua Palette mà không cần gọi nhiều lệnh tay.

**Nhịp tham khảo:** Sprint 0 (Gate 0), Sprint 1–2 (Analyzer), Sprint 3–4 (Object Manager), Sprint 5 (Link), Sprint 6–7 (Sync), Sprint 8–9 (Generator + Quantity/QA), Sprint 10 (UAT). Đây là thứ tự quản lý, không phải cam kết tiến độ; cần hiệu chỉnh sau khảo sát code và DWG thật.

## 7. Tuân thủ CAD Tool Manager & thư viện

### 7.1. Giao tiếp và vòng đời

- Add-in chính tích hợp `IExtensionApplication` và `AICad.Contracts.ICadManagedAddin` **từ assembly contract thật**, không tự khai báo bản sao.
- `ManagedCommands` khai báo đủ các lệnh cần gọi qua Manager; **chỉ ghim lệnh mở Palette** trên Ribbon.
- Chỉ có **một PaletteSet GUID ổn định**; nhấn Ribbon nhiều lần không mở trùng.
- Không tự tạo Ribbon/tab/panel, Registry, Startup Suite hoặc tự NETLOAD ngoài Manager.
- Nạp add-in không tự chỉnh DWG. Khi đóng/Terminate dọn handler, Palette và tài nguyên thuộc phạm vi add-in.
- Kiểm tra active document trước mỗi thao tác; không giữ reference đã đóng, không sửa DB trên luồng nền.
- Không giả định có thể hot-reload assembly đã nạp trên AutoCAD 2021.

### 7.2. Liên kết thư viện CAD

- Mỗi vai trò `WallLayer`, `DoorSymbolBlock`, `DoorDimStyle`, `NoteTextStyle`, `WallHatch`... phải được người dùng chọn và lưu theo tài nguyên thư viện.
- Mã dùng chung: `LAY-`, `BLK-`, `DIM-`, `TXT-`, `HAT-`; định danh thực vẫn gắn với `SourceId + Kind + Id`.
- Không hardcode tên layer, block, hatch hay tự fallback âm thầm nếu tài nguyên mất.
- Trước khi vẽ, kiểm tra mã/ID, loại, phiên bản, đơn vị, scale, xung đột định nghĩa cùng tên.
- **Không giả định API chọn/lưu/tra cứu ngược tài nguyên đã tồn tại**: đối chiếu Manager và kho, ghi ticket tích hợp còn thiếu.
- Không thêm tab “Đang sử dụng kho” trong thư viện: cấu hình tài nguyên thuộc Palette của Add-in.

### 7.3. Quy trình build & bàn giao

- Source chính thuộc Git repo. Kiểm tra `warehouse.local.json` tại từng máy; không hardcode đường dẫn.
- Bản build tạm trong `Tools/dotnet/_Development/ArchitecturalSuite/working/<ma-phien>/`.
- Bản chốt: `Tools/dotnet/_Development/ArchitecturalSuite/versions/<version>/build-<fingerprint>/` gồm `package/`, `manifest.json`, `NOTES.md`, `validation/`, source commit hoặc patch.
- Không sửa tay `Tools/catalog.json`, `_Installed`, gói đã chốt; Manager chịu trách nhiệm nhập/cập nhật và giữ cấu hình Ribbon.
- Manifest ghi SHA-256 từng file, fingerprint, target, dependency, nguồn và kết quả kiểm thử.
- Xác minh gói **đang được chọn** từ catalog/hash, không suy từ tên version lớn nhất.
- Hai máy không cập nhật catalog cùng lúc; chờ đồng bộ cloud và khởi động lại AutoCAD khi cần.

## 8. Ma trận kiểm thử bắt buộc

| Ca kiểm thử | Điều kiện đạt |
|---|---|
| Quét DWG cũ | Không sửa dữ liệu gốc, inventory lặp lại ổn định |
| Register Wall từ hai Line | Một WallGuid đúng hình và chiều dày |
| Register Door & host | Door neo đúng WallGuid và vị trí |
| Copy/Paste/Explode | Không nhân bản GUID sai; báo lỗi mất liên kết |
| Door 900 → 1000 | Ô mở, dim, view, schedule cập nhật đúng |
| Dịch cửa trên tường | Cập nhật vị trí; không tái sinh chi tiết loại nếu không cần |
| Kéo dài/dịch tường | Cửa xử lý đúng anchor rule hoặc cảnh báo |
| Xóa Door | Graph và bảng nhất quán, không xóa ghi chú tay |
| Undo/Redo | Registry, revision, view, schedule khớp với DWG |
| Save/Close/Open | Phục hồi GUID/link và kiểm tra được cache |
| Đồng bộ lặp | Idempotent, không tạo Block/Tag/Dim trùng |
| Chỉnh tay bản vẽ con | Hiện conflict; không ghi đè trước khi duyệt |
| Apply khi còn conflict | Giữ nguyên phần chỉnh tay, chặn ghi đè phần xung đột; chỉ tái sinh khi người dùng đã duyệt |
| Chọn từ Palette hoặc CAD | Zoom/highlight và Inspector trỏ đúng đối tượng/View; không sửa DWG hay tự đăng ký nét chưa quản lý |
| Tra cứu sau xóa/chuyển DWG | Báo mất liên kết hoặc làm mới ngữ cảnh; không truy nhầm handle ở tài liệu khác |
| Kiểm tra sẵn sàng | Thiếu tầng/host/tài nguyên, link lỗi hoặc revision cũ được báo rõ và chặn thao tác phụ thuộc |
| Đi tới lỗi và kiểm tra lại | Dẫn đúng đối tượng/cấu hình; sửa lỗi xong kiểm tra lại trước Generate/Apply |
| Thiếu tài nguyên thư viện | Chặn bước phụ thuộc và báo rõ tài nguyên thiếu |
| Chuyển DWG đang mở | Không thao tác nhầm tài liệu |
| Ribbon/Palette | Một nút, một bảng; không trùng event khi mở/đóng |
| Lỗi giữa lúc cập nhật | Rollback hoặc trạng thái lỗi có thể repair |
| Export | Số liệu khớp Core, truy vết được nguồn và công thức |

Kiểm thử logic bằng unit tests **không thay thế** nghiệm thu AutoCAD GUI thật. Các fixture làm từ bản sao DWG mẫu đã được kiểm soát; không dùng bản vẽ công trình gốc để thử thao tác ghi.

## 9. Rủi ro và phương án xử lý

1. **DWG cũ không đủ thông tin:** phân loại có mức tin cậy, người dùng duyệt, không đoán tự động khi thiếu bằng chứng.
2. **Các bản vẽ nằm lệch vùng tọa độ Model Space:** View có `Bounds` và `Transform` riêng; xác thực trước khi generator chạy.
3. **Undo/Copy/Paste làm sai ID:** quản lý lifecycle, clone policy và rebuild index.
4. **Xung đột hình sinh với nét thủ công:** managed entity ownership, Preview/Apply, đánh dấu conflict.
5. **SQLite không cùng revision DWG:** DWG lưu dữ liệu nhận dạng cốt lõi; có cơ chế đối chiếu/rebuild và nhật ký.
6. **Resource catalog chưa có hợp đồng tích hợp đầy đủ:** thiết kế adapter, xác minh API thật trước khi viết phụ thuộc.
7. **Manager/AutoCAD khác version:** mục tiêu đầu tiên chỉ AutoCAD 2021/.NET Framework 4.8/x64; thêm adapter cho phiên bản khác sau.

## 10. Thứ tự công việc bắt đầu ngay

- [ ] Kiểm tra repo Git, Manager thật, `ICadManagedAddin`, catalog/hash và cấu trúc kho đang dùng.
- [ ] Kiểm kê hai DWG mẫu; xuất inventory có evidence.
- [ ] Chốt `View`, `Wall`, `Door`, `Representation`, `Link`, `ChangeSet` schema v1.
- [ ] Vẽ wireframe Palette dock phải/trái và mô tả thao tác UI từng màn hình.
- [ ] Tạo solution và project skeleton theo chuẩn, đăng ký duy nhất lệnh mở Palette.
- [ ] Triển khai Analyzer read-only và UI review để xác định mặt bằng nguồn.
- [ ] Triển khai đăng ký Wall/Door có GUID, lưu/mở lại.
- [ ] Triển khai chọn Palette ↔ CAD, zoom/highlight và Object Inspector theo đúng ngữ cảnh DWG.
- [ ] Triển khai kiểm tra sẵn sàng và điều hướng tới lỗi trước Generate/Apply.
- [ ] Triển khai graph, impact, deferred sync, 2 renderer và 2 schedule.
- [ ] Thực hiện kiểm thử tích hợp AutoCAD 2021, log, manifest, UAT và đóng gói theo Manager.

## 11. Định nghĩa hoàn thành MVP

MVP chỉ được coi là hoàn thành khi **một người triển khai kiến trúc** có thể lấy mặt bằng từ DWG cũ, xác nhận tường/cửa, sinh mặt bằng xây và định vị cửa cùng bảng thống kê, sửa nguồn, xem preview, đồng bộ, xuất dữ liệu và mở lại DWG mà không mất link/ghi chú thủ công. Add-in phải được Manager nạp bằng **một nút Ribbon**, có bằng chứng kiểm thử và gói bàn giao có thể tái tạo.

---

### Nguồn quy định

- `PLAN_Autocad_v0.2.md`: https://docs.google.com/document/d/1NPOGKuhzWQU1wjS8c4B1J5o5M8lwtj22/edit
- `AGENTS.md`: https://docs.google.com/document/d/1AdF56_CxOLuHhMpwJsi0QIiFfxDcbhvv/edit
- `ADDIN-MANAGER-STANDARD.md`: https://docs.google.com/document/d/1uk6bFg38APT3Xn_g8F8fjnXrbAghgBWa/edit
- `ADDIN-DEVELOPMENT-WORKFLOW.md`: https://docs.google.com/document/d/1Le8-_1Pbis2udpk66d4hARCsoG3KG4Qu/edit
- `RESOURCE-IDENTIFIERS.md`: https://docs.google.com/document/d/1BFWLnCCkH5rtG-e3JoB63buMgicFoasI/edit
