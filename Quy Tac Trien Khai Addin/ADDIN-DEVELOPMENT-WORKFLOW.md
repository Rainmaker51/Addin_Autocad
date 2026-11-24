# Quy trình phát triển add-in AutoCAD trên hai máy

Ngày lập: 19/11/2026. Áp dụng cho mọi add-in .NET do dự án phát triển, gồm Rainmaker.Library và Rainmaker.Axis.

## 1. Đọc trước khi làm việc

Khi bắt đầu một yêu cầu phát triển add-in, đọc file này và `README.md` của add-in trong `Tools/dotnet/_Development`. Xác định kho đang kết nối qua `warehouse.local.json` của máy hiện tại; không dùng đường dẫn ổ đĩa của máy kia.

Khi tạo mới hoặc thiết kế giao diện, đọc thêm `docs/ADDIN-UI-STANDARD.md`: dùng giao diện Axis làm chuẩn cả màu, bố cục và điều khiển. Đây là yêu cầu của dự án, không phải chỉ gợi ý thẩm mỹ; không tự chuyển đổi giao diện add-in cũ ngoài phạm vi được giao.

Khi add-in mới sử dụng tài nguyên kho, đọc `docs/LIBRARY-CONSUMER-STANDARD.md` và `docs/RESOURCE-IDENTIFIERS.md`. Trong đầu vào và nghiệm thu phải chỉ rõ vai trò tài nguyên, cấu hình theo Manager ID, cách nạp lại sau xóa/gộp, chính sách khi nguồn chưa hoàn tất cập nhật và giới hạn kiểm tra phụ thuộc. Chỉ sửa tài liệu không làm các add-in cũ tự có các khả năng này.

Mã nguồn chính nằm trong repo Git AI-CAD-MCP. Thư mục phát triển trong kho dùng để lưu gói DLL, thông tin build, ghi chú và bằng chứng kiểm thử theo từng add-in/phiên bản. Không tạo một bản source chỉnh sửa độc lập ngoài Git. Nếu đang làm dở, phải bàn giao commit hoặc bản vá cùng commit gốc để có thể tái tạo build.

Đọc thêm `docs/TEST-ARTIFACT-LIFECYCLE.md` về vòng đời dữ liệu kiểm thử. Sau mỗi phiên phải giữ bằng chứng cần thiết, xác minh bản bàn giao và dọn dữ liệu tạm của phiên, kèm thống kê dung lượng. Profile AutoCAD/Host thử nghiệm dùng thư mục cục bộ `.tools/test-runs/<run-id>`, không sao chép hàng loạt vào kho đồng bộ.

## 2. Ba nơi có vai trò khác nhau

| Nơi lưu | Vai trò |
|---|---|
| Repo `src/<TênAddin>` và các project phụ thuộc | Mã nguồn có quản lý Git, phát triển và kiểm thử |
| `Tools/dotnet/_Development/<TênAddin>` | Khu phát triển và lưu phiên bản của add-in đó |
| `Tools/dotnet/_Installed/<TênAddin>/<version>-<fingerprint>` | Bản đã nhập, do CAD Manager quản lý; không chỉnh sửa thủ công |

`Tools/catalog.json` là nguồn xác định gói mà Manager đang chọn. Dùng `RelativePath`, `Enabled`, `DeletedUtc`, `FileHash` và `PackageHash` để đối chiếu; không chọn theo tên thư mục, ngày sửa hoặc chỉ theo số version.

Việc một DLL xuất hiện trong thư mục không có nghĩa app đang dùng nó. Một phiên AutoCAD đang mở có thể còn giữ bản DLL đã nạp trước đó; khởi động lại AutoCAD để xác nhận bản mới.

## 3. Cấu trúc bắt buộc

```text
Tools/dotnet/
  QUY_TRINH_PHAT_TRIEN_ADDIN.md
  PHIEN_BAN_DANG_SU_DUNG.md       # ảnh chụp đối chiếu có ngày/revision, không thay catalog
  AGENTS.md
  _Development/
    Rainmaker.Library/
      README.md
      working/                  # build tạm; không nhập vào Manager
      versions/
        1.3.1/
          build-<bundle-fingerprint-12>/
            package/            # DLL chính và toàn bộ DLL/PDB đi kèm
            manifest.json       # version, nguồn, dấu vân tay từng file, kiểm thử
            NOTES.md            # nội dung thay đổi, hướng dẫn cập nhật/hoàn tác
            source.patch        # nếu nguồn chưa commit; kèm commit gốc trong manifest
            validation/         # kết quả kiểm thử, ảnh nếu cần
    Rainmaker.Axis/
      README.md
      working/
      versions/
        1.2.0/
          build-<bundle-fingerprint-12>/...
  _Installed/                  # chỉ Manager ghi; một gói/add-in
    Rainmaker.Library/1.3.0-<fingerprint>/...
    Rainmaker.Axis/1.2.0-<fingerprint>/...
```

Add-in mới tạo thư mục `_Development/<TênAssembly>` với cùng cấu trúc. Các mẫu thư viện DWG vẫn thuộc `CAD/Originals`; không bỏ DWG công trình vào gói DLL.

## 4. Thực hiện mỗi yêu cầu

1. **Đối chiếu đầu vào.** Đọc `Tools/catalog.json`, xác định đúng tool ID, gói đang chọn, version DLL thực tế và file hash. Đọc README, manifest và ghi chú của add-in. Ghi mục tiêu sửa, phiên bản làm nền và điều kiện nghiệm thu.
2. **Sửa mã trong Git.** Dùng checkout trên máy hiện tại. Giữ nguyên những thay đổi không thuộc yêu cầu; không nhân bản source rời vào nhiều thư mục. Thay đổi code phải nằm trong các project của repo.
3. **Đặt phiên bản.** Sửa lỗi tăng patch, thêm tính năng tương thích tăng minor, thay đổi gây mất tương thích tăng major. Bản đã bàn giao không được tái sử dụng cùng số version với nội dung khác. Khi còn thử nhiều build của một version, tách bằng build ID/fingerprint và ghi rõ chưa phát hành.
4. **Build tập trung.** Publish vào một thư mục mới trong `working/` của đúng add-in, không trả DLL ra Desktop, Temp hoặc thư mục tùy ý. Ví dụ dưới đây. `bin/obj` trong repo chỉ là tệp trung gian tự sinh, không phải gói bàn giao.
5. **Kiểm thử.** Chạy kiểm thử liên quan; với lỗi dữ liệu phải tái hiện được lỗi cũ và chứng minh bản mới khắc phục. Kiểm tra nạp qua Manager, mở bảng, đọc/lưu/mở lại DWG thử và thao tác liên quan. Không dùng DWG công trình thật làm fixture. Ghi những gì chưa kiểm tra, không báo đạt khi chưa chạy.
6. **Chốt gói.** Tính dấu vân tay gói và từng file, đưa gói đạt yêu cầu vào `versions/<version>/build-<fingerprint>/`. Không ghi đè thư mục đã chốt; nếu đã có, so hash và tái sử dụng hoặc tạo build mới. Kèm manifest, NOTES, kết quả kiểm thử và commit nguồn; nguồn bẩn phải có patch hoặc commit bàn giao.
7. **Báo kết quả.** Chỉ trả một đường dẫn chuẩn tới build trong khu phát triển, nêu version, sửa gì, kiểm thử gì, tình trạng đã/chưa được Manager chọn. Nếu cần ZIP, đặt trong chính thư mục build đó, ngoài `package/`. Không tạo thêm một bản ZIP ở nơi khác.
8. **Kích hoạt khi được yêu cầu.** Khi người dùng yêu cầu cập nhật bản chạy, dùng **Cập nhật file** trên đúng mục add-in hiện có trong CAD Manager, chọn DLL chính từ `package/`; không thêm mục mới trùng tên. Manager tự lưu lịch sử vào _Development, nhập/kiểm tra gói mới, cập nhật catalog rồi dọn bản cũ trong _Installed. Không tự copy DLL sang _Installed. Nếu báo gói cũ bị khóa, đóng AutoCAD trên hai máy rồi chạy lại chuyển cấu trúc để dọn. Kiểm tra lại catalog và chức năng sau cập nhật.

Ví dụ publish từ thư mục repo (đổi add-in và mã phiên làm việc cho đúng yêu cầu):

```powershell
$warehouse = (Get-Content ./warehouse.local.json -Raw | ConvertFrom-Json).path
$buildDir = Join-Path $warehouse 'Tools/dotnet/_Development/Rainmaker.Library/working/<ma-phien-moi>'
if (Test-Path -LiteralPath $buildDir) { throw 'Chon thu muc build moi; khong ghi de.' }
$sdk = if (Test-Path ./.tools/dotnet/dotnet.exe) { './.tools/dotnet/dotnet.exe' } else { 'dotnet' }
& $sdk publish ./src/Rainmaker.Library/Rainmaker.Library.csproj -c Release -o (Join-Path $buildDir 'package')
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
```

Thư mục `working` phải được rà soát và dọn sau khi build/bằng chứng đã được lưu và kiểm tra, theo `docs/TEST-ARTIFACT-LIFECYCLE.md`; chỉ dọn dữ liệu tạm thuộc phiên công việc đó. Không tự xóa bản cũ đã bàn giao, gói đang chọn hoặc nguồn chưa bàn giao. Nếu đang chờ cài hoặc bị khóa, ghi rõ thư mục còn giữ và lý do thay vì để tích lũy không ghi nhận.

## 5. Manifest tối thiểu

- Tên add-in, tool ID (nếu đã cài), version DLL và thời điểm UTC.
- Repo nguồn, commit gốc, trạng thái source sạch/bẩn/không xác định; patch khi cần. Chuỗi ProductVersion trong DLL chỉ là thông tin build, không chứng minh source sạch.
- AutoCAD mục tiêu (2021), framework/kiến trúc (.NET Framework 4.8, x64).
- Mỗi file trong `package`: tên, dung lượng, SHA-256.
- Bundle fingerprint và thuật toán; kết quả kiểm thử và giới hạn xác minh.
- Trạng thái tại lúc tạo manifest: ứng viên, đã kiểm thử, hoặc ảnh chụp gói catalog đang chọn. Đây không phải con trỏ kích hoạt app.

Fingerprint của khu phát triển được tính SHA-256 trên các dòng `<tên-file><TAB><SHA256-thường><LF>`, sắp tên file theo OrdinalIgnoreCase. Fingerprint này khác `PackageHash` của Manager; không tự dùng nó để sửa catalog.

## 6. Các quy tắc giữ an toàn và nhất quán

- Không ghi DLL, README, ZIP hoặc manifest vào thư mục hash đã nhập của Manager. Manager kiểm tra toàn bộ nội dung gói; thêm tệp cũng có thể làm sai hash.
- Giữ các dependency đúng gói. Không chép một DLL JSON bất kỳ vào thư mục cài AutoCAD để chữa lỗi.
- Không sửa tay `Tools/catalog.json` để chuyển bản; dùng Manager để giữ ID, nhóm, tab và các ghim lệnh.
- Không mặc định chọn version lớn nhất hay ngày mới nhất. Có thể có nhiều build cùng version với nội dung khác; dùng đường dẫn catalog và hash.
- Không tự kích hoạt, xóa gói cũ, đổi nhóm/tab hoặc bố trí lại DWG khi nhiệm vụ chỉ là phát triển và bàn giao gói.
- Nếu lỗi phát hiện ở app và add-in dùng chung một DLL (ví dụ scanner), ghi rõ cả hai thành phần cần rebuild/cập nhật. Không hứa sửa cả hai nếu mới thay một gói.

## 7. Chuyển máy và khôi phục

### Một phiên bản chung cho hai máy — Manager 0.8.0 trở lên

Hai máy dùng chung `Tools/catalog.json` và `_Installed`, mỗi add-in chỉ có một gói được chọn. Mỗi lần cập nhật phải thực hiện từ **một máy duy nhất**. Máy kia đóng AutoCAD; không để hai máy cập nhật cùng lúc. Máy cập nhật dùng Manager, sau đó đóng/mở lại AutoCAD nếu DLL cũ đã được nạp. Chờ dịch vụ đồng bộ hoàn tất cả catalog lẫn DLL trước khi máy kia mở CAD.

Manager có khóa file chống ghi đồng thời trên cùng hệ thống tệp và kiểm tra SHA-256 của toàn bộ các gói đang bật trước khi nạp. Khi danh mục đã đến nhưng DLL chưa đến đủ, Manager từ chối chạy. Nếu xóa bản cũ bị khóa, Manager báo rõ bản mới đã được chọn và yêu cầu đóng CAD rồi dọn lại; không báo thành công giả.

**Thư mục đồng bộ qua cloud là các bản sao độc lập:** khóa file không phải khóa phân tán giữa hai máy và không phát hiện được máy ngoại tuyến còn giữ nguyên một bộ phiên bản cũ. Vì vậy không coi kiểm tra hash là xác nhận cloud đã tải bản mới nhất; người dùng phải chờ đồng bộ hoàn tất trước khi sử dụng máy còn lại. Phần mềm không tự thay thế dịch vụ đồng bộ Drive/OneDrive.

Trước khi chuyển kho lần đầu, cập nhật Manager/Host/MCP trên cả hai máy. Catalog định dạng mới có `StorageVersion=2`; client cũ phải cập nhật, không được tiếp tục ghi catalog. Trên một máy, khi cả hai máy đã đóng AutoCAD, chạy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Install-CadRibbonManager.ps1 -Build -MigrateInstalled
```

Máy còn lại cài Manager mới với `-Build` (không cần chuyển kho lần nữa), chờ kho đồng bộ rồi mở AutoCAD. Script chuyển cấu trúc giữ nguyên version/ID/nhóm/tab của từng add-in; không tự chọn Library 1.3.1 chỉ vì số version lớn hơn. Lịch sử catalog được lưu trong `Tools/_InstallBackups`; lịch sử DLL ở `_Development`.

Có thể kiểm tra kho trên mỗi máy mà không mở AutoCAD:

```powershell
dotnet run --project tools/Addin.Storage -c Release -- verify --app-root .
```

Nếu cần dọn lại gói cũ đang khóa hoặc chạy chuyển cấu trúc riêng, đóng AutoCAD rồi dùng `migrate` thay cho `verify`. Chỉ một máy chạy thao tác ghi này. Không dùng công cụ cũ sửa trực tiếp cấu trúc `_Installed`.

1. Commit/push mã nguồn theo yêu cầu, hoặc bàn giao patch rõ ràng nếu chưa commit. Máy kia pull đúng commit trước khi sửa tiếp.
2. Đồng bộ kho riêng, gồm `Tools/catalog.json`, các gói Manager đang tham chiếu, `_Development` và các thư viện DWG/danh mục liên quan. Dừng thao tác ghi trên cả hai máy trong lúc đồng bộ và kiểm tra đã sao chép đủ trước khi mở CAD. Không chạy hai máy cùng ghi catalog hoặc cùng một DWG.
3. Không chuyển token, session, `warehouse.local.json`, đường dẫn tuyệt đối hay runtime-cache giữa hai máy. Mỗi máy chọn đường dẫn kho của mình và build lại thành phần máy cần dùng.
4. Đọc file quy trình ở đầu thư mục dotnet, đối chiếu catalog/hash rồi tiếp tục. Cập nhật `PHIEN_BAN_DANG_SU_DUNG.md` khi đã kiểm tra lại; file này chỉ là báo cáo theo thời điểm.
5. Muốn quay lại bản trước: chọn **Cập nhật file** của đúng add-in và DLL từ build trước đã lưu, mở lại AutoCAD rồi kiểm tra. Không đổi tên hoặc di chuyển các thư mục hash để ép chuyển bản.

## 8. Hiện trạng lúc lập quy trình

Theo catalog revision 79 kiểm tra ngày 19/11/2026: Library đang chọn 1.3.0, Axis đang chọn 1.2.0. Library 1.3.1 đã có trong thư mục gói tải về nhưng chưa được catalog chọn. Các build khởi đầu trong `_Development` được sao chép để đối chiếu, không đổi gói đang chạy. Xem báo cáo cùng cấp và manifest từng build để biết hash cụ thể.

Các gói đang được catalog tham chiếu luôn được giữ nguyên. Khi người dùng yêu cầu dọn, chỉ xóa gói không được tham chiếu sau khi sao lưu và kiểm tra hash; bản mới chưa kích hoạt phải được giữ trong `_Development`. Các bản tải về trùng trong `LibraryPackage` có thể bỏ khi đã đối chiếu đủ tệp với bản chuẩn. Từ công việc tiếp theo chỉ bàn giao vào khu `_Development` theo quy trình này.
