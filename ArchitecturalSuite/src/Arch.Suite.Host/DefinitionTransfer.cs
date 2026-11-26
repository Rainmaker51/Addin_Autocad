using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.DatabaseServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rainmaker.Library.Reader;
using App=Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Arch.Suite.Host
{
    internal sealed class ResourceBinding
    {
        public string SourceId {get;set;}
        public string Kind {get;set;}
        public string Id {get;set;}
        internal ResourceKeyV1 Key()=>new ResourceKeyV1(SourceId,Kind,Id);
        internal static ResourceBinding From(LayerRole role)=>new ResourceBinding{SourceId=role.SourceId,Kind=role.Kind,Id=role.Id};
    }
    internal sealed class FloorTemplate
    {
        public string Name {get;set;}
        public double ElevationMm {get;set;}
        public double HeightMm {get;set;}
        public double BeamMm {get;set;}
    }
    internal sealed class DefinitionBundle
    {
        public string Format {get;set;}="ArchitecturalSuite.Definitions";
        public int Version {get;set;}=1;
        public WallDefinition Wall {get;set;}
        public List<DoorDefinition> Doors {get;set;}=new List<DoorDefinition>();
        public List<LayerGroupDefinition> Groups {get;set;}=new List<LayerGroupDefinition>();
        public FrameProfile Profile {get;set;}
        public List<FloorTemplate> Floors {get;set;}=new List<FloorTemplate>();
        public Dictionary<string,ResourceBinding> Resources {get;set;}=new Dictionary<string,ResourceBinding>();
    }
    internal sealed class DefinitionImportRow
    {
        public bool Use {get;set;}
        public string Section {get;set;}
        public string Key {get;set;}
        public string State {get;set;}
        public string Detail {get;set;}
        public bool CanApply {get;set;}=true;
        [JsonIgnore] internal Action<FrameDocument> Apply;
    }
    internal static class DefinitionTransfer
    {
        internal static T Copy<T>(T value)=>JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        private static string DoorKey(DoorDefinition door)=>(door.BlockName??"").Trim()+"/"+(door.TypeCode??"").Trim();
        private static string RoleKind(string role)=>role=="wall"||role=="door"?"layers":new[]{"blocks","hatches","texts","dims"}.Contains(role)?role:null;
        internal static DefinitionBundle Export(FrameDocument data,Model model,IEnumerable<string> sections)
        {
            var selected=new HashSet<string>(sections);
            var result=new DefinitionBundle();
            if(selected.Contains("Tường"))result.Wall=Copy(data.WallRule);
            result.Doors=Copy(data.DoorRules.Where(d=>selected.Contains(d.Kind)).ToList());
            result.Groups=Copy(data.LayerGroups.Where(g=>selected.Contains(g.Kind)).ToList());
            if(selected.Contains("Profile khung"))
            {result.Profile=Copy(data.Profile);if(result.Profile!=null&&!selected.Contains("Tầng"))result.Profile.Level=null;}
            if(selected.Contains("Tầng"))result.Floors=data.Floors.Select(f=>new FloorTemplate{Name=f.Name,ElevationMm=f.ElevationMm,HeightMm=f.HeightMm,BeamMm=f.BeamMm}).ToList();
            if(selected.Contains("Tài nguyên kho"))
            {
                var roles=new Dictionary<string,LayerRole>(model.Styles);roles["wall"]=model.WallLayer;roles["door"]=model.DoorLayer;
                foreach(var item in roles.Where(p=>p.Value?.Id!=null))result.Resources[item.Key]=ResourceBinding.From(item.Value);
                foreach(var item in data.PendingLibraryBindings??new Dictionary<string,ResourceBinding>())result.Resources[item.Key]=Copy(item.Value);
            }
            return result;
        }
        internal static void Write(string path,DefinitionBundle bundle)
        {
            if(Path.GetExtension(path).ToLowerInvariant()!=".json")throw new InvalidOperationException("Chọn tệp .json cho bộ định nghĩa.");
            var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllText(temporary,JsonConvert.SerializeObject(bundle,Formatting.Indented),new UTF8Encoding(false));
                if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        internal static DefinitionBundle Read(string path)
        {
            if(new FileInfo(path).Length>2*1024*1024)throw new InvalidOperationException("Bộ định nghĩa vượt 2 MB.");
            using(var input=File.OpenText(path))using(var reader=new JsonTextReader(input){MaxDepth=48,DateParseHandling=DateParseHandling.None})
            {
                var json=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
                if((string)json["Format"]!="ArchitecturalSuite.Definitions"||(int?)json["Version"]!=1)throw new InvalidOperationException("Sai định dạng/phiên bản bộ định nghĩa.");
                var bundle=json.ToObject<DefinitionBundle>(JsonSerializer.Create(new JsonSerializerSettings{TypeNameHandling=TypeNameHandling.None}));
                bundle.Doors=bundle.Doors??new List<DoorDefinition>();bundle.Groups=bundle.Groups??new List<LayerGroupDefinition>();bundle.Floors=bundle.Floors??new List<FloorTemplate>();bundle.Resources=bundle.Resources??new Dictionary<string,ResourceBinding>();
                return bundle;
            }
        }
        internal static List<DefinitionImportRow> Preview(Database db,Transaction tx,FrameDocument current,DefinitionBundle bundle,string appRoot=null)
        {
            var rows=new List<DefinitionImportRow>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var layers=(LayerTable)tx.GetObject(db.LayerTableId,OpenMode.ForRead);var blocks=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);
            DefinitionImportRow Add(string section,string key,bool conflict,Action<FrameDocument> apply,Action validate,Func<string> warning)
            {
                var row=new DefinitionImportRow{Section=section,Key=key,Apply=apply};rows.Add(row);
                try
                {
                    if(!seen.Add(section+"/"+key))throw new InvalidOperationException("Mục trùng trong tệp nhập.");
                    validate();row.Detail=warning();row.State=conflict?"Trùng · mặc định giữ hiện có":string.IsNullOrEmpty(row.Detail)?"Hợp lệ":"Cần kiểm tra nguồn";
                    row.Use=!conflict&&string.IsNullOrEmpty(row.Detail);
                }
                catch(Exception error){row.CanApply=false;row.Use=false;row.State="Không hợp lệ";row.Detail=error.Message;}
                return row;
            }
            if(bundle.Wall!=null)Add("Tường",bundle.Wall.Layer,current.WallRule.Confirmed,d=>d.WallRule=Copy(bundle.Wall),()=>RegistrationService.Validate(bundle.Wall,new DoorDefinition[0]),()=>layers.Has(bundle.Wall.Layer)?null:"Thiếu layer; chỉ lưu quy tắc, cần bổ sung layer trước khi đăng ký.");
            foreach(var item in bundle.Doors)
            {
                var door=item;
                Add("Cửa",door==null?"(trống)":DoorKey(door),door!=null&&current.DoorRules.Any(d=>string.Equals(DoorKey(d),DoorKey(door),StringComparison.OrdinalIgnoreCase)),d=>{d.DoorRules.RemoveAll(old=>string.Equals(DoorKey(old),DoorKey(door),StringComparison.OrdinalIgnoreCase));d.DoorRules.Add(Copy(door));},()=>{
                    if(door==null||door.NeedsKindConfirmation)throw new InvalidOperationException("Chọn/xác nhận loại cửa trước khi xuất.");
                    RegistrationService.Validate(new WallDefinition{Confirmed=true},new[]{door});
                },()=>{
                    if(!blocks.Has(door.BlockName))return "Thiếu block mẫu; lưu định nghĩa chờ có block trong DWG.";
                    var model=(BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db),OpenMode.ForRead);
                    var sample=model.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<BlockReference>().FirstOrDefault(b=>((BlockTableRecord)tx.GetObject(b.IsDynamicBlock?b.DynamicBlockTableRecord:b.BlockTableRecord,OpenMode.ForRead)).Name.Equals(door.BlockName,StringComparison.OrdinalIgnoreCase));
                    if(sample==null)return "Chưa có block chèn để kiểm tra property/ATT.";
                    var info=RegistrationService.Describe(tx,sample);var missing=new List<string>();
                    if(!info.Properties.ContainsKey(door.WidthProperty))missing.Add("property rộng "+door.WidthProperty);
                    if(!string.IsNullOrWhiteSpace(door.HeightProperty)&&!info.Properties.ContainsKey(door.HeightProperty))missing.Add("property cao "+door.HeightProperty);
                    foreach(var tag in new[]{door.CodeTag,door.HeightTag}.Where(v=>!string.IsNullOrWhiteSpace(v)))if(!info.Attributes.ContainsKey(tag))missing.Add("ATT "+tag);
                    return missing.Count==0?null:"Thiếu "+string.Join(", ",missing);
                });
            }
            foreach(var item in bundle.Groups)
            {
                var group=item;
                Add("Nhóm layer",group?.Kind??"(trống)",group!=null&&current.LayerGroups.Any(g=>g.Kind==group.Kind),d=>{d.LayerGroups.RemoveAll(g=>g.Kind==group.Kind);d.LayerGroups.Add(Copy(group));},()=>{
                    if(group==null||!new[]{"Trục","Nội thất","Thiết bị vệ sinh"}.Contains(group.Kind)||group.Layers==null||group.Layers.Count==0||group.Layers.Any(string.IsNullOrWhiteSpace))throw new InvalidOperationException("Nhóm/layer không hợp lệ.");
                },()=>{var missing=group.Layers.Where(n=>!layers.Has(n)).ToList();return missing.Count==0?null:"Thiếu layer: "+string.Join("; ",missing);});
            }
            if(bundle.Profile!=null)Add("Profile khung",bundle.Profile.BlockName,current.Profile!=null,d=>d.Profile=Copy(bundle.Profile),()=>{
                var profile=bundle.Profile;if(string.IsNullOrWhiteSpace(profile.BlockName)||string.IsNullOrWhiteSpace(profile.NameTag)||string.IsNullOrWhiteSpace(profile.BoundaryLayer)||profile.Columns==null||!profile.Columns.Any(c=>c!=null&&c.Use)||profile.Columns.Where(c=>c!=null&&c.Use).Select(c=>c.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=profile.Columns.Count(c=>c!=null&&c.Use)||profile.Columns.Any(c=>c==null||string.IsNullOrWhiteSpace(c.Tag)||string.IsNullOrWhiteSpace(c.Label)))throw new InvalidOperationException("Profile thiếu block/tag/biên hoặc cột ATT.");
                if(profile.CodeTag==profile.NameTag)throw new InvalidOperationException("Tag mã và tag tên phải khác nhau.");
            },()=>blocks.Has(bundle.Profile.BlockName)&&layers.Has(bundle.Profile.BoundaryLayer)?null:"Thiếu block hoặc layer biên; cần kiểm tra khi chọn khung mẫu.");
            foreach(var item in bundle.Floors)
            {
                var template=item;
                Add("Tầng",template?.Name?.Trim()??"(trống)",template!=null&&current.Floors.Any(f=>string.Equals(f.Name,template.Name,StringComparison.OrdinalIgnoreCase)),d=>{
                    var floor=FloorCatalog.Ensure(d,template.Name);floor.ElevationMm=template.ElevationMm;floor.HeightMm=template.HeightMm;floor.BeamMm=template.BeamMm;
                },()=>{if(template==null)throw new InvalidOperationException("Tầng trống.");FloorCatalog.ValidateEntry(new FloorDefinition{Name=template.Name,HeightMm=template.HeightMm,BeamMm=template.BeamMm,ElevationMm=template.ElevationMm});},()=>template.HeightMm==0?"Chưa khai báo chiều cao.":null);
            }
            LibrarySnapshotV1 snapshot=null;Exception libraryError=null;
            if(bundle.Resources.Count>0)try{snapshot=LibraryReader.Open(appRoot??ResourceLayers.AppRoot()).Read();}catch(Exception error){libraryError=error;}
            foreach(var item in bundle.Resources)
            {
                var role=item.Key;var binding=item.Value;
                Add("Tài nguyên kho",role,current.PendingLibraryBindings.ContainsKey(role),d=>d.PendingLibraryBindings[role]=Copy(binding),()=>{
                    if(binding==null||binding.SourceId!="ISO_QUANGANH"||binding.Kind!=RoleKind(role)||!Guid.TryParseExact(binding.Id,"N",out var _))throw new InvalidOperationException("Sai vai trò/khóa tài nguyên.");
                    if(libraryError!=null)throw libraryError;snapshot.ResolveResources(new[]{binding.Key()});
                },()=>null);
            }
            return rows;
        }
        internal static FrameDocument Apply(FrameDocument current,IEnumerable<DefinitionImportRow> rows)
        {
            var selected=rows.Where(r=>r.Use).ToList();if(selected.Count==0)throw new InvalidOperationException("Chọn ít nhất một mục để nạp.");
            if(selected.Any(r=>!r.CanApply))throw new InvalidOperationException("Có mục không hợp lệ; bỏ chọn mục đó.");
            var proposed=Copy(current);var before=Copy(current.Floors);
            foreach(var row in selected)row.Apply(proposed);
            // Preserve IDs and source links; configuration changes only mark pending work.
            var edited=proposed.Floors;proposed.Floors=before;FloorCatalog.Apply(proposed,edited);
            foreach(var source in proposed.Registrations)source.Status="Định nghĩa đã nạp; cần kiểm tra/đăng ký lại";
            foreach(var drawing in proposed.Drawings)drawing.NeedsUpdate=true;
            return proposed;
        }
        internal static IEnumerable<string> ChooseExport()
        {
            var panel=new StackPanel{Margin=new Thickness(12)};panel.Children.Add(new TextBlock{Text="Chọn phần xuất; không gồm đối tượng/handle/link công trình.",TextWrapping=TextWrapping.Wrap});
            var boxes=new List<CheckBox>();foreach(string key in SuiteKinds.All.Concat(new[]{"Profile khung","Tài nguyên kho","Tầng"}))
            {var box=new CheckBox{Content=key,IsChecked=key!="Tầng",Margin=new Thickness(0,4,0,4)};boxes.Add(box);panel.Children.Add(box);}
            var window=new Window{Title="Xuất bộ định nghĩa Arch",Content=panel,Width=420,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterScreen};
            var button=new Button{Content="Chọn nơi lưu JSON",Margin=new Thickness(0,8,0,0)};button.Click+=(s,e)=>window.DialogResult=true;panel.Children.Add(button);Host.ApplyTheme(window);
            return App.ShowModalWindow(window)==true?boxes.Where(b=>b.IsChecked==true).Select(b=>(string)b.Content).ToList():null;
        }
        internal static bool ChooseImport(List<DefinitionImportRow> rows)=>App.ShowModalWindow(ImportWindow(rows))==true;
        internal static Window ImportWindow(List<DefinitionImportRow> rows)
        {
            var grid=new Grid{Margin=new Thickness(10)};grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            grid.Children.Add(new TextBlock{Text="Mục trùng mặc định giữ hiện có. Tích mục muốn thay. Mục thiếu nguồn chỉ lưu quy tắc, không thêm/sửa hình CAD.",TextWrapping=TextWrapping.Wrap});
            var table=new DataGrid{ItemsSource=rows,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,Margin=new Thickness(0,8,0,8)};
            table.Columns.Add(new DataGridCheckBoxColumn{Header="Nạp",Binding=new System.Windows.Data.Binding("Use"),Width=50});
            foreach(var field in new[]{new[]{"Nhóm","Section"},new[]{"Mục","Key"},new[]{"Trạng thái","State"},new[]{"Chi tiết","Detail"}})table.Columns.Add(new DataGridTextColumn{Header=field[0],Binding=new System.Windows.Data.Binding(field[1]),IsReadOnly=true,Width=new DataGridLength(field[1]=="Detail"?3:2,DataGridLengthUnitType.Star)});
            Grid.SetRow(table,1);grid.Children.Add(table);
            var window=new Window{Title="Duyệt nạp bộ định nghĩa",Content=grid,Width=1000,Height=560,WindowStartupLocation=WindowStartupLocation.CenterScreen};
            var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
            var cancel=new Button{Content="Hủy",IsCancel=true,Margin=new Thickness(4)};var apply=new Button{Content="Áp dụng mục đã tích",Margin=new Thickness(4)};apply.SetResourceReference(FrameworkElement.StyleProperty,"ToolPrimaryButton");
            apply.Click+=(s,e)=>{table.CommitEdit(DataGridEditingUnit.Cell,true);table.CommitEdit(DataGridEditingUnit.Row,true);if(rows.Any(r=>r.Use&&r.CanApply)&&!rows.Any(r=>r.Use&&!r.CanApply))window.DialogResult=true;else MessageBox.Show("Chọn mục hợp lệ trước khi áp dụng.");};actions.Children.Add(cancel);actions.Children.Add(apply);Grid.SetRow(actions,2);grid.Children.Add(actions);Host.ApplyTheme(window);
            return window;
        }
    }
}
