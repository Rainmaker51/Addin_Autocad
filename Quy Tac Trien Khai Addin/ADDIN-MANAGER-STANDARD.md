# Tiêu chuẩn add-in dùng với CAD Tool Manager

Phiên bản tài liệu: 1.5. Ngày cập nhật: 24/11/2026.

## 1. Mục đích và phạm vi

Tài liệu này là yêu cầu đầu vào khi tạo add-in AutoCAD mới do chúng ta phát triển, kể cả trong cuộc trò chuyện hoặc dự án khác. Trợ lý và người phát triển phải đọc trước khi thiết kế, viết mã, build và bàn giao.

Nguyên tắc chính: **mỗi add-in có một bảng điều khiển riêng; trên Ribbon chỉ có một nút mở bảng đó.**

Áp dụng bắt buộc cho add-in mới. Add-in cũ được chuyển đổi khi có yêu cầu riêng; việc ban hành tài liệu không tự thay cấu hình các add-in đang dùng. DLL bên ngoài cần đánh giá khả năng tương thích trước khi áp dụng.

Trong dự án AI-CAD-MCP, đọc thêm `docs/ADDIN-DEVELOPMENT-WORKFLOW.md` để tuân thủ quy trình lưu phiên bản, kho và bàn giao. Trong dự án khác, dùng quy trình phát triển của dự án đó và giữ các yêu cầu tích hợp trong tài liệu này.

## 2. Phân chia trách nhiệm

| Thành phần | Trách nhiệm |
|---|---|
| CAD Tool Manager | Nhập và kiểm tra gói, quản lý phiên bản đang chọn, bật/tắt add-in, tạo nút Ribbon, quản lý tab/nhóm và điều phối lệnh |
| Add-in | Cung cấp một lệnh mở bảng chính, bảng điều khiển riêng, các chức năng nghiệp vụ và vòng đời tài nguyên |
| Người dùng | Chọn tab/nhóm đặt nút, dùng các chức năng trong bảng và quyết định cập nhật phiên bản |

### Tài nguyên từ kho và cấu hình nghiệp vụ

Thư viện QA quản lý tài nguyên gốc, không duyệt quyền sử dụng và không yêu cầu Role. Từng add-in chọn block/layer/dim/hatch theo ID ổn định, lưu cấu hình riêng ở `Catalog/addin-settings/<tool-id>.json` và tự kiểm tra yêu cầu nghiệp vụ như tag ATT, visibility, tham số động, phạm vi giá trị. Không ghi đè Usage/template chung để cấu hình cho riêng một công cụ. Dùng các DLL đang có; không phát sinh dịch vụ nền để đọc kho hoặc kiểm tra lựa chọn. Xem `QA-RESOURCE-WORKFLOW.md`.

Add-in không tự tạo tab, panel hay nút Ribbon, không tự đăng ký nạp bằng Registry, Startup Suite hoặc cơ chế khác song song với Manager. Gói cài thông thường được nhập qua Manager; chính CAD Tool Manager là bộ lõi có trình cài riêng.

## 3. Một nút Ribbon cho mỗi add-in

1. Cung cấp một lệnh mở bảng chính có tên ổn định, ví dụ `QAARCHOPEN`.
2. Ribbon chỉ ghim lệnh mở bảng này. Nút có tên dễ hiểu, biểu tượng và mô tả chức năng.
3. Các thao tác như tạo, sửa, xóa, cấu hình, xem thông tin và công cụ phụ nằm trong bảng điều khiển.
4. Khi nhấn lại nút, mở lại hoặc đưa bảng hiện có lên trước; không tạo thêm bảng trùng.
5. Tab và nhóm nút do Manager lưu. Add-in có thể đề xuất tên hiển thị và biểu tượng, nhưng không ghi đè lựa chọn đã lưu của người dùng.
6. Khi cập nhật DLL, giữ nguyên vị trí nút, nhãn đã chỉnh và lựa chọn bỏ ghim. Không tự khôi phục nút người dùng đã bỏ.

Ví dụ: add-in **Kiến trúc** có một nút **Kiến trúc** trên Ribbon. Bảng mở ra chứa các phần Tường, Cửa, Lưới trục và Thiết lập. Các chức năng này không tạo thêm nút Ribbon.

## 4. Bảng điều khiển riêng

- Dùng WPF trong `PaletteSet` cho bảng cần làm việc song song với bản vẽ. Dùng hộp thoại con khi cần nhập hoặc xác nhận một thao tác cụ thể.
- Mỗi add-in có tên bảng và GUID `PaletteSet` riêng, ổn định qua các phiên bản. Quản lý một instance bảng chính trong một phiên AutoCAD.
- Bảng có bố cục rõ ràng: chọn chức năng, nhập thông số, thực hiện và thông báo kết quả. Các chức năng cùng add-in có thể chia thành tab hoặc nhóm bên trong bảng.
- Dùng dark mode thống nhất với CAD Tool Manager, chữ và trạng thái dễ đọc, hỗ trợ bàn phím và cuộn khi thu nhỏ bảng.
- Kiểm tra giá trị trước khi chạy; thông báo lỗi phải nêu cách khắc phục. Khi đang xử lý, hiển thị trạng thái và ngăn thao tác lặp gây trùng đối tượng.
- Đóng hoặc ẩn bảng không làm mất dữ liệu đã lưu. Quy định rõ thông số nào chỉ giữ trong phiên, thông số nào lưu lâu dài.
- Nếu chưa mở DWG phù hợp, giải thích điều kiện cần và không chạy thao tác chỉnh sửa.

### 4.1. Giao diện chuẩn theo Axis

**Add-in tạo mới phải có giao diện cùng phong cách Axis, không chỉ cùng màu.** Đọc và tuân thủ [ADDIN-UI-STANDARD.md](ADDIN-UI-STANDARD.md): bảng màu dark, font, tab, ô nhập/chọn, nút, nhóm thu gọn, mật độ bố cục, vùng preview và trạng thái tương tác. Axis là mẫu giao diện tham chiếu; nội dung nghiệp vụ vẫn theo từng add-in.

- Tái sử dụng theme `src/CAD.Bridge.AutoCAD/CadToolTheme.xaml` và đối chiếu các mẫu điều khiển của Axis; không chỉ đổi màu nền rồi để các control trắng mặc định.
- Nếu công cụ có preview, thông số ở trái, preview ở phải, có thanh chia và nhớ tỷ lệ kéo theo máy. Mặc định 30/70 khi chưa có thiết lập; áp dụng chiều rộng tối thiểu phù hợp màn hình. Công cụ không cần preview dùng bố cục thông số/danh sách/chi tiết cùng phong cách, không tạo preview rỗng.
- Các tab mặc định là Thông số và Thư viện khi công cụ có lựa chọn tài nguyên kho; các thao tác nằm trong bảng, giữ một nút Ribbon mở bảng.
- Khi làm ở dự án khác, mang theo cả tài liệu giao diện và tài nguyên theme cần dùng. Việc tái sử dụng resource không thay yêu cầu kiểm tra tương thích Manager; không tự chép DLL của dự án khác vào gói.
- Chuẩn này áp dụng cho add-in mới. Chỉ chuyển giao diện các add-in đã có khi được yêu cầu riêng.

## 5. Lệnh và giao tiếp với Manager

### 5.1. Quy tắc đặt lệnh

- Dùng tiền tố riêng để tránh trùng AutoCAD và add-in khác; tên lệnh mở bảng không đổi tùy tiện giữa các phiên bản.
- Lệnh Manager gọi phải có `CommandMethod`, là phương thức public, trả về `void`, không có tham số. Lớp chứa lệnh instance cần constructor public không tham số để tương thích bộ dò hiện tại.
- Cờ `CommandFlags` phải phù hợp với ngữ cảnh chạy. Không dùng `Session` để thay thế việc khóa tài liệu hoặc transaction đúng cách.
- Có thể có nhiều lệnh nghiệp vụ phục vụ bảng điều khiển. **Một nút Ribbon không có nghĩa add-in chỉ được có một lệnh.**

### 5.2. Giao tiếp hiện có

Dự án AI-CAD-MCP có `AICad.Contracts.ICadManagedAddin`:

```csharp
public interface ICadManagedAddin
{
    IReadOnlyList<CadRibbonPin> ManagedCommands { get; }
    void InitializeManaged(Action<string> dispatch);
}
```

Add-in do chúng ta phát triển nên tích hợp giao tiếp này và `IExtensionApplication`. Tham chiếu bản `CAD.Contracts` tương thích do dự án Manager cung cấp; không tự tạo một interface cùng tên trong DLL khác.

`ManagedCommands` hiện là danh sách các lệnh Manager được phép quản lý và gọi. Runtime hiện tại lọc các lệnh theo danh sách này. Vì vậy, nếu bảng cần gọi các lệnh nghiệp vụ qua callback `dispatch`, phải khai báo cả các lệnh đó; không chỉ khai báo lệnh mở bảng rồi giả định các lệnh còn lại vẫn gọi được.

Chỉ ghim lệnh mở bảng lên Ribbon. Các lệnh nghiệp vụ trong `ManagedCommands` không nhất thiết phải có nút Ribbon. Các hàm xử lý nội bộ thông thường không cần biến thành lệnh AutoCAD nếu không có nhu cầu điều phối như một lệnh riêng.

Từ bảng modeless, dùng callback Manager để gọi các lệnh nghiệp vụ đã khai báo khi cần ngữ cảnh lệnh AutoCAD. Không mặc định DLL được NETLOAD toàn cục rồi gửi tên lệnh bằng `SendStringToExecute`. Nếu có chế độ chạy độc lập, phải thiết kế và kiểm thử chế độ đó riêng.

### 5.3. Khả năng hiện tại và yêu cầu cần triển khai thêm

| Nội dung | Tình trạng tại thời điểm viết |
|---|---|
| Nhập DLL, đọc lệnh, ghim riêng một lệnh mở bảng | Manager hiện hỗ trợ |
| Cung cấp nhiều lệnh quản lý nhưng chỉ ghim lệnh mở bảng | Thực hiện được bằng cấu hình ghim từng lệnh |
| Tự nhận biết lệnh mở bảng và tự tạo đúng một nút khi cài | Chưa có cơ chế khai báo riêng trong giao tiếp nêu trên |
| Bắt buộc mọi add-in chỉ có một nút, kể cả khi người dùng bấm Ghim tất cả | Manager hiện chưa cưỡng chế quy tắc này |

Trước khi có cơ chế tự động, bước cài đặt phải chọn và ghim duy nhất lệnh mở bảng; không dùng **Ghim tất cả lệnh** cho add-in có nhiều lệnh quản lý. Nếu phát triển cơ chế khai báo lệnh mở chính, cần cập nhật cả Manager, giao tiếp tích hợp và tài liệu; không tự thêm trường cấu hình rồi coi Manager hiện tại đã hiểu.

## 6. An toàn thao tác bản vẽ và vòng đời

- Khởi tạo không tự vẽ hoặc sửa DWG. Chỉ sửa theo thao tác người dùng yêu cầu trong bảng.
- Trước mỗi thao tác, xác định lại tài liệu đích; không giữ tham chiếu tài liệu đã đóng hoặc áp dụng nhầm khi người dùng chuyển DWG.
- Dùng `DocumentLock`, transaction và ngữ cảnh thực thi phù hợp. Lỗi hoặc hủy thao tác không để lại dữ liệu dở dang; gom Undo hợp lý cho từng thao tác.
- Không chạy chỉnh sửa Database AutoCAD từ luồng nền không được hỗ trợ.
- Khi nạp lại, dọn instance trước; không đăng ký trùng sự kiện, overrule hoặc timer.
- `Terminate` phải gỡ sự kiện, dừng timer/công việc nền, dọn bảng và tài nguyên add-in sở hữu. Không xóa UI hoặc tài nguyên của add-in khác.
- AutoCAD 2021 không hỗ trợ tháo riêng assembly .NET đã nạp theo cách thông thường. Khi Manager yêu cầu khởi động lại vì thay dependency hoặc xung đột DLL, phải tuân thủ; không cam kết cập nhật nóng mọi trường hợp.

## 7. Tương thích và đóng gói

Mục tiêu hiện tại: **AutoCAD 2021, .NET Framework 4.8, x64**. Khi hỗ trợ AutoCAD đời khác, ghi rõ phiên bản và cung cấp gói phù hợp; không giả định cùng một DLL chạy được trên mọi đời AutoCAD.

- Một add-in có một DLL chính và tên assembly ổn định qua các bản cập nhật.
- Gói chứa đầy đủ dependency riêng, tài nguyên và biểu tượng cần thiết. Các DLL phụ phải tương thích với tiến trình AutoCAD và dependency dùng chung của Manager.
- Không đóng gói lại DLL hệ thống AutoCAD như `acmgd.dll`, `acdbmgd.dll`, `accoremgd.dll`, `AdWindows.dll`; các tham chiếu này không được copy vào gói.
- Với Manager hiện tại, đặt DLL chính và các DLL phụ cần nạp cạnh nhau trong cùng thư mục. Nếu muốn hỗ trợ cấu trúc dependency khác, kiểm tra và triển khai khả năng đó trước.
- Không hardcode đường dẫn ổ đĩa, thư mục máy phát triển hoặc kho của máy khác.
- Cập nhật vào đúng add-in đã có trong Manager để giữ định danh và cấu hình; không nhập thành mục trùng tên.

## 8. Thiết lập và dữ liệu

- Thư mục gói cài chỉ chứa nội dung phát hành; không ghi thiết lập, log, cache hoặc DWG người dùng vào đó. Manager kiểm tra hash và có thể thay hoặc dọn gói khi cập nhật.
- Thiết lập riêng của từng máy đặt trong vùng dữ liệu người dùng với tên add-in riêng. Dữ liệu dùng chung đặt trong kho được cấu hình rõ ràng.
- Lưu phiên bản cấu trúc dữ liệu và có cách chuyển đổi khi cần. Cập nhật DLL phải giữ dữ liệu người dùng; sao lưu trước khi chuyển đổi có nguy cơ mất dữ liệu.
- Không đưa token, thông tin đăng nhập, đường dẫn cục bộ hoặc file cấu hình bí mật vào gói/Git.

### 8.1. Cấu hình sử dụng thư viện từ kho

**Mọi add-in mới sử dụng tài nguyên CAD phải liên kết tài nguyên bằng mã định danh của kho.** Các yêu cầu liên kết trong mục 8 áp dụng cả cho skill dùng kho; các yêu cầu DLL, Ribbon và bảng riêng ở những mục khác vẫn chỉ áp dụng cho add-in. Cấu hình xác định dim style, layer, block, text style và hatch theo vai trò nghiệp vụ. Add-in/skill không dùng bộ tài nguyên hardcode riêng để thay thế các lựa chọn đã lưu.

**Nơi chọn tài nguyên là bảng điều khiển add-in hoặc trang cấu hình skill.** Theo điều chỉnh phạm vi mới nhất, hủy triển khai tab **Đang sử dụng kho**; hiện chỉ triển khai định danh cho năm loại tài nguyên. Không thêm tab cấu hình hoặc tổng hợp sử dụng vào thư viện.

Mã ngắn `BLK-`, `LAY-`, `DIM-`, `TXT-`, `HAT-` gắn với ID đồng bộ ổn định, lưu riêng trong `Catalog/resource-codes.json`. Đổi tên/chuyển nhóm không đổi mã nếu vẫn là cùng ID. Xem `docs/RESOURCE-IDENTIFIERS.md` để biết cấu trúc đã triển khai, giới hạn ánh xạ block nguồn/biến thể và cách tích hợp. Các phần hợp đồng chọn tài nguyên của add-in/skill dưới đây vẫn là yêu cầu cho bước tích hợp sau, chưa tự áp dụng vào add-in đang cài.

**Bắt buộc đọc [LIBRARY-CONSUMER-STANDARD.md](LIBRARY-CONSUMER-STANDARD.md)** khi viết add-in mới có dùng kho. Tài liệu đó quy định quyền chỉnh sửa của QA, cấu hình để kiểm tra xóa/gộp, cách nạp lại lựa chọn và xử lý giao dịch thư viện chưa hoàn tất. QA 1.8.1 đã có kiểm tra/chuyển một số liên kết thực tế; các yêu cầu tổng quát về vai trò, phiên bản và API chung bên dưới không vì vậy trở thành tính năng Manager đã hỗ trợ. Phải đối chiếu bảng hiện trạng trong tài liệu dùng kho và nêu phần từng add-in còn phải triển khai. Không tự chuyển đổi add-in cũ khi chỉ cập nhật tiêu chuẩn.

### 8.2. Khai báo vai trò và chọn tài nguyên

- Add-in/skill khai báo các vai trò cần dùng, loại tài nguyên tương ứng, vai trò bắt buộc/tùy chọn và các tùy chỉnh được phép. Không phải công cụ nào cũng cần đủ cả năm loại tài nguyên.
- Phân biệt khai báo nhu cầu (ví dụ cần một dim style cho dim nhịp) với lựa chọn cụ thể (vai trò đó được gán mã `DIM-0001`). Người dùng gán và thay đổi lựa chọn trong chính add-in/skill.
- Chỉ lựa chọn đã lưu được coi là cấu hình sử dụng có hiệu lực để kiểm tra phụ thuộc. Bản nháp chỉnh sửa trong bảng không thay thế cấu hình đang có hiệu lực trước khi lưu thành công.
- Khi có gợi ý mặc định, add-in/skill phải phân giải và hiển thị tài nguyên cụ thể trước khi lưu lựa chọn. Không tự thêm một trang chọn mặc định chung trong tab tổng quan của thư viện.
- Nếu một liên kết đã cấu hình bị hỏng hoặc thiếu tài nguyên, phải báo lỗi; không âm thầm rơi về tài nguyên khác. Vai trò chưa gán phải ghi rõ “Chưa chọn”, không coi như đang sử dụng tài nguyên mặc định.
- Lưu liên kết bằng định danh tài nguyên ổn định, định danh nguồn, loại tài nguyên và chính sách phiên bản. Không dùng tên hiển thị, tên nhóm hoặc vị trí trình bày làm khóa duy nhất. Chuyển nhóm không làm mất liên kết.
- Cấu hình dùng chung phải đồng bộ được giữa hai máy; đường dẫn kho được phân giải theo cấu hình của máy hiện tại. Không lưu cứng đường dẫn tuyệt đối của máy phát triển vào cấu hình dùng chung.

Ví dụ các vai trò của Tạo trục:

| Vai trò | Tài nguyên cần gán |
|---|---|
| Đường tim trục | Layer đường trục |
| Đầu ký hiệu trục | Block đầu trục đã duyệt |
| Layer đầu trục | Layer ký hiệu trục |
| Dim từng nhịp | Dim style đo khoảng cách từng nhịp |
| Dim tổng | Dim style đo tổng; có thể gán cùng dim style của dim nhịp |
| Layer dim | Layer chứa đối tượng dimension |
| Chữ ký hiệu trục | Text style hoặc thuộc tính chữ trong block đầu trục, theo cấu trúc tài nguyên |

Tên layer hoặc block trong ví dụ triển khai chỉ là giá trị được gán, không thay thế định danh vai trò. Với hatch, liên kết phải xác định đúng cấu hình công dụng, gồm pattern, scale, góc và màu/chế độ màu; không chỉ tìm theo tên pattern.

### 8.3. Đọc, kiểm tra và nhập tài nguyên

1. Khi mở bảng hoặc làm mới, đọc cấu hình kho và hiển thị mục **Thư viện đang dùng**: vai trò, tên tài nguyên, phiên bản được phân giải, tên file nguồn và đường dẫn nguồn trên máy hiện tại.
2. Trước khi vẽ, kiểm tra lại cấu hình và tài nguyên: còn tồn tại, đúng loại, không bị loại khỏi sử dụng và đáp ứng các thuộc tính bắt buộc. Nếu thiếu hoặc sai, chỉ rõ vai trò bị lỗi và cách chọn lại; chặn thao tác phụ thuộc vào tài nguyên đó.
3. Nhập đúng định nghĩa nguồn vào DWG đích. Nếu đã có định nghĩa cùng tên nhưng khác thông số, áp dụng chính sách xung đột được cấu hình rõ ràng: dùng bản khớp, nhập với tên riêng hoặc cập nhật định nghĩa khi được cho phép. Không mặc định dùng bản cùng tên, tự ghi đè hoặc thay đổi các đối tượng sẵn có trong bản vẽ.
4. Phân giải và cố định tập tài nguyên cho một lần thực hiện; kiểm tra phiên bản/hash trước khi ghi để tránh dùng lẫn hai bản khi kho đang thay đổi. Ghi thông tin tài nguyên đã dùng để có thể kiểm tra lại kết quả.
5. Khi tài nguyên đang được cấu hình sử dụng bị xóa, đổi định danh hoặc thay đổi không tương thích, hệ thống phải chỉ ra các add-in/skill bị ảnh hưởng. Không để liên kết hỏng mà vẫn báo cấu hình hợp lệ.

### 8.4. Giữ đúng thông số và tỷ lệ

- Các thông số thư viện quản lý như font, chiều cao chữ, mũi tên dim, độ chính xác, định dạng số, màu và kiểu nét phải theo tài nguyên được chọn. Add-in không tự ghi đè nếu cấu hình không cho phép.
- Tách thông số bố trí nghiệp vụ (ví dụ khoảng cách đặt chuỗi dim, vị trí dim tổng) khỏi định nghĩa dim style. Nếu nghiệp vụ cần override một thông số style, phải khai báo, cho phép trong cấu hình và hiển thị rõ override đó.
- Xử lý đúng đơn vị, tỷ lệ chèn block và cơ chế dim thường/annotative. Không tự nhân tỷ lệ lần nữa khi tài nguyên đã áp dụng tỷ lệ hoặc annotation scale.
- Khi lựa chọn hoặc phiên bản tài nguyên thay đổi, nêu rõ cấu hình áp dụng cho lần thực hiện tiếp theo. Không tự sửa các đối tượng đã vẽ trước đó nếu người dùng chưa yêu cầu cập nhật bản vẽ.

### 8.5. Mã tài nguyên và thông tin liên kết dùng chung

- Mỗi tài nguyên có mã định danh ổn định kèm loại thư viện. Quy ước đã triển khai: `BLK-0001` (Block), `LAY-0001` (Layer), `DIM-0001` (Dim), `TXT-0001` (Text), `HAT-0001` (Hatch). Số được cấp tăng dần cho mỗi loại và không trùng trong sổ mã dùng chung; mã gắn với bộ khóa nguồn/loại/ID đồng bộ, không thay thế ID gốc.
- Đổi tên, chuyển nhóm hoặc thay vị trí mẫu trên CAD vẫn giữ mã. Không tái sử dụng mã đã xóa cho tài nguyên khác. Khi bổ sung mã cho thư viện hiện có, phải bảo toàn/ánh xạ các định danh và liên kết cũ, không đánh lại mã tùy ý.
- Các cấu hình hatch khác scale, góc hoặc màu phải có định danh phân biệt để chọn chính xác. Tài nguyên thay đổi thông số vẫn cần thông tin phiên bản/hash theo chính sách đã lưu.
- Mỗi add-in/skill có định danh công cụ ổn định, độc lập với tên hiển thị và số version. Với add-in, đối chiếu ID thực tế do Manager quản lý; không suy đoán danh tính từ tên DLL hoặc đường dẫn cài.
- Lựa chọn được lưu theo hợp đồng dữ liệu dùng chung, có phiên bản cấu trúc. Thông tin tối thiểu gồm: loại công cụ (`addin`/`skill`), ID công cụ, ID cấu hình nếu có nhiều cấu hình, mã vai trò, loại thư viện, mã tài nguyên, định danh nguồn, chính sách phiên bản và revision của cấu hình.
- Kho giữ bản liên kết đã lưu để app đọc được khi bảng add-in đã đóng hoặc DLL chưa được nạp. Add-in/skill là nơi cập nhật lựa chọn qua giao tiếp chung; không ghi tùy ý vào catalog nội bộ hoặc duy trì hai bản cấu hình có thể mâu thuẫn.
- Khi lưu, kiểm tra đúng loại tài nguyên, sự tồn tại và revision để tránh ghi đè thay đổi từ máy khác. Khi công cụ chưa hỗ trợ hợp đồng này, hiển thị “Chưa cung cấp thông tin”, không suy luận rằng công cụ không dùng kho.
- Với cấu hình JSON add-in đã được QA nhận diện, dùng vị trí và giới hạn thực tế trong [quy định dùng kho](LIBRARY-CONSUMER-STANDARD.md). API đăng ký vai trò/phiên bản tổng quát và các trường hợp ngoài phạm vi đó phải được thống nhất khi triển khai; các trường tối thiểu ở đây không phải schema Manager tự thực thi.

### 8.6. Phạm vi quản lý liên kết, không thêm tab tổng hợp

- Không triển khai danh sách công cụ đang dùng kho, tra cứu ngược “Được sử dụng bởi” hoặc thống kê lịch sử thực thi trong đợt này.
- Mã hiển thị trong danh sách và hồ sơ của từng loại tài nguyên, có thể sao chép để tham chiếu. Tài nguyên chưa khớp ID đồng bộ hiện “Mã chờ đồng bộ”, không được cấp mã chỉ dựa vào tên.
- Sổ mã phải dùng chung giữa hai máy, giữ các số đã cấp, không tự đánh lại mã khi tên/nhóm hoặc hash nội dung thay đổi. Không đưa sổ mã vào danh sách tệp cũ có thể xóa.
- QA đã có kiểm tra phụ thuộc và chuyển ID hatch trong phạm vi được mô tả ở [quy định dùng kho](LIBRARY-CONSUMER-STANDARD.md). Add-in mới phải triển khai bộ chọn, lưu/đọc lại liên kết và kiểm tra nguồn chưa hoàn tất cập nhật; các add-in/skill cũ cần được đánh giá riêng, không tự có cơ chế này chỉ vì được cấp mã.

### 8.7. Quyền chỉnh sửa và vòng đời tài nguyên theo QA

- QA quản lý DWG nguồn, danh mục và nhóm; add-in tiêu thụ ghi lựa chọn của nó và triển khai vào DWG công trình, không sửa kho chung hoặc journal QA.
- Lưu lựa chọn theo `SourceId + Kind + Id`. Cấu hình JSON của add-in mới đặt tại `Catalog/addin-settings/<ManagerToolId>.json`, chứa các ID hiện hành dưới dạng chuỗi để QA nhận diện; không chứa lịch sử/cache lựa chọn cũ. Đây là nơi QA hiện kiểm tra, không phải API Manager tự lưu nghiệp vụ.
- Lần mở bảng/lần triển khai tiếp theo phải nạp lại lựa chọn và thông số kho. Gộp hatch có thể chuyển ID nguồn sang ID đích trong file cấu hình; không ghi đè lại bằng ID nguồn từ bộ nhớ. Không tự thay tài nguyên khi liên kết thiếu/sai loại.
- Không tự dựng lại mẫu nguồn đã xóa. Định nghĩa còn trong DWG, mã còn trong sổ hoặc ảnh còn trong cache không chứng minh tài nguyên còn được phép dùng.
- Kiểm tra phiên bản/hash và trạng thái chưa hoàn tất cập nhật. Không tự sửa journal; chính sách chặn hoặc dùng bản xuất trước đã xác minh phải rõ ràng theo quy định dùng kho.
- Giữ đúng cấu hình hatch, màu và scale; giữ tên/thông số Dim/Text/Layer/Block đã chọn, xử lý tỷ lệ đúng. Đổi thư viện áp dụng ở lần triển khai sau, không tự sửa đối tượng công trình đã vẽ.
- Phụ thuộc skill và cấu hình ngoài các vị trí được QA hỗ trợ phải được đánh giá/triển khai adapter riêng; không cam kết phát hiện hoặc viết lại mọi skill.

## 9. Hồ sơ cần bàn giao

Mỗi bản bàn giao phải có:

1. Tên add-in, version, AutoCAD mục tiêu, framework và kiến trúc.
2. DLL chính, dependency và SHA-256 các file; định danh build rõ ràng.
3. Source commit hoặc commit gốc kèm patch đủ để tái tạo build.
4. Tên lệnh mở bảng duy nhất, nhãn nút, biểu tượng và mô tả đề xuất.
5. Danh sách lệnh nghiệp vụ cần Manager điều phối và chức năng tương ứng trong bảng.
6. README hướng dẫn cài/cập nhật qua Manager, sử dụng bảng và vị trí dữ liệu.
7. Ghi chú thay đổi, kết quả kiểm thử, giới hạn đã biết và trạng thái đã/chưa cài.
8. Danh sách vai trò thư viện, mã/loại tài nguyên, nơi chọn trong add-in/skill, hợp đồng lưu liên kết để tổng hợp và tra cứu ngược, chính sách phiên bản/xung đột/tỷ lệ và các override được phép. Nêu rõ phần tích hợp kho còn thiếu nếu chưa triển khai đầy đủ.

Trong AI-CAD-MCP, đặt gói đúng cấu trúc `_Development/<TênAddin>/versions/<version>/build-<fingerprint>/`; build tạm dưới `working/`. Manager quản lý `_Installed` và catalog. Không sửa tay hai nơi này để kích hoạt phiên bản.

## 10. Điều kiện nghiệm thu

- [ ] Cài qua Manager bằng DLL chính, đủ dependency, không cần NETLOAD hoặc tự đăng ký nạp riêng.
- [ ] Ribbon có đúng một nút mở bảng cho add-in trong cấu hình nghiệm thu.
- [ ] Tất cả chức năng người dùng cần đều thao tác được trong bảng riêng.
- [ ] Nhấn nút nhiều lần, đóng rồi mở lại không tạo bảng hoặc handler trùng.
- [ ] Chạy được các thao tác trong bảng qua Manager, không phụ thuộc lệnh đã NETLOAD toàn cục.
- [ ] Chuyển DWG, hủy lệnh và nhập sai thông số được xử lý đúng; Undo phù hợp.
- [ ] Cập nhật giữ nút Ribbon, tab/nhóm và dữ liệu người dùng.
- [ ] Add-in/skill dùng tài nguyên CAD khai báo vai trò và lưu lựa chọn bằng mã/loại tài nguyên; hiển thị tài nguyên/nguồn/phiên bản đang dùng trong chính bảng cấu hình của công cụ.
- [ ] Mã/ID tài nguyên giữ nguyên khi đổi tên hoặc nhóm; không tái dùng mã đã cấp cho ID khác; tài nguyên chưa xác nhận ID được báo chờ đồng bộ.
- [ ] Thiếu tài nguyên hoặc liên kết hỏng được báo rõ; không tự đoán, dùng nhầm bản cùng tên hoặc thay thế âm thầm.
- [ ] Nhập vào DWG có định nghĩa cùng tên nhưng khác thông số được xử lý theo chính sách; không tự ảnh hưởng đối tượng sẵn có.
- [ ] Dim thường/annotative, đơn vị, tỷ lệ block và các override cho phép được kiểm thử; thông số thư viện không bị ghi đè ngoài quy định.
- [ ] Thay tên/nhóm, cập nhật công cụ hoặc tài nguyên không làm hỏng mã liên kết; cấu hình dùng được trên máy có đường dẫn kho khác.
- [ ] Tắt add-in không để lại sự kiện hoặc UI hoạt động ngoài ý muốn.
- [ ] Kiểm thử trên AutoCAD 2021 thực tế với DWG thử, không dùng bản vẽ công trình thật làm fixture.
- [ ] Bàn giao đủ nguồn, version, hash, hướng dẫn và bằng chứng kiểm thử; ghi rõ phần chưa kiểm tra.
- [ ] Giao diện add-in mới đã đối chiếu với Axis và `ADDIN-UI-STANDARD.md`, gồm cả control, bố cục, trạng thái và khả năng co kéo.

Kiểm thử Core Console có thể xác minh logic và thao tác Database. Bảng WPF, nút Ribbon, focus và thao tác modeless cần kiểm tra thêm trong GUI AutoCAD; không báo kiểm thử GUI đạt chỉ từ kết quả Core Console.

## 11. Dùng tài liệu trong cuộc trò chuyện hoặc dự án khác

Sao chép file này vào `docs/ADDIN-MANAGER-STANDARD.md` của dự án mới hoặc đính kèm vào cuộc trò chuyện. Mang theo `ADDIN-UI-STANDARD.md` và theme XAML tham chiếu để dự án nhận áp dụng được giao diện Axis. Nếu công cụ dùng kho, mang thêm `LIBRARY-CONSUMER-STANDARD.md`, `RESOURCE-IDENTIFIERS.md` và adapter/source tham chiếu cần thiết. Nếu dùng `AGENTS.md`, thêm đoạn sau vào file hiện có, giữ nguyên các quy tắc khác của dự án:

```markdown
## Add-in AutoCAD dùng với CAD Tool Manager

Trước khi thiết kế, phát triển, sửa lỗi, build hoặc bàn giao add-in, đọc và tuân thủ
`docs/ADDIN-MANAGER-STANDARD.md`.
Mỗi add-in mới phải có bảng điều khiển riêng và đúng một nút Ribbon mở bảng đó.
Manager quản lý tab/nhóm/nút; add-in không tự tạo Ribbon hoặc tự đăng ký nạp riêng.
Giao diện add-in mới phải theo Axis, cả màu sắc, bố cục, control và tương tác;
đọc docs/ADDIN-UI-STANDARD.md và tái sử dụng theme được chỉ rõ trong tài liệu.
Kiểm tra bản giao tiếp Manager thực tế trước khi tích hợp; không tự giả định API.
Giữ cấu hình Ribbon và dữ liệu người dùng khi cập nhật.
Add-in/skill chọn tài nguyên trong chính bảng cấu hình của công cụ và lưu nguồn,
loại và ID ổn định theo vai trò. Đọc docs/LIBRARY-CONSUMER-STANDARD.md và
docs/RESOURCE-IDENTIFIERS.md; lưu cấu hình để QA kiểm tra xóa/gộp, nạp lại ở lần
triển khai sau, không phục hồi mẫu/ID đã loại từ cache. QA chỉnh sửa thư viện;
addin tiêu thụ không sửa danh mục hoặc journal. Không thêm tab tổng hợp sử dụng kho.
Hiển thị tài nguyên đang dùng; không hardcode thay
lựa chọn hoặc tự thay thế khi liên kết hỏng. Kiểm tra phiên bản, xung đột và tỷ lệ.
Nếu kho chưa có cơ chế tích hợp cần thiết, nêu rõ và triển khai phần còn thiếu;
không coi yêu cầu trong tài liệu là tính năng đã tồn tại.
Ghi rõ nguồn, version, hash, kiểm thử và trạng thái đã/chưa cài khi bàn giao.
```

Khi chỉ đính kèm trong chat, có thể yêu cầu: “Phát triển add-in theo file tiêu chuẩn đính kèm. Mỗi add-in có bảng riêng và một nút Ribbon. Trước khi viết mã, đối chiếu giao tiếp của Manager đang dùng và nêu phần còn thiếu để tích hợp.”

Tài liệu là tiêu chuẩn triển khai và nghiệm thu. Bản thân file Markdown không tự cài add-in hay thay đổi hành vi Manager.
