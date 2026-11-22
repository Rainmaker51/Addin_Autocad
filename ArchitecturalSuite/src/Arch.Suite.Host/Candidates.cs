using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Arch.Suite.Host
{
    internal static class Candidates
    {
        internal sealed class Candidate
        {
            public string Kind {get;set;}
            public string[] Handles {get;set;}
            public string Layer {get;set;}
            public string Evidence {get;set;}
            public string Caption => Kind+"    #"+string.Join(" / ",Handles)+"    "+Layer;
        }
        internal static List<Candidate> Search(Document doc,Transaction tx,Model m)
        {
            if(m.Bounds==null)throw new InvalidOperationException("Chọn mặt bằng nguồn trước khi tìm ứng viên.");
            var result=new List<Candidate>();
            var buckets=new Dictionary<Tuple<int,int>,List<Line>>();
            var used=new HashSet<string>(m.Parts.SelectMany(p=>p.Handles).Concat(m.Outputs.Values.Select(o=>o.Handle)));
            var space=(BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(doc.Database),OpenMode.ForRead);
            foreach(ObjectId id in space)
            {
                var e=(Entity)tx.GetObject(id,OpenMode.ForRead);
                if(used.Contains(e.Handle.ToString()) || !(e is Line || e is BlockReference))continue;
                var bounds=e.Bounds;
                if(!bounds.HasValue)continue;
                var a=bounds.Value.MinPoint;var b=bounds.Value.MaxPoint;
                if(a.X<m.Bounds[0] || a.Y<m.Bounds[1] || b.X>m.Bounds[2] || b.Y>m.Bounds[3])continue;
                var block=e as BlockReference;
                if(block!=null){
                    var name=((BlockTableRecord)tx.GetObject(block.IsDynamicBlock?block.DynamicBlockTableRecord:block.BlockTableRecord,OpenMode.ForRead)).Name;
                    var nameLower=name.ToLowerInvariant();
                    if(new[]{"cua","cửa","door"}.Any(nameLower.Contains) && !new[]{"handle","tay nam","tay nắm"}.Any(nameLower.Contains))
                        result.Add(new Candidate{Kind="Cửa",Layer=e.Layer,Handles=new[]{e.Handle.ToString()},Evidence="Gợi ý theo tên block "+name+". Xác nhận đây là cửa và chọn tường chủ."});
                    continue;
                }
                var line=(Line)e;
                if(line.Length<200 || Math.Abs(line.StartPoint.Z-line.EndPoint.Z)>0.01)continue;
                var mid=line.StartPoint+(line.EndPoint-line.StartPoint)*0.5;
                int x=(int)Math.Floor(mid.X/500),y=(int)Math.Floor(mid.Y/500);
                for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++){
                    List<Line> near;if(!buckets.TryGetValue(Tuple.Create(x+dx,y+dy),out near))continue;
                    foreach(var other in near){
                        if(line.Layer!=other.Layer || Math.Abs(line.Length-other.Length)>1)continue;
                        Point3d start,end;double thickness;
                        try{Model.WallGeometry(line,other,out start,out end,out thickness);}catch(InvalidOperationException){continue;}
                        if(thickness>=50 && thickness<=500)result.Add(new Candidate{Kind="Tường",Layer=e.Layer,Handles=new[]{other.Handle.ToString(),line.Handle.ToString()},Evidence="Hai Line song song, cùng layer, khớp đầu. Khoảng cách "+thickness.ToString("0.#")+" mm. Cần duyệt."});
                    }
                }
                var key=Tuple.Create(x,y);List<Line> bucket;
                if(!buckets.TryGetValue(key,out bucket))buckets[key]=bucket=new List<Line>();bucket.Add(line);
            }
            return result;
        }
    }
}
