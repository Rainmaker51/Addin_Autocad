using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

namespace Arch.Suite.Host
{
    public class Inventory
    {
        [CommandMethod("QAARCHSCAN")]
        public void Scan()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            try { Export(doc); }
            catch (System.Exception error)
            {
                doc.Editor.WriteMessage("\nKhông thể hoàn tất kiểm kê: " + error.Message);
                Host.ShowResult("Không thể hoàn tất kiểm kê\n" + error.Message);
            }
        }

        private static void Export(Autodesk.AutoCAD.ApplicationServices.Document doc)
        {
            var target = doc.Editor.GetFileNameForSave("Đường dẫn báo cáo JSON mới");
            if (target.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                Host.ShowResult("Đã hủy kiểm kê.");
                return;
            }
            var path = Path.ChangeExtension(target.StringResult, ".json");
            var rows = new List<object>();
            var types = new SortedDictionary<string,int>(StringComparer.Ordinal);
            var layouts = new List<string>();
            var db = doc.Database;
            using (var tx = db.TransactionManager.StartOpenCloseTransaction())
            {
                var blocks = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId id in blocks)
                {
                    var record = (BlockTableRecord)tx.GetObject(id, OpenMode.ForRead);
                    if (!record.IsLayout) continue;
                    var layout = ((Layout)tx.GetObject(record.LayoutId, OpenMode.ForRead)).LayoutName;
                    layouts.Add(layout);
                    foreach (ObjectId eid in record)
                    {
                        var entity = (Entity)tx.GetObject(eid, OpenMode.ForRead);
                        var type = eid.ObjectClass.DxfName;
                        int count;
                        types.TryGetValue(type, out count);
                        types[type] = count + 1;
                        var block = entity as BlockReference;
                        var name = block == null ? null : ((BlockTableRecord)tx.GetObject(block.IsDynamicBlock ? block.DynamicBlockTableRecord : block.BlockTableRecord, OpenMode.ForRead)).Name;
                        var bounds = entity.Bounds;
                        rows.Add(new {layout, handle = eid.Handle.ToString(), type, layer = entity.Layer, block = name,
                            extent = bounds.HasValue ? new [] {bounds.Value.MinPoint.X,bounds.Value.MinPoint.Y,bounds.Value.MaxPoint.X,bounds.Value.MaxPoint.Y} : null});
                    }
                }
            }
            var json = new JavaScriptSerializer {MaxJsonLength = int.MaxValue}.Serialize(new {drawing = Path.GetFileName(doc.Name), units = db.Insunits.ToString(), layouts, types, entities = rows});
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json);
                var requested = path;
                for (var number = 2; ; number++)
                {
                    try { File.Move(temporary, path); break; }
                    catch (IOException) when (File.Exists(path))
                    {
                        path = Path.Combine(Path.GetDirectoryName(requested),
                            Path.GetFileNameWithoutExtension(requested) + " (" + number + ").json");
                    }
                }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            doc.Editor.WriteMessage("\nĐã xuất " + rows.Count + " thực thể: " + path);
            Host.ShowResult("Kết quả kiểm kê · " + Path.GetFileName(doc.Name) +
                "\n" + rows.Count.ToString("N0") + " thực thể   |   " + layouts.Count + " không gian bản vẽ\n\n" +
                string.Join("\n", types.OrderByDescending(pair => pair.Value)
                    .Select(pair => pair.Key + "    " + pair.Value.ToString("N0"))) +
                "\n\nBáo cáo đã lưu\n" + path);
        }
    }
}
