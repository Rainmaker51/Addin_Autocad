using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Arch.Suite.Host;

public class FrameSmoke
{
    private static void Demand(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static string Root => Path.GetDirectoryName(Application.DocumentManager.MdiActiveDocument.Name);
    private static ObjectId Layer(Database db, Transaction tx, string name)
    {
        var layers = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForWrite);
        if (layers.Has(name)) return layers[name];
        var layer = new LayerTableRecord { Name = name };
        var id = layers.Add(layer); tx.AddNewlyCreatedDBObject(layer, true); return id;
    }
    private static Polyline Rectangle(double x, double y, double width, double height, bool closed = true)
    {
        var p = new Polyline(4);
        p.AddVertexAt(0, new Point2d(x, y), 0, 0, 0);
        p.AddVertexAt(1, new Point2d(x + width, y), 0, 0, 0);
        p.AddVertexAt(2, new Point2d(x + width, y + height), 0, 0, 0);
        p.AddVertexAt(3, new Point2d(x, y + height), 0, 0, 0);
        p.Closed = closed; return p;
    }
    private static void Add(BlockTableRecord owner, Entity entity, Transaction tx)
    {
        owner.AppendEntity(entity); tx.AddNewlyCreatedDBObject(entity, true);
    }
    [CommandMethod("ASFRAMESETUP")]
    public void Setup()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        try
        {
            using (var tx = doc.Database.TransactionManager.StartTransaction())
            {
                var db = doc.Database;
                var boundaryLayer = Layer(db, tx, "QA_KH_Quet");
                var wallLayer = Layer(db, tx, "QA_KT_Tuong");
                var blocks = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForWrite);
                var definition = new BlockTableRecord { Name = "FRAME_TEST" };
                blocks.Add(definition); tx.AddNewlyCreatedDBObject(definition, true);
                var boundary = Rectangle(0, 0, 1000, 800); boundary.LayerId = boundaryLayer;
                Add(definition, boundary, tx);
                foreach (var tag in new[] { "KHBV", "TENBANVE" })
                {
                    var att = new AttributeDefinition { Tag = tag, TextString = tag, Height = 10, Position = Point3d.Origin };
                    Add(definition, att, tx);
                }
                var model = (BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                var block = new BlockReference(new Point3d(10000, 20000, 0), definition.ObjectId)
                { Rotation = Math.PI / 6, ScaleFactors = new Scale3d(2) };
                Add(model, block, tx);
                foreach (ObjectId id in definition)
                {
                    var att = tx.GetObject(id, OpenMode.ForRead) as AttributeDefinition;
                    if (att == null) continue;
                    var reference = new AttributeReference();
                    reference.SetAttributeFromBlock(att, block.BlockTransform);
                    reference.TextString = att.Tag == "KHBV" ? "KT-01" : "Mặt bằng tầng 1";
                    block.AttributeCollection.AppendAttribute(reference); tx.AddNewlyCreatedDBObject(reference, true);
                }
                foreach (var p in new[] { Rectangle(100,100,100,100), Rectangle(900,100,200,100),
                    Rectangle(1200,100,100,100), Rectangle(400,100,100,100,false) })
                {
                    p.LayerId = wallLayer; p.TransformBy(block.BlockTransform); Add(model, p, tx);
                }
                var config = new DrawingFrame { Handle = block.Handle.ToString(), BlockName = "FRAME_TEST",
                    BoundaryLayer = "QA_KH_Quet", NameTag = "TENBANVE", Level = "T1",
                    Columns = new System.Collections.Generic.List<FrameColumn> {
                        new FrameColumn{Tag="KHBV",Label="Mã"}, new FrameColumn{Tag="TENBANVE",Label="Tên"} } };
                var snapshot = FrameReader.Read(db, tx, config, true);
                Demand(snapshot.Title == "Mặt bằng tầng 1" && snapshot.Code == "KT-01", "ATT mapping");
                Demand(snapshot.Candidates.Count == 3, "Rotated/translated scope contains unexpected candidates");
                Demand(snapshot.Candidates.Count(c => c.Status == "Cần xác nhận") == 2, "Open and crossing wall detection");
                Demand(snapshot.Boundary[0].GetDistanceTo(new Point2d(10000,20000)) < 1e-6, "Block transform");
                var data = FrameDocument.Load(db, tx);
                data.Frames.Add(config); data.Save(db, tx);
                var legacy = Model.Load(tx, db); legacy.ViewName = "Existing 0.2"; legacy.Save(tx, db);
                tx.Commit();
            }
            File.WriteAllText(Path.Combine(Root, "frame-setup.txt"), "PASS: rotated/scaled boundary, ATT, 3 candidates, 2 reviews, saved frame and legacy data.");
        }
        catch (System.Exception error) { File.WriteAllText(Path.Combine(Root, "frame-setup.txt"), error.ToString()); }
    }
    [CommandMethod("ASFRAMEREOPEN")]
    public void Reopen()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        try
        {
            using (var tx = doc.Database.TransactionManager.StartTransaction())
            {
                var db = doc.Database;
                var data = FrameDocument.Load(db, tx);
                Demand(data.Frames.Count == 1 && data.Revision == 1, "Lost frame data");
                var config = data.Frames.Single();
                var snapshot = FrameReader.Read(db, tx, config, true);
                Demand(snapshot.Title == "Mặt bằng tầng 1" && config.Level == "T1" &&
                    config.Columns[1].Label == "Tên" && snapshot.Candidates.Count == 3, "Lost ATT or geometry");
                Demand(Model.Load(tx, db).ViewName == "Existing 0.2", "Legacy data changed");
                var block = FrameReader.Resolve(db, tx, config.Handle);
                var definition = (BlockTableRecord)tx.GetObject(block.BlockTableRecord, OpenMode.ForWrite);
                bool rejected = false;
                Add(definition, new Polyline(), tx);
                var duplicate = Rectangle(0, 0, 800, 600);
                duplicate.Layer = config.BoundaryLayer; Add(definition, duplicate, tx);
                try { FrameReader.Read(db, tx, config, false); } catch (InvalidOperationException) { rejected = true; }
                Demand(rejected, "Multiple boundaries accepted");
                duplicate.Erase();
                var boundary = definition.Cast<ObjectId>().Select(id => tx.GetObject(id, OpenMode.ForRead))
                    .OfType<Polyline>().First(p => p.Layer == config.BoundaryLayer);
                boundary.UpgradeOpen(); boundary.Closed = false; rejected = false;
                try { FrameReader.Read(db, tx, config, false); } catch (InvalidOperationException) { rejected = true; }
                Demand(rejected, "Open boundary accepted");
                boundary.Closed = true;
                string layer = config.BoundaryLayer; config.BoundaryLayer = "MISSING"; rejected = false;
                try { FrameReader.Read(db, tx, config, false); } catch (InvalidOperationException) { rejected = true; }
                Demand(rejected, "Missing boundary accepted"); config.BoundaryLayer = layer;
                block.UpgradeOpen(); block.ScaleFactors = new Scale3d(2,3,1); rejected = false;
                try { FrameReader.Read(db, tx, config, false); } catch (InvalidOperationException) { rejected = true; }
                Demand(rejected, "Nonuniform scale accepted");
                // The transaction is deliberately aborted. None of the negative cases may alter the saved DWG.
            }
            File.WriteAllText(Path.Combine(Root, "frame-reopen.txt"), "PASS: reopened copied DWG, frames/ATT/legacy preserved; rejects missing, multiple, open and nonuniform boundaries.");
        }
        catch (System.Exception error) { File.WriteAllText(Path.Combine(Root, "frame-reopen.txt"), error.ToString()); }
    }
    private static System.Collections.Generic.IEnumerable<T> Find<T>(DependencyObject root) where T:DependencyObject
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {
            var child=VisualTreeHelper.GetChild(root,i);if(child is T match)yield return match;
            foreach(var item in Find<T>(child))yield return item;
        }
    }
    [CommandMethod("ASFRAMEUI")]
    public void Ui()
    {
        var root = Root;
        var document = Application.DocumentManager.MdiActiveDocument;
        var snapshots = new System.Collections.Generic.List<FrameSnapshot>();
        Model model;FrameDocument definitions;
        using (var tx = document.Database.TransactionManager.StartOpenCloseTransaction())
        {
            foreach (var frame in FrameDocument.Load(document.Database, tx).Frames)
                snapshots.Add(FrameReader.Read(document.Database, tx, frame, true));
            model = Model.Load(tx, document.Database);definitions=FrameDocument.Load(document.Database,tx);
        }
        System.Exception failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var panel = new SuitePanel(key => { });
                panel.SetData("Mặt bằng thử.dwg", snapshots, model);panel.SetDefinitions(definitions);
                Demand((string)panel.BoundaryLayer.SelectedItem == "QA_KH_Quet", "Boundary choice missing after reload");
                Demand(panel.ReadDraft().BoundaryLayer == "QA_KH_Quet", "Boundary choice not saved");
                panel.Attributes.SelectedItem = panel.DraftColumns[1];
                panel.MoveColumn(-1);
                Demand(panel.ReadDraft().Columns[0].Tag == "TENBANVE", "ATT reorder failed");
                foreach (var size in new[] { new Size(1240,760), new Size(800,580) })
                {
                    panel.Measure(size); panel.Arrange(new Rect(size)); panel.UpdateLayout();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));
                    panel.Measure(size); panel.Arrange(new Rect(size)); panel.UpdateLayout();
                    Demand(panel.DesiredSize.Width <= size.Width + 1, "UI overflows narrow width");
                    var bitmap = new RenderTargetBitmap((int)size.Width,(int)size.Height,96,96,PixelFormats.Pbgra32);
                    bitmap.Render(panel);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var file = File.Create(Path.Combine(root,"frame-ui-"+size.Width+".png"))) encoder.Save(file);
                }
                var details=Find<System.Windows.Controls.TabControl>(panel).Single(t=>t.Items.OfType<System.Windows.Controls.TabItem>().Any(i=>Equals(i.Header,"Định nghĩa tường / cửa")));
                details.SelectedItem=details.Items.OfType<System.Windows.Controls.TabItem>().Single(i=>Equals(i.Header,"Định nghĩa tường / cửa"));
                var wrap=new System.Windows.Controls.Border{Padding=new Thickness(8,6,18,16),Child=panel};
                var reviewSize=new Size(1240,760);wrap.Measure(reviewSize);wrap.Arrange(new Rect(reviewSize));wrap.UpdateLayout();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>{}));
                wrap.UpdateLayout();
                var review=new RenderTargetBitmap(1240,760,96,96,PixelFormats.Pbgra32);review.Render(wrap);
                var image=new PngBitmapEncoder();image.Frames.Add(BitmapFrame.Create(review));
                using(var stream=File.Create(Path.Combine(root,"definitions-ui.png")))image.Save(stream);
                foreach(var button in Find<System.Windows.Controls.Button>(panel).Where(b=>Equals(b.Content,"Lưu định nghĩa")||Equals(b.Content,"Chọn khung mẫu")))
                {
                    var rect=button.TransformToAncestor(wrap).TransformBounds(new Rect(button.RenderSize));
                    Demand(rect.Right<=reviewSize.Width-17 && rect.Bottom<=reviewSize.Height-15,"Header/footer exceeds native client padding");
                }
                File.WriteAllText(Path.Combine(root, "frame-ui.txt"), "PASS: WPF theme and compact layout at 1240x760 / 800x580; definition form; contextual primary button and header/footer client padding.");
            }
            catch (System.Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) File.WriteAllText(Path.Combine(root, "frame-ui.txt"), failure.ToString());
    }
}
