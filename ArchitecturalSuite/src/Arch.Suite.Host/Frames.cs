using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json;

namespace Arch.Suite.Host
{
    internal sealed class FrameColumn
    {
        public bool Use { get; set; } = true;
        public string Tag { get; set; }
        public string Label { get; set; }
    }

    internal sealed class DrawingFrame
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Handle { get; set; }
        public string BlockName { get; set; }
        public string BoundaryLayer { get; set; }
        public string Level { get; set; }
        public string FloorId {get;set;}
        public bool IsDerived {get;set;}
        public string NameTag { get; set; }
        public string CodeTag {get;set;}
        public List<FrameColumn> Columns { get; set; } = new List<FrameColumn>();
        public string WallLayer { get; set; } = "QA_KT_Tuong";
    }

    internal sealed class FrameDocument
    {
        public int Schema { get; set; } = 2;
        public int Revision { get; set; }
        public List<FloorDefinition> Floors {get;set;}=new List<FloorDefinition>();
        public List<LayerGroupDefinition> LayerGroups {get;set;}=new List<LayerGroupDefinition>();
        public List<LinkedDrawing> Drawings {get;set;}=new List<LinkedDrawing>();
        public FrameProfile Profile { get; set; }
        public WallDefinition WallRule { get; set; } = new WallDefinition();
        public List<DoorDefinition> DoorRules { get; set; } = new List<DoorDefinition>();
        public List<SourceRegistration> Registrations { get; set; } = new List<SourceRegistration>();
        public List<DrawingFrame> Frames { get; set; } = new List<DrawingFrame>();
        private const string Key = "ARCH_SUITE_FRAMES_V1";

        internal static FrameDocument Load(Database db, Transaction tx)
        {
            var nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            if (!nod.Contains(Key)) return new FrameDocument();
            var record = tx.GetObject(nod.GetAt(Key), OpenMode.ForRead) as Xrecord;
            if (record == null) throw new InvalidOperationException("Dữ liệu khung không hợp lệ.");
            using (var data = record.Data)
            {
                if (data == null) throw new InvalidOperationException("Dữ liệu khung bị trống.");
                var values = data.AsArray();
                if (values.Any(v => v.TypeCode != (int)DxfCode.Text || !(v.Value is string)))
                    throw new InvalidOperationException("Dữ liệu khung không đúng cấu trúc.");
                var result = JsonConvert.DeserializeObject<FrameDocument>(string.Concat(values.Select(v => (string)v.Value)));
                if (result == null || (result.Schema != 1 && result.Schema != 2) || result.Frames == null ||
                    result.Frames.Any(f => f == null || string.IsNullOrWhiteSpace(f.Id) || f.Columns == null) ||
                    result.Frames.Select(f => f.Id).Distinct().Count() != result.Frames.Count ||
                    result.Frames.Select(f => f.Handle).Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Frames.Count)
                    throw new InvalidOperationException("Phiên bản hoặc danh tính khung không hợp lệ.");
                if(result.WallRule==null)result.WallRule=new WallDefinition();
                if(result.DoorRules==null)result.DoorRules=new List<DoorDefinition>();
                if(result.Schema==1)foreach(var door in result.DoorRules)door.NeedsKindConfirmation=true;
                if(result.Registrations==null)result.Registrations=new List<SourceRegistration>();
                result.Floors=result.Floors??new List<FloorDefinition>();
                result.LayerGroups=result.LayerGroups??new List<LayerGroupDefinition>();
                result.Drawings=result.Drawings??new List<LinkedDrawing>();
                foreach(var frame in result.Frames)if(frame.FloorId==null)SuiteKinds.BindFloor(result,frame);
                foreach(var source in result.Registrations)
                {
                    var owner=result.Frames.SingleOrDefault(f=>f.Id==source.FrameId);
                    if(owner!=null)SuiteKinds.Stamp(source,result,owner);
                }
                return result;
            }
        }

        internal void Save(Database db, Transaction tx)
        {
            DrawingLinks.KeepCaches(db,tx,this);
            Schema=2;Revision++;
            var nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            Xrecord record;
            if (nod.Contains(Key)) record = (Xrecord)tx.GetObject(nod.GetAt(Key), OpenMode.ForWrite);
            else
            {
                nod.UpgradeOpen();
                record = new Xrecord();
                nod.SetAt(Key, record);
                tx.AddNewlyCreatedDBObject(record, true);
            }
            string json = JsonConvert.SerializeObject(this);
            var chunks = new List<TypedValue>();
            for (int i = 0; i < json.Length; i += 128)
                chunks.Add(new TypedValue((int)DxfCode.Text, json.Substring(i, Math.Min(128, json.Length - i))));
            using (var data = new ResultBuffer(chunks.ToArray())) record.Data = data;
        }
    }

    internal sealed class FrameShape
    {
        public double[] X { get; set; }
        public double[] Y { get; set; }
        public bool Closed { get; set; }
        public bool Crossing { get; set; }
    }

    internal sealed class FrameCandidate
    {
        public string Handle { get; set; }
        public string Kind { get; set; }
        public string Layer { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
        public string Registered { get; set; } = "Chưa đăng ký";
    }

    internal sealed class FrameSnapshot
    {
        public DrawingFrame Frame { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<string> BoundaryLayers { get; set; } = new List<string>();
        public Point2d[] Boundary { get; set; }
        public string Status { get; set; }
        public string Title { get; set; }
        public string Code { get; set; }
        public List<FrameCandidate> Candidates { get; set; } = new List<FrameCandidate>();
        public List<FrameShape> Shapes { get; set; } = new List<FrameShape>();
    }

    internal static class FrameReader
    {
        internal static BlockReference Resolve(Database db, Transaction tx, string handle)
        {
            long number;
            ObjectId id;
            if (!long.TryParse(handle, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out number) ||
                !db.TryGetObjectId(new Handle(number), out id) || id.IsNull || id.IsErased)
                throw new InvalidOperationException("Khung đã mất. Chọn lại khung trong CAD.");
            var block = tx.GetObject(id, OpenMode.ForRead) as BlockReference;
            if (block == null || block.OwnerId != SymbolUtilityServices.GetBlockModelSpaceId(db))
                throw new InvalidOperationException("Chọn block khung trực tiếp trong Model Space.");
            return block;
        }

        internal static FrameSnapshot Read(Database db, Transaction tx, DrawingFrame frame, bool scan)
        {
            var block = Resolve(db, tx, frame.Handle);
            var definition = (BlockTableRecord)tx.GetObject(block.BlockTableRecord, OpenMode.ForRead);
            if (definition.IsFromExternalReference) throw new InvalidOperationException("Chọn block khung trong DWG, không dùng Xref.");
            var named = (BlockTableRecord)tx.GetObject(
                block.IsDynamicBlock ? block.DynamicBlockTableRecord : block.BlockTableRecord, OpenMode.ForRead);
            if (!string.IsNullOrEmpty(frame.BlockName) && frame.BlockName != named.Name)
                throw new InvalidOperationException("Định nghĩa khung đã thay đổi. Đăng ký lại khung.");
            var result = new FrameSnapshot { Frame = frame };
            foreach (ObjectId id in block.AttributeCollection)
            {
                var attribute = (AttributeReference)tx.GetObject(id, OpenMode.ForRead);
                if (result.Attributes.ContainsKey(attribute.Tag)) throw new InvalidOperationException("Tag ATT trùng trong block: " + attribute.Tag);
                result.Attributes.Add(attribute.Tag, attribute.TextString);
            }
            foreach (ObjectId id in definition)
            {
                var attribute = tx.GetObject(id, OpenMode.ForRead) as AttributeDefinition;
                if (attribute != null && attribute.Constant && !result.Attributes.ContainsKey(attribute.Tag))
                    result.Attributes.Add(attribute.Tag, attribute.TextString);
            }
            result.Title = Attribute(result, frame.NameTag);
            result.Code = Attribute(result, frame.CodeTag??frame.Columns.FirstOrDefault(c => c.Use)?.Tag);
            var boundaries = definition.Cast<ObjectId>().Select(id => tx.GetObject(id, OpenMode.ForRead))
                .OfType<Polyline>().ToList();
            result.BoundaryLayers = boundaries.Select(p => p.Layer).Distinct().OrderBy(n => n).ToList();
            if (string.IsNullOrWhiteSpace(frame.BoundaryLayer))
            {
                result.Status = "Chọn layer biên trong block.";
                return result;
            }
            var selected = boundaries.Where(p => p.Layer.Equals(frame.BoundaryLayer, StringComparison.OrdinalIgnoreCase)).ToList();
            if (selected.Count != 1) throw new InvalidOperationException("Cần đúng 1 polyline biên trên layer " + frame.BoundaryLayer + "; tìm thấy " + selected.Count + ".");
            Polyline boundary = selected[0];
            if (!boundary.Closed || boundary.NumberOfVertices < 3 ||
                Enumerable.Range(0, boundary.NumberOfVertices).Any(i => Math.Abs(boundary.GetBulgeAt(i)) > 1e-10))
                throw new InvalidOperationException("Biên phải là polyline kín, ít nhất 3 đỉnh và các cạnh thẳng.");
            if ((block.Normal - Vector3d.ZAxis).Length > 1e-8 ||
                block.ScaleFactors.X <= 0 || block.ScaleFactors.Y <= 0 ||
                Math.Abs(block.ScaleFactors.X - block.ScaleFactors.Y) > 1e-8)
                throw new InvalidOperationException("Khung cần nằm trên mặt phẳng XY và scale đều, dương.");
            result.Boundary = Enumerable.Range(0, boundary.NumberOfVertices).Select(i =>
            {
                var point = boundary.GetPoint3dAt(i).TransformBy(block.BlockTransform);
                return new Point2d(point.X, point.Y);
            }).ToArray();
            ValidatePolygon(result.Boundary);
            result.Status = "Biên hợp lệ";
            if (scan) Scan(db, tx, result);
            return result;
        }

        internal static string Attribute(FrameSnapshot snapshot, string tag)
        {
            string value;
            return tag != null && snapshot.Attributes.TryGetValue(tag, out value) ? value : "";
        }

        internal static void ValidatePolygon(Point2d[] polygon)
        {
            if (polygon == null || polygon.Length < 3) throw new InvalidOperationException("Biên cần ít nhất 3 đỉnh.");
            double area = 0;
            Point2d origin = polygon[0];
            for (int i = 0; i < polygon.Length; i++)
            {
                Point2d a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                if (a.GetDistanceTo(b) < 1e-7) throw new InvalidOperationException("Biên có cạnh trùng/độ dài bằng 0.");
                area += (a.X - origin.X) * (b.Y - origin.Y) - (b.X - origin.X) * (a.Y - origin.Y);
                for (int j = i + 1; j < polygon.Length; j++)
                {
                    if (j == i + 1 || (i == 0 && j == polygon.Length - 1)) continue;
                    if (Intersects(a, b, polygon[j], polygon[(j + 1) % polygon.Length]))
                        throw new InvalidOperationException("Biên tự cắt; chỉnh biên trước khi đăng ký.");
                }
            }
            if (Math.Abs(area) < 1e-6) throw new InvalidOperationException("Biên không có diện tích.");
        }

        private static double Cross(Point2d a, Point2d b, Point2d c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }
        private static bool OnSegment(Point2d a, Point2d b, Point2d p)
        {
            return Math.Abs(Cross(a, b, p)) <= 1e-7 * Math.Max(1, a.GetDistanceTo(b)) &&
                p.X >= Math.Min(a.X, b.X) - 1e-7 && p.X <= Math.Max(a.X, b.X) + 1e-7 &&
                p.Y >= Math.Min(a.Y, b.Y) - 1e-7 && p.Y <= Math.Max(a.Y, b.Y) + 1e-7;
        }
        private static bool Intersects(Point2d a, Point2d b, Point2d c, Point2d d)
        {
            double x = Cross(a, b, c), y = Cross(a, b, d), z = Cross(c, d, a), w = Cross(c, d, b);
            return (x * y < 0 && z * w < 0) || OnSegment(a, b, c) || OnSegment(a, b, d) ||
                OnSegment(c, d, a) || OnSegment(c, d, b);
        }
        internal static bool Contains(Point2d[] polygon, Point2d point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Point2d a = polygon[j], b = polygon[i];
                if (OnSegment(a, b, point)) return true;
                if ((a.Y > point.Y) != (b.Y > point.Y) &&
                    point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
            }
            return inside;
        }

        internal static void Scan(Database db, Transaction tx, FrameSnapshot snapshot)
        {
            var polygon = snapshot.Boundary;
            var model = (BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
            var managed=FrameDocument.Load(db,tx);
            foreach (ObjectId id in model)
            {
                if (id.Handle.ToString() == snapshot.Frame.Handle) continue;
                if(managed.Frames.Any(f=>f.Handle==id.Handle.ToString())||managed.Drawings.SelectMany(d=>d.Groups).Any(g=>g.ContentHandle==id.Handle.ToString()))continue;
                var entity = tx.GetObject(id, OpenMode.ForRead) as Entity;
                if (entity == null || !entity.Visible) continue;
                Extents3d? bounds;
                try { bounds = entity.Bounds; }
                catch (Autodesk.AutoCAD.Runtime.Exception) { continue; }
                if (!bounds.HasValue) continue;
                var min = bounds.Value.MinPoint; var max = bounds.Value.MaxPoint;
                var corners = new[] { new Point2d(min.X, min.Y), new Point2d(max.X, min.Y),
                    new Point2d(max.X, max.Y), new Point2d(min.X, max.Y) };
                bool intersects = corners.Any(p => Contains(polygon, p)) || polygon.Any(p =>
                    p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y);
                bool edgeCrosses = false;
                for (int i = 0; i < polygon.Length; i++)
                    for (int j = 0; j < 4; j++)
                        if (Intersects(polygon[i], polygon[(i + 1) % polygon.Length], corners[j], corners[(j + 1) % 4]))
                            edgeCrosses = true;
                if (!intersects && !edgeCrosses) continue;
                bool crossing = !corners.All(p => Contains(polygon, p));
                // An extents box crossing a concave edge is deliberately conservative.
                if (edgeCrosses) crossing = true;
                var polyline = entity as Polyline;
                string kind = "Tham chiếu";
                string reason = crossing ? "Bao hình chạm/cắt biên; cần xác nhận." : "Nằm trong phạm vi.";
                if (entity.Layer.Equals(snapshot.Frame.WallLayer, StringComparison.OrdinalIgnoreCase))
                {
                    kind = "Nét tường";
                    if (polyline == null) reason += " Cần polyline để kiểm tra vùng tường.";
                    else if (!polyline.Closed) reason += " Polyline chưa Closed.";
                    else
                    {
                        try
                        {
                            if (Enumerable.Range(0, polyline.NumberOfVertices).Any(i => Math.Abs(polyline.GetBulgeAt(i)) > 1e-10))
                                reason += " Vùng có cung, cần duyệt riêng.";
                            else ValidatePolygon(Enumerable.Range(0, polyline.NumberOfVertices)
                                .Select(i => { var p = polyline.GetPoint3dAt(i); return new Point2d(p.X, p.Y); }).ToArray());
                        }
                        catch (InvalidOperationException error) { reason += " " + error.Message; }
                    }
                }
                else if (entity is BlockReference)
                {
                    var block = (BlockReference)entity;
                    string name = ((BlockTableRecord)tx.GetObject(block.IsDynamicBlock ?
                        block.DynamicBlockTableRecord : block.BlockTableRecord, OpenMode.ForRead)).Name;
                    kind = block.IsDynamicBlock ? "Block dynamic" : "Block";
                    reason += " " + name + "; cần cấu hình nhóm/property.";
                }
                bool review = crossing || (kind == "Nét tường" && (polyline == null || !polyline.Closed || reason.Length > "Nằm trong phạm vi.".Length));
                snapshot.Candidates.Add(new FrameCandidate { Handle = entity.Handle.ToString(), Kind = kind,
                    Layer = entity.Layer, Status = review ? "Cần xác nhận" : "Trong phạm vi", Reason = reason });
                if (snapshot.Shapes.Count >= 3000) continue;
                Point2d[] points = corners;
                bool closed = true;
                if (polyline != null && polyline.NumberOfVertices > 1 && polyline.NumberOfVertices <= 1000 &&
                    Enumerable.Range(0, polyline.NumberOfVertices).All(i => Math.Abs(polyline.GetBulgeAt(i)) < 1e-10))
                {
                    points = Enumerable.Range(0, polyline.NumberOfVertices)
                        .Select(i => { var p = polyline.GetPoint3dAt(i); return new Point2d(p.X, p.Y); }).ToArray();
                    closed = polyline.Closed;
                }
                var line = entity as Line;
                if (line != null) { points = new[] { new Point2d(line.StartPoint.X, line.StartPoint.Y), new Point2d(line.EndPoint.X, line.EndPoint.Y) }; closed = false; }
                snapshot.Shapes.Add(new FrameShape { X = points.Select(p => p.X).ToArray(), Y = points.Select(p => p.Y).ToArray(), Closed = closed, Crossing = crossing });
            }
        }
    }
}
