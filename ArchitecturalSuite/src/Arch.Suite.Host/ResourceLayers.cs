using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AICad.Contracts;
using Rainmaker.Library.Reader;
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
        public string SourceFile {get;set;}
        public string ReaderRevision {get;set;}
        public bool IsUnavailable {get;set;}
        public override string ToString()=>Code+" · "+Name;
    }
    internal static class ResourceLayers
    {
        internal static string AppRoot()
        {
            var file=Path.Combine(Path.GetDirectoryName(typeof(AICad.Bridge.Plugin).Assembly.Location),"..","install.json");
            if(!File.Exists(file))throw new InvalidOperationException("Mở Architectural Suite qua Manager để đọc kho.");
            var root=JObject.Parse(File.ReadAllText(file)).ToObject<InstallConfig>().ContentRoot;
            if(string.IsNullOrWhiteSpace(root))throw new InvalidOperationException("Manager chưa cấu hình kho.");
            return root;
        }
        internal static string CurrentStatus()
        {
            try { var snapshot=LibraryReader.Open(AppRoot()).Read();return "Reader V1 · "+snapshot.ListResources().Count+" tài nguyên sẵn dùng · "+snapshot.Revision; }
            catch(LibraryReadException error){return error.Code+" · "+error.Message;}
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
            string pending=null;
            var resources=ReadOptions(appRoot);
            var preferences=LibraryPreferences.Load(appRoot);
            var frameData=FrameDocument.Load(doc.Database,tx);
            var bindings=(JObject)preferences["Bindings"];
            foreach(var item in frameData.PendingLibraryBindings)bindings[item.Key]=JObject.FromObject(item.Value);
            var options=resources.Where(r=>r.Kind=="layers").ToList();
            var wall=new ComboBox{ItemsSource=options,IsTextSearchEnabled=true,MinWidth=300};
            var door=new ComboBox{ItemsSource=options,IsTextSearchEnabled=true};
            wall.SelectedItem=frameData.PendingLibraryBindings.ContainsKey("wall")?LibraryPreferences.Resolve(preferences,"wall",options):options.FirstOrDefault(o=>o.Id==model.WallLayer?.Id)??(model.WallLayer==null?LibraryPreferences.Resolve(preferences,"wall",options):null);
            door.SelectedItem=frameData.PendingLibraryBindings.ContainsKey("door")?LibraryPreferences.Resolve(preferences,"door",options):options.FirstOrDefault(o=>o.Id==model.DoorLayer?.Id)??(model.DoorLayer==null?LibraryPreferences.Resolve(preferences,"door",options):null);
            LayerRole Pending(string role,List<LayerRole> choices)
            {
                var resolved=LibraryPreferences.Resolve(preferences,role,choices);if(resolved!=null)return resolved;
                var binding=frameData.PendingLibraryBindings[role];var missing=new LayerRole{SourceId=binding.SourceId,Kind=binding.Kind,Id=binding.Id,Code="Không khả dụng",Name=role+" · "+binding.Id,IsUnavailable=true};choices.Add(missing);return missing;
            }
            if(frameData.PendingLibraryBindings.ContainsKey("wall")&&wall.SelectedItem==null){wall.SelectedItem=Pending("wall",options);wall.Items.Refresh();}
            if(frameData.PendingLibraryBindings.ContainsKey("door")&&door.SelectedItem==null){door.SelectedItem=Pending("door",options);door.Items.Refresh();}
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
                if(frameData.PendingLibraryBindings.ContainsKey(spec[0]))combo.SelectedItem=Pending(spec[0],choices);
                if(combo.SelectedItem==null&&!model.Styles.ContainsKey(spec[0]))combo.SelectedItem=LibraryPreferences.Resolve(preferences,spec[0],resources);
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
            var roles=additional.ToDictionary(i=>i.Key,i=>i.Value.SelectedItem as LayerRole);roles["wall"]=(LayerRole)wall.SelectedItem;roles["door"]=(LayerRole)door.SelectedItem;
            LibraryPreferences.Save(appRoot,(int)preferences["Revision"],roles);
            frameData.PendingLibraryBindings.Clear();frameData.Save(doc.Database,tx);
        }
        internal static List<LayerRole> ReadOptions(string appRoot,bool allowPendingRead=false)
        {
            var snapshot=LibraryReader.Open(appRoot).Read();
            return snapshot.ListResources().Select(r=>new LayerRole{SourceId=r.Key.SourceId,Kind=r.Key.Kind,Id=r.Key.Id,Code=r.Code,Name=r.Name,
                Settings=r.ParametersJsonV1,Version=r.Version,SourceHash=r.SourceHash,SourceFile=r.SourceFile,ReaderRevision=snapshot.Revision}).ToList();
        }
        internal static void ApplyResources(Document doc,Transaction tx,Model model,string appRoot,LayerRole wall,LayerRole door,Dictionary<string,LayerRole> additional)
        {
            var snapshot=LibraryReader.Open(appRoot).Read();
            var selected=new[]{wall,door}.Concat(additional.Values).Where(r=>r?.Id!=null).ToList();
            if(selected.Any(r=>r.IsUnavailable))throw new InvalidOperationException("Chọn lại tài nguyên đang báo Không khả dụng trước khi áp dụng.");
            if(selected.Any(r=>r.ReaderRevision!=snapshot.Revision))throw new InvalidOperationException("Kho đã thay đổi. Mở lại bộ chọn tài nguyên.");
            var resolved=snapshot.ResolveResources(selected.Select(r=>new ResourceKeyV1(r.SourceId,r.Kind,r.Id)));
            foreach(var role in selected)
            {
                var resource=resolved.First(r=>r.Key.SourceId==role.SourceId&&r.Key.Kind==role.Kind&&r.Key.Id==role.Id);
                role.Name=resource.Name;role.Version=resource.Version;role.SourceHash=resource.SourceHash;role.SourceFile=resource.SourceFile;role.Settings=resource.ParametersJsonV1;
            }
            var files=selected.Where(r=>r.Kind!="hatches").Select(r=>r.SourceFile).Distinct().ToList();
            // Each kind can be exported from a different verified asset.
            foreach(var file in files)
            using(var source=new Database(false,true)){
                source.ReadDwgFile(file,FileOpenMode.OpenForReadAndAllShare,false,null);
                using(var read=source.TransactionManager.StartOpenCloseTransaction()){
                    var src=(LayerTable)read.GetObject(source.LayerTableId,OpenMode.ForRead);
                    foreach(var role in new[]{wall,door}.Where(r=>r.SourceFile==file).Distinct()){
                        if(!src.Has(role.Name))throw new InvalidOperationException("Thiếu layer nguồn "+role.Code);
                        var record=(LayerTableRecord)read.GetObject(src[role.Name],OpenMode.ForRead);
                        role.Appearance=Appearance(read,record);
                        var dest=(LayerTable)tx.GetObject(doc.Database.LayerTableId,OpenMode.ForRead);
                        if(!dest.Has(role.Name)){
                            var ids=new ObjectIdCollection{record.ObjectId};using(var mapping=new IdMapping())
                                source.WblockCloneObjects(ids,doc.Database.LayerTableId,mapping,DuplicateRecordCloning.Ignore,false);
                        }
                        role.LocalHandle=dest[role.Name].Handle.ToString();Demand(doc.Database,tx,role);
                    }
                    foreach(var choice in additional){
                        var role=choice.Value;if(role?.Id==null){model.Styles.Remove(choice.Key);continue;}
                        if(role.SourceFile!=file)continue;
                        LayerRole previous;
                        if(model.Styles.TryGetValue(choice.Key,out previous) && previous.Id==role.Id && previous.SourceHash==role.SourceHash)continue;
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
            foreach(var choice in additional.Where(p=>p.Value?.Kind=="hatches"))model.Styles[choice.Key]=choice.Value;
            snapshot.ResolveResources(selected.Select(r=>new ResourceKeyV1(r.SourceId,r.Kind,r.Id)));
            snapshot.VerifyUnchanged();
            model.WallLayer=wall;model.DoorLayer=door;model.Save(tx,doc.Database);
            Host.ShowResult("Tường "+model.WallLayer+"\nCửa "+model.DoorLayer+"\nReader V1 · "+snapshot.Revision);
        }
    }
}

