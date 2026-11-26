# Quy định add-in/skill sử dụng thư viện kho

Phiên bản 1.2 — 25/11/2026. Bổ sung Reader 1.0.0, Manager 0.10.0 và Axis 1.9.0. Đọc cùng [giao thức Reader V1](LIBRARY-READER-PROTOCOL.md), [chuẩn Manager](ADDIN-MANAGER-STANDARD.md), [định danh](RESOURCE-IDENTIFIERS.md) và [quy trình phát triển](ADDIN-DEVELOPMENT-WORKFLOW.md).

**Thay đổi từ QA 1.9:** chỉ thêm/sửa/xóa/thay cấu hình tài nguyên trên DWG nguồn; QA quản lý thông tin, quét, đối chiếu và trình bày/xuất danh mục. Các cơ chế xóa/gộp trực tiếp CAD của 1.8 bên dưới chỉ là ghi nhận lịch sử, không phải luồng giao diện hiện hành. Gộp danh mục chỉ được xác nhận sau khi hatch đã được sửa cho khớp trên CAD. Xem [hướng dẫn QA 1.9](QA-LIBRARY-1.9.md).

Áp dụng bắt buộc khi viết add-in mới có dùng Block, Layer, Hatch, Text hoặc Dim của kho. Với add-in/skill cũ, đánh giá và chuyển đổi khi được giao; không sửa cấu hình đang dùng chỉ vì tài liệu được cập nhật. Nếu dự án khác chưa có cơ chế tương ứng, phải triển khai và kiểm thử adapter, không giả định Manager tự cung cấp.

## 1. Quyền quản lý và nguồn dữ liệu

- Add-in mới đọc kho qua `Rainmaker.Library.Reader` V1 do Manager 0.10.0 cung cấp, không tự ghép file Template/Styles/Hatches/state/codes/metadata. Tham chiếu `Private=false`; không đóng gói bản Reader riêng. Reader là DLL hạ tầng không có Ribbon, không cần Host/QA đang mở. Cấu trúc storage đổi thì cập nhật adapter Reader/QA và giữ API/payload V1 cho consumer; xem tài liệu giao thức. Các add-in cũ chỉ chuyển khi được giao; Axis 1.9.0 đã được chuyển trong đợt này.
- QA là công cụ chỉnh sửa thư viện: mẫu/định nghĩa trong DWG nguồn, danh mục, nhóm, thông tin quản lý, xóa và gộp tài nguyên. App và add-in/skill tiêu thụ đọc danh mục đã xuất hợp lệ. Việc sửa thư viện bằng lệnh CAD phải đi qua lưu nguồn và cập nhật kho tại QA.
- Add-in tiêu thụ được ghi lựa chọn của chính nó và tạo/sửa đối tượng trong DWG công trình theo yêu cầu người dùng. Không sửa DWG thư viện nguồn, danh mục chung, sổ mã, nhóm, trạng thái duyệt, con trỏ version hoặc journal của QA.
- Hình học hoặc block do add-in tự sinh theo thông số nghiệp vụ (ví dụ cột tự tạo của Axis) không mặc nhiên là tài nguyên thư viện. Chỉ đưa vào kho qua QA khi có yêu cầu; quy định liên kết áp dụng cho những tài nguyên thực sự lấy từ kho.
- Đường dẫn kho phải lấy từ cấu hình của máy hiện tại và bộ phân giải của dự án, không hardcode ổ đĩa máy phát triển. Hiện nguồn tích hợp là `ISO_QUANGANH`; nhiều DWG nguồn là khả năng phải triển khai thêm, chưa được coi là đã hỗ trợ.
- Danh mục và asset/snapshot đã xuất là một bộ phiên bản. Nếu hình học đang lấy từ bản sao asset, kiểm tra đúng hash được danh mục tham chiếu; không thay bằng DWG gốc đang sửa nhưng chưa xuất chỉ vì cùng tên file. Hiển thị tên/đường dẫn nguồn và phiên bản đang dùng để kiểm tra.

## 2. Định danh và lựa chọn

- Khóa liên kết là **SourceId + Kind + Id**. `Kind` dùng các giá trị `blocks`, `layers`, `hatches`, `texts`, `dims`. Mã ngắn `BLK-`, `LAY-`, `HAT-`, `TXT-`, `DIM-` phục vụ tìm/chọn/hiển thị; phải phân giải thành đúng ID trước khi lưu.
- Tên, đường dẫn, nhóm, handle DWG và hash nội dung không thay ID. Handle chỉ có nghĩa trong database tương ứng, không mang handle của bản chụp/clone sang DWG khác để tìm đối tượng.
- Đổi tên/chuyển nhóm giữ liên kết nếu ID vẫn còn. Sổ mã giữ cả ID đã loại; chỉ tìm thấy mã trong sổ không chứng minh tài nguyên còn được phép dùng. Không tái cấp ID/mã cũ cho tài nguyên khác.
- Block nguồn và hồ sơ sử dụng/biến thể là hai thông tin khác nhau. Nếu cần biến thể, giữ cả khóa hồ sơ và tham số động/ATT theo giao tiếp hiện có; không chỉ lưu mã block nguồn rồi làm mất lựa chọn biến thể.
- Mỗi vai trò nghiệp vụ khai báo loại tài nguyên và bắt buộc/tùy chọn. Người dùng chọn trong bảng của add-in/skill; lựa chọn chưa lưu không được coi là cấu hình có hiệu lực. Thiếu hoặc sai loại phải báo vai trò/mã bị lỗi và cho chọn lại, không tự dùng tài nguyên khác hoặc hardcode fallback.

## 3. Cấu hình để QA kiểm tra phụ thuộc

**Cách hiện tại cho add-in:** cấu hình dùng chung đặt tại `Catalog/addin-settings/<ManagerToolId>.json`. Tên file phải khớp ID thật của mục trong Manager; không suy ra từ tên DLL hoặc tên hiển thị. Đối chiếu `AxisLibrary.cs` và catalog trước khi tích hợp, giữ đường dẫn/định dạng cũ khi nâng cấp công cụ đã cài.

- Lưu ID đang chọn thành giá trị chuỗi JSON đầy đủ; không chỉ lưu mã ngắn, tên, ID băm lại, chuỗi ghép hoặc giá trị được mã hóa. QA hiện kiểm tra/chuyển ID bằng các giá trị chuỗi trong JSON.
- File này chỉ chứa cấu hình hiện hành. Lịch sử, cache, bản nháp đã bỏ và các snapshot lựa chọn cũ phải tách riêng; QA chưa có schema vai trò để phân biệt ID đang dùng với ID nằm trong lịch sử của cùng file.
- Có `SchemaVersion`, `Revision`, `SourceId` và liên kết theo vai trò. Add-in chịu trách nhiệm đọc/ghi, kiểm tra revision, ghi nguyên tử, giữ các trường không liên quan và chuyển đổi schema có sao lưu. Không coi Manager là API tự lưu/kiểm tra cấu hình nghiệp vụ.
- Không ghi cấu hình vào `_Installed`, thư mục build hoặc catalog quản lý gói. Không đưa token và đường dẫn tuyệt đối của máy vào file dùng chung.

Mẫu cho add-in mới (minh họa; phải viết serializer/resolver của add-in và dùng ID thực tế):

```json
{
  "SchemaVersion": 1,
  "Revision": 0,
  "SourceId": "ISO_QUANGANH",
  "Bindings": {
    "axisLine": { "Kind": "layers", "Id": "<id-layer-da-chon>" },
    "bayDimension": { "Kind": "dims", "Id": "<id-dim-da-chon>" },
    "wallFill": { "Kind": "hatches", "Id": "<id-hatch-da-chon>" }
  }
}
```

`Bindings` là mẫu tổ chức cấu hình cho công cụ mới, không phải schema/API Manager đã triển khai. Axis hiện dùng các trường như `AxisLayerId`, `BayDimId`, `WallHatchId`; không tự chuyển Axis sang mẫu này.

**Skill:** hiện QA kiểm tra các liên kết được hệ thống biết trong recipe/workflow, gồm block/layer/style và một số trường hợp hatch theo pattern. Chưa có cơ chế đăng ký phụ thuộc tổng quát cho mọi skill hoặc file cấu hình tùy ý. Skill mới phải chỉ ra nó lưu liên kết ở đâu và kiểm thử QA có nhận diện; nếu chưa có adapter, phải ghi rõ giới hạn và bổ sung khi được giao.

## 4. Đọc lại kho và giữ đúng thông số

1. Khi mở/nạp bảng hoặc bắt đầu lần triển khai mới, đọc lại lựa chọn đã lưu và phân giải phiên bản đã xuất hiện hành. Không giữ lựa chọn trong bộ nhớ từ lần mở trước rồi ghi đè cấu hình vừa được QA gộp/chuyển ID.
2. Trước khi ghi CAD, kiểm tra lại tài nguyên còn được dùng, đúng nguồn/loại, đúng hash và đủ thuộc tính. Cố định bộ tài nguyên cho một lần thực hiện; nếu con trỏ/revision thay đổi trong lúc đọc, đọc lại hoặc báo xung đột, không trộn hai bản.
3. Hatch lấy đúng cấu hình ID đã chọn: pattern, scale, góc, ACI/TrueColor/ByLayer, layer và thông số user-defined liên quan. Không tìm bằng pattern rồi chọn cấu hình đầu tiên. Ví dụ QA sửa scale thành 300 thì lần nạp/triển khai tiếp theo phải nhận 300.
4. Dim/Text/Layer/Block theo thông số đã chọn. Giữ đúng tên dim thư viện; không tự tạo dim `RMAX_*` thay cho lựa chọn. Override nghiệp vụ phải được khai báo, cho phép và hiển thị; xử lý đơn vị và annotative để không nhân tỷ lệ hai lần.
5. Khi nhập định nghĩa trùng tên vào DWG công trình, xác minh nội dung và dùng chính sách xung đột đã công bố. Không âm thầm dùng bản khác hoặc ghi đè làm đổi các đối tượng đang có.

Không bắt buộc thêm nút đồng bộ tức thời trong từng add-in. Các đối tượng đã vẽ giữ nguyên cho tới khi người dùng yêu cầu cập nhật chúng.

Mặc định phân giải theo con trỏ bản đã xuất hiện hành, không chọn thư mục có số version lớn nhất/ngày mới nhất. Chỉ hỗ trợ ghim phiên bản khi adapter thực tế có chức năng đó và xác minh đủ hash; không tự phát minh trường/API ghim phiên bản rồi coi Manager đã thực thi.

## 5. Xóa, gộp và nguồn chưa hoàn tất cập nhật

- Không tự khôi phục mẫu trong thư viện từ cache/hồ sơ cũ. Các trạng thái **Chỉ còn định nghĩa**, **Đã mất trong CAD**, thùng rác hoặc mã đã loại không phải lý do để add-in tạo lại mẫu nguồn.
- QA 1.8.1 có xóa mẫu/định nghĩa và loại danh mục. Kiểm tra phụ thuộc hiện bao gồm cấu hình add-in đang bật mà tên file khớp Manager ID, cùng các recipe/workflow được hỗ trợ. Không cam kết phát hiện công cụ lưu ID ngoài những nơi này.
- Khi gộp hatch, QA giữ ID/mã đích, loại nguồn và có thể thay ID nguồn trong các cấu hình JSON add-in. Vì vậy lần mở bảng/lần triển khai sau phải nạp lại file và phân giải ID đích; không lưu đè lại ID nguồn từ cache. Không tự tạo bảng đổi ID hoặc cấp mã mới để thay QA.
- Skill gọi hatch bằng pattern trực tiếp không được tự động đổi như liên kết ID. QA có kiểm tra phụ thuộc được hỗ trợ, nhưng không có cơ chế viết lại mọi skill/plugin bên ngoài.
- Nếu có `Catalog/qa-transactions/ISO_QUANGANH.pending.json`, nguồn có thể đã đổi trong khi kho chưa hoàn tất. Add-in không xóa/sửa/tiếp tục journal. Bản đọc mới phải báo chưa hoàn tất và chặn triển khai phụ thuộc vào nguồn đó; chỉ cho dùng một bản xuất trước đã kiểm tra đủ tính toàn vẹn nếu công cụ có chính sách này và hiển thị rõ cho người dùng.
- Kho không truy cập được, asset sai hash hoặc thiếu file: báo lỗi/giữ chỉ xem. Không tự tạo kho thay thế, thay con trỏ version hay chuyển sang file có cùng tên.
- Dùng chung hai máy: chỉ một máy cập nhật kho tại một thời điểm, đợi đồng bộ đủ trước khi đổi máy. File lock không bảo đảm khóa phân tán qua dịch vụ đồng bộ thư mục.

## 6. Hiện trạng và phần phải tích hợp

| Nội dung | Hiện trạng đối chiếu |
|---|---|
| ID/mã ổn định cho 5 loại | Đã có; xem tài liệu định danh và kiểm tra nguồn/ID còn hoạt động. |
| QA chỉnh thư viện, xóa/gộp, journal và bản sao | Đã có trong QA 1.8.1; xem hướng dẫn QA và manifest build. |
| Kiểm tra cấu hình JSON, chuyển ID hatch và một số phụ thuộc skill | Đã có phạm vi cụ thể ở trên; không phải bộ phân tích mọi file/DLL/skill. |
| Bộ chọn, serializer, resolver, đọc lại cấu hình, kiểm tra journal trong mọi add-in | Là yêu cầu cho add-in mới; phải kiểm tra/triển khai từng công cụ, không tự có do cài Manager/QA. |
| API đăng ký vai trò/phiên bản dùng chung cho mọi add-in/skill; nhiều nguồn; tab ai đang dùng | Chưa được tài liệu này triển khai; không tự dựng thêm tab hoặc giả định API tồn tại. |

QA chạy trong AutoCAD, không bắt buộc dịch vụ nền cho việc quản lý kho. Add-in tiêu thụ không được mặc định buộc chạy Host chỉ để đọc tài nguyên; nếu có nhu cầu phụ thuộc riêng phải mô tả và kiểm thử rõ ràng.

## 7. Nghiệm thu add-in mới dùng kho

- Chọn/lưu/mở lại đủ vai trò; sai loại, thiếu ID và tài nguyên đã loại phải được xử lý rõ.
- Đổi tên/chuyển nhóm giữ liên kết; đổi màu/scale/góc/font/dim trong QA được nhận ở lần triển khai sau.
- Xóa tài nguyên mà add-in đang chọn phải được QA nhận diện; gộp hatch phải chuyển lựa chọn sang ID đích, mở lại add-in không phục hồi ID nguồn.
- Kiểm tra nguồn chưa hoàn tất cập nhật, kho mất kết nối, sai hash, cấu hình bị phiên khác sửa, thư viện đổi giữa lúc đọc và hai máy có đường dẫn kho khác nhau.
- Kiểm tra tài nguyên trùng tên và tỷ lệ trong DWG công trình; không tự sửa các đối tượng cũ hoặc DWG nguồn.
- Bàn giao mô tả vai trò, Manager ID, đường dẫn/schema cấu hình, cách phân giải ID/phiên bản, phạm vi kiểm tra phụ thuộc, các tình huống đã thử và hạn chế còn lại. Tuân thủ [vòng đời dữ liệu thử](TEST-ARTIFACT-LIFECYCLE.md).

Khi chuyển sang cuộc trò chuyện hoặc dự án khác, mang cả tài liệu này, chuẩn Manager/UI, tài liệu định danh và adapter/source tham chiếu cần thiết; không chỉ mang một file DLL rồi mặc định giao tiếp tương thích.
