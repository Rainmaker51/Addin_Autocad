using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Documents;
using System.Windows.Media;
using Newtonsoft.Json;

namespace Arch.Suite.Host
{
    internal sealed class SuitePanel : Grid
    {
        private readonly Action<string> request;
        internal readonly DataGrid Frames = Table();
        internal readonly DataGrid Candidates = Table();
        internal readonly DataGrid Attributes = Table();
        internal readonly DataGrid LegacyParts = Table();
        internal readonly DataGrid LegacyCandidates = Table();
        internal readonly DataGrid RegisteredSources = Table();
        internal readonly TextBox WallHeight = new TextBox { Text = "3300" };
        internal readonly TextBox DoorHeight = new TextBox { Text = "2200" };
        internal readonly TextBox DoorBlock = new TextBox { IsReadOnly = true };
        internal readonly ComboBox DoorWidthProperty = new ComboBox();
        internal readonly ComboBox DoorHeightProperty = new ComboBox();
        internal readonly ComboBox DoorHeightTag = new ComboBox();
        internal readonly ComboBox DoorCodeTag = new ComboBox();
        internal List<FrameSnapshot> BatchFrames { get; private set; } = new List<FrameSnapshot>();
        private List<DoorDefinition> storedDoorRules = new List<DoorDefinition>();
        private DoorSample doorSample;
        private readonly TextBlock profileInfo = Label("Chọn khung mẫu và lưu profile để quét nhiều khung.");
        private readonly DataGrid doorRuleTable = Table();
        private readonly TextBlock doorValues = Label("");
        private Button saveFrames;
        private bool defining;
        internal readonly TextBox Level = new TextBox();
        internal readonly TextBox WallLayer = new TextBox();
        internal readonly ComboBox BoundaryLayer = new ComboBox();
        internal readonly ComboBox NameTag = new ComboBox();
        internal readonly TextBlock Status = Label("");
        internal readonly FramePreview Preview = new FramePreview();
        private readonly TextBlock document = Label("");
        private readonly TextBlock summary = Label("");
        private readonly TextBlock libraryInfo = Label("");
        private readonly TextBlock libraryState=Label("");
        private readonly DataGrid directory = Table();
        private readonly ColumnDefinition leftColumn = new ColumnDefinition { Width = new GridLength(220), MinWidth = 200 };
        private bool updating;
        internal FrameSnapshot SelectedSnapshot { get; private set; }
        internal DrawingFrame Draft { get; private set; }
        internal string SelectedId { get; private set; }
        internal List<FrameColumn> DraftColumns { get; private set; } = new List<FrameColumn>();
        private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArchitecturalSuite", "palette-layout.json");

        internal SuitePanel(Action<string> request)
        {
            this.request = request;
            FontResources();
            UseLayoutRounding = true;
            TextElement.SetFontFamily(this, new FontFamily("Segoe UI"));
            TextElement.SetFontSize(this, 12);
            TextElement.SetForeground(this, (Brush)Resources["ToolText"]);
            Background = (Brush)Resources["ToolBackground"];
            Margin = new Thickness(0);
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition());
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var heading = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            heading.ColumnDefinitions.Add(new ColumnDefinition());
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var topButtons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, MaxWidth = 360 };
            topButtons.Children.Add(Button("Chọn khung mẫu", "frame-pick"));
            topButtons.Children.Add(Button("Quét chọn khung", "frame-batch"));
            topButtons.Children.Add(Button("Đọc lại DWG", "frame-refresh"));
            SetColumn(topButtons, 1); heading.Children.Add(topButtons);
            var title = new StackPanel(); title.Children.Add(Label("Architectural Suite 0.4.0", 18));
            document.TextTrimming=TextTrimming.CharacterEllipsis; document.TextWrapping=TextWrapping.NoWrap;
            title.Children.Add(document); heading.Children.Add(title); Children.Add(heading);
            var footer = new Grid { Margin = new Thickness(0, 5, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            saveFrames = new Button { Content="Lưu khung / setup", Margin=new Thickness(8,0,0,0) };
            saveFrames.SetResourceReference(StyleProperty,"ToolPrimaryButton");
            saveFrames.Click+=(s,e)=>request(defining ? "frame-definitions-save" : BatchFrames.Count>0 ? "frame-save-batch" : "frame-save");
            SetColumn(saveFrames,1); footer.Children.Add(saveFrames);
            Status.SetBinding(ToolTipProperty,new System.Windows.Data.Binding("Text"){Source=Status});
            Status.MaxHeight = 60; footer.Children.Add(Status); SetRow(footer, 2); Children.Add(footer);
            var board = new Grid(); board.ColumnDefinitions.Add(leftColumn);
            board.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            board.ColumnDefinitions.Add(new ColumnDefinition());
            SetRow(board, 1); Children.Add(board);
            ReadLayout();
            board.SizeChanged += (s, e) =>
            {
                leftColumn.MaxWidth = Math.Max(200, Math.Min(600, e.NewSize.Width - 350));
                if (leftColumn.Width.Value > leftColumn.MaxWidth)
                    leftColumn.Width = new GridLength(leftColumn.MaxWidth);
            };
            var splitter = new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch,
                ResizeDirection = GridResizeDirection.Columns, ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                Background = new SolidColorBrush(Color.FromRgb(53, 71, 93)) };
            splitter.DragCompleted += (s, e) => SaveLayout();
            SetColumn(splitter, 1); board.Children.Add(splitter);
            var tabs = new TabControl();
            var form = new StackPanel { Margin = new Thickness(4) };
            var frame = new StackPanel();
            frame.Children.Add(Label("Tên tầng")); frame.Children.Add(Level);
            frame.Children.Add(Label("Tag tên bản vẽ")); frame.Children.Add(NameTag);
            frame.Children.Add(Label("Layer biên trong block")); frame.Children.Add(BoundaryLayer);
            frame.Children.Add(Button("Kiểm tra biên", "frame-check"));
            frame.Children.Add(Button("Lưu profile khung", "frame-profile"));
            frame.Children.Add(profileInfo);
            frame.Children.Add(Button("Áp tầng cho tập quét", "frame-batch-level"));
            Level.TextChanged+=(s,e)=>{
                if(!updating && SelectedSnapshot!=null && BatchFrames.Any(f=>f.Frame.Id==SelectedSnapshot.Frame.Id))
                    SelectedSnapshot.Frame.Level=Level.Text.Trim();
            };
            form.Children.Add(Group("Khung và phạm vi", frame, true));
            var scan = new StackPanel();
            scan.Children.Add(Label("Layer vùng tường")); scan.Children.Add(WallLayer);
            scan.Children.Add(Button("Quét trong khung", "frame-scan"));
            scan.Children.Add(Button("Đăng ký tường hợp lệ", "frame-register-walls"));
            scan.Children.Add(Button("Đăng ký cửa đã định nghĩa", "frame-register-doors"));
            scan.Children.Add(Label("Đường hở / cắt biên được đưa vào danh sách xác nhận."));
            scan.Children.Add(Button("Đi tới mục đang chọn", "frame-locate"));
            form.Children.Add(Group("Đối tượng trong khung", scan, true));
            var setup = new StackPanel();
            setup.Children.Add(Label("Chọn và sửa cột trong tab Setup ATT bên phải."));
            setup.Children.Add(Button("Đưa cột lên", "column-up")); setup.Children.Add(Button("Đưa cột xuống", "column-down"));
            setup.Children.Add(Button("Xuất danh mục CSV", "frame-export"));
            form.Children.Add(Group("Danh mục bản vẽ", setup, false));
            var legacy = new StackPanel();
            foreach (var item in new[] {
                new[]{"region","Vùng nguồn bản 0.2"},new[]{"wall","Đăng ký 2 Line (0.2)"},
                new[]{"door","Đăng ký cửa (0.2)"},new[]{"inspect","Tra quan hệ (0.2)"},
                new[]{"edit","Sửa kích thước (0.2)"},new[]{"layout","Đặt 2 mặt bằng (0.2)"},
                new[]{"preview","Xem trước (0.2)"},new[]{"apply","Duyệt cập nhật (0.2)"},
                new[]{"walltable","Thống kê tường (0.2)"},new[]{"doortable","Thống kê cửa (0.2)"},
                new[]{"csv","Xuất khối lượng (0.2)"},new[]{"report","Báo cáo mô hình"},
                new[]{"repair","Xử lý xung đột (0.2)"},new[]{"index","Dựng lại chỉ mục"},
                new[]{"audit","Nhật ký dữ liệu"}}) legacy.Children.Add(Button(item[1], item[0]));
            legacy.Children.Insert(0, Label("Luồng cũ dùng dữ liệu 0.2 riêng; kết quả quét khung mới chưa đăng ký vào luồng này."));
            form.Children.Add(Group("Công cụ dữ liệu 0.2", legacy, false));
            tabs.Items.Add(new TabItem { Header = "Thông số", Content = Scroll(form) });
            var library = new StackPanel { Margin = new Thickness(4) };
            library.Children.Add(Label("Tài nguyên đã lưu trong DWG", 14));
            library.Children.Add(libraryInfo);library.Children.Add(libraryState);
            library.Children.Add(Button("Chọn tài nguyên CAD", "layers"));
            library.Children.Add(Button("Đọc trạng thái kho", "library-status"));
            library.Children.Add(Label("Khi QA còn giao dịch chờ, có thể xem lựa chọn. Chỉ áp dụng tài nguyên khi kho đã hoàn tất."));
            tabs.Items.Add(new TabItem { Header = "Thư viện", Content = Scroll(library) });
            board.Children.Add(tabs);

            var right = new Grid { Margin = new Thickness(7, 0, 0, 0), MinWidth = 330 };
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(115) });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition());
            Frames.IsReadOnly = false;
            Frames.Columns.Add(Column("Tên bản vẽ", "Title", 2));
            Frames.Columns.Add(Column("Mã", "Code", 1));
            Frames.Columns.Add(new DataGridTextColumn { Header="Tầng",Binding=new System.Windows.Data.Binding("Frame.Level"),
                Width=new DataGridLength(1,DataGridLengthUnitType.Star) });
            Frames.Columns.Add(Column("Trạng thái", "Status", 2));
            Frames.SelectionChanged += (s, e) =>
            {
                if (updating) return;
                var selected = Frames.SelectedItem as FrameSnapshot;
                if (selected == null) return;
                SelectSnapshot(selected); request("frame-select");
            };
            right.Children.Add(Frames);
            right.Children.Add(AtRow(summary, 1));
            var details = new TabControl();
            var view = new Grid();
            view.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star), MinHeight = 180 });
            view.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            view.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 95 });
            view.Children.Add(Preview);
            var tools = new WrapPanel();
            var fit = new Button { Content = "Vừa khung", Margin = new Thickness(0, 3, 6, 3) };
            fit.Click += (s, e) => Preview.Fit(); tools.Children.Add(fit);
            tools.Children.Add(Label("Cam: hiện trạng · Vàng: cắt biên · Xanh: biên quét. Lăn chuột zoom, chuột giữa pan."));
            view.Children.Add(AtRow(tools, 1));
            Candidates.Columns.Add(Column("Nhóm", "Kind", 1));
            Candidates.Columns.Add(Column("Handle", "Handle", 1));
            Candidates.Columns.Add(Column("Layer", "Layer", 2));
            Candidates.Columns.Add(Column("Trạng thái", "Status", 1));
            Candidates.Columns.Add(Column("Đăng ký", "Registered", 1));
            Candidates.Columns.Add(Column("Nhận xét", "Reason", 3));
            Candidates.IsReadOnly = true;
            Candidates.MouseDoubleClick += (s, e) => request("frame-locate");
            view.Children.Add(AtRow(Candidates, 2));
            details.Items.Add(new TabItem { Header = "Phạm vi / đối tượng", Content = view });
            Attributes.IsReadOnly = false;
            Attributes.Columns.Add(new DataGridCheckBoxColumn { Header = "Dùng", Binding = new System.Windows.Data.Binding("Use"), Width = 55 });
            Attributes.Columns.Add(new DataGridTextColumn { Header = "Tag ATT", IsReadOnly = true, Binding = new System.Windows.Data.Binding("Tag"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            Attributes.Columns.Add(new DataGridTextColumn { Header = "Tên cột danh mục", Binding = new System.Windows.Data.Binding("Label"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            details.Items.Add(new TabItem { Header = "Setup ATT", Content = Attributes });
            directory.AutoGenerateColumns = true; directory.IsReadOnly = true;
            details.Items.Add(new TabItem { Header = "Danh mục", Content = directory });
            details.Items.Add(new TabItem { Header = "Định nghĩa tường / cửa", Content = Scroll(DefinitionForm()) });
            RegisteredSources.IsReadOnly=true;
            RegisteredSources.Columns.Add(Column("Loại","Kind",1));
            RegisteredSources.Columns.Add(Column("Mã","Code",1));
            RegisteredSources.Columns.Add(Column("Handle","Handle",1));
            RegisteredSources.Columns.Add(Column("Rộng · mm","WidthMm",1));
            RegisteredSources.Columns.Add(Column("Cao · mm","HeightMm",1));
            RegisteredSources.Columns.Add(Column("Diện tích vùng · mm²","AreaMm2",1));
            RegisteredSources.Columns.Add(Column("Trạng thái","Status",3));
            var registered=new Grid();registered.RowDefinitions.Add(new RowDefinition());
            registered.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});registered.Children.Add(RegisteredSources);
            var registrationActions=new WrapPanel();registrationActions.Children.Add(Button("Đi tới cấu kiện","frame-registration-locate"));
            registrationActions.Children.Add(Button("Gỡ đăng ký · giữ hình CAD","frame-registration-remove"));
            registered.Children.Add(AtRow(registrationActions,1));
            details.Items.Add(new TabItem { Header="Đã đăng ký",Content=registered });
            var oldData = new Grid();
            oldData.RowDefinitions.Add(new RowDefinition());
            oldData.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            oldData.RowDefinitions.Add(new RowDefinition());
            oldData.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            LegacyParts.IsReadOnly = true;
            LegacyParts.Columns.Add(Column("Mã", "Code", 1));
            LegacyParts.Columns.Add(Column("Tầng", "Level", 1));
            LegacyParts.Columns.Add(Column("Loại", "Kind", 1));
            LegacyParts.Columns.Add(Column("Cao · mm", "Height", 1));
            LegacyCandidates.IsReadOnly = true;
            LegacyCandidates.Columns.Add(Column("Nhóm", "Kind", 1));
            LegacyCandidates.Columns.Add(Column("Layer", "Layer", 1));
            LegacyCandidates.Columns.Add(Column("Lý do", "Evidence", 3));
            oldData.Children.Add(LegacyParts);
            var oldActions = new WrapPanel();
            var oldFind = new Button { Content = "Đi tới cấu kiện", Margin = new Thickness(0,3,6,3) };
            oldFind.Click += (s,e) => { var part = LegacyParts.SelectedItem as Part; if (part != null) request("zoom/" + part.Id); };
            var oldRemove = new Button { Content = "Gỡ đăng ký", Margin = new Thickness(0,3,6,3) };
            oldRemove.Click += (s,e) => { var part = LegacyParts.SelectedItem as Part; if (part != null) request("remove/" + part.Id); };
            oldActions.Children.Add(oldFind); oldActions.Children.Add(oldRemove);
            oldData.Children.Add(AtRow(oldActions, 1));
            oldData.Children.Add(AtRow(LegacyCandidates, 2));
            var candidateActions = new WrapPanel();
            candidateActions.Children.Add(Button("Tìm gợi ý 0.2", "candidates"));
            var accept = new Button { Content = "Đăng ký mục chọn", Margin = new Thickness(0,3,6,3) };
            accept.Click += (s,e) =>
            {
                var candidate = LegacyCandidates.SelectedItem as Candidates.Candidate;
                if (candidate != null) request("accept/" + string.Join(",", candidate.Handles));
            };
            candidateActions.Children.Add(accept);
            oldData.Children.Add(AtRow(candidateActions, 3));
            details.Items.Add(new TabItem { Header = "Dữ liệu 0.2", Content = oldData });
            details.SelectionChanged+=(s,e)=>{
                if(!ReferenceEquals(e.Source,details))return;
                defining=Equals((details.SelectedItem as TabItem)?.Header,"Định nghĩa tường / cửa");UpdateSaveAction();
            };
            right.Children.Add(AtRow(details, 2));
            SetColumn(right, 2); board.Children.Add(right);
            Unloaded += (s, e) => SaveLayout();
        }
        private UIElement DefinitionForm()
        {
            var form=new StackPanel{Margin=new Thickness(8)};
            var wall=new StackPanel();
            wall.Children.Add(Label("Tường: Polyline kín cạnh thẳng trên layer vùng tường ở bảng trái.",14));
            wall.Children.Add(Label("Lưu định nghĩa để xác nhận chiều cao trước khi đăng ký."));
            wall.Children.Add(Label("Chiều cao tường · mm"));wall.Children.Add(WallHeight);
            wall.Children.Add(Label("Đăng ký giữ toàn bộ biên gốc và chiều cao. Đường hở/cắt biên cần xử lý trước. Phần tường trên cửa được tính khi bổ sung quan hệ ô chờ."));
            form.Children.Add(Group("Định nghĩa tường",wall,true));
            var door=new StackPanel();
            door.Children.Add(Button("Chọn block cửa mẫu trên CAD","frame-door-sample"));
            door.Children.Add(Label("Tên block gốc"));door.Children.Add(DoorBlock);
            door.Children.Add(doorValues);
            door.Children.Add(Label("Property chiều rộng bộ cửa"));door.Children.Add(DoorWidthProperty);
            door.Children.Add(Label("Property chiều cao (để trống nếu dùng ATT / bảng loại)"));door.Children.Add(DoorHeightProperty);
            door.Children.Add(Label("Tag ATT chiều cao · mm (để trống nếu dùng property / bảng loại)"));door.Children.Add(DoorHeightTag);
            door.Children.Add(Label("Chiều cao theo bảng loại · mm"));door.Children.Add(DoorHeight);
            door.Children.Add(Label("Tag mã cửa (tùy chọn)"));door.Children.Add(DoorCodeTag);
            door.Children.Add(Label("Property rộng/cao được quy đổi theo scale block và đơn vị DWG. Giá trị ATT cao và chiều cao bảng loại nhập bằng mm. Xác nhận property rộng đang là rộng bộ cửa trước khi lưu."));
            door.Children.Add(Label("Đăng ký bộ cửa ở bước này chưa tự gắn tường hoặc ngắt hình. Các quy tắc neo và ô chờ tiếp tục duyệt riêng."));
            form.Children.Add(Group("Định nghĩa cửa dynamic",door,true));
            doorRuleTable.IsReadOnly=true;doorRuleTable.Height=125;
            doorRuleTable.Columns.Add(Column("Block","BlockName",2));
            doorRuleTable.Columns.Add(Column("Property rộng","WidthProperty",1));
            doorRuleTable.Columns.Add(Column("Property cao","HeightProperty",1));
            doorRuleTable.Columns.Add(Column("ATT cao","HeightTag",1));
            doorRuleTable.Columns.Add(Column("Cao loại · mm","HeightMm",1));
            doorRuleTable.Columns.Add(Column("ATT mã","CodeTag",1));
            form.Children.Add(Group("Các loại cửa đã lưu (chọn lại block mẫu để sửa)",doorRuleTable,true));
            form.Children.Add(Label("Bấm Lưu định nghĩa ở góc dưới để ghi thiết lập tường và loại cửa đang chọn vào DWG."));
            return form;
        }
        internal void SetLibraryStatus(string value){libraryState.Text=value;}
        internal void SetDefinitions(FrameDocument data)
        {
            if(SelectedSnapshot==null)WallLayer.Text=data.WallRule.Layer;
            WallHeight.Text=data.WallRule.HeightMm.ToString(System.Globalization.CultureInfo.InvariantCulture);
            storedDoorRules=data.DoorRules;
            doorRuleTable.ItemsSource=data.DoorRules;
            if(data.Profile!=null)profileInfo.Text="Profile: "+data.Profile.BlockName+" · "+data.Profile.BoundaryLayer;
            else profileInfo.Text="Chưa lưu profile khung.";
        }
        internal void ShowDoorSample(DoorSample sample)
        {
            doorSample=sample;DoorBlock.Text=sample.BlockName;
            var previous=storedDoorRules.SingleOrDefault(d=>d.BlockName==sample.BlockName);
            DoorWidthProperty.ItemsSource=sample.Properties.Keys.ToList();
            DoorWidthProperty.SelectedItem=previous?.WidthProperty;
            DoorHeightProperty.ItemsSource=new[]{""}.Concat(sample.Properties.Keys).ToList();
            DoorHeightProperty.SelectedItem=previous?.HeightProperty??"";
            DoorHeightTag.ItemsSource=new[]{""}.Concat(sample.Attributes.Keys).ToList();
            DoorHeightTag.SelectedItem=previous?.HeightTag??"";
            DoorCodeTag.ItemsSource=new[]{""}.Concat(sample.Attributes.Keys).ToList();
            DoorCodeTag.SelectedItem=previous?.CodeTag??"";
            DoorHeight.Text=(previous?.HeightMm??2200).ToString(System.Globalization.CultureInfo.InvariantCulture);
            doorValues.Text=string.Join(" · ",sample.Properties.Select(p=>p.Key+" = "+p.Value.ToString("0.###")));
            if(sample.Properties.Count==0)doorValues.Text="Block chưa có property số dương để ánh xạ chiều rộng.";
        }
        private static double Millimeters(TextBox box,string name)
        {
            double value;
            if(!double.TryParse(box.Text,System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,out value)||value<=0||double.IsNaN(value)||double.IsInfinity(value))
                throw new InvalidOperationException(name+" cần là số mm dương.");
            return value;
        }
        internal WallDefinition ReadWallRule()=>new WallDefinition{Confirmed=true,Layer=WallLayer.Text.Trim(),HeightMm=Millimeters(WallHeight,"Cao tường")};
        internal DoorDefinition ReadDoorRule()
        {
            if(doorSample==null)return null;
            return new DoorDefinition{BlockName=doorSample.BlockName,WidthProperty=DoorWidthProperty.SelectedItem as string,
                HeightProperty=DoorHeightProperty.SelectedItem as string,HeightTag=DoorHeightTag.SelectedItem as string,
                CodeTag=DoorCodeTag.SelectedItem as string,HeightMm=Millimeters(DoorHeight,"Cao cửa theo loại")};
        }
        internal void ShowBatch(string name,List<FrameSnapshot> saved,List<FrameSnapshot> batch,Model legacy,FrameDocument data)
        {
            BatchFrames=batch;
            SetData(name,saved.Where(f=>!batch.Any(b=>b.Frame.Handle==f.Frame.Handle)).Concat(batch).ToList(),legacy);
            BatchFrames=batch;
            UpdateSaveAction();
            SetDefinitions(data);
        }
        internal void CommitFrameEdits()
        {
            Frames.CommitEdit(DataGridEditingUnit.Cell,true);
            Frames.CommitEdit(DataGridEditingUnit.Row,true);
        }
        internal void ApplyBatchLevel()
        {
            if(BatchFrames.Count==0)throw new InvalidOperationException("Quét chọn khung trước.");
            if(string.IsNullOrWhiteSpace(Level.Text))throw new InvalidOperationException("Nhập tầng cần áp dụng.");
            foreach(var item in BatchFrames)item.Frame.Level=Level.Text.Trim();
            Frames.Items.Refresh();Status.Text="Đã áp tầng cho "+BatchFrames.Count+" khung nháp. Bấm Lưu các khung hợp lệ để ghi vào DWG.";
        }
        private void UpdateSaveAction(){saveFrames.Content=defining?"Lưu định nghĩa":BatchFrames.Count>0?"Lưu các khung hợp lệ":"Lưu khung / setup";}
        internal void ClearBatch(){BatchFrames.Clear();UpdateSaveAction();}
        internal void FlushColumns()
        {
            Attributes.CommitEdit(DataGridEditingUnit.Cell, true);
            Attributes.CommitEdit(DataGridEditingUnit.Row, true);
        }
        internal void MoveColumn(int direction)
        {
            FlushColumns();
            var column = Attributes.SelectedItem as FrameColumn;
            int i = DraftColumns.IndexOf(column), target = i + direction;
            if (i < 0 || target < 0 || target >= DraftColumns.Count) return;
            DraftColumns.RemoveAt(i); DraftColumns.Insert(target, column);
            Attributes.ItemsSource = null; Attributes.ItemsSource = DraftColumns; Attributes.SelectedItem = column;
        }
        internal void SelectSnapshot(FrameSnapshot snapshot)
        {
            SelectedSnapshot = snapshot; SelectedId = snapshot.Frame.Id;
            Draft = JsonConvert.DeserializeObject<DrawingFrame>(JsonConvert.SerializeObject(snapshot.Frame));
            Level.Text = Draft.Level ?? "";
            WallLayer.Text = Draft.WallLayer;
            BoundaryLayer.ItemsSource = snapshot.BoundaryLayers; BoundaryLayer.SelectedItem = Draft.BoundaryLayer;
            NameTag.ItemsSource = snapshot.Attributes.Keys.ToList();
            NameTag.SelectedItem = Draft.NameTag;
            DraftColumns = Draft.Columns.Select(c => new FrameColumn { Use = c.Use, Tag = c.Tag, Label = c.Label }).ToList();
            foreach (string tag in snapshot.Attributes.Keys)
                if (!DraftColumns.Any(c => c.Tag == tag)) DraftColumns.Add(new FrameColumn { Tag = tag, Label = tag });
            Attributes.ItemsSource = DraftColumns;
            Candidates.ItemsSource = snapshot.Candidates;
            Preview.Show(snapshot);
            summary.Text = snapshot.Frame.BlockName + " · " + snapshot.Status + " · " + snapshot.Candidates.Count + " đối tượng";
        }
        internal DrawingFrame ReadDraft()
        {
            if (Draft == null) throw new InvalidOperationException("Chọn block khung trước.");
            FlushColumns();
            var value = JsonConvert.DeserializeObject<DrawingFrame>(JsonConvert.SerializeObject(Draft));
            value.Level = Level.Text.Trim();
            value.NameTag = NameTag.SelectedItem as string;
            value.BoundaryLayer = BoundaryLayer.SelectedItem as string;
            value.WallLayer = WallLayer.Text.Trim();
            value.Columns = DraftColumns.Select(c => new FrameColumn { Use = c.Use, Tag = c.Tag, Label = c.Label?.Trim() }).ToList();
            return value;
        }
        internal void SetData(string documentName, List<FrameSnapshot> frames, Model model, string selectId = null)
        {
            updating = true;
            try
            {
                try{SetLibraryStatus(ResourceLayers.CurrentStatus());}
                catch(System.Exception error){SetLibraryStatus("Không đọc được kho: "+error.Message);}
                document.Text = Path.GetFileName(documentName);
                Frames.ItemsSource = frames;
                LegacyParts.ItemsSource = model.Parts.OrderBy(p => p.Level).ThenBy(p => p.Code).ToList();
                var selected = frames.FirstOrDefault(f => f.Frame.Id == (selectId ?? SelectedId)) ?? frames.FirstOrDefault();
                Frames.SelectedItem = selected;
                if (selected != null) SelectSnapshot(selected);
                else ClearSelection();
                var table = new DataTable();
                table.Columns.Add("Tầng");
                foreach (string label in frames.SelectMany(f => f.Frame.Columns).Where(c => c.Use).Select(c => c.Label).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
                    if (!table.Columns.Contains(label)) table.Columns.Add(label);
                foreach (var frame in frames)
                {
                    var row = table.NewRow(); row["Tầng"] = frame.Frame.Level ?? "";
                    foreach (var column in frame.Frame.Columns.Where(c => c.Use && !string.IsNullOrWhiteSpace(c.Label)))
                        row[column.Label] = FrameReader.Attribute(frame, column.Tag);
                    table.Rows.Add(row);
                }
                directory.ItemsSource = table.DefaultView;
                libraryInfo.Text = string.Join("\n", new[] { model.WallLayer, model.DoorLayer }
                    .Concat(model.Styles.Values).Where(r => r != null).Select(r => r.Code + " · " + r.Name));
                if (libraryInfo.Text.Length == 0) libraryInfo.Text = "Chưa có bộ tài nguyên trong DWG.";
            }
            finally { updating = false; }
        }
        internal void ClearSelection()
        {
            Draft = null; SelectedId = null; SelectedSnapshot = null;
            doorSample=null;DoorBlock.Clear();DoorWidthProperty.ItemsSource=null;DoorHeightProperty.ItemsSource=null;
            DoorHeightTag.ItemsSource=null;DoorCodeTag.ItemsSource=null;doorValues.Text="";
            storedDoorRules=new List<DoorDefinition>();RegisteredSources.ItemsSource=null;
            DraftColumns = new List<FrameColumn>();
            Attributes.ItemsSource = null; Candidates.ItemsSource = null;
            Level.Clear(); BoundaryLayer.ItemsSource = null; BoundaryLayer.Text = "";
            NameTag.ItemsSource = null; Preview.Show(null); summary.Text = "Chọn block khung trên CAD để bắt đầu.";
        }
        private void FontResources()
        {
            var name = GetType().Assembly.GetManifestResourceNames().Single(n => n.EndsWith(".CadToolTheme.xaml"));
            using (var stream = GetType().Assembly.GetManifestResourceStream(name))
                Resources = (ResourceDictionary)XamlReader.Load(stream);
            Resources[typeof(TextBox)] = Compact(typeof(TextBox));
            Resources[typeof(ComboBox)] = Compact(typeof(ComboBox));
            Resources[typeof(Button)] = Compact(typeof(Button));
            var gridStyle = new Style(typeof(DataGrid), (Style)Resources[typeof(DataGrid)]);
            gridStyle.Setters.Add(new Setter(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto));
            Resources[typeof(DataGrid)] = gridStyle;
        }
        private Style Compact(Type type)
        {
            var style = new Style(type, (Style)Resources[type]);
            style.Setters.Add(new Setter(Control.MinHeightProperty, 28d));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 12d));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 3, 6, 3)));
            return style;
        }
        private static DataGrid Table() => new DataGrid
        {
            AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false,
            RowHeight = 27, FontSize = 12, SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow, Margin = new Thickness(0, 0, 0, 4)
        };
        private static DataGridTextColumn Column(string title, string path, int width) => new DataGridTextColumn
        {
            IsReadOnly=true,
            Header = title, Binding = new System.Windows.Data.Binding(path),
            Width = new DataGridLength(width, DataGridLengthUnitType.Star)
        };
        private Button Button(string title, string command, bool primary = false)
        {
            var button = new Button { Content = title, Margin = new Thickness(0, 3, 4, 2), HorizontalAlignment = HorizontalAlignment.Stretch };
            if (primary) button.SetResourceReference(StyleProperty, "ToolPrimaryButton");
            button.Click += (s, e) => request(command);
            return button;
        }
        private static TextBlock Label(string text, double size = 12) => new TextBlock
        {
            Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 3)
        };
        private static Expander Group(string title, UIElement content, bool open) =>
            new Expander { Header = title, Content = content, IsExpanded = open };
        private static ScrollViewer Scroll(UIElement content) => new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        private static UIElement AtRow(UIElement element, int row) { SetRow(element, row); return element; }
        private void ReadLayout()
        {
            try
            {
                if (!File.Exists(settingsPath)) return;
                double value = JsonConvert.DeserializeObject<double>(File.ReadAllText(settingsPath));
                if (!double.IsNaN(value) && !double.IsInfinity(value) && value >= 200 && value <= 600) leftColumn.Width = new GridLength(value);
            }
            catch (System.Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException) { }
        }
        private void SaveLayout()
        {
            try
            {
                double width = Math.Max(200, Math.Min(600, leftColumn.ActualWidth));
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                File.WriteAllText(settingsPath, JsonConvert.SerializeObject(width));
            }
            catch (System.Exception error) when (error is IOException || error is UnauthorizedAccessException) { }
        }
    }
}
