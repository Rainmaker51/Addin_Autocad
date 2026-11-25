using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Newtonsoft.Json;

namespace Arch.Suite.Host
{
    internal sealed class FloorDefinition
    {
        public string Id {get;set;}=Guid.NewGuid().ToString("N");
        public string Name {get;set;}
        public double ElevationMm {get;set;}
        public double HeightMm {get;set;}
        public double BeamMm {get;set;}
        public double WallHeightMm=>HeightMm-BeamMm;
        internal void Validate()
        {
            if(string.IsNullOrWhiteSpace(Name)||!Finite(HeightMm)||!Finite(BeamMm)||!Finite(ElevationMm)||HeightMm<=0||BeamMm<0||BeamMm>=HeightMm)
                throw new InvalidOperationException("Tầng cần tên, cao tầng dương và 0 ≤ cao dầm < cao tầng.");
        }
        private static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
    }
    internal sealed class LayerGroupDefinition
    {
        public string Kind {get;set;}
        public List<string> Layers {get;set;}=new List<string>();
    }
    internal static class SuiteKinds
    {
        internal static readonly string[] All={"Tường","Cửa đi","Cửa sổ","Trục","Nội thất","Thiết bị vệ sinh"};
        internal static string Classify(Transaction tx,Entity entity,FrameDocument data)
        {
            var block=entity as BlockReference;
            if(block!=null)
            {
                var name=((BlockTableRecord)tx.GetObject(block.IsDynamicBlock?block.DynamicBlockTableRecord:block.BlockTableRecord,OpenMode.ForRead)).Name;
                var doors=data.DoorRules.Where(d=>string.Equals(d.BlockName,name,StringComparison.OrdinalIgnoreCase)).ToList();
                if(doors.Count>0)return RegistrationService.ReadDoor(entity.Database,tx,new FrameSnapshot{Frame=new DrawingFrame()},new FrameCandidate{Handle=entity.Handle.ToString()},doors).Kind;
            }
            if(entity.Layer.Equals(data.WallRule.Layer,StringComparison.OrdinalIgnoreCase))return "Tường";
            var matches=data.LayerGroups.Where(g=>g.Layers.Contains(entity.Layer,StringComparer.OrdinalIgnoreCase)).Select(g=>g.Kind).Distinct().ToList();
            if(matches.Contains("Thiết bị vệ sinh")&&matches.All(k=>k=="Thiết bị vệ sinh"||k=="Nội thất"))return "Thiết bị vệ sinh";
            return matches.Count==0?null:matches.Count==1?matches[0]:"Cần xác nhận";
        }
        internal static FloorDefinition Floor(FrameDocument data,DrawingFrame frame)
        {
            var floor=data.Floors.SingleOrDefault(f=>f.Id==frame.FloorId);
            if(floor==null)throw new InvalidOperationException("Khung chưa gắn tầng có chiều cao. Lưu tầng rồi Lưu khung.");
            floor.Validate();return floor;
        }
        internal static void BindFloor(FrameDocument data,DrawingFrame frame)
        {
            var floor=data.Floors.SingleOrDefault(f=>string.Equals(f.Name,frame.Level,StringComparison.OrdinalIgnoreCase));
            frame.FloorId=floor?.Id;
        }
        internal static void Stamp(SourceRegistration source,FrameDocument data,DrawingFrame frame)
        {
            source.ElevationMm=(data.Floors.SingleOrDefault(f=>f.Id==frame.FloorId)?.ElevationMm??0)+source.SillMm;source.FloorId=frame.FloorId;source.Level=frame.Level;source.FrameName=frame.BlockName;
        }
        internal static SourceRegistration Register(Database db,Transaction tx,FrameDocument data,FrameSnapshot frame,FrameCandidate candidate,double? beamOverride=null)
        {
            Floor(data,frame.Frame);
            if(candidate.Status=="Cần xác nhận")throw new InvalidOperationException(candidate.Reason);
            var entity=Model.Resolve(tx,db,candidate.Handle);
            if(entity==null)throw new InvalidOperationException("Đối tượng nguồn đã mất.");
            string kind=Classify(tx,entity,data);
            if(kind==null||kind=="Cần xác nhận")throw new InvalidOperationException("Đối tượng chưa có nhóm duy nhất.");
            SourceRegistration source;
            if(kind=="Tường")
            {
                var floor=Floor(data,frame.Frame);double beam=beamOverride??floor.BeamMm;
                if(double.IsNaN(beam)||double.IsInfinity(beam)||beam<0||beam>=floor.HeightMm)throw new InvalidOperationException("Chiều cao dầm riêng phải từ 0 và nhỏ hơn cao tầng.");
                source=RegistrationService.ReadWall(db,tx,frame,candidate,new WallDefinition{Confirmed=data.WallRule.Confirmed,Layer=data.WallRule.Layer,HeightMm=floor.HeightMm-beam});
                source.BeamOverrideMm=beamOverride;source.Evidence+=" · cao tầng − cao dầm: "+floor.HeightMm+" − "+beam+" mm";
            }
            else if(kind=="Cửa đi"||kind=="Cửa sổ")source=RegistrationService.ReadDoor(db,tx,frame,candidate,data.DoorRules);
            else source=new SourceRegistration{FrameId=frame.Frame.Id,Handle=candidate.Handle,Kind=kind,Code=kind+"-"+candidate.Handle,Evidence="Layer đối tượng chèn / đối tượng rời: "+entity.Layer,Status="Đã đăng ký"};
            Stamp(source,data,frame.Frame);return source;
        }
        internal static List<SourceRegistration> Refresh(Database db,Transaction tx,FrameDocument data,DrawingFrame frame)
        {
            var snapshot=FrameReader.Read(db,tx,frame,true);
            var existing=data.Registrations.Where(r=>r.FrameId==frame.Id).ToList();
            var proposed=new List<SourceRegistration>();
            foreach(var previous in existing)
            {
                if(Model.Resolve(tx,db,previous.Handle)==null)continue;
                var candidate=snapshot.Candidates.SingleOrDefault(c=>c.Handle==previous.Handle);
                if(candidate==null)throw new InvalidOperationException("Nguồn #"+previous.Handle+" đã mất/ra ngoài khung. Quét và gỡ/đăng ký lại trước khi đồng bộ.");
                var current=Register(db,tx,data,snapshot,candidate,previous.BeamOverrideMm);current.Id=previous.Id;proposed.Add(current);
            }
            return proposed;
        }
    }
}
