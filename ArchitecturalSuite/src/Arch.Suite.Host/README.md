# Architectural Suite 0.6.0

AutoCAD 2021 x64 / .NET Framework 4.8. Giữ một nút QAARCHOPEN, tool ID b40fc389e1784687b8e85403c213c024 và Palette GUID hiện có. Bản 0.6.0 sửa đồng bộ tầng, bổ sung xuất/nạp bộ định nghĩa, hỗ trợ Spline khi sinh bản vẽ và sửa điều hướng tới nguồn. Giữ luồng vẽ nguồn bằng add-in và đồng bộ hình đích từ nguồn.

## Điều kiện chạy và quy tắc kho mới

Cần Manager 0.10.0 cung cấp Rainmaker.Library.Reader V1. Project tham chiếu Reader với Private=false; không nhập Reader thành add-in, không kèm Reader riêng vào package của Arch. Arch đọc danh mục/ID/asset qua API V1, phân giải lại và kiểm tra snapshot trước khi áp dụng vào CAD. Không cần QA/HTTP Host đang mở để đọc kho.

Bộ chọn thư viện lưu lựa chọn hiện hành tại Catalog/addin-settings/b40fc389e1784687b8e85403c213c024.json, gồm SchemaVersion, Revision, SourceId và Bindings theo SourceId/Kind/Id. Đọc lại mỗi lần mở bộ chọn; kiểm tra revision, khóa ghi và ghi nguyên tử, giữ trường không liên quan. Lựa chọn hiện có trong DWG được ưu tiên; mặc định kho dùng cho vai trò chưa có lựa chọn trong DWG. ID không còn khả dụng không được phục hồi từ cache.

Theo Reader V1 mới, khi gặp LIBRARY_PENDING/CATALOG_CHANGED hoặc lỗi toàn vẹn, dừng chọn/áp dụng và báo mã lỗi để xử lý tại QA/nạp lại. Không còn đọc trực tiếp file danh mục hoặc cho dùng snapshot cũ để vượt giao dịch chờ.

## Luồng làm việc

1. Vẽ bản nguồn trong Model; dùng block khung có đúng một Polyline kín cạnh thẳng làm biên quét. Mở Arch từ Manager, Chọn khung mẫu.
2. Chọn tag tên, **tag mã bản vẽ riêng**, layer biên và tên tầng. Setup ATT chỉ quyết định cột/thứ tự danh mục; không quyết định tag mã. Lưu profile để quét chọn nhiều khung cùng mẫu, sửa tầng từng dòng hoặc áp tầng hàng loạt rồi lưu tập hợp lệ.
3. Tầng của khung và tab Tầng dùng cùng danh mục. Chọn khung sẽ chọn dòng tầng tương ứng; tên tầng mới được thêm với trạng thái chờ khai báo chiều cao. Nhập cao độ sàn, cao tầng và cao dầm (mm), Lưu tầng. Cao xây = cao tầng − cao dầm; chưa khai báo chiều cao thì không dùng để tính tường. Tên không trùng; cao dầm từ 0 và nhỏ hơn cao tầng. Đổi tên giữ ID và cập nhật khung/đăng ký; đổi chiều cao đánh dấu tường và bản liên kết cần cập nhật, không âm thầm sửa hình.
4. Tab Định nghĩa: chọn module riêng trong cây Tường / Cửa → Cửa đi, Cửa sổ / Trục / Nội thất / Thiết bị vệ sinh. Bấm Lưu định nghĩa theo ngữ cảnh.
5. Chọn khung nguồn đã lưu, Quét trong khung và Đăng ký các nhóm hợp lệ; hoặc đăng ký riêng tường/cửa. Đối tượng chưa đủ quy tắc, hở/cắt biên hoặc chồng nhóm được báo, không đăng ký bằng phỏng đoán.
6. Tab Đã đăng ký: lọc loại, tầng và khung; xem vị trí nguồn, kích thước/bậu, trạng thái; định vị hoặc gỡ đăng ký giữ hình. Với tường có dầm khác mặc định, nhập Dầm riêng rồi Lưu dầm tường; để trống để kế thừa tầng.
7. Tab Bản vẽ liên kết: nhập tên/mã đích, chọn nhóm, Xem trước, Sinh bản vẽ, duyệt Apply trên dòng lệnh và chỉ vị trí chèn khung mới. Chỉ sao chép nội dung đã đăng ký thuộc các nhóm chọn; không sao chép đối tượng chưa đăng ký. Chặn vị trí khung mới chồng phạm vi khung đã đăng ký theo bao hình thận trọng.
8. Sửa thiết kế tại nguồn. Chọn bản đích trong danh sách, xem trước và Đồng bộ/lưu lựa chọn, duyệt Apply hoặc Skip. Đối tượng mới cần quét/đăng ký trước khi thuộc nhóm liên kết; xóa đối tượng nguồn được đưa vào thay đổi cần duyệt khi đồng bộ.
9. Di chuyển toàn khung qua công cụ Arch. Bỏ một nhóm rồi đồng bộ sẽ ngắt nhóm đó, giữ hình độc lập; Ngắt toàn bộ giữ toàn bộ hình. Muốn liên kết lại nhóm đã ngắt, sinh khung mới để tránh chồng với hình độc lập đã giữ.
10. Save DWG trước khi đóng/chuyển máy. Không đếm hình biểu diễn ở đích thành cấu kiện nguồn.

## Quy tắc định nghĩa

- Tường là Polyline kín cạnh thẳng trên layer cấu hình, giữ đủ biên gốc. Chiều cao lấy từ tầng trừ dầm mặc định hoặc dầm riêng của tường. Chưa phân đoạn vùng nhiều nhánh hay xử lý ô chờ.
- Cửa: chọn block dynamic mẫu, property khoảng cách cho rộng bộ cửa; cao từ một nguồn: property, ATT số mm hoặc bảng loại. Nhập tag mã và mã loại nếu một block dùng cho nhiều loại. Nhập bậu mm so với sàn, đặc biệt với cửa sổ. Property quy đổi theo scale đều XY và INSUNITS mm/cm/m; ATT/bảng loại/bậu dùng mm.
- Nội thất, thiết bị vệ sinh và trục hiện nhận theo nhiều layer (ngăn bằng dấu chấm phẩy). Block chỉ xét layer chèn ở Model; không xét nét bên trong. Đối tượng rời xét layer chính nó. Đây là nhóm quản lý, không đóng nguồn thành một block chung.
- Cửa đi/cửa sổ chuyên biệt không đưa vào nội thất. Thiết bị vệ sinh ưu tiên hơn nội thất. Chồng với nhóm khác chưa có ưu tiên được báo cần xác nhận. Trục hiện là nhóm layer; chưa có quy tắc trục chuyên sâu hoặc sinh mới hệ trục.

## Bản vẽ đích và bảo vệ liên kết

Khung đích có ID riêng, bản định nghĩa khung riêng, tên/mã riêng; thông tin tên/mã có thể sửa trên ATT của khung đích. Ô tên/mã ở tab liên kết dùng khi Sinh mới. Nội dung được chụp thành hình biểu diễn tách khỏi định nghĩa block nguồn dùng chung, giữ đúng trạng thái đã duyệt cho đến lần đồng bộ tiếp theo.

Nội dung đích nằm trên layer quản lý khóa, block không cho explode. Khi Arch được Manager nạp, theo dõi lệnh CAD và phục hồi sửa hình đích theo bản đã duyệt; hoạt động khi Palette đóng. Bản lưu hình nằm trong DWG, có hard pointer để PURGE không xóa nhầm. Ghi chú/hình thủ công ngoài phần quản lý không nằm trong thao tác tái tạo nhóm.

Nếu có bản sao native dùng chung định nghĩa của nội dung liên kết ngoài quản lý, Arch cảnh báo trong danh sách, giữ hình và dừng đồng bộ bản liên quan để người dùng xử lý. Không tự xóa bản sao. Ngắt liên kết giữ hình; các nhóm không còn liên kết được trả về layer không khóa và ngừng nhận cập nhật.

Hỗ trợ sao chép Line, Polyline, Arc, Circle, Ellipse, Solid, Text/MText, hatch, Spline (giữ dữ liệu NURBS, control points, knots và weights) và hình block lồng đã đánh giá; dimension được tách thành hình thể hiện. Entity chưa hỗ trợ được chặn ở bước kiểm tra, không tạo thiếu âm thầm. Chưa giữ liên kết kích thước native ở bản đích hoặc hỗ trợ đầy đủ đối tượng chuyên ngành/3D.

## Lưu và nâng cấp

Khung, profile, tầng, định nghĩa, đăng ký, bản vẽ và quan hệ nhóm nằm trong ARCH_SUITE_FRAMES_V1 schema 3. Cache và hình biểu diễn cũng nằm trong DWG; chuyển toàn bộ DWG đã lưu sang máy có Arch tương thích vẫn dùng tiếp được. COPYCLIP/WBLOCK một phần chưa có quy trình chuyển graph liên kết.

Đọc được dữ liệu khung schema 1 và 2. Khi tên tầng cũ chưa có trong danh mục, tạo mục chờ khai báo chiều cao với ID ổn định giữa các lần đọc. Các định nghĩa cửa cũ yêu cầu xác nhận lại Cửa đi/Cửa sổ; giữ ánh xạ cũ để chọn lại và lưu. Tầng cũ chỉ có tên cần bổ sung cao tầng/dầm. DWG đã ghi schema 3 cần Arch 0.6.0 hoặc phiên bản tương thích; không dùng bản cũ để ghi lại dữ liệu mới.

Nhóm/tab dữ liệu 0.2 đã bỏ khỏi giao diện. Dữ liệu ARCH_SUITE_V1 không tự xóa hoặc chuyển đổi; tài nguyên đã lưu theo luồng trước được giữ khi mở. Cấu kiện nguồn/đăng ký mới không nhập vào mô hình hai Line cũ.

## Xuất và nạp bộ định nghĩa

Lưu định nghĩa trong DWG trước, vào tab Định nghĩa → Xuất cấu hình, chọn nhóm rồi lưu JSON. Có thể xuất tường, cửa đi/cửa sổ, nhóm layer, profile khung và khóa tài nguyên kho. Tầng là tùy chọn riêng, mặc định không xuất. Bộ JSON không chứa đối tượng đăng ký, handle, ID công trình, tọa độ hoặc graph liên kết; không thay thế việc gửi DWG đã lưu.

Ở DWG tiếp theo, bấm Nạp cấu hình và duyệt bảng từng mục. Mục trùng mặc định giữ hiện có; tích mục cần thay. Mục thiếu layer/block/property được báo để kiểm tra; nếu nạp quy tắc thì vẫn phải bổ sung tài nguyên trước khi đăng ký. Mục lỗi hoặc khóa kho không còn hợp lệ không được áp dụng. Chỉ các mục đã tích được lưu vào DWG; không thay đổi cấu hình mặc định chung của kho. Khóa tài nguyên đã nạp được giữ trong DWG, chọn lại tại Thư viện rồi Áp dụng để Reader xác thực và nhập asset.

## Lỗi sinh bản vẽ và định vị

Bước chuẩn bị hình, tách block và sao chép dùng thực thể đã thuộc Database, tránh gọi API CAD phụ thuộc Database trên clone rời. Khi một nguồn không được hỗ trợ, thông báo có giai đoạn, loại, handle, layer, đường dẫn block lồng và vị trí gần đúng; bấm Đi tới nguồn báo lỗi để kiểm tra. Lỗi đầy đủ ghi tại `%LOCALAPPDATA%/ArchitecturalSuite/logs`. Nếu thao tác thất bại, giao dịch bị hủy thay vì giữ một bản vẽ sinh dở.

Đi tới mục đã chọn zoom/highlight đúng đối tượng trong Model và giữ hướng nhìn/twist hiện hành, kể cả hướng isometric trực giao. Không tự đổi từ Layout sang Model; góc nhìn phối cảnh được báo chưa hỗ trợ. Với lỗi eNotInDatabase trên DWG người dùng đã báo, cần thử lại trên bản sao DWG đó; kiểm thử hiện tại dùng fixture tổng hợp, không khẳng định đã tái hiện mọi dữ liệu công trình.

## Phạm vi còn lại

Chưa tự ngắt/nối tường, host/điểm neo/flip và kích thước ô chờ, trừ khối lượng, lanh tô, sinh dim/mã/hatch mới theo mẫu triển khai hoặc sàn. Cần nghiệm thu thao tác chuột toàn luồng, BEDIT/REFEDIT/Undo/Redo đặc thù và dock/DPI trên GUI thực; chưa thử chuyển máy vật lý thứ hai. Cơ chế bảo vệ cần Arch được nạp, không phải mã hóa/chống sửa DWG bằng công cụ bên ngoài.

## Build và cập nhật

Build Release net48 x64; ManagerRepo trỏ repo AI-CAD-MCP có Reader V1 và các DLL Manager. Không đóng gói DLL Autodesk, Manager hoặc Reader. Chọn DLL trong package của build đã niêm phong bằng Cập nhật file trên đúng mục Architectural Suite trong Manager rồi mở lại CAD khi được yêu cầu. Không sửa catalog hay _Installed bằng tay.
