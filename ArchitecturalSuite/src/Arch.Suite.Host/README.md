# Architectural Suite 0.4.0 — Profile khung và định nghĩa tường / cửa

Bản cập nhật quản lý khung, đăng ký vùng tường và bộ cửa dynamic. AutoCAD 2021 x64, .NET Framework 4.8. CAD Tool Manager giữ một nút QAARCHOPEN và Palette GUID hiện có.

## Luồng mới

1. Mở Model của DWG làm việc, mở Architectural Suite từ Manager.
2. Chọn **Chọn khung mẫu**, chỉ một block khung trực tiếp trong Model.
3. Trong bảng trái, nhập tầng, chọn tag tên bản vẽ và layer biên. Block cần có đúng một polyline kín cạnh thẳng trên layer đó. Chấp nhận khung quay và scale đều dương trên mặt phẳng XY.
4. Tab **Setup ATT** bên phải cho chọn các tag làm cột, đặt tên cột và dùng **Đưa cột lên/xuống** trong nhóm Danh mục bản vẽ bên trái. Tên cột phải riêng biệt và khác cột Tầng có sẵn.
5. **Kiểm tra biên** hoặc **Quét trong khung** để xem trước. Các thao tác này chỉ đọc. Layer vùng tường nhập ở bảng trái, mặc định QA_KT_Tuong. Quét liệt kê thực thể trực tiếp trong Model, tách các nét trong định nghĩa block khỏi vùng tường.
6. Danh sách có handle, layer, trạng thái và lý do. Polyline tường hở, vùng cần duyệt và bao hình chạm/cắt biên được báo. Chọn dòng, bấm **Đi tới mục đang chọn** để định vị trong CAD. Double-click dòng cũng định vị.
7. **Lưu khung / setup** ghi cấu hình vào DWG. Nhấn Save trong AutoCAD trước khi đóng hoặc gửi file.
8. Có thể chọn và lưu thêm khung. Tab **Danh mục** đọc các cột theo thứ tự đã lưu. **Xuất danh mục CSV** xuất cấu hình đã lưu, giữ nguyên file nếu tên đã tồn tại.
9. Preview hiện biên xanh, hình hiện trạng cam và đối tượng cắt biên vàng. Lăn chuột zoom tại con trỏ, giữ chuột giữa để pan và bấm **Vừa khung** để đặt lại góc nhìn.

Bảng trái mặc định 220 DIP, co được đến 200 DIP và kéo rộng khi cần. Chiều rộng được lưu riêng trên máy. Hàng dữ liệu 27 DIP, ô nhập/nút thông thường khoảng 28 DIP. Preview chiếm phần lớn vùng phải; bảng kết quả phía dưới có thanh cuộn.

## Quét chọn nhiều khung

Chọn khung mẫu, nhập tầng, tag tên, layer biên và setup ATT; bấm **Lưu profile khung** hoặc **Lưu khung / setup**. **Quét chọn khung** cho quét vùng/chọn nhiều block trực tiếp trong Model. App giữ các block cùng tên gốc của mẫu, kiểm tra từng khung và hiện bản nháp. Sửa tầng từng dòng; hoặc chọn một dòng, nhập tầng bên trái rồi **Áp tầng cho tập quét**. Bấm **Lưu các khung hợp lệ**. Khung lỗi được giữ riêng để sửa; đăng ký lại khung cũ giữ ID. Mỗi DWG lưu một profile đang dùng; các khung đã đăng ký giữ setup riêng.

## Duyệt định nghĩa tường và cửa

1. Trong **Định nghĩa tường / cửa**, kiểm tra layer vùng tường bên trái và chiều cao mm. Giá trị ban đầu 3300 chỉ là gợi ý; phải bấm **Lưu định nghĩa** ở footer để xác nhận trước khi đăng ký.
2. **Chọn block cửa mẫu trên CAD**. Chọn property chiều rộng đang thể hiện rộng bộ cửa; app chỉ đưa property kiểu khoảng cách có giá trị dương vào danh sách. Chọn đúng tên dù block có nhiều khoảng cách.
3. Chiều cao lấy từ đúng một nguồn: dynamic property khoảng cách; hoặc tag ATT số mm; hoặc chiều cao mm theo loại block. Nếu dùng property thì để ATT trống. Chọn tag mã cửa nếu có.
4. **Lưu định nghĩa** ghi tường và loại cửa đang chọn trong DWG. Bảng bên dưới liệt kê các loại đã lưu. Chọn lại một block mẫu cùng loại để đọc/sửa cấu hình đã lưu.
5. Quay lại **Phạm vi / đối tượng**, chọn khung đã lưu, bấm **Đăng ký tường hợp lệ** và **Đăng ký cửa đã định nghĩa**. Chỉ quét thực thể trực tiếp trong Model nằm trọn khung. Nét bên trong block cửa không đăng ký thành tường. Đường hở/cắt biên báo lý do để xử lý.
6. Tab **Đã đăng ký** chứa ID liên kết với handle/khung, chiều cao, rộng bộ cửa, diện tích vùng tường theo mm² và trạng thái. Diện tích vùng là diện tích mặt bằng, chưa phải diện tích xây hay khối lượng. Có định vị CAD và gỡ đăng ký giữ hình.
7. Save DWG trước khi đóng/chuyển máy. Khi hình hoặc định nghĩa thay đổi, đăng ký lại để cập nhật số liệu và giữ ID. Chưa tự cập nhật số liệu đăng ký theo chỉnh sửa CAD; không dùng bảng này làm khối lượng nghiệm thu.

Tường được nhận bằng Polyline kín cạnh thẳng trên layer cấu hình, giữ đủ tọa độ biên gốc; chiều cao hiện áp dụng chung cho lần đăng ký. Chưa hỗ trợ lỗ trong vùng, biên cong hay gán chiều cao riêng từng đoạn. Cửa cần block dynamic trên XY, scale đều trong mặt bằng; kích thước property quy đổi theo INSUNITS mm/cm/m và scale block. ATT cao/bảng loại dùng mm. Chưa khai báo neo, flip, bậu và kích thước ô chờ trong bản này.

## Thư viện khi có giao dịch chờ

Tab Thư viện hiển thị loại thao tác QA và phase; **Đọc trạng thái kho** cập nhật thông tin. Bộ chọn vẫn mở bản tài nguyên đã công bố để xem, nhưng vô hiệu hóa Áp dụng khi có giao dịch chờ và kiểm tra lại ngay trước khi ghi. Architectural Suite không tự xóa journal hoặc xác nhận kết quả QA.

Lỗi **DWG đã lưu chưa đúng nội dung thao tác chờ** vẫn cần được xử lý trong QA bằng đối chiếu DWG nguồn và giao dịch gộp hatch. Bản 0.4.0 sửa phản hồi/bộ chọn phía Arch, chưa thay thuật toán khôi phục đồng bộ của QA.

## Lưu và chuyển DWG

Khung, tầng, ATT/cột, profile, định nghĩa tường/cửa và danh sách đăng ký nằm trong Xrecord ARCH_SUITE_FRAMES_V1 của DWG. Mô hình 0.2 vẫn nằm ở ARCH_SUITE_V1 và được giữ nguyên. Mở toàn bộ DWG đã Save trên phiên/máy có Suite tương thích sẽ đọc lại dữ liệu. Bộ tài nguyên đã áp dụng theo luồng 0.2 cũng lưu trong DWG; kho của máy nhận phải có tài nguyên phù hợp nếu tạo/cập nhật nội dung cần thư viện. Copy một phần bằng WBLOCK/COPYCLIP cần ánh xạ riêng ở bước sau.

## Phạm vi bản này và bước tiếp

Bản này bổ sung profile khung và đăng ký vùng tường/cửa theo khung. Chưa có quan hệ ô chờ, chia vùng tường nhiều nhánh thành đoạn, tự ngắt/nối tường, khối lượng trừ ô chờ hoặc sinh/đồng bộ các khung đích từ mô hình mới. Các chức năng 0.2 vẫn dùng mô hình cũ riêng.

Quét theo bao hình là phương án thận trọng: đối tượng quay hoặc nét cong sát biên có thể được đưa vào danh sách xác nhận. Preview vẽ nét thẳng đúng tọa độ; các hình cong/block khác dùng bao hình để định vị. Chưa phân loại trục/cos tự động khi chưa có quy tắc.

Tab **Thư viện** hiển thị các tài nguyên đã lưu và mở bộ chọn 0.2 với theme thống nhất. Profile khung lưu trong DWG; cấu hình mặc định dùng chung theo Manager ID chưa mở rộng trong đợt này. Không tự thay lựa chọn của DWG bằng mặc định máy/kho.

Nhóm **Công cụ dữ liệu 0.2** thu gọn giữ các thao tác đang có cho mô hình cũ. Các chức năng đó vẫn dùng vùng nguồn và đăng ký hai Line riêng. Kết quả quét khung mới chưa nhập vào mô hình này.

## Nạp bản cập nhật

Dùng Cập nhật file trên đúng mục Architectural Suite trong Manager, chọn Arch.Suite.Host.dll của gói đã bàn giao rồi mở lại AutoCAD khi Manager yêu cầu. Giữ nguyên tool ID, lệnh QAARCHOPEN và Palette GUID. Bản ứng viên chưa được chọn trong catalog cho đến khi thực hiện cập nhật qua Manager.

## Hướng dẫn luồng 0.2 giữ tương thích

# Architectural Suite 0.2.0

Add-in cho AutoCAD 2021 x64, .NET Framework 4.8. CAD Tool Manager ghim `QAARCHOPEN` để mở bảng. Các thao tác từ Palette chạy trong ngữ cảnh lệnh AutoCAD của tài liệu đang chọn.

## Thử một lượt trên bản sao DWG

1. Mở Model của DWG đơn vị mm. Chọn **Chọn mặt bằng nguồn**, chỉ hai góc, nhập tên và tầng. **Kiểm kê · JSON** xuất thống kê toàn bản vẽ.
2. Chọn **Đăng ký tường** và chỉ hai Line thẳng song song cùng cao độ, có đầu tương ứng khớp trong 1 mm. Nhập mã riêng và chiều cao. Có thể dùng **Duyệt gợi ý tường / cửa** để tìm trong vùng và xác nhận từng ứng viên.
3. Chọn **Đăng ký cửa**, chỉ block cửa rồi một cạnh tường chủ đã đăng ký. Điểm chèn block xác định tâm ô mở dọc tường. Nhập rộng, cao, mã, cao độ bậu và hướng ký hiệu. Tra cứu cấu kiện qua danh sách hoặc chọn hình nguồn trong CAD.
4. Trong **Bộ tài nguyên CAD**, chọn hai layer của kho. Có thể gán block ký hiệu cửa, dim định vị, chữ mã cửa và hatch tường. Bộ tài nguyên lưu cùng DWG theo mã, ID, phiên bản và hash nguồn. Layer trùng tên nhưng khác thuộc tính sẽ được báo để xử lý. Block ký hiệu được đặt tại tâm theo đơn vị của định nghĩa và hướng đã chọn.
5. Chọn **Đặt hai mặt bằng**, lần lượt chỉ điểm chuẩn của mặt bằng nguồn, điểm tương ứng ở mặt bằng xây, điểm tương ứng ở mặt bằng định vị. Chọn các vùng trống. Nhấn **Xem trước** rồi **Duyệt cập nhật**, chọn **Apply** ở dòng lệnh để ghi.
6. Bấm **Thống kê tường** và **Thống kê cửa** để đặt từng bảng vào DWG. Diện tích ròng tính trên một mặt tường: dài × cao trừ tổng rộng × cao của các ô cửa. CSV có đơn vị và GUID nguồn.
7. Dùng **Sửa kích thước** đổi rộng cửa, xem trước rồi duyệt. Kiểm tra ô mở và bảng. Khi xóa cấu kiện nguồn, chọn nó trong danh sách rồi **Gỡ đăng ký**; gỡ cửa trước tường chủ. Sau đó xem trước phần cần thu hồi. Với nét hoặc bảng bị chỉnh tay, xem handle xung đột trước khi quyết định tái sinh.
8. Thử Undo, Redo, lưu và mở lại. **Báo cáo mô hình** xuất vùng, cấu kiện, quan hệ, ứng viên, cấu hình và revision. **Dựng lại chỉ mục** tạo SQLite trong vùng dữ liệu người dùng của Windows từ dữ liệu đã lưu trong DWG.

## Hướng dẫn từng tính năng trong Palette

Khi bấm một nút, đọc dòng lệnh ở đáy cửa sổ AutoCAD. Nếu dòng lệnh yêu cầu chọn điểm hoặc đối tượng, hãy bấm trên vùng vẽ. Nếu yêu cầu mã hoặc kích thước, nhập giá trị rồi nhấn Enter. Kết quả và lỗi nằm ở cuối Palette. Nhấn Esc để hủy thao tác đang nhập. Khi đổi DWG, kiểm tra tên bản vẽ ở đầu Palette.

### Bản vẽ và đăng ký cấu kiện

| Nút | Cách dùng và kết quả |
|---|---|
| **Chọn mặt bằng nguồn** | Trong Model, chỉ hai góc đối diện bao quanh đúng một mặt bằng. Nhập tên mặt bằng và tầng, ví dụ T1. Add-in lưu hình chữ nhật này làm phạm vi tìm và đăng ký. Chọn kỹ trước khi đăng ký tường đầu tiên vì vùng bị khóa khi đã có cấu kiện. |
| **Kiểm kê · JSON** | Chọn nơi lưu báo cáo. Add-in đếm các thực thể trực tiếp trong Model và Layout của toàn DWG, hiện tổng số và chi tiết theo loại. Khi trùng tên, báo cáo mới có hậu tố (2), (3)… Bước này tùy chọn. Bạn có thể chuyển thẳng từ chọn mặt bằng sang đăng ký tường. |
| **Đăng ký tường** | Chỉ lần lượt hai Line biểu diễn hai mặt của cùng một tường. Hai Line phải song song, cùng cao độ, hai đầu tương ứng khớp trong 1 mm. Nhập mã, ví dụ W-01, và chiều cao. Add-in lấy tim nằm giữa hai Line, tính chiều dài và bề dày, rồi thêm tường vào danh sách. Chiều của Line đầu tiên quyết định đầu tường để tính khoảng neo cửa. |
| **Đăng ký cửa** | Chọn một block cửa rồi chọn một trong hai Line của tường đã đăng ký. Nhập chiều rộng, mã cửa, chiều cao, cao độ bậu và hướng ký hiệu. Add-in chiếu điểm chèn block lên tim tường và dùng vị trí đó làm tâm ô mở. Nếu block đặt gốc tại bản lề, kiểm tra và chỉnh khoảng neo trước khi sinh mặt bằng. |
| **Duyệt gợi ý tường / cửa → Tìm trong mặt bằng** | Mở mục gập, tìm trong vùng nguồn đã chọn. Ví dụ, hai Line cùng layer, dài ít nhất 200 mm, cách nhau 50–500 mm và khớp hai đầu có thể thành một ứng viên tường. Block có tên gợi ý cửa có thể thành ứng viên cửa. Chọn một dòng để đọc lý do gợi ý. |
| **Đăng ký mục chọn** | Sau khi kiểm tra ứng viên, chọn nút này và nhập thông số như khi đăng ký thủ công. Với cửa, vẫn phải chỉ tường chủ. Chỉ những mục đã đăng ký mới được sinh hình và thống kê. |
| **Tìm theo mã / tầng** | Gõ một phần tên như W- hoặc T1 để lọc danh sách. Bấm một dòng để xem mã, tầng, GUID và chiều cao. Khi chọn đúng một thực thể nguồn đã đăng ký trong CAD, Palette chọn dòng tương ứng. |
| **Định vị cấu kiện đã chọn** | Chọn một dòng rồi bấm nút trong Model. AutoCAD zoom tới hình nguồn và chọn các thực thể của cấu kiện. Dùng để kiểm tra W-01 có trỏ đúng hai cạnh tường hay không. |
| **Tra quan hệ** | Chỉ một nét tường hoặc block cửa nguồn. Với tường, đọc danh sách mã cửa thuộc tường. Với cửa, đọc mã tường chủ, chiều rộng và khoảng neo theo mm. |
| **Sửa kích thước** | Chỉ cấu kiện nguồn. Với tường, chỉnh chiều cao; chiều dài và bề dày lấy từ hai Line. Với cửa, chỉnh cao, rộng, khoảng cách từ đầu tường tới tâm cửa, bậu và hướng. Sau đó chạy Xem trước để biết hình và bảng nào cần cập nhật. |
| **Gỡ đăng ký** | Chọn dòng trong danh sách, bấm nút, nhập Remove để xác nhận. Nét/block nguồn được giữ lại. Gỡ cửa trước khi gỡ tường chủ. Chạy Xem trước và Duyệt cập nhật để thu hồi hình dẫn xuất. Cũng dùng cách này khi hình nguồn đã bị xóa và chỉ còn dòng đăng ký. |

### Sinh bản vẽ và duyệt thay đổi

| Nút | Cách dùng và kết quả |
|---|---|
| **Bộ tài nguyên CAD** | Chọn layer cho nét tường/đường chuẩn và layer cho định vị cửa từ kho ISO_QUANGANH. Chọn thêm block ký hiệu cửa, dim định vị, chữ mã cửa và hatch tường nếu cần thể hiện chúng. Nhấn **Áp dụng bộ layer**. Khi có lỗi, xem mã tài nguyên hoặc layer được báo; kiểm tra layer khóa, tắt, đóng băng hoặc khác thuộc tính dù trùng tên. |
| **Đặt hai mặt bằng** | Chỉ ba điểm: một mốc dễ nhận trên bản nguồn, vị trí muốn đặt chính mốc đó ở mặt bằng xây, rồi vị trí tương ứng ở mặt bằng cửa. Hai điểm sau quyết định độ dời của toàn bộ hình. Nên bắt cùng quy ước mốc, ví dụ giao trục A–1. Chọn hai vùng trống đủ rộng. |
| **Kiểm tra dữ liệu** | Đọc lại hình nguồn và kiểm tra trước khi sinh: các Line tường còn hợp lệ, block cửa còn tồn tại, ô cửa nằm trong chiều dài tường, các ô mở không chồng nhau, đỉnh cửa không vượt đỉnh tường. Nếu có lỗi, sửa cấu kiện được báo rồi chạy lại. |
| **Xem trước** | Đọc số nét sẽ tạo, sửa, thu hồi, số ký hiệu/hatch/dim, số bảng cần cập nhật và xung đột. Đây là bản tóm tắt bằng số ở Palette. Nếu số lượng bất ngờ, tra lại cấu kiện và vị trí hai mặt bằng trước khi duyệt. |
| **Duyệt cập nhật** | Add-in tính lại tác động. Khi dòng lệnh hỏi Apply/Skip, nhập Apply rồi Enter để ghi, hoặc Skip để hoãn. Enter mặc định chọn Skip. Lần đầu tạo hai mặt bằng; những lần sau chỉ cập nhật phần liên quan. Khi mọi thứ đã khớp, số thay đổi bằng 0. |
| **Xử lý xung đột** | Khi hình đã sinh bị kéo, sửa hoặc xóa bằng lệnh CAD, xem handle được báo. Muốn giữ chỉnh sửa để xem xét, dừng ở bước xem trước. Muốn tái sinh theo nguồn, nhập đúng handle, duyệt Restore, sau đó Xem trước và Duyệt cập nhật. Nếu bảng đã bị xóa, dùng nút thống kê tương ứng để đặt lại bảng sau khi xử lý liên kết. |

### Bảng số liệu, báo cáo và lịch sử

| Nút | Cách dùng và kết quả |
|---|---|
| **Thống kê tường** | Bấm rồi chỉ điểm đặt bảng trong DWG. Mỗi dòng gồm mã, tầng, dài, dày, cao và diện tích ròng một mặt. Ví dụ tường 5.000 × 3.200 mm có một cửa 900 × 2.200 mm cho diện tích 16 − 1,98 = **14,02 m²**. Bấm lại để kiểm tra/cập nhật bảng hiện có. |
| **Thống kê cửa** | Chỉ điểm đặt bảng cửa. Mỗi dòng gồm mã cửa, tầng, mã tường chủ, rộng, cao và khoảng neo. Đối chiếu cột neo khi ký hiệu cửa bị lệch dọc tường. |
| **Xuất bảng CSV** | Chọn tên file mới để mở trong Excel. Mỗi dòng ứng với một tường hoặc cửa, kèm GUID nguồn. Kích thước dùng mm, diện tích tường dùng m². |
| **Báo cáo mô hình** | Chọn tên JSON mới. File chứa vùng nguồn, các cấu kiện đã đăng ký, ứng viên, quan hệ cửa–tường, tài nguyên và revision. Dùng để rà dữ liệu hoặc gửi kèm khi báo lỗi. |
| **Nhật ký thay đổi** | Đọc revision hiện tại và tối đa 100 mục gần nhất mà add-in đã ghi vào mô hình DWG. Dùng để xem lần đăng ký hoặc cập nhật gần nhất. |
| **Dựng lại chỉ mục** | Tạo lại file SQLite từ mô hình trong DWG và hiện đường dẫn ở Palette. Dùng khi cần một chỉ mục tra cứu mới. Việc lưu đối tượng và liên kết qua các phiên dựa vào thao tác Save DWG. |

## Phạm vi

Phiên bản này dùng một mặt bằng nguồn và hai vùng đầu ra trong Model Space của một DWG. Quy tắc nhận diện tự động cung cấp gợi ý để người thiết kế duyệt. Tỷ lệ block ký hiệu theo đơn vị, bề rộng ô mở được điều khiển riêng bằng thông số cửa. Các block cửa động cần cấu hình tham số đặc thù và bản vẽ nhiều DWG nằm ngoài luồng thử này.

## Build và cập nhật

Build Release project `Arch.Suite.Host.csproj` với SDK dùng được cho net48. Thiết lập `ManagerRepo` nếu checkout AI-CAD-MCP không nằm cạnh repo này, `AutoCadDir` nếu AutoCAD 2021 cài tại vị trí khác. CAD Tool Manager cung cấp CAD.Contracts, CAD.Bridge.AutoCAD và Newtonsoft.Json. Các assembly Autodesk do AutoCAD cung cấp.

Cập nhật đúng mục Architectural Suite qua **Cập nhật file** trong Manager. Dùng DLL trong thư mục `package` của build đã lưu trong kho. Để quay lại, chọn build trước của cùng mục và mở lại AutoCAD. Khi đang mở một DWG đã đăng ký, lưu DWG trước khi đóng ứng dụng.
