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
        internal readonly ComboBox DefinitionMenu=new ComboBox{ItemsSource=SuiteKinds.All,SelectedIndex=0};
        internal readonly TextBox DoorTypeCode=new TextBox();
        internal readonly TextBox DoorSill=new TextBox{Text="0"};
        internal readonly TextBox GroupLayers=new TextBox();
        internal readonly DataGrid Floors=Table();
        internal readonly ComboBox FilterKind=new ComboBox{ItemsSource=new[]{"Tất cả"}.Concat(SuiteKinds.All),SelectedIndex=0,MinWidth=110};
        internal readonly ComboBox FilterFloor=new ComboBox{MinWidth=80};
        internal readonly ComboBox FilterFrame=new ComboBox{MinWidth=140};
        internal readonly TextBox BeamOverride=new TextBox{Width=100};
        internal readonly TextBox DrawingName=new TextBox();
        internal readonly TextBox DrawingCode=new TextBox();
        internal readonly DataGrid LinkedDrawings=Table();
        private readonly Dictionary<string,CheckBox> linkedKinds=new Dictionary<string,CheckBox>();
        private List<SourceRegistration> registeredRows=new List<SourceRegistration>();
        private FrameDocument currentDefinitions=new FrameDocument();
        internal string DefinitionKind=>DefinitionMenu.SelectedItem as string;
        internal List<string> LinkKinds=>linkedKinds.Where(p=>p.Value.IsChecked==true).Select(p=>p.Key).ToList();
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
        private string activeSection;
        internal readonly TextBox Level = new TextBox();
        internal readonly TextBox WallLayer = new TextBox();
        internal readonly ComboBox BoundaryLayer = new ComboBox();
        internal readonly ComboBox FrameCodeTag=new ComboBox();
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
            var title = new StackPanel(); title.Children.Add(Label("Architectural Suite 0.5.0", 18));
            document.TextTrimming=TextTrimming.CharacterEllipsis; document.TextWrapping=TextWrapping.NoWrap;
            title.Children.Add(document); heading.Children.Add(title); Children.Add(heading);
            var footer = new Grid { Margin = new Thickness(0, 5, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            saveFrames = new Button { Content="Lưu khung / setup", Margin=new Thickness(8,0,0,0) };
            saveFrames.SetResourceReference(StyleProperty,"ToolPrimaryButton");
            saveFrames.Click+=(s,e)=>request(defining?"frame-definitions-save":activeSection=="Tầng"?"frame-floor-save":activeSection=="Bản vẽ liên kết"?"frame-link-preview":BatchFrames.Count>0?"frame-save-batch":"frame-save");
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
            frame.Children.Add(Label("Tag mã bản vẽ (độc lập thứ tự ATT)"));frame.Children.Add(FrameCodeTag);
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
            scan.Children.Add(Button("Đăng ký các nhóm hợp lệ", "frame-register-all"));
            scan.Children.Add(Label("Đường hở / cắt biên được đưa vào danh sách xác nhận."));
            scan.Children.Add(Button("Đi tới mục đang chọn", "frame-locate"));
            form.Children.Add(Group("Đối tượng trong khung", scan, true));
            var setup = new StackPanel();
            setup.Children.Add(Label("Chọn và sửa cột trong tab Setup ATT bên phải."));
            setup.Children.Add(Button("Đưa cột lên", "column-up")); setup.Children.Add(Button("Đưa cột xuống", "column-down"));
            setup.Children.Add(Button("Xuất danh mục CSV", "frame-export"));
            form.Children.Add(Group("Danh mục bản vẽ", setup, false));
            tabs.Items.Add(new TabItem { Header = "Thông số", Content = Scroll(form) });
            var library = new StackPanel { Margin = new Thickness(4) };
            library.Children.Add(Label("Tài nguyên đã lưu trong DWG", 14));
            library.Children.Add(libraryInfo);library.Children.Add(libraryState);
            library.Children.Add(Button("Chọn tài nguyên CAD", "layers"));
            library.Children.Add(Button("Đọc trạng thái kho", "library-status"));
            library.Children.Add(Label("Đọc kho qua Reader V1 do Manager cung cấp. Khi kho chưa hợp lệ, dừng chọn/áp dụng và xử lý tại QA."));
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
                if(!selected.Frame.IsDerived)LinkedDrawings.SelectedItem=null;
                SelectSnapshot(selected); request("frame-select");
            };
            Frames.CellEditEnding+=(s,e)=>{
                if(e.EditAction!=DataGridEditAction.Commit||!Equals(e.Column.Header,"Tầng"))return;
                var row=e.Row.Item as FrameSnapshot;var editor=e.EditingElement as TextBox;
                if(row!=null&&editor!=null&&row.Frame.Id==SelectedId)Level.Text=editor.Text;
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
            details.Items.Add(new TabItem { Header = "Định nghĩa", Content = DefinitionForm() });
            RegisteredSources.IsReadOnly=true;
            RegisteredSources.Columns.Add(Column("Loại","Kind",1));
            RegisteredSources.Columns.Add(Column("Mã","Code",1));
            RegisteredSources.Columns.Add(Column("Tầng","Level",1));
            RegisteredSources.Columns.Add(Column("Bản vẽ nguồn","FrameName",2));
            RegisteredSources.Columns.Add(Column("Rộng · mm","WidthMm",1));
            RegisteredSources.Columns.Add(Column("Cao · mm","HeightMm",1));RegisteredSources.Columns.Add(Column("Bậu · mm","SillMm",1));
            RegisteredSources.Columns.Add(Column("Diện tích vùng · mm²","AreaMm2",1));
            RegisteredSources.Columns.Add(Column("Trạng thái","Status",3));
            var registered=new Grid();registered.RowDefinitions.Add(new RowDefinition());
            registered.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            registered.RowDefinitions.Insert(0,new RowDefinition{Height=GridLength.Auto});
            registered.Children.Add(AtRow(RegisteredSources,1));
            var filters=new WrapPanel();filters.Children.Add(Label("Loại"));filters.Children.Add(FilterKind);filters.Children.Add(Label("Tầng"));filters.Children.Add(FilterFloor);filters.Children.Add(Label("Khung"));filters.Children.Add(FilterFrame);
            registered.Children.Add(filters);
            FilterKind.SelectionChanged+=(s,e)=>FilterRegistered();FilterFloor.SelectionChanged+=(s,e)=>FilterRegistered();FilterFrame.SelectionChanged+=(s,e)=>FilterRegistered();
            RegisteredSources.SelectionChanged+=(s,e)=>{var item=RegisteredSources.SelectedItem as SourceRegistration;BeamOverride.Text=item?.BeamOverrideMm?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"";};
            var registrationActions=new WrapPanel();registrationActions.Children.Add(Button("Đi tới cấu kiện","frame-registration-locate"));
            registrationActions.Children.Add(Button("Gỡ đăng ký · giữ hình CAD","frame-registration-remove"));
            registrationActions.Children.Add(Label("Dầm riêng · mm (trống: theo tầng)"));registrationActions.Children.Add(BeamOverride);registrationActions.Children.Add(Button("Lưu dầm tường","frame-wall-beam"));
            registered.Children.Add(AtRow(registrationActions,2));
            details.Items.Add(new TabItem { Header="Đã đăng ký",Content=registered });
            details.Items.Add(new TabItem{Header="Tầng",Content=FloorForm()});
            details.Items.Add(new TabItem{Header="Bản vẽ liên kết",Content=LinksForm()});
            details.SelectionChanged+=(s,e)=>{
                if(!ReferenceEquals(e.Source,details))return;
                activeSection=(details.SelectedItem as TabItem)?.Header as string;defining=activeSection=="Định nghĩa";UpdateSaveAction();
            };
            right.Children.Add(AtRow(details, 2));
            SetColumn(right, 2); board.Children.Add(right);
            Unloaded += (s, e) => SaveLayout();
        }
        private UIElement DefinitionForm()
        {
            var container=new Grid{Margin=new Thickness(8)};
            container.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(140)});container.ColumnDefinitions.Add(new ColumnDefinition());
            var tree=new TreeView{Background=(Brush)Resources["ToolBackground"],Foreground=(Brush)Resources["ToolText"],BorderThickness=new Thickness(0),Margin=new Thickness(0,0,8,0)};
            tree.Items.Add(new TreeViewItem{Header="Tường",Tag="Tường",IsSelected=true,Foreground=(Brush)Resources["ToolText"]});
            var doors=new TreeViewItem{Header="Cửa",IsExpanded=true,Foreground=(Brush)Resources["ToolText"]};doors.Items.Add(new TreeViewItem{Header="Cửa đi",Tag="Cửa đi",Foreground=(Brush)Resources["ToolText"]});doors.Items.Add(new TreeViewItem{Header="Cửa sổ",Tag="Cửa sổ",Foreground=(Brush)Resources["ToolText"]});tree.Items.Add(doors);
            foreach(string kind in new[]{"Trục","Nội thất","Thiết bị vệ sinh"})tree.Items.Add(new TreeViewItem{Header=kind,Tag=kind,Foreground=(Brush)Resources["ToolText"]});
            tree.SelectedItemChanged+=(s,e)=>{var item=e.NewValue as TreeViewItem;if(item?.Tag is string kind)DefinitionMenu.SelectedItem=kind;};
            container.Children.Add(tree);
            var body=new StackPanel();var scroll=Scroll(body);SetColumn(scroll,1);container.Children.Add(scroll);
            Action show=()=>{
                body.Children.Clear();doorSample=null;DoorBlock.Clear();
                if(DefinitionKind=="Tường")
                {
                    body.Children.Add(Label("Polyline kín trên layer vùng tường ở bảng trái. Cao xây = cao tầng − cao dầm. Cấu hình chiều cao ở tab Tầng; ghi đè dầm từng tường ở Đã đăng ký."));
                    body.Children.Add(Label("Lưu định nghĩa để xác nhận quy tắc nhận diện tường."));
                }
                else if(DefinitionKind=="Cửa đi"||DefinitionKind=="Cửa sổ")
                {
                    body.Children.Add(Button("Chọn block cửa mẫu trên CAD","frame-door-sample"));
                    body.Children.Add(Label("Block gốc"));body.Children.Add(DoorBlock);body.Children.Add(doorValues);
                    body.Children.Add(Label("Mã loại (trống: mặc định cho block)"));body.Children.Add(DoorTypeCode);
                    body.Children.Add(Label("Tag mã cửa"));body.Children.Add(DoorCodeTag);
                    body.Children.Add(Label("Property rộng bộ cửa"));body.Children.Add(DoorWidthProperty);
                    body.Children.Add(Label("Property cao (hoặc ATT/bảng loại)"));body.Children.Add(DoorHeightProperty);
                    body.Children.Add(Label("Tag ATT cao · mm"));body.Children.Add(DoorHeightTag);
                    body.Children.Add(Label("Cao theo loại · mm"));body.Children.Add(DoorHeight);
                    body.Children.Add(Label("Bậu so với sàn tầng · mm"));body.Children.Add(DoorSill);
                    body.Children.Add(Label("Chỉ chọn một nguồn cao. Rộng property quy đổi theo scale và INSUNITS. Chưa gắn ô chờ/tường."));
                    doorRuleTable.IsReadOnly=true;doorRuleTable.Height=130;
                    if(doorRuleTable.Columns.Count==0){doorRuleTable.Columns.Add(Column("Block","BlockName",2));doorRuleTable.Columns.Add(Column("Mã loại","TypeCode",1));doorRuleTable.Columns.Add(Column("Cao mm","HeightMm",1));doorRuleTable.Columns.Add(Column("Bậu mm","SillMm",1));}
                    doorRuleTable.ItemsSource=storedDoorRules.Where(d=>d.Kind==DefinitionKind||d.NeedsKindConfirmation).ToList();body.Children.Add(doorRuleTable);
                }
                else
                {
                    body.Children.Add(Label("Các layer nhận diện (ngăn bằng dấu ;). Block xét layer chèn; không xét nét bên trong. Thiết bị vệ sinh ưu tiên hơn nội thất; cửa chuyên biệt không nhập vào nhóm layer."));
                    body.Children.Add(GroupLayers);body.Children.Add(Button("Lấy layer từ đối tượng CAD","frame-group-layer"));
                    GroupLayers.Text=string.Join("; ",currentDefinitions.LayerGroups.FirstOrDefault(g=>g.Kind==DefinitionKind)?.Layers??new List<string>());
                }
            };
            DefinitionMenu.SelectionChanged+=(s,e)=>show();show();return container;
        }
        private UIElement FloorForm()
        {
            var panel=new Grid();panel.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});panel.RowDefinitions.Add(new RowDefinition());panel.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            panel.Children.Add(Label("Đơn vị mm. Cao xây = cao tầng − cao dầm; phải xác nhận theo quy ước đo của hồ sơ. Tên tầng được dùng khi Lưu khung."));
            Floors.IsReadOnly=false;
            foreach(var spec in new[]{new[]{"Tên tầng","Name"},new[]{"Cao độ sàn","ElevationMm"},new[]{"Cao tầng","HeightMm"},new[]{"Cao dầm","BeamMm"}})
                Floors.Columns.Add(new DataGridTextColumn{Header=spec[0],Binding=new System.Windows.Data.Binding(spec[1]),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
            Floors.Columns.Add(Column("Cao xây","WallHeightMm",1));panel.Children.Add(AtRow(Floors,1));
            var actions=new WrapPanel();actions.Children.Add(Button("Thêm tầng","frame-floor-add"));actions.Children.Add(Button("Lưu tầng","frame-floor-save",true));actions.Children.Add(Button("Bỏ tầng chưa dùng","frame-floor-remove"));panel.Children.Add(AtRow(actions,2));return panel;
        }
        private UIElement LinksForm()
        {
            var form=new StackPanel{Margin=new Thickness(8)};
            form.Children.Add(Label("Chọn khung nguồn trong bảng trên để sinh; chọn dòng bản vẽ dưới để quản lý đích. Chỉ sao chép các cấu kiện đã đăng ký thuộc nhóm chọn. Hình đích không tính khối lượng."));
            form.Children.Add(Label("Tên bản vẽ đích khi sinh mới"));form.Children.Add(DrawingName);form.Children.Add(Label("Mã bản vẽ đích khi sinh mới"));form.Children.Add(DrawingCode);
            var kinds=new WrapPanel();foreach(var kind in SuiteKinds.All){var box=new CheckBox{Content=kind,Margin=new Thickness(6),IsChecked=kind=="Tường"};linkedKinds[kind]=box;kinds.Children.Add(box);}form.Children.Add(kinds);
            var actions=new WrapPanel();foreach(var spec in new[]{new[]{"Xem trước","frame-link-preview"},new[]{"Sinh bản vẽ","frame-link-create"},new[]{"Đồng bộ / lưu lựa chọn","frame-link-sync"},new[]{"Di chuyển toàn khung","frame-link-move"},new[]{"Ngắt toàn bộ · giữ hình","frame-link-unlink"}})actions.Children.Add(Button(spec[0],spec[1]));form.Children.Add(actions);
            LinkedDrawings.Height=180;LinkedDrawings.Columns.Add(Column("Tên","Name",2));LinkedDrawings.Columns.Add(Column("Mã","Code",1));LinkedDrawings.Columns.Add(Column("Khung nguồn","SourceName",2));LinkedDrawings.Columns.Add(Column("Tầng","Level",1));LinkedDrawings.Columns.Add(Column("Trạng thái","State",2));
            LinkedDrawings.SelectionChanged+=(s,e)=>{var selected=LinkedDrawings.SelectedItem as LinkedDrawing;if(selected==null)return;DrawingName.Text=selected.Name;DrawingCode.Text=selected.Code;foreach(var pair in linkedKinds)pair.Value.IsChecked=selected.Groups.Any(g=>g.Linked&&g.Kind==pair.Key);};form.Children.Add(LinkedDrawings);
            form.Children.Add(Label("Tên/mã khung đích chỉnh độc lập bằng ATT trên CAD. Các ô tên/mã phía trên dùng cho thao tác Sinh mới."));
            form.Children.Add(Label("Bỏ nhóm rồi đồng bộ: ngắt nhóm và giữ nguyên hình độc lập. Ngắt không xóa hình. Muốn liên kết lại nhóm đã ngắt, sinh khung mới để tránh chồng hình."));
            return Scroll(form);
        }
        internal List<FloorDefinition> ReadFloors()
        {
            if(!Floors.CommitEdit(DataGridEditingUnit.Cell,true)||!Floors.CommitEdit(DataGridEditingUnit.Row,true))throw new InvalidOperationException("Sửa các giá trị tầng đang báo lỗi trước khi lưu.");
            return Floors.ItemsSource as List<FloorDefinition>??new List<FloorDefinition>();
        }
        internal void SetRegistered(FrameDocument data,IEnumerable<FrameSnapshot> frames)
        {
            foreach(var source in data.Registrations)
            {
                var frame=frames.FirstOrDefault(f=>f.Frame.Id==source.FrameId);
                if(frame!=null){source.Level=frame.Frame.Level;source.FrameName=(frame.Title??frame.Frame.BlockName)+" #"+frame.Frame.Handle;}
            }
            registeredRows=data.Registrations;
            string kindSelection=FilterKind.SelectedItem as string;FilterKind.ItemsSource=new[]{"Tất cả"}.Concat(SuiteKinds.All).Concat(registeredRows.Select(r=>r.Kind)).Distinct().ToList();FilterKind.SelectedItem=kindSelection??"Tất cả";
            string floor=FilterFloor.SelectedItem as string,frameName=FilterFrame.SelectedItem as string;
            FilterFloor.ItemsSource=new[]{"Tất cả"}.Concat(registeredRows.Select(r=>r.Level).Where(v=>v!=null).Distinct()).ToList();FilterFloor.SelectedItem=floor??"Tất cả";if(FilterFloor.SelectedItem==null)FilterFloor.SelectedIndex=0;
            FilterFrame.ItemsSource=new[]{"Tất cả"}.Concat(registeredRows.Select(r=>r.FrameName).Where(v=>v!=null).Distinct()).ToList();FilterFrame.SelectedItem=frameName??"Tất cả";if(FilterFrame.SelectedItem==null)FilterFrame.SelectedIndex=0;FilterRegistered();
        }
        private void FilterRegistered()
        {
            string kind=FilterKind.SelectedItem as string,floor=FilterFloor.SelectedItem as string,frame=FilterFrame.SelectedItem as string;
            RegisteredSources.ItemsSource=registeredRows.Where(r=>(kind==null||kind=="Tất cả"||r.Kind==kind)&&(floor==null||floor=="Tất cả"||r.Level==floor)&&(frame==null||frame=="Tất cả"||r.FrameName==frame)).ToList();
        }
        internal void SetLibraryStatus(string value){libraryState.Text=value;}
        internal void SetDefinitions(FrameDocument data)
        {
            if(SelectedSnapshot==null)WallLayer.Text=data.WallRule.Layer;
            WallHeight.Text=data.WallRule.HeightMm.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if(DefinitionKind!="Tường"&&DefinitionKind!="Cửa đi"&&DefinitionKind!="Cửa sổ")GroupLayers.Text=string.Join("; ",data.LayerGroups.FirstOrDefault(g=>g.Kind==DefinitionKind)?.Layers??new List<string>());
            foreach(var drawing in data.Drawings)
            {
                var source=Frames.Items.OfType<FrameSnapshot>().FirstOrDefault(f=>f.Frame.Id==drawing.SourceFrameId);
                drawing.SourceName=source?.Title??"Thiếu khung nguồn";drawing.Level=source?.Frame.Level;
                var target=Frames.Items.OfType<FrameSnapshot>().FirstOrDefault(f=>f.Frame.Id==drawing.TargetFrameId);
                if(target!=null){drawing.Name=target.Title;drawing.Code=target.Code;}
            }
            currentDefinitions=data;Floors.ItemsSource=data.Floors;LinkedDrawings.ItemsSource=data.Drawings;
            storedDoorRules=data.DoorRules;
            doorRuleTable.ItemsSource=data.DoorRules.Where(d=>d.Kind==DefinitionKind||d.NeedsKindConfirmation).ToList();
            if(data.Profile!=null)profileInfo.Text="Profile: "+data.Profile.BlockName+" · "+data.Profile.BoundaryLayer;
            else profileInfo.Text="Chưa lưu profile khung.";
        }
        internal void ShowDoorSample(DoorSample sample)
        {
            doorSample=sample;DoorBlock.Text=sample.BlockName;
            var previous=storedDoorRules.FirstOrDefault(d=>d.BlockName==sample.BlockName&&d.Kind==DefinitionKind&&d.TypeCode==DoorTypeCode.Text.Trim())??storedDoorRules.FirstOrDefault(d=>d.BlockName==sample.BlockName&&(d.Kind==DefinitionKind||d.NeedsKindConfirmation));
            DoorTypeCode.Text=previous?.TypeCode??"";DoorSill.Text=(previous?.SillMm??0).ToString(System.Globalization.CultureInfo.InvariantCulture);
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
        internal static double Nonnegative(TextBox box,string name)
        {
            if(!double.TryParse(box.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var value)||double.IsNaN(value)||double.IsInfinity(value)||value<0)throw new InvalidOperationException(name+" cần số mm không âm.");return value;
        }
        internal WallDefinition ReadWallRule()=>new WallDefinition{Confirmed=true,Layer=WallLayer.Text.Trim(),HeightMm=Millimeters(WallHeight,"Cao tường")};
        internal DoorDefinition ReadDoorRule()
        {
            if(doorSample==null||!(DefinitionKind=="Cửa đi"||DefinitionKind=="Cửa sổ"))return null;
            return new DoorDefinition{Kind=DefinitionKind,TypeCode=DoorTypeCode.Text.Trim(),SillMm=Nonnegative(DoorSill,"Bậu cửa"),BlockName=doorSample.BlockName,WidthProperty=DoorWidthProperty.SelectedItem as string,
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
        private void UpdateSaveAction(){saveFrames.Content=defining?"Lưu định nghĩa":activeSection=="Tầng"?"Lưu tầng":activeSection=="Bản vẽ liên kết"?"Xem trước liên kết":BatchFrames.Count>0?"Lưu các khung hợp lệ":"Lưu khung / setup";}
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
            FrameCodeTag.ItemsSource=snapshot.Attributes.Keys.ToList();FrameCodeTag.SelectedItem=Draft.CodeTag;
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
            value.NameTag = NameTag.SelectedItem as string;value.CodeTag=FrameCodeTag.SelectedItem as string;
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
            NameTag.ItemsSource = null;FrameCodeTag.ItemsSource=null; Preview.Show(null); summary.Text = "Chọn block khung trên CAD để bắt đầu.";
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
