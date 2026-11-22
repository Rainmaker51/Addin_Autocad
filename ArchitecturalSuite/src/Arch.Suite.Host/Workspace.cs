using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace Arch.Suite.Host
{
    internal static class Workspace
    {
        private static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        private sealed class Segment
        {
            public Point3d A,B;
            public string Layer;
        }
        private static string P(Point3d p) => string.Join(",",new[]{p.X,p.Y,p.Z}.Select(n=>n.ToString("R",Inv)));
        private static string Signature(Line l) => P(l.StartPoint)+";"+P(l.EndPoint)+";"+l.Layer+";"+l.Color+";"+l.Linetype+";"+l.LineWeight+";"+l.LinetypeScale.ToString("R",Inv);
        private static Vector3d Shift(double[] xyz) => new Vector3d(xyz[0],xyz[1],xyz[2]);
        private static void Add(Dictionary<string,Segment> items,string key,Point3d a,Point3d b,string layer,Vector3d shift)
        {
            if(a.DistanceTo(b)>0.001) items.Add(key,new Segment{A=a+shift,B=b+shift,Layer=layer});
        }
        private static void Layout(Document doc,Model model)
        {
            var origin=Model.Point(doc.Editor,"Chọn điểm chuẩn của mặt bằng nguồn");
            var masonry=Model.Point(doc.Editor,"Điểm chuẩn tương ứng trên mặt bằng xây")-origin;
            var doors=Model.Point(doc.Editor,"Điểm chuẩn tương ứng trên mặt bằng cửa")-origin;
            if(masonry.Length<1 || doors.Length<1 || (doors-masonry).Length<1) throw new InvalidOperationException("Chọn ba vị trí khác nhau.");
            if(model.Bounds!=null){
                var width=model.Bounds[2]-model.Bounds[0];var height=model.Bounds[3]-model.Bounds[1];
                if(new[]{masonry,doors,doors-masonry}.Any(delta=>Math.Abs(delta.X)<width && Math.Abs(delta.Y)<height))
                    throw new InvalidOperationException("Vùng đầu ra chồng lên mặt bằng nguồn hoặc lên nhau. Chọn lại điểm đặt.");
            }
            model.Masonry=masonry.ToArray(); model.Doors=doors.ToArray();
        }
        private static Dictionary<string,Segment> Analyze(Document doc,Transaction tx,Model model,List<string> stats)
        {
            var db=doc.Database;var items=new Dictionary<string,Segment>();
            if(db.Insunits!=UnitsValue.Millimeters) throw new InvalidOperationException("Bản vẽ cần đơn vị mm.");
            foreach(var wall in model.Parts.Where(p=>p.Kind=="Wall"))
            {
                Point3d a,b;double t;
                try {Model.Geometry(tx,db,wall,out a,out b,out t);}
                catch(InvalidOperationException ex){throw new InvalidOperationException(wall.Code+" · "+ex.Message);}
                var source=(Line)Model.Resolve(tx,db,wall.Handles[0]);
                var along=(b-a).GetNormal();var side=(source.StartPoint-a);
                var length=a.DistanceTo(b);double cursor=0,openArea=0;string previous="start";
                Action<double,double,string> span=(lo,hi,key)=>{
                    if(model.Masonry==null)return;
                    foreach(var sign in new[]{-1,1}) Add(items,wall.Id+"/"+key+"/"+sign,a+along*lo+side*sign,a+along*hi+side*sign,source.Layer,Shift(model.Masonry));
                };
                var doors=model.Parts.Where(p=>p.HostId==wall.Id).ToList();
                foreach(var door in doors)
                {
                    var block=Model.Resolve(tx,db,door.Handles[0]) as BlockReference;
                    if(block==null) throw new InvalidOperationException(door.Code+" · Mất block nguồn.");
                    if(door.BlockOrigin!=null && block.Position.DistanceTo(new Point3d(door.BlockOrigin))>0.001)
                        door.Offset=(block.Position-a).DotProduct(along);
                    door.BlockOrigin=block.Position.ToArray();
                }
                foreach(var door in doors.OrderBy(p=>p.Offset))
                {
                    var block=Model.Resolve(tx,db,door.Handles[0]) as BlockReference;
                    if(block==null) throw new InvalidOperationException(door.Code+" · Mất block nguồn.");
                    var lo=door.Offset-door.Width/2;var hi=door.Offset+door.Width/2;
                    if(lo<cursor-0.001 || hi>length+0.001 || door.Height+door.Sill>wall.Height) throw new InvalidOperationException(door.Code+" · Kiểm tra vị trí, chiều cao hoặc ô cửa chồng nhau.");
                    span(cursor,lo,previous+"-"+door.Id); previous=door.Id;cursor=hi;
                    openArea+=door.Width*door.Height;
                    if(model.Doors!=null)
                    {
                        Add(items,door.Id+"/opening",a+along*lo,a+along*hi,block.Layer,Shift(model.Doors));
                        Add(items,door.Id+"/jamb1",a+along*lo-side,a+along*lo+side,block.Layer,Shift(model.Doors));
                        Add(items,door.Id+"/jamb2",a+along*hi-side,a+along*hi+side,block.Layer,Shift(model.Doors));
                    }
                    stats.Add(Row(door,wall.Code,door.Width,door.Height,door.Offset,0));
                }
                span(cursor,length,previous+"-end");
                if(model.Doors!=null) Add(items,wall.Id+"/axis",a,b,source.Layer,Shift(model.Doors));
                stats.Add(Row(wall,"",length,wall.Height,t,(length*wall.Height-openArea)/1000000));
            }
            return items;
        }
        private static string Row(Part p,string host,double size,double height,double extra,double area)
        {
            var fields=new[]{p.Kind,p.Code,p.Level,host,size.ToString("0.###",Inv),height.ToString("0.###",Inv),extra.ToString("0.###",Inv),area.ToString("0.###",Inv),p.Id};
            return string.Join(",",fields.Select(s=>"\""+s.Replace("\"","\"\"")+"\""));
        }
        private static void Sync(Document doc,Transaction tx,Model model,bool apply)
        {
            var db=doc.Database;
            if(model.Masonry==null) throw new InvalidOperationException("Đặt vị trí hai mặt bằng trước.");
            var targets=Analyze(doc,tx,model,new List<string>());
            var wallLayer=ResourceLayers.Demand(db,tx,model.WallLayer);
            var doorLayer=ResourceLayers.Demand(db,tx,model.DoorLayer);
            var doorIds=new HashSet<string>(model.Parts.Where(p=>p.Kind=="Door").Select(p=>p.Id));
            foreach(var item in targets)item.Value.Layer=doorIds.Contains(item.Key.Split('/')[0])?doorLayer:wallLayer;
            var remove=new List<string>();var update=new List<string>();var conflicts=new List<string>();
            foreach(var pair in model.Outputs)
            {
                var line=Model.Resolve(tx,db,pair.Value.Handle) as Line;
                if(line==null || Signature(line)!=pair.Value.Signature){conflicts.Add(pair.Value.Handle);continue;}
                Segment s;
                if(!targets.TryGetValue(pair.Key,out s)) remove.Add(pair.Key);
                else if(line.StartPoint.DistanceTo(s.A)>0.001 || line.EndPoint.DistanceTo(s.B)>0.001 || line.Layer!=s.Layer) update.Add(pair.Key);
            }
            var create=targets.Keys.Except(model.Outputs.Keys).ToList();
            var details=new DetailRenderer();details.Prepare(db,tx,model,wallLayer,doorLayer);conflicts.AddRange(details.Conflicts);
            var changedTables=model.Tables.Keys.Where(k=>Schedules.Place(doc,tx,model,k=="DOOR_SCHEDULE",preview:true)!=null).ToList();
            var preview=$"Tạo {create.Count} nét · Sửa {update.Count} · Thu hồi {remove.Count} · Ký hiệu/hatch/dim {details.Count} · Bảng {changedTables.Count} · Xung đột {conflicts.Count}";
            Host.ShowResult(preview+(conflicts.Count==0?"":"\nHandle cần kiểm tra: "+string.Join(", ",conflicts)));
            if(!apply || conflicts.Count>0 || create.Count+update.Count+remove.Count+changedTables.Count+details.Count==0)return;
            var ask=new PromptKeywordOptions("\n"+preview+" [Apply/Skip] <Skip>: ","Apply Skip"){AllowNone=true};
            var approval=doc.Editor.GetKeywords(ask);
            if(approval.Status!=PromptStatus.OK || approval.StringResult!="Apply")return;
            foreach(var key in remove)
            {
                var entity=Model.Resolve(tx,db,model.Outputs[key].Handle);
                entity.UpgradeOpen();entity.Erase();model.Outputs.Remove(key);
            }
            var space=(BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db),OpenMode.ForWrite);
            foreach(var key in update.Concat(create))
            {
                var s=targets[key];Output old;Line line;
                if(model.Outputs.TryGetValue(key,out old))
                {
                    line=(Line)Model.Resolve(tx,db,old.Handle);line.UpgradeOpen();line.StartPoint=s.A;line.EndPoint=s.B;
                }
                else
                {
                    line=new Line(s.A,s.B);line.SetDatabaseDefaults(db);space.AppendEntity(line);tx.AddNewlyCreatedDBObject(line,true);
                }
                line.Layer=s.Layer;Model.Tag(tx,line,"ARCH_SUITE_VIEW",key.Split('/')[0],key);model.Outputs[key]=new Output{Handle=line.Handle.ToString(),Signature=Signature(line)};
            }
            foreach(var key in changedTables)Schedules.Place(doc,tx,model,key=="DOOR_SCHEDULE",approved:true);
            if(details.Count>0)details.Apply(db,tx,model);
            model.Save(tx,db,preview);Host.ShowResult("Đã cập nhật · "+preview);
        }
        internal static void Run(Document doc,string action)
        {
            using(var tx=doc.Database.TransactionManager.StartTransaction())
            {
                var m=Model.Load(tx,doc.Database);
                if(action=="candidates"){
                    var found=Candidates.Search(doc,tx,m);
                    Host.ShowCandidates(found);Host.ShowResult(found.Count+" ứng viên. Chọn một dòng để xem bằng chứng trước khi đăng ký.");return;
                }
                if(action.StartsWith("accept/",StringComparison.Ordinal)){
                    var handles=action.Substring(7).Split(',');
                    m.Register(doc,tx,handles.Length==1,handles);tx.Commit();Host.ShowCandidates(new List<Candidates.Candidate>());return;
                }
                if(action.StartsWith("remove/",StringComparison.Ordinal)){
                    var stale=m.Parts.Single(p=>p.Id==action.Substring(7));
                    if(m.Parts.Any(p=>p.HostId==stale.Id))throw new InvalidOperationException("Gỡ đăng ký các cửa thuộc tường trước.");
                    var question=new PromptKeywordOptions("\nGỡ đăng ký "+stale.Code+" [Remove/Skip] <Skip>: ","Remove Skip"){AllowNone=true};
                    if(doc.Editor.GetKeywords(question).StringResult!="Remove")return;
                    m.Unregister(tx,doc.Database,stale);tx.Commit();Host.ShowResult("Đã gỡ đăng ký "+stale.Code+". Xem trước để cập nhật mặt bằng.");return;
                }
                if(action.StartsWith("zoom/",StringComparison.Ordinal)){
                    var part=m.Parts.Single(p=>p.Id==action.Substring(5));
                    var entities=part.Handles.Select(h=>Model.Resolve(tx,doc.Database,h)).ToArray();
                    if(entities.Any(e=>e==null || !e.Bounds.HasValue))throw new InvalidOperationException("Cấu kiện thiếu hình học nguồn.");
                    if(doc.Database.TileMode==false)throw new InvalidOperationException("Chuyển sang Model để định vị.");
                    var box=entities[0].GeometricExtents;
                    foreach(var entity in entities.Skip(1))box.AddExtents(entity.GeometricExtents);
                    using(var view=doc.Editor.GetCurrentView()){
                        var transform=Matrix3d.Rotation(-view.ViewTwist,view.ViewDirection,view.Target)*Matrix3d.Displacement(view.Target-Point3d.Origin)*Matrix3d.PlaneToWorld(view.ViewDirection);
                        box.TransformBy(transform.Inverse());
                        var a=box.MinPoint;var b=box.MaxPoint;var ratio=view.Width/view.Height;
                        view.CenterPoint=new Point2d((a.X+b.X)/2,(a.Y+b.Y)/2);
                        view.Height=Math.Max(100,Math.Max((b.X-a.X)/ratio,b.Y-a.Y)*1.4);view.Width=view.Height*ratio;doc.Editor.SetCurrentView(view);
                    }
                    doc.Editor.SetImpliedSelection(entities.Select(e=>e.ObjectId).ToArray());Host.ShowResult(part.Caption);return;
                }
                switch(action)
                {
                    case "report":
                        var output=doc.Editor.GetFileNameForSave("Tên báo cáo mô hình JSON mới");
                        if(output.Status!=PromptStatus.OK)throw new OperationCanceledException();
                        var report=new {schemaVersion=1,drawing=Path.GetFileName(doc.Name),documentGuid=doc.Database.FingerprintGuid,units=doc.Database.Insunits.ToString(),model=m,
                            classificationCandidates=m.Bounds==null?null:Candidates.Search(doc,tx,m),
                            dependencies=m.Parts.Where(p=>p.HostId!=null).Select(p=>new{source=p.Id,relation="HOSTED_BY",target=p.HostId}).ToArray()};
                        var reportPath=Path.ChangeExtension(output.StringResult,".json");
                        var content=new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Serialize(report);
                        using(var file=new FileStream(reportPath,FileMode.CreateNew,FileAccess.Write))
                        using(var writer=new StreamWriter(file))writer.Write(content);
                        Host.ShowResult("Đã xuất báo cáo mô hình\n"+reportPath);break;
                    case "index":Host.ShowResult("Đã dựng lại chỉ mục · revision "+m.Revision+"\n"+ProjectIndex.Rebuild(doc.Database,m));break;
                    case "audit":Host.ShowResult("Nhật ký bản vẽ · revision "+m.Revision+"\n"+string.Join("\n",m.History.AsEnumerable().Reverse().Take(100)));break;
                    case "layers":ResourceLayers.Configure(doc,tx,m);break;
                    case "repair":
                        var handle=Model.Text(doc.Editor,"Handle nét xung đột (xem trong Xem trước)","").ToUpperInvariant();
                        var pair=m.Outputs.SingleOrDefault(p=>p.Value.Handle==handle);
                        var map=m.Outputs;
                        if(pair.Key==null){map=m.Details;pair=map.SingleOrDefault(p=>p.Value.Handle==handle);}
                        if(pair.Key==null){map=m.Tables;pair=map.SingleOrDefault(p=>p.Value.Handle==handle);}
                        if(pair.Key==null)throw new InvalidOperationException("Handle không thuộc hình hoặc bảng đã sinh.");
                        var actual=Model.Resolve(tx,doc.Database,handle);
                        var current=Model.Resolve(tx,doc.Database,handle) as Line;
                        string signature=null;
                        if(actual!=null)signature=map==m.Outputs && current!=null?Signature(current):map==m.Tables && actual is Table?Schedules.Fingerprint((Table)actual):DetailRenderer.State(actual,tx);
                        if(actual!=null && signature==pair.Value.Signature)throw new InvalidOperationException("Đối tượng đang khớp dữ liệu.");
                        var confirm=new PromptKeywordOptions("\nCho phép tái sinh nét "+handle+" theo nguồn ở lần Apply tới? [Restore/Skip] <Skip>: ","Restore Skip"){AllowNone=true};
                        if(doc.Editor.GetKeywords(confirm).StringResult!="Restore")return;
                        if(actual==null)map.Remove(pair.Key);
                        else {pair.Value.Signature=signature;pair.Value.Input=null;}
                        m.Save(tx,doc.Database);Host.ShowResult("Đã duyệt riêng nét "+handle+". Xem trước trước khi cập nhật.");break;
                    case "region":
                        if(m.Parts.Count>0)throw new InvalidOperationException("Phạm vi nguồn đã có cấu kiện đăng ký.");
                        var first=Model.Point(doc.Editor,"Chọn góc thứ nhất của mặt bằng nguồn");
                        var last=Model.Point(doc.Editor,"Chọn góc đối diện");
                        if(Math.Abs(first.X-last.X)<1 || Math.Abs(first.Y-last.Y)<1)throw new InvalidOperationException("Chọn vùng có diện tích.");
                        m.ViewName=Model.Text(doc.Editor,"Tên mặt bằng","Mặt bằng nguồn");
                        m.LevelName=Model.Text(doc.Editor,"Tầng","T1");
                        m.Bounds=new[]{Math.Min(first.X,last.X),Math.Min(first.Y,last.Y),Math.Max(first.X,last.X),Math.Max(first.Y,last.Y)};
                        m.Save(tx,doc.Database);Host.ShowResult(m.ViewName+" · "+m.LevelName);break;
                    case "walltable":case "doortable":
                        Analyze(doc,tx,m,new List<string>());Schedules.Place(doc,tx,m,action=="doortable");break;
                    case "wall":case "door":m.Register(doc,tx,action=="door");break;
                    case "layout":Layout(doc,m);m.Save(tx,doc.Database);Host.ShowResult("Đã lưu vị trí hai mặt bằng.");break;
                    case "preview":case "apply":Sync(doc,tx,m,action=="apply");break;
                    case "parts":Host.ShowResult(m.Parts.Count+" cấu kiện\n"+string.Join("\n",m.Parts.Select(p=>p.Level+"   "+p.Code+"   "+(p.Kind=="Wall"?"Tường":"Cửa")+"   #"+p.Handles[0])));break;
                    case "inspect":case "edit":case "unregister":
                        var e=Model.Pick(doc,tx,"Chọn cấu kiện nguồn");
                        var part=m.Find(e.Handle.ToString());
                        if(part==null)throw new InvalidOperationException("Đối tượng chưa đăng ký.");
                        if(action=="edit")
                        {
                            part.Height=Model.Number(doc.Editor,"Chiều cao mm",part.Height);
                            if(part.Kind=="Door"){
                                part.BlockOrigin=((BlockReference)e).Position.ToArray();
                                part.Width=Model.Number(doc.Editor,"Rộng cửa mm",part.Width);
                                part.Offset=Model.Number(doc.Editor,"Tâm cửa cách đầu tường (mm)",part.Offset);
                                Model.DoorOptions(doc.Editor,part);
                            }
                            Analyze(doc,tx,m,new List<string>());m.Save(tx,doc.Database,"Sửa "+part.Caption);
                        }
                        if(action=="unregister"){
                            if(m.Parts.Any(p=>p.HostId==part.Id))throw new InvalidOperationException("Gỡ các cửa thuộc tường trước.");
                            m.Unregister(tx,doc.Database,part);Host.ShowResult("Đã gỡ đăng ký "+part.Code);break;
                        }
                        Host.ShowResult(part.Code+" · "+part.Level+"\nGUID "+part.Id+"\nCao "+part.Height+" mm\n"+
                            (part.HostId==null?"Cửa thuộc tường: "+string.Join(", ",m.Parts.Where(p=>p.HostId==part.Id).Select(p=>p.Code)):
                            "Tường chủ "+m.Parts.Single(p=>p.Id==part.HostId).Code+" · Rộng "+part.Width+" mm · Neo "+part.Offset+" mm"));break;
                    case "stats":case "csv":
                        var rows=new List<string>();Analyze(doc,tx,m,rows);
                        if(action=="stats")Host.ShowResult("Tường "+m.Parts.Count(p=>p.Kind=="Wall")+" · Cửa "+m.Parts.Count(p=>p.Kind=="Door")+"\n"+string.Join("\n",m.Parts.Select(p=>p.Code+" · "+p.Level+" · cao "+p.Height+" mm")));
                        else {
                            var path=doc.Editor.GetFileNameForSave("Tên file CSV mới");if(path.Status!=PromptStatus.OK)throw new OperationCanceledException();
                            using(var f=new FileStream(Path.ChangeExtension(path.StringResult,".csv"),FileMode.CreateNew))
                            using(var writer=new StreamWriter(f,new UTF8Encoding(true))){
                                writer.WriteLine("Loai,Ma,Tang,TuongChu,DaiHoacRong_mm,Cao_mm,DayTuongHoacNeoCua_mm,DienTichTuongRong_m2,GUID");
                                foreach(var row in rows)writer.WriteLine(row);
                            }
                            Host.ShowResult("Đã xuất "+rows.Count+" dòng thống kê.");
                        }break;
                }
                tx.Commit();
            }
        }
    }
}
