using System;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;

namespace Arch.Suite.Host
{
    internal sealed class SourceFailure : InvalidOperationException
    {
        public string SourceHandle {get;}
        private static string Position(Entity source)
        {
            try{var bounds=source.GeometricExtents;var p=bounds.MinPoint+(bounds.MaxPoint-bounds.MinPoint)*0.5;return " · vị trí "+p;}catch(Autodesk.AutoCAD.Runtime.Exception){return "";}
        }
        public SourceFailure(string stage,string kind,Entity source,string path,Exception inner)
            :base(stage+" · "+kind+" · #"+source.Handle+" · layer "+source.Layer+" · "+path+Position(source)+": "+inner.Message,inner){SourceHandle=source.Handle.ToString();}
    }
    internal static class SuiteDiagnostics
    {
        internal static string Write(Document doc,string operation,Exception error)
        {
            try
            {
                var folder=Environment.GetEnvironmentVariable("ARCH_SUITE_TEST_LOGS");
                if(string.IsNullOrWhiteSpace(folder))folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArchitecturalSuite","logs");
                Directory.CreateDirectory(folder);var file=Path.Combine(folder,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff")+"-"+Guid.NewGuid().ToString("N")+".log");
                File.WriteAllText(file,"Arch 0.6.0\nDWG: "+doc.Name+"\nOperation: "+operation+"\nUTC: "+DateTime.UtcNow.ToString("o")+"\n"+error);return file;
            }
            catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}
        }
    }
    internal static class ViewNavigation
    {
        internal static Matrix3d WorldToDisplay(ViewTableRecord view)
        {
            var matrix=Matrix3d.PlaneToWorld(view.ViewDirection);
            matrix=Matrix3d.Displacement(view.Target-Point3d.Origin)*matrix;
            matrix=Matrix3d.Rotation(-view.ViewTwist,view.ViewDirection,view.Target)*matrix;
            return matrix.Inverse();
        }
        internal static void Fit(ViewTableRecord view,Extents3d bounds)
        {
            if(view.PerspectiveEnabled)throw new InvalidOperationException("Góc nhìn phối cảnh chưa hỗ trợ zoom chính xác; chuyển về góc nhìn trực giao rồi thử lại.");
            var displayed=bounds;displayed.TransformBy(WorldToDisplay(view));
            double ratio=view.Height>0&&view.Width>0?view.Width/view.Height:1;
            double width=Math.Max(1,displayed.MaxPoint.X-displayed.MinPoint.X),height=Math.Max(1,displayed.MaxPoint.Y-displayed.MinPoint.Y);
            view.Height=Math.Max(height,width/ratio)*1.5;view.Width=view.Height*ratio;
            view.CenterPoint=new Point2d((displayed.MinPoint.X+displayed.MaxPoint.X)*0.5,(displayed.MinPoint.Y+displayed.MaxPoint.Y)*0.5);
        }
        internal static void Locate(Document doc,Transaction tx,string handle)
        {
            if(string.IsNullOrWhiteSpace(handle))throw new InvalidOperationException("Chọn một dòng đối tượng trước khi đi tới.");
            if(!doc.Database.TileMode)throw new InvalidOperationException("Chuyển sang Model để định vị đối tượng nguồn.");
            var entity=Model.Resolve(tx,doc.Database,handle);if(entity==null)throw new InvalidOperationException("Đối tượng đã mất. Quét lại khung.");
            using(var view=doc.Editor.GetCurrentView()){Fit(view,entity.GeometricExtents);doc.Editor.SetCurrentView(view);}
            doc.Editor.SetImpliedSelection(new[]{entity.ObjectId});entity.Highlight();
        }
    }
}
