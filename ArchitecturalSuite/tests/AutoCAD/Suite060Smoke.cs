using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Newtonsoft.Json.Linq;
using Arch.Suite.Host;
using App=Autodesk.AutoCAD.ApplicationServices.Core.Application;

public class Suite060Smoke
{
    static string Root=>Path.GetDirectoryName(App.DocumentManager.MdiActiveDocument.Name);
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static ObjectId Layer(Database db,Transaction tx,string name)
    {var table=(LayerTable)tx.GetObject(db.LayerTableId,OpenMode.ForWrite);if(table.Has(name))return table[name];var layer=new LayerTableRecord{Name=name};table.Add(layer);tx.AddNewlyCreatedDBObject(layer,true);return layer.ObjectId;}
    static void Add(Transaction tx,BlockTableRecord owner,Entity entity){owner.AppendEntity(entity);tx.AddNewlyCreatedDBObject(entity,true);}
    static Spline Curve()=>new Spline(2,true,false,false,new Point3dCollection(new[]{new Point3d(0,0,0),new Point3d(25,40,0),new Point3d(50,0,0)}),new DoubleCollection(new[]{0d,0,0,1,1,1}),new DoubleCollection(new[]{1d,0.7,1}),1e-9,1e-9);
    [CommandMethod("AS060TEST")]
    public void Run()
    {
        try
        {
            var db=App.DocumentManager.MdiActiveDocument.Database;
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var data=FrameDocument.Load(db,tx);var frame=data.Frames.Single();
                Check(data.Floors.Count==1&&data.Floors[0].Name=="T1"&&!data.Floors[0].IsConfigured&&data.Floors[0].Id==frame.FloorId,"Frame floor name did not create canonical pending entry");
                var floor=data.Floors.Single();string floorId=floor.Id;floor.HeightMm=3600;floor.BeamMm=600;data.WallRule.Confirmed=true;
                var sourceBlock=FrameReader.Resolve(db,tx,frame.Handle);var model=(BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db),OpenMode.ForWrite);
                var layer=Layer(db,tx,"TEST_SPLINE");data.LayerGroups.Add(new LayerGroupDefinition{Kind="Nội thất",Layers=new List<string>{"TEST_SPLINE"}});
                var blocks=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForWrite);
                var inner=new BlockTableRecord{Name="CURVE_INNER"};blocks.Add(inner);tx.AddNewlyCreatedDBObject(inner,true);Add(tx,inner,Curve());
                var outer=new BlockTableRecord{Name="CURVE_OUTER"};blocks.Add(outer);tx.AddNewlyCreatedDBObject(outer,true);
                Add(tx,outer,new BlockReference(new Point3d(10,10,0),inner.ObjectId){Rotation=0.3});
                Add(tx,outer,new AttributeDefinition{Tag="CODE",TextString="CURVE",Height=3,Position=Point3d.Origin});
                var root=new BlockReference(new Point3d(250,400,0).TransformBy(sourceBlock.BlockTransform),outer.ObjectId){LayerId=layer,Rotation=0.4};Add(tx,model,root);
                var att=new AttributeReference();att.SetAttributeFromBlock((AttributeDefinition)tx.GetObject(outer.Cast<ObjectId>().Last(),OpenMode.ForRead),root.BlockTransform);att.TextString="A-01";root.AttributeCollection.AppendAttribute(att);tx.AddNewlyCreatedDBObject(att,true);
                var standalone=Curve();standalone.TransformBy(Matrix3d.Displacement(new Point3d(600,400,0).TransformBy(sourceBlock.BlockTransform)-Point3d.Origin));standalone.LayerId=layer;Add(tx,model,standalone);
                data.Save(db,tx);tx.Commit();
            }
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var data=FrameDocument.Load(db,tx);var frame=data.Frames.Single();string floorId=frame.FloorId;
                var sourceBlock=FrameReader.Resolve(db,tx,frame.Handle);
                var model=(BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db),OpenMode.ForWrite);
                var blocks=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForWrite);
                var standalone=model.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<Spline>().Single();
                var snapshot=FrameReader.Read(db,tx,frame,true);
                foreach(var candidate in snapshot.Candidates.Where(c=>c.Status!="Cần xác nhận"))
                {
                    if(SuiteKinds.Classify(tx,Model.Resolve(tx,db,candidate.Handle),data)==null)continue;
                    RegistrationService.Upsert(data,SuiteKinds.Register(db,tx,data,snapshot,candidate));
                }
                int blockCount=blocks.Cast<ObjectId>().Count();int modelCount=model.Cast<ObjectId>().Count();
                var preview=DrawingLinks.Preview(db,tx,data,frame,new[]{"Nội thất"});
                Check(preview.Contains("Tạo")&&blocks.Cast<ObjectId>().Count()==blockCount&&model.Cast<ObjectId>().Count()==modelCount,"Preview leaked a temporary block or entity");
                var drawing=DrawingLinks.Create(db,tx,data,frame,"Mặt bằng Spline","SP-01",new Point3d(20000,20000,0),new[]{"Nội thất"});
                var debugGroup=drawing.Groups.Single();
                var signature=typeof(DrawingLinks).GetMethod("Signature",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                var debug=new List<string>{"EXPECTED="+debugGroup.ContentHash};
                foreach(var handle in new[]{debugGroup.CacheHandle,debugGroup.ContentDefinitionHandle})
                {
                    var block=(BlockTableRecord)tx.GetObject(db.GetObjectId(false,new Handle(Convert.ToInt64(handle,16)),0),OpenMode.ForRead);
                    var entities=block.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<Entity>().ToList();
                    debug.Add("HASH="+DrawingLinks.Hash(entities));debug.AddRange(entities.Select(e=>(string)signature.Invoke(null,new object[]{e})));
                }
                File.WriteAllLines(Path.Combine(Root,"spline-signatures.txt"),debug);
                Check(!DrawingLinks.Protect(db,tx,data),"Spline snapshot not stable immediately after generation");
                var group=drawing.Groups.Single();var definition=(BlockTableRecord)tx.GetObject(db.GetObjectId(false,new Handle(Convert.ToInt64(group.ContentDefinitionHandle,16)),0),OpenMode.ForRead);
                var splines=definition.Cast<ObjectId>().Select(id=>tx.GetObject(id,OpenMode.ForRead)).OfType<Spline>().ToList();
                Check(splines.Count==2&&splines.All(c=>c.IsRational),"Standalone/nested rational Spline was flattened or lost");
                standalone.UpgradeOpen();standalone.SetControlPointAt(1,standalone.GetControlPointAt(1)+new Vector3d(5,0,0));
                Check(DrawingLinks.Preview(db,tx,data,frame,new[]{"Nội thất"},drawing).Contains("Cần cập nhật"),"Spline control-point edit was not detected");
                DrawingLinks.Sync(db,tx,data,drawing,new[]{"Nội thất"});
                var edited=DefinitionTransfer.Copy(data.Floors);edited[0].Name="Tầng 1";edited[0].HeightMm=4000;FloorCatalog.Apply(data,edited);
                Check(frame.FloorId==floorId&&data.Frames.All(f=>f.Level=="Tầng 1")&&data.Registrations.All(r=>r.Level=="Tầng 1")&&drawing.NeedsUpdate,"Floor rename/height did not propagate by ID");
                var modelData=Model.Load(tx,db);modelData.WallLayer=new LayerRole{SourceId="ISO_QUANGANH",Kind="layers",Id="0123456789abcdef0123456789abcdef",Name="Fake",SourceFile="C:\\private\\asset.dwg",LocalHandle="ABC"};
                var exported=DefinitionTransfer.Export(data,modelData,SuiteKinds.All.Concat(new[]{"Profile khung","Tài nguyên kho","Tầng"}));
                data.Profile=FrameProfile.From(frame);exported.Profile=DefinitionTransfer.Copy(data.Profile);
                string path=Path.Combine(Root,"definitions.json");DefinitionTransfer.Write(path,exported);string json=File.ReadAllText(path);
                Check(!json.Contains("LocalHandle")&&!json.Contains("SourceFile")&&!json.Contains("Registrations")&&!json.Contains("Drawings")&&!json.Contains("FloorId"),"Export leaked project IDs or physical asset paths");
                var imported=DefinitionTransfer.Read(path);var rows=DefinitionTransfer.Preview(db,tx,data,imported,Path.Combine(Root,"missing-warehouse"));
                Check(rows.Where(r=>r.Section=="Tài nguyên kho").All(r=>!r.CanApply)&&rows.Where(r=>r.Section=="Tầng").All(r=>!r.Use),"Unavailable resource or conflict was auto-applied");
                foreach(var row in rows)row.Use=row.CanApply&&row.Section!="Tài nguyên kho";
                int sourceCount=data.Registrations.Count;var proposed=DefinitionTransfer.Apply(data,rows);
                Check(proposed.Registrations.Count==sourceCount&&proposed.Frames.Count==data.Frames.Count&&proposed.Drawings.Count==1&&proposed.Floors[0].Id==floorId,"Import changed project entities, graph or canonical floor IDs");
                var noFloors=DefinitionTransfer.Export(data,modelData,new[]{"Profile khung"});Check(noFloors.Floors.Count==0&&noFloors.Profile.Level==null,"Floor settings exported without explicit option");
                // Failure is contextual and rolls back the entire staged frame when the caller aborts.
                var bad=(LayerTableRecord)tx.GetObject(Layer(db,tx,"TEST_BAD"),OpenMode.ForRead);data.LayerGroups.Add(new LayerGroupDefinition{Kind="Trục",Layers=new List<string>{bad.Name}});
                var point=new DBPoint(new Point3d(300,300,0).TransformBy(sourceBlock.BlockTransform)){LayerId=bad.ObjectId};Add(tx,model,point);snapshot=FrameReader.Read(db,tx,frame,true);
                RegistrationService.Upsert(data,SuiteKinds.Register(db,tx,data,snapshot,snapshot.Candidates.Single(c=>c.Handle==point.Handle.ToString())));
                bool contextual=false;try{DrawingLinks.Preview(db,tx,data,frame,new[]{"Trục"});}catch(SourceFailure error){contextual=error.SourceHandle==point.Handle.ToString()&&error.Message.Contains("TEST_BAD");}
                Check(contextual,"Failure omitted source handle/layer");data.Registrations.RemoveAll(r=>r.Handle==point.Handle.ToString());
                Check(!DrawingLinks.Protect(db,tx,proposed),"Snapshot changed before save");Dump(db,tx,proposed,"before");proposed.Save(db,tx);tx.Commit();
            }
            File.WriteAllText(Path.Combine(Root,"suite060.txt"),"PASS: pending floor canonicalization; nested/standalone rational Spline, exact clone and CP change; resident scratch no leaks; floor rename/height graph pending; portable JSON without entity IDs/paths; conflict/missing resource preview; per-item apply preserves project graph; contextual errors.");
        }
        catch(System.Exception error){File.WriteAllText(Path.Combine(Root,"suite060.txt"),error.ToString());}
    }
    private static void Dump(Database db,Transaction tx,FrameDocument data,string phase)
    {
        var lines=new List<string>();foreach(var g in data.Drawings.SelectMany(d=>d.Groups))
        {
                        lines.Add("GROUP "+g.Kind+" EXPECTED "+g.ContentHash);
            var instance=Model.Resolve(tx,db,g.ContentHandle) as BlockReference;
            lines.Add("INSTANCE "+(instance==null?"NULL":instance.Position+" ROT "+instance.Rotation+" SCALE "+instance.ScaleFactors+" LAYER "+instance.Layer+" LAYERID "+instance.LayerId+" VISIBLE "+instance.Visible+" DEF "+instance.BlockTableRecord)+" WANTED "+g.Layer+" OFFSET "+string.Join(",",g.Offset));
            foreach(var handle in new[]{g.CacheHandle,g.ContentDefinitionHandle})
            {
                db.TryGetObjectId(new Handle(Convert.ToInt64(handle,16)),out var id);var b=(BlockTableRecord)tx.GetObject(id,OpenMode.ForRead);
                foreach(ObjectId e in b)lines.Add(handle+" "+DrawingLinks.Signature((Entity)tx.GetObject(e,OpenMode.ForRead)));
            }
        }
        File.WriteAllLines(Path.Combine(Root,"hash-"+phase+".txt"),lines);
    }    [CommandMethod("AS060REOPEN")]
    public void Reopen()
    {
        try
        {
            var db=App.DocumentManager.MdiActiveDocument.Database;using(var tx=db.TransactionManager.StartTransaction())
            {
                var data=FrameDocument.Load(db,tx);Check(data.Schema==3&&data.Floors.Count==1&&data.Floors[0].Name=="Tầng 1"&&data.Drawings.Count==1,"Schema/floor/config graph lost");
                Dump(db,tx,data,"after");Check(!DrawingLinks.Protect(db,tx,data),"Spline cache not stable after save/reopen");
            }
            File.WriteAllText(Path.Combine(Root,"reopen060.txt"),"PASS: schema3 floor/definition/graph and Spline snapshot survive copy/reopen.");
        }
        catch(System.Exception error){File.WriteAllText(Path.Combine(Root,"reopen060.txt"),error.ToString());}
    }
    [CommandMethod("AS060EDGE")]
    public void Edge()
    {
        try
        {
            var db=App.DocumentManager.MdiActiveDocument.Database;int count;
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var data=FrameDocument.Load(db,tx);count=data.Frames.Count;
                DrawingLinks.Create(db,tx,data,data.Frames.Single(f=>!f.IsDerived),"Rollback","ROLLBACK",new Point3d(50000,50000,0),new[]{"Nội thất"});
                // Abort the complete user operation, never reuse an aborted parent's new objects.
            }
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var data=FrameDocument.Load(db,tx);Check(data.Frames.Count==count&&!DrawingLinks.Protect(db,tx,data),"Aborted operation changed existing linked drawing");
                var blocks=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForRead);
                Check(!blocks.Cast<ObjectId>().Select(id=>(BlockTableRecord)tx.GetObject(id,OpenMode.ForRead)).Any(b=>b.Name.StartsWith("ARCH_TEMP_")),"Aborted operation leaked scratch");
            }
            using(var legacy=new Database(true,true))using(var tx=legacy.TransactionManager.StartTransaction())
            {
                var nod=(DBDictionary)tx.GetObject(legacy.NamedObjectsDictionaryId,OpenMode.ForWrite);var record=new Xrecord();nod.SetAt("ARCH_SUITE_FRAMES_V1",record);tx.AddNewlyCreatedDBObject(record,true);
                using(var json=new ResultBuffer(new TypedValue((int)DxfCode.Text,"{\"Schema\":2,\"Frames\":[{\"Id\":\"frame1\",\"Handle\":\"ABC\",\"Level\":\"T1\",\"Columns\":[]}]}")))record.Data=json;
                var a=FrameDocument.Load(legacy,tx);var b=FrameDocument.Load(legacy,tx);Check(a.Floors.Single().Id==b.Floors.Single().Id,"Legacy pending floor ID changes between reads");
                var edits=DefinitionTransfer.Copy(a.Floors);edits[0].HeightMm=3600;edits[0].BeamMm=600;FloorCatalog.Apply(b,edits);Check(b.Floors.Single().WallHeightMm==3000,"Legacy UI floor edit cannot save");
            }
            using(var tx=db.TransactionManager.StartOpenCloseTransaction())
            {
                var data=FrameDocument.Load(db,tx);var bundle=new DefinitionBundle();
                bundle.Doors.Add(new DoorDefinition{BlockName="MISSING_DOOR_TEMPLATE",WidthProperty="Distance",HeightMm=2200,Kind="Cửa đi"});
                var rows=DefinitionTransfer.Preview(db,tx,data,bundle);var row=rows.Single();
                Check(row.CanApply&&!row.Use&&row.Detail.Contains("block"),"Missing door template not flagged for explicit review");
                row.Use=true;var applied=DefinitionTransfer.Apply(data,rows);
                Check(applied.DoorRules.Single().HeightMm==2200&&applied.DoorRules.Single().Kind=="Cửa đi"&&applied.Registrations.Count==data.Registrations.Count,"Portable door rule failed or changed source registrations");
            }            foreach(var text in new[]{"{\"Format\":\"ArchitecturalSuite.Definitions\",\"Version\":99}","{\"Format\":\"ArchitecturalSuite.Definitions\",\"Version\":1,\"Version\":1}"})
            {
                string path=Path.Combine(Root,"invalid-definitions.json");File.WriteAllText(path,text);bool rejected=false;try{DefinitionTransfer.Read(path);}catch{rejected=true;}Check(rejected,"Invalid/future definition JSON accepted");
            }
            File.WriteAllText(Path.Combine(Root,"edge060.txt"),"PASS: whole-operation rollback preserves saved graph and cache without scratch; schema2 pending floor IDs stable across reads and editable; future/duplicate-key JSON rejected; missing door template requires review and loads portable rule only.");
        }
        catch(System.Exception error){File.WriteAllText(Path.Combine(Root,"edge060.txt"),error.ToString());}
    }
    [CommandMethod("AS060ZOOM")]
    public void Zoom()
    {
        try
        {
            var doc=App.DocumentManager.MdiActiveDocument;var db=doc.Database;
            using(var tx=db.TransactionManager.StartOpenCloseTransaction())
            {
                var data=FrameDocument.Load(db,tx);var source=data.Registrations.First(r=>r.Kind=="Nội thất");var entity=Model.Resolve(tx,db,source.Handle);
                using(var original=doc.Editor.GetCurrentView())
                {
                    foreach(var direction in new[]{Vector3d.ZAxis,new Vector3d(1,1,1)})
                    using(var view=doc.Editor.GetCurrentView())
                    {
                        view.PerspectiveEnabled=false;view.ViewDirection=direction;view.ViewTwist=0.6;doc.Editor.SetCurrentView(view);
                        ViewNavigation.Locate(doc,tx,source.Handle);
                        using(var actual=doc.Editor.GetCurrentView())
                        {
                            Check((actual.ViewDirection.GetNormal()-direction.GetNormal()).Length<1e-6&&Math.Abs(actual.ViewTwist-0.6)<1e-6,"Zoom changed view orientation");
                            var bounds=entity.GeometricExtents;bounds.TransformBy(ViewNavigation.WorldToDisplay(actual));
                            Check(Math.Abs((bounds.MinPoint.X+bounds.MaxPoint.X)/2-actual.CenterPoint.X)<1e-4&&Math.Abs((bounds.MinPoint.Y+bounds.MaxPoint.Y)/2-actual.CenterPoint.Y)<1e-4,"Zoom did not center selected entity");
                        }
                    }
                    doc.Editor.SetCurrentView(original);
                }
            }
            File.WriteAllText(Path.Combine(Root,"zoom060.txt"),"PASS: actual GUI view centered selected source under twist and isometric directions, orientation retained.");
        }
        catch(System.Exception error){File.WriteAllText(Path.Combine(Root,"zoom060.txt"),error.ToString());}
    }
}
