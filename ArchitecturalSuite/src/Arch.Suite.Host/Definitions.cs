using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json;

namespace Arch.Suite.Host
{
    internal sealed class FrameProfile
    {
        public string BlockName { get; set; }
        public string BoundaryLayer { get; set; }
        public string NameTag { get; set; }
        public string Level { get; set; }
        public string WallLayer { get; set; }
        public List<FrameColumn> Columns { get; set; } = new List<FrameColumn>();
        internal DrawingFrame ForBlock(string handle)
        {
            return new DrawingFrame { Handle = handle, BlockName = BlockName, BoundaryLayer = BoundaryLayer,
                NameTag = NameTag, Level = Level, WallLayer = WallLayer,
                Columns = Columns.Select(c => new FrameColumn { Use=c.Use,Tag=c.Tag,Label=c.Label }).ToList() };
        }
        internal static FrameProfile From(DrawingFrame frame)
        {
            return JsonConvert.DeserializeObject<FrameProfile>(JsonConvert.SerializeObject(frame));
        }
    }

    internal sealed class WallDefinition
    {
        public bool Confirmed { get; set; }
        public string Layer { get; set; } = "QA_KT_Tuong";
        public double HeightMm { get; set; } = 3300;
    }
    internal sealed class DoorDefinition
    {
        public string BlockName { get; set; }
        public string WidthProperty { get; set; }
        public string HeightProperty { get; set; }
        public string HeightTag { get; set; }
        public double HeightMm { get; set; } = 2200;
        public string CodeTag { get; set; }
    }
    internal sealed class SourceRegistration
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string FrameId { get; set; }
        public string Handle { get; set; }
        public string Kind { get; set; }
        public string Code { get; set; }
        public double HeightMm { get; set; }
        public double WidthMm { get; set; }
        public double AreaMm2 { get; set; }
        public double[] OriginalBoundary { get; set; }
        public string Evidence { get; set; }
        public string Status { get; set; }
    }
    internal sealed class DoorSample
    {
        public string BlockName { get; set; }
        public Dictionary<string,double> Properties { get; set; } = new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string,string> Attributes { get; set; } = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    }

    internal static class RegistrationService
    {
        internal static void Validate(WallDefinition wall, IEnumerable<DoorDefinition> doors)
        {
            if (wall == null || !wall.Confirmed || string.IsNullOrWhiteSpace(wall.Layer) || !Positive(wall.HeightMm))
                throw new InvalidOperationException("Lưu định nghĩa với layer tường và chiều cao tường dương (mm) trước khi đăng ký.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var door in doors)
            {
                if (string.IsNullOrWhiteSpace(door.BlockName) || !names.Add(door.BlockName) ||
                    string.IsNullOrWhiteSpace(door.WidthProperty))
                    throw new InvalidOperationException("Mỗi loại block cửa cần một định nghĩa và property chiều rộng.");
                if (string.IsNullOrWhiteSpace(door.HeightProperty) && string.IsNullOrWhiteSpace(door.HeightTag) && !Positive(door.HeightMm))
                    throw new InvalidOperationException("Chọn nguồn chiều cao hoặc nhập chiều cao cửa dương.");
                if (!string.IsNullOrWhiteSpace(door.HeightProperty) && !string.IsNullOrWhiteSpace(door.HeightTag))
                    throw new InvalidOperationException("Chỉ chọn một nguồn chiều cao: property hoặc ATT.");
            }
        }
        private static bool Positive(double value) => value > 0 && !double.IsInfinity(value) && !double.IsNaN(value);
        internal static double MmPerUnit(Database db)
        {
            switch (db.Insunits)
            {
                case UnitsValue.Millimeters: return 1;
                case UnitsValue.Centimeters: return 10;
                case UnitsValue.Meters: return 1000;
                default: throw new InvalidOperationException("Cần khai báo INSUNITS mm, cm hoặc m trước khi đăng ký kích thước.");
            }
        }
        internal static DoorSample Describe(Transaction tx, BlockReference block)
        {
            var sample = new DoorSample { BlockName = ((BlockTableRecord)tx.GetObject(block.IsDynamicBlock ?
                block.DynamicBlockTableRecord : block.BlockTableRecord, OpenMode.ForRead)).Name };
            if (block.IsDynamicBlock)
                foreach (DynamicBlockReferenceProperty property in block.DynamicBlockReferencePropertyCollection)
                {
                    double value;
                    if (property.UnitsType.ToString()=="Distance" && property.Value != null && double.TryParse(Convert.ToString(property.Value, CultureInfo.InvariantCulture),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out value) && Positive(value))
                        sample.Properties[property.PropertyName] = value;
                }
            foreach (ObjectId id in block.AttributeCollection)
            {
                var att=(AttributeReference)tx.GetObject(id,OpenMode.ForRead);
                sample.Attributes[att.Tag] = att.TextString;
            }
            var definition=(BlockTableRecord)tx.GetObject(block.BlockTableRecord,OpenMode.ForRead);
            foreach(ObjectId id in definition)
            {
                var att=tx.GetObject(id,OpenMode.ForRead) as AttributeDefinition;
                if(att!=null && att.Constant && !sample.Attributes.ContainsKey(att.Tag))sample.Attributes[att.Tag]=att.TextString;
            }
            return sample;
        }
        internal static SourceRegistration ReadWall(Database db, Transaction tx, FrameSnapshot frame, FrameCandidate candidate, WallDefinition definition)
        {
            Validate(definition,new DoorDefinition[0]);
            var entity = Model.Resolve(tx, db, candidate.Handle);
            var wall = entity as Polyline;
            if (wall == null || !wall.Layer.Equals(definition.Layer,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Chọn Polyline trên đúng layer tường.");
            if (candidate.Status == "Cần xác nhận") throw new InvalidOperationException(candidate.Reason);
            if (!wall.Closed || wall.NumberOfVertices < 3 ||
                (wall.Normal - Vector3d.ZAxis).Length > 1e-8 ||
                Enumerable.Range(0,wall.NumberOfVertices).Any(i=>Math.Abs(wall.GetBulgeAt(i))>1e-10))
                throw new InvalidOperationException("Vùng tường cần Polyline kín, cạnh thẳng và nằm trên mặt phẳng XY.");
            var points=Enumerable.Range(0,wall.NumberOfVertices).Select(i=>wall.GetPoint3dAt(i)).ToArray();
            FrameReader.ValidatePolygon(points.Select(p=>new Point2d(p.X,p.Y)).ToArray());
            double factor=MmPerUnit(db);
            return new SourceRegistration { FrameId=frame.Frame.Id,Handle=candidate.Handle,Kind="Tường",
                HeightMm=definition.HeightMm,AreaMm2=Math.Abs(wall.Area)*factor*factor,
                OriginalBoundary=points.SelectMany(p=>new[]{p.X,p.Y,p.Z}).ToArray(),
                Evidence="Polyline kín · "+definition.Layer+" · "+wall.NumberOfVertices+" đỉnh",
                Status="Đã đăng ký vùng; chưa phân đoạn / ô chờ" };
        }
        internal static SourceRegistration ReadDoor(Database db, Transaction tx, FrameSnapshot frame, FrameCandidate candidate, IEnumerable<DoorDefinition> definitions)
        {
            if(candidate.Status=="Cần xác nhận")throw new InvalidOperationException(candidate.Reason);
            var block=Model.Resolve(tx,db,candidate.Handle) as BlockReference;
            if(block==null)throw new InvalidOperationException("Chọn block cửa.");
            var sample=Describe(tx,block);
            var definition=definitions.SingleOrDefault(d=>d.BlockName==sample.BlockName);
            if(definition==null)throw new InvalidOperationException("Chưa có định nghĩa cửa cho "+sample.BlockName+".");
            if((block.Normal-Vector3d.ZAxis).Length>1e-8 || Math.Abs(Math.Abs(block.ScaleFactors.X)-Math.Abs(block.ScaleFactors.Y))>1e-8)
                throw new InvalidOperationException("Cửa cần nằm trên XY và scale đều trong mặt bằng.");
            double width;
            if(!sample.Properties.TryGetValue(definition.WidthProperty,out width))
                throw new InvalidOperationException("Không đọc được property rộng "+definition.WidthProperty+".");
            double factor=MmPerUnit(db)*Math.Abs(block.ScaleFactors.X);
            double height=definition.HeightMm;
            string source="bảng loại";
            if(!string.IsNullOrWhiteSpace(definition.HeightProperty))
            {
                if(!sample.Properties.TryGetValue(definition.HeightProperty,out height))
                    throw new InvalidOperationException("Thiếu property cao "+definition.HeightProperty+".");
                height*=factor;source="property "+definition.HeightProperty;
            }
            else if(!string.IsNullOrWhiteSpace(definition.HeightTag))
            {
                string value;
                if(!sample.Attributes.TryGetValue(definition.HeightTag,out value) ||
                    !double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out height))
                    throw new InvalidOperationException("ATT chiều cao cần giá trị số mm.");
                source="ATT "+definition.HeightTag+" (mm)";
            }
            if(!Positive(height)||!Positive(width*factor))throw new InvalidOperationException("Kích thước cửa không hợp lệ.");
            string code;
            if(string.IsNullOrWhiteSpace(definition.CodeTag)||!sample.Attributes.TryGetValue(definition.CodeTag,out code))
                code=sample.BlockName;
            return new SourceRegistration{FrameId=frame.Frame.Id,Handle=candidate.Handle,Kind="Cửa",Code=code,
                WidthMm=width*factor,HeightMm=height,
                Evidence=sample.BlockName+" · rộng: "+definition.WidthProperty+" · cao: "+source,
                Status="Đã đăng ký bộ cửa; chờ xác nhận tường / ô chờ"};
        }
        internal static void Upsert(FrameDocument data, SourceRegistration source)
        {
            var prior=data.Registrations.SingleOrDefault(r=>r.Handle==source.Handle);
            if(prior!=null && prior.FrameId!=source.FrameId)
                throw new InvalidOperationException("Đối tượng đã đăng ký ở khung khác; cần xử lý phạm vi chồng nhau.");
            if(prior!=null){source.Id=prior.Id;data.Registrations.Remove(prior);}
            if(string.IsNullOrWhiteSpace(source.Code))source.Code="W-"+source.Handle;
            data.Registrations.Add(source);
        }
        internal static void ValidateFrame(DrawingFrame frame, FrameSnapshot snapshot)
        {
            if(string.IsNullOrWhiteSpace(frame.BoundaryLayer)||snapshot.Boundary==null)
                throw new InvalidOperationException("Chọn layer có đúng một biên kín hợp lệ trong block trước.");
            if(string.IsNullOrWhiteSpace(frame.Level))throw new InvalidOperationException("Nhập tầng.");
            if(string.IsNullOrWhiteSpace(frame.NameTag)||!snapshot.Attributes.ContainsKey(frame.NameTag))
                throw new InvalidOperationException("Khung thiếu tag tên bản vẽ "+frame.NameTag+".");
            var columns=frame.Columns.Where(c=>c.Use).ToList();
            if(columns.Count==0||columns.Any(c=>string.IsNullOrWhiteSpace(c.Label)||c.Label=="Tầng"||!snapshot.Attributes.ContainsKey(c.Tag))||
                columns.Select(c=>c.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=columns.Count)
                throw new InvalidOperationException("Kiểm tra cột ATT: có ít nhất 1 cột, tag tồn tại và tên riêng biệt, khác Tầng.");
        }
    }
}
