using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Arch.Suite.Host
{
    internal static class FloorCatalog
    {
        private static string Name(string value)=>(value??"").Trim().Normalize(System.Text.NormalizationForm.FormC);
        internal static FloorDefinition Ensure(FrameDocument data,string name)
        {
            name=Name(name);if(name=="")return null;
            var matches=data.Floors.Where(f=>string.Equals(Name(f.Name),name,StringComparison.OrdinalIgnoreCase)).ToList();
            if(matches.Count>1)throw new InvalidOperationException("Trùng tên tầng: "+name+". Sửa danh mục trước khi gán khung.");
            if(matches.Count==1)return matches[0];
            var result=new FloorDefinition{Name=name};
            if(data.Frames.Count>0)
            {
                var seed="ARCH_PENDING_FLOOR|"+string.Join("|",data.Frames.Select(f=>f.Id).OrderBy(id=>id,StringComparer.Ordinal))+"|"+name.ToUpperInvariant();
                using(var sha=System.Security.Cryptography.SHA256.Create())result.Id=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(seed))).Replace("-","").ToLowerInvariant().Substring(0,32);
            }
            data.Floors.Add(result);return result;
        }
        internal static void Assign(FrameDocument data,DrawingFrame frame,string name)
        {
            var floor=Ensure(data,name);frame.FloorId=floor?.Id;frame.Level=floor?.Name??Name(name);
        }
        internal static void Normalize(FrameDocument data)
        {
            foreach(var frame in data.Frames)
            {
                var floor=data.Floors.SingleOrDefault(f=>f.Id==frame.FloorId);
                if(floor==null)Assign(data,frame,frame.Level);else frame.Level=floor.Name;
            }
            foreach(var source in data.Registrations)
            {
                var frame=data.Frames.SingleOrDefault(f=>f.Id==source.FrameId);
                if(frame!=null){source.FloorId=frame.FloorId;source.Level=frame.Level;}
            }
        }
        internal static void ValidateEntry(FloorDefinition floor)
        {
            if(floor==null||string.IsNullOrWhiteSpace(floor.Name)||string.IsNullOrWhiteSpace(floor.Id))throw new InvalidOperationException("Tầng cần tên và ID.");
            if(double.IsNaN(floor.ElevationMm)||double.IsInfinity(floor.ElevationMm))throw new InvalidOperationException("Cao độ tầng không hợp lệ.");
            if(floor.HeightMm==0&&floor.BeamMm==0)return; // An explicit pending entry, never used to calculate quantities.
            floor.Validate();
        }
        internal static void Apply(FrameDocument data,IEnumerable<FloorDefinition> floors)
        {
            var items=floors.ToList();foreach(var floor in items){floor.Name=Name(floor.Name);ValidateEntry(floor);}
            if(items.Select(f=>f.Id).Distinct().Count()!=items.Count||items.Select(f=>f.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=items.Count)throw new InvalidOperationException("Tên/ID tầng không được trùng.");
            var changed=new HashSet<string>();
            foreach(var item in items)
            {
                var old=data.Floors.SingleOrDefault(f=>f.Id==item.Id);
                if(old==null||old.HeightMm!=item.HeightMm||old.BeamMm!=item.BeamMm||old.ElevationMm!=item.ElevationMm)changed.Add(item.Id);
            }
            foreach(var old in data.Floors.Where(f=>!items.Any(n=>n.Id==f.Id)))
                if(data.Frames.Any(f=>f.FloorId==old.Id))throw new InvalidOperationException("Không bỏ tầng đang có khung: "+old.Name);
            var profileFloor=data.Profile==null?null:data.Floors.FirstOrDefault(f=>f.Name==data.Profile.Level);
            data.Floors=items;
            if(profileFloor!=null&&data.Profile!=null)data.Profile.Level=items.FirstOrDefault(f=>f.Id==profileFloor.Id)?.Name;
            Normalize(data);
            foreach(var source in data.Registrations.Where(r=>changed.Contains(r.FloorId)&&r.Kind=="Tường"))source.Status="Cao tầng/dầm đã đổi; cần cập nhật tường";
            foreach(var drawing in data.Drawings)
            {
                var source=data.Frames.FirstOrDefault(f=>f.Id==drawing.SourceFrameId);
                var target=data.Frames.FirstOrDefault(f=>f.Id==drawing.TargetFrameId);
                if(source!=null&&target!=null){target.FloorId=source.FloorId;target.Level=source.Level;}
                if(source!=null&&changed.Contains(source.FloorId))drawing.NeedsUpdate=true;
            }
        }
    }
}
