using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using AICad.Contracts;

namespace Arch.Suite.Host
{
    internal static class LibraryPreferences
    {
        private const string ToolId="b40fc389e1784687b8e85403c213c024";
        internal static string FilePath(string appRoot)=>Path.Combine(WarehousePaths.Resolve(appRoot),"Catalog","addin-settings",ToolId+".json");
        internal static JObject Load(string appRoot)
        {
            string file=FilePath(appRoot);
            var result=File.Exists(file)?JObject.Parse(File.ReadAllText(file)):new JObject{["SchemaVersion"]=1,["Revision"]=0,["SourceId"]="ISO_QUANGANH",["Bindings"]=new JObject()};
            if((int?)result["SchemaVersion"]!=1||((int?)result["Revision"]==null||(int?)result["Revision"]<0)||(string)result["SourceId"]!="ISO_QUANGANH"||!(result["Bindings"] is JObject))throw new InvalidOperationException("Cấu hình thư viện Arch không tương thích; giữ nguyên file để đối chiếu.");
            return result;
        }
        internal static void Save(string appRoot,int expectedRevision,Dictionary<string,LayerRole> roles)
        {
            var file=FilePath(appRoot);Directory.CreateDirectory(Path.GetDirectoryName(file));
            using(var lease=new FileStream(file+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            {
                var settings=Load(appRoot);if((int)settings["Revision"]!=expectedRevision)throw new InvalidOperationException("Lựa chọn thư viện đã đổi. Đóng và mở lại bộ chọn trước khi lưu.");
                var bindings=(JObject)settings["Bindings"];
                foreach(var item in roles)
                {
                    if(item.Value?.Id==null){bindings.Remove(item.Key);continue;}
                    bindings[item.Key]=new JObject{["SourceId"]=item.Value.SourceId,["Kind"]=item.Value.Kind,["Id"]=item.Value.Id};
                }
                settings["Revision"]=expectedRevision+1;
                AICad.Contracts.Json.Atomic(file,settings);
            }
        }
        internal static LayerRole Resolve(JObject preferences,string role,List<LayerRole> options)
        {
            var key=preferences["Bindings"]?[role];
            return key==null?null:options.SingleOrDefault(r=>r.SourceId==(string)key["SourceId"]&&r.Kind==(string)key["Kind"]&&r.Id==(string)key["Id"]);
        }
    }
}
