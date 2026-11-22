using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Web.Script.Serialization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace Arch.Suite.Host
{
    internal sealed class Part
    {
        [ScriptIgnore] public string Caption => Code+"    "+Level+"    "+(Kind=="Wall"?"Tường":"Cửa");
        public string Id { get; set; }
        public string Kind { get; set; }
        public string Code { get; set; }
        public string Level { get; set; }
        public string[] Handles { get; set; }
        public string HostId { get; set; }
        public double Height { get; set; }
        public double Width { get; set; }
        public double Offset { get; set; }
        public double[] BlockOrigin { get; set; }
        public bool ReverseSwing {get;set;}
        public double Sill {get;set;}
    }
    internal sealed class Output
    {
        public string Handle { get; set; }
        public string Signature { get; set; }
        public string Input {get;set;}
    }
    internal sealed class Model
    {
        public int Schema { get; set; } = 1;
        public int Revision { get; set; }
        public List<string> History {get;set;}=new List<string>();
        public List<Part> Parts { get; set; } = new List<Part>();
        public Dictionary<string,Output> Outputs { get; set; } = new Dictionary<string,Output>();
        public Dictionary<string,Output> Tables { get; set; } = new Dictionary<string,Output>();
        public double[] Masonry { get; set; }
        public double[] Doors { get; set; }
        public string ViewName { get; set; }
        public string LevelName { get; set; }
        public LayerRole WallLayer {get;set;}
        public LayerRole DoorLayer {get;set;}
        public Dictionary<string,LayerRole> Styles {get;set;}=new Dictionary<string,LayerRole>();
        public Dictionary<string,Output> Details {get;set;}=new Dictionary<string,Output>();
        public double[] Bounds { get; set; }
        public static Model Load(Transaction tx, Database db)
        {
            var nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains("ARCH_SUITE_V1")) return new Model();
            var rec = (Xrecord)tx.GetObject(nod.GetAt("ARCH_SUITE_V1"), OpenMode.ForRead);
            using (var data = rec.Data)
            {
                var model = new JavaScriptSerializer().Deserialize<Model>(string.Concat(data.AsArray().Select(v => (string)v.Value)));
                if (model.Schema != 1) throw new InvalidOperationException("Phiên bản dữ liệu chưa hỗ trợ.");
                return model;
            }
        }
        public void Save(Transaction tx, Database db,string reason="Cập nhật cấu hình")
        {
            Revision++;
            History.Add(Revision+" · "+DateTime.UtcNow.ToString("u")+" · "+reason);
            var nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            Xrecord rec;
            if (nod.Contains("ARCH_SUITE_V1"))
                rec = (Xrecord)tx.GetObject(nod.GetAt("ARCH_SUITE_V1"), OpenMode.ForWrite);
            else
            {
                nod.UpgradeOpen();
                rec = new Xrecord();
                nod.SetAt("ARCH_SUITE_V1", rec);
                tx.AddNewlyCreatedDBObject(rec,true);
            }
            var json = new JavaScriptSerializer().Serialize(this);
            using (var data = new ResultBuffer())
            {
                for (int i=0;i<json.Length;i+=128)
                    data.Add(new TypedValue(1,json.Substring(i,Math.Min(128,json.Length-i))));
                rec.Data=data;
            }
        }
        public static Entity Resolve(Transaction tx, Database db, string handle)
        {
            ObjectId id;
            if(!db.TryGetObjectId(new Handle(Convert.ToInt64(handle,16)),out id) || id.IsErased) return null;
            return tx.GetObject(id,OpenMode.ForRead) as Entity;
        }
        public static string Text(Editor ed,string label,string fallback)
        {
            var r=ed.GetString(new PromptStringOptions("\n"+label+" "){AllowSpaces=true,DefaultValue=fallback,UseDefaultValue=true});
            if(r.Status!=PromptStatus.OK) throw new OperationCanceledException();
            var value=r.StringResult.Trim();
            if(value.Length==0) throw new InvalidOperationException("Cần nhập "+label);
            return value;
        }
        public static double Number(Editor ed,string label,double defaultValue)
        {
            var r=ed.GetDouble(new PromptDoubleOptions("\n"+label+" "){DefaultValue=defaultValue,UseDefaultValue=true,AllowZero=false,AllowNegative=false});
            if(r.Status!=PromptStatus.OK) throw new OperationCanceledException();
            return r.Value;
        }
        public static void DoorOptions(Editor editor,Part door)
        {
            var sill=editor.GetDouble(new PromptDoubleOptions("\nCao độ bậu so với chân tường (mm) "){DefaultValue=door.Sill,UseDefaultValue=true,AllowNegative=false,AllowZero=true});
            if(sill.Status!=PromptStatus.OK)throw new OperationCanceledException();
            door.Sill=sill.Value;
            var side=new PromptKeywordOptions("\nHướng ký hiệu theo tường [Forward/Reverse] ","Forward Reverse"){AllowNone=true};
            side.Keywords.Default=door.ReverseSwing?"Reverse":"Forward";
            var answer=editor.GetKeywords(side);
            if(answer.Status!=PromptStatus.OK && answer.Status!=PromptStatus.None)throw new OperationCanceledException();
            if(answer.Status==PromptStatus.OK)door.ReverseSwing=answer.StringResult=="Reverse";
        }
        public static Point3d Point(Editor ed,string label)
        {
            var r=ed.GetPoint("\n"+label+" ");
            if(r.Status!=PromptStatus.OK) throw new OperationCanceledException();
            return r.Value.TransformBy(ed.CurrentUserCoordinateSystem);
        }
        public static Entity Pick(Document doc,Transaction tx,string label)
        {
            var r=doc.Editor.GetEntity("\n"+label+" ");
            if(r.Status!=PromptStatus.OK) throw new OperationCanceledException();
            var e=(Entity)tx.GetObject(r.ObjectId,OpenMode.ForRead);
            if(e.OwnerId!=SymbolUtilityServices.GetBlockModelSpaceId(doc.Database)) throw new InvalidOperationException("Chọn đối tượng trong Model.");
            return e;
        }
        public Part Find(string handle) => Parts.SingleOrDefault(p => p.Handles.Contains(handle));
        internal static void Tag(Transaction tx,Entity entity,string key,string guid,string role)
        {
            if(!entity.IsWriteEnabled)entity.UpgradeOpen();
            if(entity.ExtensionDictionary.IsNull)entity.CreateExtensionDictionary();
            var dict=(DBDictionary)tx.GetObject(entity.ExtensionDictionary,OpenMode.ForWrite);
            Xrecord record;
            if(dict.Contains(key))record=(Xrecord)tx.GetObject(dict.GetAt(key),OpenMode.ForWrite);
            else{record=new Xrecord();dict.SetAt(key,record);tx.AddNewlyCreatedDBObject(record,true);}
            using(var data=new ResultBuffer(new TypedValue(1,"1"),new TypedValue(1,guid),new TypedValue(1,role),new TypedValue(1,entity.Handle.ToString())))record.Data=data;
        }
        public void Unregister(Transaction tx,Database db,Part part)
        {
            foreach(var handle in part.Handles){
                var entity=Resolve(tx,db,handle);
                if(entity==null || entity.ExtensionDictionary.IsNull)continue;
                var dict=(DBDictionary)tx.GetObject(entity.ExtensionDictionary,OpenMode.ForWrite);
                if(dict.Contains("ARCH_SUITE_SOURCE")){var id=dict.GetAt("ARCH_SUITE_SOURCE");dict.Remove("ARCH_SUITE_SOURCE");tx.GetObject(id,OpenMode.ForWrite).Erase();}
            }
            Parts.Remove(part);Save(tx,db,"Gỡ đăng ký "+part.Caption);
        }
        public static void WallGeometry(Line a, Line b,out Point3d start,out Point3d end,out double thickness)
        {
            if(a==null || b==null || a.Length<1 || b.Length<1) throw new InvalidOperationException("Cần hai Line còn tồn tại.");
            var v=a.EndPoint-a.StartPoint;
            if(Math.Abs(v.Z)>0.01 || !v.IsParallelTo(b.EndPoint-b.StartPoint)) throw new InvalidOperationException("Hai cạnh tường phải nằm ngang và song song.");
            var p=b.StartPoint; var q=b.EndPoint;
            if(v.DotProduct(q-p)<0){p=b.EndPoint;q=b.StartPoint;}
            var axis=v.GetNormal();
            thickness=(p-a.StartPoint).CrossProduct(axis).Length;
            if(thickness<1 || Math.Abs(p.Z-a.StartPoint.Z)>0.01 || Math.Abs((p-a.StartPoint).DotProduct(axis))>1 || Math.Abs((q-a.EndPoint).DotProduct(axis))>1)
                throw new InvalidOperationException("Hai cạnh phải cùng cao độ và khớp hai đầu (sai số 1 mm).");
            start=a.StartPoint+(p-a.StartPoint)*0.5;
            end=a.EndPoint+(q-a.EndPoint)*0.5;
        }
        public void Register(Document doc,Transaction tx,bool door,string[] handles=null)
        {
            var db=doc.Database;
            if(db.Insunits!=UnitsValue.Millimeters) throw new InvalidOperationException("Đăng ký yêu cầu bản vẽ đơn vị mm.");
            var e=handles==null?Pick(doc,tx,door?"Chọn block cửa":"Chọn Line cạnh tường thứ nhất"):Resolve(tx,db,handles[0]);
            var second=handles==null || door?Pick(doc,tx,door?"Chọn cạnh tường chủ đã đăng ký":"Chọn Line cạnh tường thứ hai"):Resolve(tx,db,handles[1]);
            if(e==null || second==null)throw new InvalidOperationException("Hình học đã đổi. Tìm lại ứng viên.");
            if(Bounds!=null && new[]{e,second}.Any(item=>!item.Bounds.HasValue ||
                item.Bounds.Value.MinPoint.X<Bounds[0] || item.Bounds.Value.MinPoint.Y<Bounds[1] ||
                item.Bounds.Value.MaxPoint.X>Bounds[2] || item.Bounds.Value.MaxPoint.Y>Bounds[3]))
                throw new InvalidOperationException("Đối tượng nằm ngoài phạm vi mặt bằng nguồn.");
            var h=e.Handle.ToString(); var h2=second.Handle.ToString();
            if(Find(h)!=null || (!door && Find(h2)!=null) || Outputs.Values.Any(o=>o.Handle==h || o.Handle==h2)) throw new InvalidOperationException("Đối tượng đã được quản lý.");
            var part=new Part {Id=Guid.NewGuid().ToString("N"),Kind=door?"Door":"Wall",Handles=door?new[]{h}:new[]{h,h2}};
            Point3d start,end; double thickness;
            if(door)
            {
                var block=e as BlockReference; var host=Find(h2);
                if(block==null || host?.Kind!="Wall") throw new InvalidOperationException("Chọn block cửa và một cạnh tường đã đăng ký.");
                Geometry(tx,db,host,out start,out end,out thickness);
                part.Width=Number(doc.Editor,"Chiều rộng ô cửa (mm)",900);
                part.Offset=(block.Position-start).DotProduct((end-start).GetNormal());
                part.BlockOrigin=block.Position.ToArray();
                part.HostId=host.Id;part.Level=host.Level;
                if(part.Offset-part.Width/2<0 || part.Offset+part.Width/2>start.DistanceTo(end)) throw new InvalidOperationException("Ô cửa vượt đầu tường. Điểm chèn block dùng làm tâm cửa.");
            }
            else
            {
                WallGeometry(e as Line,second as Line,out start,out end,out thickness);
                part.Level=LevelName??Text(doc.Editor,"Tầng",Parts.LastOrDefault()?.Level??"T1");
            }
            part.Code=Text(doc.Editor,"Mã cấu kiện",(door?"D-":"W-")+(Parts.Count+1).ToString("000"));
            if(Parts.Any(p=>string.Equals(p.Code,part.Code,StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Mã đã được sử dụng.");
            part.Height=Number(doc.Editor,"Chiều cao (mm)",door?2200:3300);
            if(door){DoorOptions(doc.Editor,part);if(part.Height+part.Sill>Parts.Single(p=>p.Id==part.HostId).Height)throw new InvalidOperationException("Đỉnh cửa vượt chiều cao tường.");}
            foreach(var handle in part.Handles)Tag(tx,Resolve(tx,db,handle),"ARCH_SUITE_SOURCE",part.Id,part.Kind);
            Parts.Add(part);Save(tx,db,"Đăng ký "+part.Caption);
            Host.ShowResult("Đã đăng ký "+part.Code+" · "+part.Level);
        }
        public static void Geometry(Transaction tx,Database db,Part wall,out Point3d start,out Point3d end,out double thickness)
        {
            WallGeometry(Resolve(tx,db,wall.Handles[0]) as Line,Resolve(tx,db,wall.Handles[1]) as Line,out start,out end,out thickness);
        }
    }
}
