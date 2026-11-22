using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace Arch.Suite.Host
{
    internal static class Schedules
    {
        private static string Number(double n) => n.ToString("0.###",CultureInfo.InvariantCulture);
        internal static string Fingerprint(Table t)
        {
            var values=new List<string>{t.Position.ToString(),t.Direction.ToString(),t.Layer,t.TableStyle.Handle.ToString()};
            foreach(var row in t.Rows)values.Add(Number(row.Height));
            foreach(var col in t.Columns)values.Add(Number(col.Width));
            for(int r=0;r<t.Rows.Count;r++)for(int c=0;c<t.Columns.Count;c++)values.Add(t.Cells[r,c].TextString);
            using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\u001f",values))));
        }
        internal static string Place(Document doc,Transaction tx,Model model,bool doors,bool preview=false,bool approved=false)
        {
            var key=doors?"DOOR_SCHEDULE":"WALL_SCHEDULE";
            var data=new List<string[]> {doors?new[]{"Cửa","Tầng","Tường chủ","Rộng (mm)","Cao (mm)","Neo (mm)"}:new[]{"Tường","Tầng","Dài (mm)","Dày (mm)","Cao (mm)","Ròng (m²)"}};
            foreach(var p in model.Parts.Where(p=>p.Kind==(doors?"Door":"Wall")).OrderBy(p=>p.Level).ThenBy(p=>p.Code))
            {
                if(doors)data.Add(new[]{p.Code,p.Level,model.Parts.Single(w=>w.Id==p.HostId).Code,Number(p.Width),Number(p.Height),Number(p.Offset)});
                else
                {
                    Autodesk.AutoCAD.Geometry.Point3d a,b;double thickness;
                    Model.Geometry(tx,doc.Database,p,out a,out b,out thickness);
                    var length=a.DistanceTo(b);var area=length*p.Height-model.Parts.Where(d=>d.HostId==p.Id).Sum(d=>d.Width*d.Height);
                    data.Add(new[]{p.Code,p.Level,Number(length),Number(thickness),Number(p.Height),Number(area/1e6)});
                }
            }
            Output link;Table table;
            if(model.Tables.TryGetValue(key,out link)){
                table=Model.Resolve(tx,doc.Database,link.Handle) as Table;
                if(table==null || Fingerprint(table)!=link.Signature)throw new InvalidOperationException("Bảng đã mất hoặc được chỉnh tay. Kiểm tra handle "+link.Handle);
                if(table.Rows.Count==data.Count && Enumerable.Range(0,data.Count).All(r=>Enumerable.Range(0,6).All(c=>table.Cells[r,c].TextString==data[r][c]))){
                    if(!preview)Host.ShowResult("Bảng đã khớp với nguồn.");return null;
                }
                if(preview)return key;
                var prompt=new PromptKeywordOptions("\nCập nhật bảng "+key+" [Apply/Skip] <Skip>: ","Apply Skip"){AllowNone=true};
                if(!approved && doc.Editor.GetKeywords(prompt).StringResult!="Apply")return null;
                table.UpgradeOpen();
            } else {
                var pos=Model.Point(doc.Editor,"Chọn vị trí bảng "+key);
                table=new Table{TableStyle=doc.Database.Tablestyle,Position=pos};
                var space=(BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(doc.Database),OpenMode.ForWrite);
                space.AppendEntity(table);tx.AddNewlyCreatedDBObject(table,true);
            }
            if(table.Rows.Count!=data.Count || table.Columns.Count!=6)table.SetSize(data.Count,6);
            if(link==null){
                if(table.Cells[0,0].IsMerged==true)table.UnmergeCells(CellRange.Create(table,0,0,0,5));
                table.SetColumnWidth(1250);table.SetRowHeight(320);
            }
            for(int r=0;r<data.Count;r++)for(int c=0;c<6;c++){
                var cell=table.Cells[r,c];if(cell.TextString!=data[r][c])cell.TextString=data[r][c];
                if(link==null)cell.TextHeight=120;
            }
            table.GenerateLayout();
            model.Tables[key]=new Output{Handle=table.Handle.ToString(),Signature=Fingerprint(table)};model.Save(tx,doc.Database);
            Host.ShowResult(key+" · "+(data.Count-1)+" cấu kiện");
            return key;
        }
    }
}
