# Chuẩn giao diện add-in — theo Axis

Phiên bản 1.0, ngày 23/11/2026. Áp dụng bắt buộc cho add-in tạo mới theo yêu cầu người dùng: **giao diện giống Axis**, gồm màu sắc, bố cục và cách thao tác. Đọc cùng [chuẩn Manager](ADDIN-MANAGER-STANDARD.md) và [quy trình phát triển](ADDIN-DEVELOPMENT-WORKFLOW.md).

## 1. Mẫu và tài nguyên tham chiếu

- Theme nền: [CadToolTheme.xaml](../src/CAD.Bridge.AutoCAD/CadToolTheme.xaml).
- Bố cục bảng: [AxisDialogLayout.cs](../src/Rainmaker.Axis/AxisDialogLayout.cs).
- ComboBox chọn tài nguyên có thể gõ/tìm: [AxisResourcePicker.xaml](../src/Rainmaker.Axis/AxisResourcePicker.xaml).
- Preview tương tác: [AxisPreview.cs](../src/Rainmaker.Axis/AxisPreview.cs).

Tái sử dụng style/resource và cấu trúc giao diện. Tên chức năng, trường nhập, danh sách và dữ liệu preview phải phù hợp add-in mới; không sao chép nghiệp vụ tạo trục. Không tự đổi phong cách sang nền trắng, gradient hoặc bộ control khác. Không sửa Axis/Manager chỉ để ban hành chuẩn này.

Trong repo, dùng ResourceDictionary đã có và các brush theo khóa, tránh rải lại mã màu trong từng control. Nếu dự án khác không có resource này, đưa bản theme cần thiết vào source/tài nguyên nhúng của add-in và ghi nguồn; đối chiếu tương thích trước khi dùng assembly Manager. Không tạo một bản DLL Manager/dependency riêng chỉ để lấy giao diện.

## 2. Màu chuẩn

Các giá trị dưới đây đối chiếu từ theme Axis đang dùng; file XAML là nguồn kỹ thuật của các style hiện có.

| Vai trò | Resource | Màu |
|---|---|---|
| Nền chính | `ToolBackground` | `#101720` |
| Nền nhóm, bảng | `ToolSurface` | `#192331` |
| Ô nhập, nút phụ | `ToolInput` | `#202D3F` |
| Viền | `ToolBorder` | `#637B98` |
| Chữ chính | `ToolText` | `#E6EDF7` |
| Chữ phụ | `ToolMuted` | `#A8B8CC` |
| Nhấn mạnh, focus, nút chính | `ToolAccent` | `#94B9FF` |
| Đang chọn | `ToolSelected` | `#243F66` |

Nút chính dùng `ToolPrimaryButton`, nền accent và chữ đậm tối `#101F36`. Áp dụng dark mode cả ô nhập, danh sách xổ xuống, bảng, tooltip và thanh cuộn. Giữ hover/focus/pressed/disabled theo theme; không chỉ tô màu nền cửa sổ. Trạng thái lỗi đỏ/hồng, cảnh báo vàng, thành công xanh phải kèm nội dung, không chỉ đổi màu. Các khóa brush trạng thái ngoài bảng này chưa được theme chung khai báo: nếu cần thì định nghĩa rõ trong resource của add-in, không giả định Manager đã cung cấp.

## 3. Bố cục bảng

- Một bảng chính, tên công cụ và version rõ ở đầu; thao tác tạo/chọn đối tượng nằm ở thanh công cụ phía trên.
- **Thông số** chứa các trường nghiệp vụ và nhóm chức năng. **Thư viện** chứa lựa chọn tài nguyên kho khi add-in có dùng kho. Không thêm tab rỗng hoặc đưa chi tiết kỹ thuật không cần thiết vào luồng người dùng.
- Nhóm dùng Expander, có thể thu gọn. Đặt trường liên quan trên cùng hàng, nhãn ở trên, thống nhất chiều cao và căn đáy với nút bên cạnh. Có đơn vị mm/độ/tỷ lệ; gợi ý qua tooltip hoặc dòng phụ.
- Với công cụ có preview: vùng trái nhập thông số, vùng phải xem trước; có GridSplitter. Mặc định khoảng **30% / 70%**, có chiều rộng tối thiểu và cuộn dọc để không cắt control trên màn hình nhỏ.
- Nhớ tỷ lệ kéo gần nhất qua lần đóng/mở CAD trong thiết lập theo máy của chính add-in. Không dùng chung file cấu hình của Axis, không đưa đường dẫn tuyệt đối máy phát triển vào dữ liệu dùng chung.
- Công cụ không có preview có thể dùng bảng thông số/danh sách/chi tiết cùng style; không ép có một vùng preview trống. PaletteSet/hộp thoại vẫn chọn theo ngữ cảnh trong chuẩn Manager.
- Nút thực hiện chính ở cuối bảng, phía phải; nút Hủy và lưu thiết lập gần đó khi cần. Vùng nút và thông báo không bị cuộn mất cùng form. Giữ bố cục gọn nhưng không che chữ hoặc làm nút khó bấm.

## 4. Control và chữ

- Font Segoe UI; nội dung nhập/nút khoảng 13 DIP, nhãn/chú thích 11–12 DIP, tiêu đề khoảng 20–22 DIP. Điều chỉnh cho DPI và màn hình thực, không thu nhỏ toàn bộ để cố nhét thêm trường.
- Ô nhập, nút và tab bo nhẹ khoảng 5 DIP, viền 1 DIP, khoảng cách đều như Axis. Control thông thường theo theme khoảng 34–36 DIP; hàng thao tác gọn có thể khoảng 30 DIP khi vẫn đủ đọc và bấm.
- Checkbox/radio/tab thể hiện rõ lựa chọn. Ô nhập có caret, selection và focus tương phản; danh sách xổ xuống vẫn dark khi mở.
- Chọn tài nguyên theo mã và tên; với ComboBox cho gõ/tìm, phải kiểm tra phần text đang chọn thật sự hiện khi nạp lại. Không dùng template ComboBox chỉ phù hợp không-editable cho một ô editable.
- Bảng có header rõ, dòng chọn dễ nhận ra; tên dài có tooltip hoặc cách xem đủ. Không để ô trống giả do cột co về 0 khi mở bảng.
- Số đang sửa giữ độ chính xác cần thiết; số hiển thị có thể rút gọn nhưng không dùng giá trị đã làm tròn để ghi đè dữ liệu chính xác.

## 5. Tương tác và preview

- Kiểm tra dữ liệu trước thực hiện, báo lỗi tại bảng và chặn hành động chưa hợp lệ. Có trạng thái đang xử lý; ngăn thao tác lặp sinh trùng đối tượng.
- Nếu có quét chọn: trái → phải chọn hình nằm trọn, phải → trái chọn cả hình giao; Ctrl hỗ trợ chọn nhiều. Giữ chuột giữa để pan, lăn chuột zoom tại con trỏ, có Vừa khung.
- Dùng màu/lời chú giải nhất quán: phương án xanh, hiện trạng cam, đối tượng đang chọn vàng; khung Window xanh dương liền, Crossing xanh lá đứt. Chỉ hiển thị lớp/định hướng hỗ trợ cần thiết, cho bật/tắt để tránh rối.
- Nếu preview cho chỉnh bản nháp, cung cấp Hoàn tác/Làm lại theo hành động khi phù hợp; không coi chọn đối tượng hoặc pan/zoom là bước thay đổi dữ liệu. Phân biệt thao tác trong bản nháp với việc đã ghi CAD.
- Chọn hiện trạng hoặc đổi thông số không được ghi DWG ngầm. Chỉ cập nhật đối tượng theo hành động thực hiện rõ ràng của người dùng.
- Giữ phản hồi nhanh; không đọc lại kho, đối chiếu toàn DWG hoặc dựng lại mọi control mỗi lần di chuột/chọn. Không đưa AutoCAD Database sang luồng nền không được hỗ trợ.

## 6. Kiểm tra trước bàn giao

Đối chiếu ảnh render/cửa sổ với Axis: màu, control, tab, nhóm, mật độ và nút chính. Kiểm tra màn hình nhỏ/DPI, cuộn, co kéo và đọc lại tỷ lệ đã lưu. Kiểm tra hover/focus/disabled, danh sách xổ xuống và giá trị nạp lại, thông báo lỗi và phím tắt. Với preview, kiểm tra chọn/pan/zoom và trạng thái bản nháp/đã ghi.

Kiểm thử Core hoặc ảnh WPF không thay kiểm thử chuột/Ribbon trong AutoCAD GUI. Ghi rõ phần đã/chưa kiểm tra. Chuẩn này không tự chuyển đổi các add-in cũ, không đổi gói đang cài và không tạo thêm nút Ribbon; mỗi add-in vẫn chỉ một nút mở bảng do Manager quản lý.

Khi chuyển dự án hoặc cuộc trò chuyện: mang theo tài liệu này, chuẩn Manager và theme XAML cần thiết. Trong AGENTS.md của dự án nhận, yêu cầu đọc hai chuẩn trước khi xây dựng add-in mới.
