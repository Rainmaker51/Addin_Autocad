using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Arch.Suite.Host
{
    internal sealed class LinkedDrawing
    {
        public string Id {get;set;}=Guid.NewGuid().ToString("N");
        [Newtonsoft.Json.JsonIgnore] public string SourceName {get;set;}
        [Newtonsoft.Json.JsonIgnore] public string Level {get;set;}
        [Newtonsoft.Json.JsonIgnore] public int UnmanagedCopiesCount {get;set;}
        [Newtonsoft.Json.JsonIgnore] public string State=>UnmanagedCopiesCount>0?UnmanagedCopiesCount+" bản sao cần kiểm tra":(NeedsUpdate?"Cần cập nhật · ":"")+Groups.Count(g=>g.Linked)+" liên kết · "+Groups.Count(g=>!g.Linked)+" độc lập";
        public bool NeedsUpdate {get;set;}
        public string SourceFrameId {get;set;}
        public string TargetFrameId {get;set;}
        public double[] TargetPoint {get;set;}
        public List<LinkedGroup> Groups {get;set;}=new List<LinkedGroup>();
        public double FrameRotation {get;set;}
        public double[] FrameScale {get;set;}
        public string Name {get;set;}
        public string Code {get;set;}
    }
    internal sealed class LinkedGroup
    {
        public string Id {get;set;}=Guid.NewGuid().ToString("N");
        public string Kind {get;set;}
        public bool Linked {get;set;}=true;
        public string CacheHandle {get;set;}
        public string ContentDefinitionHandle {get;set;}
        public string ContentHandle {get;set;}
        public string SourceHash {get;set;}
        public string ContentHash {get;set;}
        public string Layer {get;set;}
        public double[] Offset {get;set;}
    }
    internal static class DrawingLinks
    {
        private static string Number(double n)=>n.ToString("R",CultureInfo.InvariantCulture);
        private static string Value(object value)
        {
            if(value is Point3d p)return Number(p.X)+","+Number(p.Y)+","+Number(p.Z);
            if(value is Point2d q)return Number(q.X)+","+Number(q.Y);
            if(value is Vector3d v)return Number(v.X)+","+Number(v.Y)+","+Number(v.Z);
            if(value is double d)return Number(d);
            return Convert.ToString(value,CultureInfo.InvariantCulture);
        }
        internal static string Signature(Entity entity)
        {
            var text=new StringBuilder(entity.GetType().FullName);
            foreach(var property in entity.GetType().GetProperties(BindingFlags.Instance|BindingFlags.Public).OrderBy(p=>p.Name))
            {
                if(property.GetIndexParameters().Length>0)continue;
                if(entity is Spline&&!new[]{"Layer","Linetype","LinetypeScale","LineWeight","ColorIndex","Visible","Material","PlotStyleName","Annotative","Normal"}.Contains(property.Name))continue;
                if(entity is Spline splineWithoutFit&&!splineWithoutFit.HasFitData&&new[]{"NumFitPoints","StartFitTangent","EndFitTangent","FitTolerance"}.Contains(property.Name))continue;
                var type=property.PropertyType;
                if(!(type.IsPrimitive||type.IsEnum||type==typeof(string)||type==typeof(Point3d)||type==typeof(Point2d)||type==typeof(Vector3d)))continue;
                if(new[]{"UnmanagedObject","BlockName","IsModified","IsModifiedXData","IsModifiedGraphics","IsNewObject","IsWriteEnabled","IsReadEnabled","IsNotifyEnabled","IsTransactionResident","IsPersistent","IsCancelling","IsReallyClosing","IsErased","IsEffectivelyErased","IsDisposed","IsUndoing","IsAProxy","IsNotifying"}.Contains(property.Name))continue;
                try{text.Append('|').Append(property.Name).Append('=').Append(Value(property.GetValue(entity,null)));}catch(TargetInvocationException){}
            }
            text.Append('|').Append(entity.Color.Red).Append(',').Append(entity.Color.Green).Append(',').Append(entity.Color.Blue);
            if(entity is Polyline poly)for(int i=0;i<poly.NumberOfVertices;i++)text.Append('|').Append(Value(poly.GetPoint3dAt(i))).Append(':').Append(Number(poly.GetBulgeAt(i))).Append(':').Append(Number(poly.GetStartWidthAt(i))).Append(':').Append(Number(poly.GetEndWidthAt(i)));
            if(entity is Spline spline)
            {
                var nurbs=spline.NurbsData;text.Append("|NURBS:").Append(nurbs.Degree).Append(':').Append(nurbs.Closed).Append(':').Append(nurbs.Periodic).Append(':').Append(nurbs.Rational);
                foreach(Point3d point in nurbs.GetControlPoints())text.Append('|').Append(Value(point));
                foreach(double knot in nurbs.GetKnots())text.Append("|k:").Append(Number(knot));
                foreach(double weight in nurbs.GetWeights())text.Append("|w:").Append(Number(weight));
            }
            if(entity is Solid solid)for(int i=0;i<4;i++)text.Append(Value(solid.GetPointAt((short)i)));
            if(entity is Hatch hatch)
            {
                for(int i=0;i<hatch.NumberOfLoops;i++)
                {
                    var loop=hatch.GetLoopAt(i);text.Append("|loop:");
                    if(loop.IsPolyline)foreach(BulgeVertex vertex in loop.Polyline)text.Append(Value(vertex.Vertex)).Append(':').Append(Number(vertex.Bulge)).Append(';');
                    else foreach(Curve2d curve in loop.Curves)foreach(var point in curve.GetSamplePoints(32))text.Append(Value(point)).Append(';');
                }
            }
            // Unsupported entities are rejected before generation rather than hashed by bounds alone.
            return text.ToString();
        }
        internal static bool Supported(Entity entity)=>entity is Line||entity is Polyline||entity is Circle||entity is Arc||entity is DBText||entity is MText||entity is Ellipse||entity is Solid||entity is Hatch||entity is Spline;
        private static string SourceHash(Database db,Transaction tx,FrameDocument data,DrawingFrame frame,string kind,GeometryBatch leaves)
        {
            var metadata=string.Join("|",SuiteKinds.Refresh(db,tx,data,frame).Where(r=>r.Kind==kind).OrderBy(r=>r.Id).Select(r=>r.Id+":"+r.Code+":"+Number(r.HeightMm)+":"+Number(r.WidthMm)+":"+Number(r.SillMm)+":"+Number(r.ElevationMm)+":"+r.FloorId));
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(leaves.Fingerprint+metadata))).Replace("-","").ToLowerInvariant();
        }
        internal static string HashSignatures(IEnumerable<string> signatures)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n",signatures)))).Replace("-","").ToLowerInvariant();
        }
        internal static string Hash(IEnumerable<Entity> entities)=>HashSignatures(entities.Select(Signature));
        private static BlockTableRecord Record(Database db,Transaction tx,string handle,OpenMode mode=OpenMode.ForRead)
        {
            if(!db.TryGetObjectId(new Handle(Convert.ToInt64(handle,16)),out var id)||id.IsErased)throw new InvalidOperationException("Thiếu dữ liệu hình liên kết. Chọn đồng bộ để dựng lại.");
            return (BlockTableRecord)tx.GetObject(id,mode);
        }
        private static List<Entity> Contents(Transaction tx,BlockTableRecord record)=>record.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<Entity>().ToList();
        private static ObjectId Layer(Database db,Transaction tx,string name,bool locked)
        {
            var table=(LayerTable)tx.GetObject(db.LayerTableId,OpenMode.ForRead);
            LayerTableRecord layer;
            if(table.Has(name))layer=(LayerTableRecord)tx.GetObject(table[name],OpenMode.ForWrite);
            else{table.UpgradeOpen();layer=new LayerTableRecord{Name=name};table.Add(layer);tx.AddNewlyCreatedDBObject(layer,true);}
            layer.IsLocked=locked;return layer.ObjectId;
        }
        private static BlockTableRecord NewRecord(Database db,Transaction tx,string name)
        {
            var table=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForWrite);
            var record=new BlockTableRecord{Name=name,Explodable=false};table.Add(record);tx.AddNewlyCreatedDBObject(record,true);return record;
        }
        private static void Append(Transaction tx,BlockTableRecord record,Entity entity){record.AppendEntity(entity);tx.AddNewlyCreatedDBObject(entity,true);}
        internal static DrawingFrame Source(FrameDocument data,DrawingFrame selected)
        {
            if(selected==null)throw new InvalidOperationException("Chọn khung nguồn hoặc khung đích.");
            var drawing=data.Drawings.SingleOrDefault(d=>d.TargetFrameId==selected.Id);
            if(drawing!=null&&!drawing.Groups.Any(g=>g.Linked))throw new InvalidOperationException("Khung đã ngắt liên kết; chọn khung nguồn đã đăng ký để sinh mới.");
            var source=drawing==null?selected:data.Frames.SingleOrDefault(f=>f.Id==drawing.SourceFrameId);
            if(source==null||source.IsDerived)throw new InvalidOperationException("Khung nguồn không còn hợp lệ.");return source;
        }
        private static GeometryBatch Build(Database db,Transaction tx,FrameDocument data,DrawingFrame source,string kind)
        {
            var registered=SuiteKinds.Refresh(db,tx,data,source).Where(r=>r.Kind==kind).OrderBy(r=>r.Id).ToList();
            return GeometryBatch.Build(db,registered,kind);
        }
        internal static void CheckCopies(Database db,Transaction tx,FrameDocument data)
        {
            var model=(BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db),OpenMode.ForRead);
            var references=model.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<BlockReference>().ToList();
            foreach(var drawing in data.Drawings)
            {
                drawing.UnmanagedCopiesCount=0;
                foreach(var group in drawing.Groups.Where(g=>g.Linked))
                {
                    var definition=Record(db,tx,group.ContentDefinitionHandle);
                    drawing.UnmanagedCopiesCount+=references.Count(b=>b.BlockTableRecord==definition.ObjectId&&b.Handle.ToString()!=group.ContentHandle);
                }
            }
        }
        internal static string Preview(Database db,Transaction tx,FrameDocument data,DrawingFrame source,IEnumerable<string> kinds,LinkedDrawing drawing=null)
        {
            CheckCopies(db,tx,data);
            if(drawing?.UnmanagedCopiesCount>0)throw new InvalidOperationException("Có bản sao dùng chung định nghĩa liên kết ngoài quản lý. Kiểm tra và xử lý trước khi đồng bộ; Arch không tự xóa bản sao.");
            var notes=new List<string>();
            foreach(string kind in kinds)
            {
                var leaves=Build(db,tx,data,source,kind);
                try
                {
                    var prior=drawing?.Groups.SingleOrDefault(g=>g.Kind==kind&&g.Linked);
                    notes.Add(kind+": "+leaves.Count+" nét · "+(prior==null?"Tạo":prior.SourceHash==SourceHash(db,tx,data,source,kind,leaves)?"Không đổi":"Cần cập nhật"));
                }
                finally{leaves.Dispose();}
            }
            return string.Join("\n",notes);
        }
        private static void Fill(Transaction tx,BlockTableRecord record,IEnumerable<Entity> entities)
        {
            foreach(var old in Contents(tx,record)){old.UpgradeOpen();old.Erase();}
            var ids=entities.Select(e=>e.ObjectId).ToArray();
            if(ids.Length>0)record.Database.DeepCloneObjects(new ObjectIdCollection(ids),record.ObjectId,new IdMapping(),false);
        }
        private static void ApplyGroup(Database db,Transaction tx,FrameDocument data,LinkedDrawing drawing,DrawingFrame source,string kind)
        {
            var group=drawing.Groups.SingleOrDefault(g=>g.Kind==kind&&g.Linked);
            if(group==null&&drawing.Groups.Any(g=>g.Kind==kind&&!g.Linked))throw new InvalidOperationException(kind+" đã ngắt và giữ hình. Sinh khung mới để tránh chồng hình khi liên kết lại.");
            var leaves=Build(db,tx,data,source,kind);
            try
            {
                if(group==null)
                {
                    group=new LinkedGroup{Kind=kind};drawing.Groups.Add(group);
                    group.Layer="ARCH_LINK_"+group.Id;
                    group.CacheHandle=NewRecord(db,tx,"ARCH_CACHE_"+group.Id).Handle.ToString();
                    group.ContentDefinitionHandle=NewRecord(db,tx,"ARCH_VIEW_"+group.Id).Handle.ToString();
                }
                Layer(db,tx,group.Layer,false);
                var cache=Record(db,tx,group.CacheHandle,OpenMode.ForWrite);var content=Record(db,tx,group.ContentDefinitionHandle,OpenMode.ForWrite);
                Fill(tx,cache,leaves.Entities);Fill(tx,content,Contents(tx,cache));
                group.SourceHash=SourceHash(db,tx,data,source,kind,leaves);group.ContentHash=Hash(Contents(tx,content));
                var sourceBlock=FrameReader.Resolve(db,tx,source.Handle);
                var target=new Point3d(drawing.TargetPoint[0],drawing.TargetPoint[1],drawing.TargetPoint[2]);
                var delta=target-sourceBlock.Position;group.Offset=new[]{delta.X,delta.Y,delta.Z};
                var reference=group.ContentHandle==null?null:Model.Resolve(tx,db,group.ContentHandle) as BlockReference;
                if(reference==null)
                {
                    reference=new BlockReference(new Point3d(delta.X,delta.Y,delta.Z),content.ObjectId){LayerId=Layer(db,tx,group.Layer,false)};
                    Append(tx,(BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db),OpenMode.ForWrite),reference);
                    group.ContentHandle=reference.Handle.ToString();
                }
                else{reference.UpgradeOpen();reference.Position=new Point3d(delta.X,delta.Y,delta.Z);reference.ScaleFactors=new Scale3d(1);reference.Rotation=0;reference.BlockTableRecord=content.ObjectId;}
                Layer(db,tx,group.Layer,true);
            }
            finally{leaves.Dispose();}
        }
        internal static LinkedDrawing Create(Database db,Transaction tx,FrameDocument data,DrawingFrame source,string name,string code,Point3d target,IEnumerable<string> kinds)
        {
            var selected=kinds.Distinct().ToList();
            if(source.IsDerived||selected.Count==0||string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(code))throw new InvalidOperationException("Chọn nguồn, nhóm liên kết, tên và mã bản vẽ đích.");
            if(data.Drawings.Any(d=>string.Equals(d.Code,code,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Mã bản vẽ đích đã dùng.");
            Preview(db,tx,data,source,selected);
            var original=FrameReader.Resolve(db,tx,source.Handle);
            var sourceSnapshot=FrameReader.Read(db,tx,source,false);
            var deltaToTarget=target-original.Position;
            var proposed=sourceSnapshot.Boundary.Select(q=>new Point2d(q.X+deltaToTarget.X,q.Y+deltaToTarget.Y)).ToArray();
            foreach(var existing in data.Frames)
            {
                FrameSnapshot bounds;try{bounds=FrameReader.Read(db,tx,existing,false);}catch(InvalidOperationException){continue;}
                if(bounds.Boundary==null)continue;
                if(proposed.Min(q=>q.X)<bounds.Boundary.Max(q=>q.X)&&proposed.Max(q=>q.X)>bounds.Boundary.Min(q=>q.X)&&proposed.Min(q=>q.Y)<bounds.Boundary.Max(q=>q.Y)&&proposed.Max(q=>q.Y)>bounds.Boundary.Min(q=>q.Y))throw new InvalidOperationException("Vị trí khung mới chồng phạm vi khung đã đăng ký. Chọn vùng trống khác.");
            }
            if(string.IsNullOrWhiteSpace(source.CodeTag)||source.CodeTag==source.NameTag||!sourceSnapshot.Attributes.ContainsKey(source.CodeTag))throw new InvalidOperationException("Chọn tag mã bản vẽ riêng, khác tag tên rồi Lưu khung trước khi sinh.");
            var privateDefinition=NewRecord(db,tx,"ARCH_FRAME_"+Guid.NewGuid().ToString("N"));
            var originalDefinition=(BlockTableRecord)tx.GetObject(original.BlockTableRecord,OpenMode.ForRead);
            try{db.DeepCloneObjects(new ObjectIdCollection(originalDefinition.Cast<ObjectId>().ToArray()),privateDefinition.ObjectId,new IdMapping(),false);}
            catch(Exception error){throw new SourceFailure("Sao chép định nghĩa khung","Khung",original,source.BlockName,error);}
            var copy=new BlockReference(target,privateDefinition.ObjectId);
            Append(tx,(BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db),OpenMode.ForWrite),copy);
            copy.SetPropertiesFrom(original);copy.Normal=original.Normal;copy.ScaleFactors=original.ScaleFactors;copy.Rotation=original.Rotation;
            var shift=Matrix3d.Displacement(target-original.Position);
            foreach(ObjectId id in original.AttributeCollection)
            {
                var attribute=(AttributeReference)((AttributeReference)tx.GetObject(id,OpenMode.ForRead)).Clone();
                copy.AttributeCollection.AppendAttribute(attribute);tx.AddNewlyCreatedDBObject(attribute,true);attribute.TransformBy(shift);
            }
            foreach(ObjectId id in copy.AttributeCollection)
            {
                var attribute=(AttributeReference)tx.GetObject(id,OpenMode.ForWrite);
                if(attribute.Tag==source.NameTag)attribute.TextString=name;
                if(attribute.Tag==source.CodeTag)attribute.TextString=code;
            }
            foreach(ObjectId id in privateDefinition)
            {
                var attribute=tx.GetObject(id,OpenMode.ForRead) as AttributeDefinition;
                if(attribute==null||!attribute.Constant)continue;
                if(attribute.Tag==source.NameTag||attribute.Tag==source.CodeTag){attribute.UpgradeOpen();attribute.TextString=attribute.Tag==source.NameTag?name:code;}
            }
            var frame=FrameProfile.From(source).ForBlock(copy.Handle.ToString());frame.BlockName=privateDefinition.Name;frame.IsDerived=true;frame.FloorId=source.FloorId;
            data.Frames.Add(frame);
            var drawing=new LinkedDrawing{FrameRotation=copy.Rotation,FrameScale=new[]{copy.ScaleFactors.X,copy.ScaleFactors.Y,copy.ScaleFactors.Z},SourceFrameId=source.Id,TargetFrameId=frame.Id,Name=name,Code=code,TargetPoint=new[]{target.X,target.Y,target.Z}};
            data.Drawings.Add(drawing);
            foreach(var kind in selected)ApplyGroup(db,tx,data,drawing,source,kind);
            foreach(var current in SuiteKinds.Refresh(db,tx,data,source))RegistrationService.Upsert(data,current);
            return drawing;
        }
        internal static void Sync(Database db,Transaction tx,FrameDocument data,LinkedDrawing drawing,IEnumerable<string> kinds)
        {
            var source=data.Frames.Single(f=>f.Id==drawing.SourceFrameId);
            var targetFrame=data.Frames.Single(f=>f.Id==drawing.TargetFrameId);targetFrame.FloorId=source.FloorId;targetFrame.Level=source.Level;
            var selected=kinds.Distinct().ToList();if(selected.Count==0)throw new InvalidOperationException("Chọn ít nhất một nhóm; dùng Ngắt toàn bộ nếu muốn giữ hình độc lập.");
            Preview(db,tx,data,source,selected,drawing);
            foreach(var group in drawing.Groups.Where(g=>g.Linked&&!selected.Contains(g.Kind)))Detach(db,tx,group);
            foreach(var kind in selected)ApplyGroup(db,tx,data,drawing,source,kind);
            data.Registrations.RemoveAll(r=>r.FrameId==source.Id&&Model.Resolve(tx,db,r.Handle)==null);
            foreach(var sourceItem in SuiteKinds.Refresh(db,tx,data,source))RegistrationService.Upsert(data,sourceItem);
            drawing.NeedsUpdate=false;
        }
        private static void Detach(Database db,Transaction tx,LinkedGroup group)
        {
            group.Linked=false;Layer(db,tx,group.Layer,false);
            Record(db,tx,group.ContentDefinitionHandle,OpenMode.ForWrite).Explodable=true;
        }
        internal static void Unlink(Database db,Transaction tx,LinkedDrawing drawing){foreach(var group in drawing.Groups.Where(g=>g.Linked))Detach(db,tx,group);}
        internal static void Move(Database db,Transaction tx,FrameDocument data,LinkedDrawing drawing,Point3d target)
        {
            var delta=target-new Point3d(drawing.TargetPoint[0],drawing.TargetPoint[1],drawing.TargetPoint[2]);
            var frame=FrameReader.Resolve(db,tx,data.Frames.Single(f=>f.Id==drawing.TargetFrameId).Handle);
            frame.UpgradeOpen();frame.TransformBy(Matrix3d.Displacement(delta));
            foreach(var group in drawing.Groups)
            {
                bool locked=group.Linked;Layer(db,tx,group.Layer,false);
                var entity=Model.Resolve(tx,db,group.ContentHandle);
                if(entity!=null){entity.UpgradeOpen();entity.TransformBy(Matrix3d.Displacement(delta));}
                if(group.Offset!=null){group.Offset[0]+=delta.X;group.Offset[1]+=delta.Y;group.Offset[2]+=delta.Z;}
                Layer(db,tx,group.Layer,locked);
            }
            drawing.TargetPoint=new[]{target.X,target.Y,target.Z};
        }
        internal static void KeepCaches(Database db,Transaction tx,FrameDocument data)
        {
            const string key="ARCH_SUITE_LINK_OWNERS_V1";
            var ids=new List<ObjectId>();
            foreach(var group in data.Drawings.SelectMany(d=>d.Groups).Where(g=>g.Linked))
            {
                ids.Add(Record(db,tx,group.CacheHandle).ObjectId);ids.Add(Record(db,tx,group.ContentDefinitionHandle).ObjectId);
            }
            var nod=(DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForRead);
            if(ids.Count==0&&!nod.Contains(key))return;
            Xrecord record;
            if(nod.Contains(key))record=(Xrecord)tx.GetObject(nod.GetAt(key),OpenMode.ForWrite);
            else{nod.UpgradeOpen();record=new Xrecord();nod.SetAt(key,record);tx.AddNewlyCreatedDBObject(record,true);}
            using(var pointers=new ResultBuffer(ids.Distinct().Select(id=>new TypedValue((int)DxfCode.HardPointerId,id)).ToArray()))record.Data=pointers;
        }
        internal static bool Protect(Database db,Transaction tx,FrameDocument data)
        {
            bool repaired=false;
            foreach(var drawing in data.Drawings)
            {
                if(!drawing.Groups.Any(g=>g.Linked))continue;
                var target=data.Frames.Single(f=>f.Id==drawing.TargetFrameId);
                if(!db.TryGetObjectId(new Handle(Convert.ToInt64(target.Handle,16)),out var targetId))throw new InvalidOperationException("Khung đích đã mất; cần khôi phục trước khi tiếp tục.");
                var targetFrame=(BlockReference)tx.GetObject(targetId,OpenMode.ForRead,true);
                var targetPosition=new Point3d(drawing.TargetPoint[0],drawing.TargetPoint[1],drawing.TargetPoint[2]);
                if(targetFrame.IsErased||targetFrame.Position.DistanceTo(targetPosition)>1e-8||Math.Abs(targetFrame.Rotation-drawing.FrameRotation)>1e-8||Math.Abs(targetFrame.ScaleFactors.X-drawing.FrameScale[0])>1e-8||Math.Abs(targetFrame.ScaleFactors.Y-drawing.FrameScale[1])>1e-8)
                {
                    targetFrame.UpgradeOpen();if(targetFrame.IsErased)targetFrame.Erase(false);
                    targetFrame.Position=targetPosition;targetFrame.Rotation=drawing.FrameRotation;targetFrame.ScaleFactors=new Scale3d(drawing.FrameScale[0],drawing.FrameScale[1],drawing.FrameScale[2]);repaired=true;
                }
                foreach(var group in drawing.Groups.Where(g=>g.Linked))
            {
                var definition=Record(db,tx,group.ContentDefinitionHandle);
                var approvedCache=Record(db,tx,group.CacheHandle);
                if(Hash(Contents(tx,approvedCache))!=group.ContentHash&&Hash(Contents(tx,definition))==group.ContentHash)
                {approvedCache.UpgradeOpen();Fill(tx,approvedCache,Contents(tx,definition));repaired=true;}
                var instance=Model.Resolve(tx,db,group.ContentHandle) as BlockReference;
                var position=new Point3d(group.Offset[0],group.Offset[1],group.Offset[2]);
                bool dirty=Hash(Contents(tx,definition))!=group.ContentHash||instance==null||instance.Position.DistanceTo(position)>1e-8||Math.Abs(instance.Rotation)>1e-8||Math.Abs(instance.ScaleFactors.X-1)>1e-8||Math.Abs(instance.ScaleFactors.Y-1)>1e-8||instance.BlockTableRecord!=definition.ObjectId||instance.Layer!=group.Layer||!instance.Visible;
                if(dirty)
                {
                    var cache=Record(db,tx,group.CacheHandle);
                    if(Hash(Contents(tx,cache))!=group.ContentHash)throw new InvalidOperationException("Bản lưu hình liên kết đã thay đổi; cần xem trước và đồng bộ từ nguồn.");
                    Layer(db,tx,group.Layer,false);definition.UpgradeOpen();Fill(tx,definition,Contents(tx,cache));definition.Explodable=false;
                    if(instance==null){instance=new BlockReference(position,definition.ObjectId);Append(tx,(BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db),OpenMode.ForWrite),instance);group.ContentHandle=instance.Handle.ToString();}
                    else instance.UpgradeOpen();
                    instance.Position=position;instance.Rotation=0;instance.ScaleFactors=new Scale3d(1);instance.BlockTableRecord=definition.ObjectId;instance.Visible=true;instance.LayerId=Layer(db,tx,group.Layer,false);repaired=true;
                }
                Layer(db,tx,group.Layer,true);
            }
            }
            return repaired;
        }
    }
}
