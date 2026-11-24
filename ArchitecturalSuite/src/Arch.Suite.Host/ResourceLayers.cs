using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AICad.Contracts;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using Newtonsoft.Json.Linq;
using App=Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Arch.Suite.Host
{
    internal sealed class LayerRole
    {
        public string SourceId {get;set;}
        public string Id {get;set;}
        public string Code {get;set;}
        public string LocalHandle {get;set;}
        public string Appearance {get;set;}
        public string Version {get;set;}
        public string SourceHash {get;set;}
        public string Name {get;set;}
        public string Kind {get;set;}
        public string Settings {get;set;}
        public override string ToString()=>Code+" · "+Name;
    }
    internal static class ResourceLayers
    {
        private sealed class ResourceItem
        {
            public string Id {get;set;}
            public string Kind {get;set;}
            public string Name {get;set;}
            public bool Draft {get;set;}
            public JObject Values {get;set;}
        }
        private static string AppRoot()
        {
            var file=Path.Combine(Path.GetDirectoryName(typeof(AICad.Bridge.Plugin).Assembly.Location),"..","install.json");
            if(!File.Exists(file))throw new InvalidOperationException("Mở Architectural Suite qua Manager để đọc kho.");
            var root=JObject.Parse(File.ReadAllText(file)).ToObject<InstallConfig>().ContentRoot;
            if(string.IsNullOrWhiteSpace(root))throw new InvalidOperationException("Manager chưa cấu hình kho.");
            return root;
        }
        internal static string PendingStatus(string root)
        {
            var journal=Path.Combine(root,"Catalog","qa-transactions","ISO_QUANGANH.pending.json");
            if(File.Exists(journal))
            {
                var item=JObject.Parse(File.ReadAllText(journal));
                return "QA có thao tác chờ: "+(string)item["operation"]+" · "+(string)item["phase"]+". DWG nguồn và nội dung chờ cần được QA đối chiếu.";
            }
            if(File.Exists(Path.Combine(root,"Catalog","sync","pending.json")))return "Kho còn phiên đồng bộ danh mục chưa hoàn tất.";
            return null;
        }
        internal static string CurrentStatus()
        {
            return PendingStatus(WarehousePaths.Resolve(AppRoot()))??"Kho không có giao dịch chờ. Có thể chọn tài nguyên đã công bố.";
        }
        internal static string Appearance(Transaction tx,LayerTableRecord layer)
        {
            var lt=(LinetypeTableRecord)tx.GetObject(layer.LinetypeObjectId,OpenMode.ForRead);
            return string.Join("|",layer.Color.ColorMethod,layer.Color.ColorIndex,layer.Color.Red,layer.Color.Green,layer.Color.Blue,layer.LineWeight,layer.IsPlottable,lt.Name,lt.PatternLength,string.Join(",",Enumerable.Range(0,lt.NumDashes).Select(i=>lt.DashLengthAt(i).ToString("R",System.Globalization.CultureInfo.InvariantCulture))));
        }
        internal static string Demand(Database db,Transaction tx,LayerRole role)
        {
            if(role==null)throw new InvalidOperationException("Chọn bộ layer cho tường và cửa trước khi sinh mặt bằng.");
            ObjectId id;
            if(!db.TryGetObjectId(new Handle(Convert.ToInt64(role.LocalHandle,16)),out id)||id.IsErased)throw new InvalidOperationException("Thiếu layer "+role.Code+". Chọn lại bộ layer.");
            var layer=tx.GetObject(id,OpenMode.ForRead) as LayerTableRecord;
            if(layer==null || Appearance(tx,layer)!=role.Appearance || layer.IsLocked || layer.IsFrozen || layer.IsOff)throw new InvalidOperationException("Layer "+role.Code+" đã thay đổi, bị khóa hoặc đang ẩn.");
            return layer.Name;
        }
        internal static void Configure(Document doc,Transaction tx,Model model)
        {
            var appRoot=AppRoot();
            var pending=PendingStatus(WarehousePaths.Resolve(appRoot));
            var resources=ReadOptions(appRoot,true);
            var options=resources.Where(r=>r.Kind=="layers").ToList();
            var wall=new ComboBox{ItemsSource=options,IsTextSearchEnabled=true,MinWidth=300};
            var door=new ComboBox{ItemsSource=options,IsTextSearchEnabled=true};
            wall.SelectedItem=options.FirstOrDefault(o=>o.Id==model.WallLayer?.Id);
            door.SelectedItem=options.FirstOrDefault(o=>o.Id==model.DoorLayer?.Id);
            var panel=new StackPanel{Margin=new Thickness(12)};
            if(pending!=null)panel.Children.Add(new TextBlock{Text=pending+"\nĐang xem bản công bố trước. Hoàn tất đồng bộ trong QA trước khi áp dụng.",TextWrapping=TextWrapping.Wrap,MaxWidth=460,Margin=new Thickness(0,0,0,8)});
            panel.Children.Add(new TextBlock{Text="Nét tường và đường chuẩn"});panel.Children.Add(wall);
            panel.Children.Add(new TextBlock{Text="Ký hiệu định vị cửa",Margin=new Thickness(0,8,0,0)});panel.Children.Add(door);
            var additional=new Dictionary<string,ComboBox>();
            foreach(var spec in new[]{new[]{"blocks","Block ký hiệu cửa"},new[]{"dims","Dim định vị"},new[]{"texts","Chữ mã cửa"},new[]{"hatches","Hatch tường"}}){
                var choices=resources.Where(r=>r.Kind==spec[0]).ToList();
                choices.Insert(0,new LayerRole{Name="Không dùng"});
                var combo=new ComboBox{ItemsSource=choices,IsTextSearchEnabled=true};
                LayerRole prior;if(model.Styles.TryGetValue(spec[0],out prior))combo.SelectedItem=choices.FirstOrDefault(r=>r.Id==prior.Id);
                if(combo.SelectedItem==null)combo.SelectedIndex=0;
                panel.Children.Add(new TextBlock{Text=spec[1]+" (tùy chọn)",Margin=new Thickness(0,6,0,0)});panel.Children.Add(combo);additional[spec[0]]=combo;
            }
            var win=new Window{Title="Tài nguyên từ kho · ISO_QUANGANH",Content=panel,SizeToContent=SizeToContent.WidthAndHeight,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};
            var ok=new Button{Content="Áp dụng bộ layer",Margin=new Thickness(0,10,0,0),IsDefault=true,MinHeight=28};
            ok.IsEnabled=pending==null;
            ok.Click+=(s,e)=>{if(wall.SelectedItem!=null && door.SelectedItem!=null)win.DialogResult=true;};panel.Children.Add(ok);
            Host.ApplyTheme(win);
            if(App.ShowModalWindow(win)!=true)return;
            ApplyResources(doc,tx,model,appRoot,(LayerRole)wall.SelectedItem,(LayerRole)door.SelectedItem,
                additional.ToDictionary(i=>i.Key,i=>i.Value.SelectedItem as LayerRole));
        }
        internal static List<LayerRole> ReadOptions(string appRoot,bool allowPendingRead=false)
        {
            var root=WarehousePaths.Resolve(appRoot);
            var pending=PendingStatus(root);
            if(pending!=null && !allowPendingRead)throw new InvalidOperationException(pending+" Hoàn tất đồng bộ trong QA trước khi áp dụng.");
            var state=JObject.Parse(File.ReadAllText(Path.Combine(root,"Catalog","sync","state.json")))["Items"].ToObject<List<ResourceItem>>();
            var codes=new LibraryResourceCodes(appRoot).Read().Items;
            return (from code in codes where code.SourceId=="ISO_QUANGANH"
                         join item in state.Where(v=>!v.Draft) on new {code.Id,code.Kind} equals new {item.Id,item.Kind}
                         orderby code.Code select new LayerRole{Code=code.Code,Id=code.Id,SourceId=code.SourceId,Name=item.Name,Kind=item.Kind,Settings=item.Values.ToString()}).ToList();
        }
        internal static void ApplyResources(Document doc,Transaction tx,Model model,string appRoot,LayerRole wall,LayerRole door,Dictionary<string,LayerRole> additional)
        {
            var root=WarehousePaths.Resolve(appRoot);
            var pending=PendingStatus(root);
            if(pending!=null)throw new InvalidOperationException(pending+" Chưa thể áp dụng tài nguyên.");
            var folder=Path.Combine(root,"Styles","ISO_QUANGANH");
            var active=JObject.Parse(File.ReadAllText(Path.Combine(folder,"active.json")));
            var version=ContentStore.VersionName((string)active["version"]);var catalog=Path.Combine(folder,version,"catalog.json");
            if(AICad.Contracts.Json.FileHash(catalog)!=(string)active["checksum"])throw new InvalidOperationException("Hash danh mục không khớp.");
            var hash=(string)JObject.Parse(File.ReadAllText(catalog))["sourceHash"];
            if(!System.Text.RegularExpressions.Regex.IsMatch(hash??"","^[0-9a-f]{64}$"))throw new InvalidOperationException("Hash DWG không hợp lệ.");
            var file=Path.Combine(root,"CAD","Assets",hash+".dwg");
            if(AICad.Contracts.Json.FileHash(file)!=hash)throw new InvalidOperationException("Hash DWG nguồn không khớp.");
            using(var source=new Database(false,true)){
                source.ReadDwgFile(file,FileOpenMode.OpenForReadAndAllShare,false,null);
                using(var read=source.TransactionManager.StartOpenCloseTransaction()){
                    var src=(LayerTable)read.GetObject(source.LayerTableId,OpenMode.ForRead);
                    foreach(var role in new[]{wall,door}.Distinct()){
                        new LibraryCatalog(appRoot).Available("layers",AICad.Contracts.Json.Hash(new[]{"ISO_QUANGANH",role.Name}));
                        if(!src.Has(role.Name))throw new InvalidOperationException("Thiếu layer nguồn "+role.Code);
                        var record=(LayerTableRecord)read.GetObject(src[role.Name],OpenMode.ForRead);
                        role.Appearance=Appearance(read,record);role.Version=version;role.SourceHash=hash;
                        var dest=(LayerTable)tx.GetObject(doc.Database.LayerTableId,OpenMode.ForRead);
                        if(!dest.Has(role.Name)){
                            var ids=new ObjectIdCollection{record.ObjectId};using(var mapping=new IdMapping())
                                source.WblockCloneObjects(ids,doc.Database.LayerTableId,mapping,DuplicateRecordCloning.Ignore,false);
                        }
                        role.LocalHandle=dest[role.Name].Handle.ToString();Demand(doc.Database,tx,role);
                    }
                    foreach(var choice in additional){
                        var role=choice.Value;if(role?.Id==null){model.Styles.Remove(choice.Key);continue;}
                        LayerRole previous;
                        if(model.Styles.TryGetValue(choice.Key,out previous) && previous.Id==role.Id && previous.SourceHash==hash)continue;
                        role.Version=version;role.SourceHash=hash;
                        if(role.Kind!="hatches"){
                            ObjectId owner=role.Kind=="blocks"?source.BlockTableId:role.Kind=="dims"?source.DimStyleTableId:source.TextStyleTableId;
                            ObjectId target=role.Kind=="blocks"?doc.Database.BlockTableId:role.Kind=="dims"?doc.Database.DimStyleTableId:doc.Database.TextStyleTableId;
                            var symbols=(SymbolTable)read.GetObject(owner,OpenMode.ForRead);
                            if(!symbols.Has(role.Name))throw new InvalidOperationException("Thiếu định nghĩa "+role.Code+" trong DWG kho.");
                            var srcId=symbols[role.Name];
                            using(var map=new IdMapping()){
                                source.WblockCloneObjects(new ObjectIdCollection{srcId},target,map,DuplicateRecordCloning.MangleName,false);
                                role.LocalHandle=map[srcId].Value.Handle.ToString();
                            }
                        }
                        model.Styles[choice.Key]=role;
                    }
                }
            }
            model.WallLayer=wall;model.DoorLayer=door;model.Save(tx,doc.Database);
            Host.ShowResult("Tường "+model.WallLayer+"\nCửa "+model.DoorLayer+"\nNguồn ISO_QUANGANH · "+version);
        }
    }
}

