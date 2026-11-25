using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Arch.Suite.Host;
using App=Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Rainmaker.Library.Reader;

public class Suite050Smoke
{
    static string Root=>Path.GetDirectoryName(App.DocumentManager.MdiActiveDocument.Name);
    static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static ObjectId Layer(Database db,Transaction tx,string name)
    {
        var table=(LayerTable)tx.GetObject(db.LayerTableId,OpenMode.ForWrite);if(table.Has(name))return table[name];
        var layer=new LayerTableRecord{Name=name};table.Add(layer);tx.AddNewlyCreatedDBObject(layer,true);return layer.ObjectId;
    }
    static void Add(Transaction tx,BlockTableRecord owner,Entity entity){owner.AppendEntity(entity);tx.AddNewlyCreatedDBObject(entity,true);}
    [CommandMethod("AS050TEST")]
    public void Run()
    {
        try
        {
            var db=App.DocumentManager.MdiActiveDocument.Database;
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var data=FrameDocument.Load(db,tx);var frame=data.Frames.Single();
                var floor=new FloorDefinition{Name="T1",HeightMm=3600,BeamMm=600};floor.Validate();
                data.Floors.Add(floor);SuiteKinds.BindFloor(data,frame);data.WallRule.Confirmed=true;
                Check(frame.FloorId==floor.Id&&floor.WallHeightMm==3000,"Floor binding/calculation");
                var snapshot=FrameReader.Read(db,tx,frame,true);
                var wall=snapshot.Candidates.Single(c=>c.Status!="Cần xác nhận");
                var registered=SuiteKinds.Register(db,tx,data,snapshot,wall);
                Check(registered.HeightMm==3000&&registered.Level=="T1","Inherited wall height and floor");
                registered=SuiteKinds.Register(db,tx,data,snapshot,wall,800);
                Check(registered.HeightMm==2800&&registered.BeamOverrideMm==800,"Beam override");RegistrationService.Upsert(data,registered);
                var model=(BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db),OpenMode.ForWrite);
                var frameBlock=FrameReader.Resolve(db,tx,frame.Handle);
                var interior=Layer(db,tx,"TEST_INTERIOR");var sanitary=Layer(db,tx,"TEST_SANITARY");
                data.LayerGroups.Add(new LayerGroupDefinition{Kind="Nội thất",Layers=new List<string>{"TEST_INTERIOR","TEST_SANITARY"}});
                data.LayerGroups.Add(new LayerGroupDefinition{Kind="Thiết bị vệ sinh",Layers=new List<string>{"TEST_SANITARY"}});
                data.LayerGroups.Add(new LayerGroupDefinition{Kind="Trục",Layers=new List<string>{"TEST_AXIS"}});
                var blocks=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForWrite);var definition=new BlockTableRecord{Name="TEST_FURNITURE"};blocks.Add(definition);tx.AddNewlyCreatedDBObject(definition,true);
                Add(tx,definition,new Line(Point3d.Origin,new Point3d(20,20,0)){LayerId=sanitary});
                var furniture=new BlockReference(new Point3d(300,400,0).TransformBy(frameBlock.BlockTransform),definition.ObjectId){LayerId=interior};Add(tx,model,furniture);
                Check(SuiteKinds.Classify(tx,furniture,data)=="Nội thất","Nested line layer incorrectly classified whole block");
                var fixture=new Circle(new Point3d(500,400,0).TransformBy(frameBlock.BlockTransform),Vector3d.ZAxis,10){LayerId=sanitary};Add(tx,model,fixture);
                Check(SuiteKinds.Classify(tx,fixture,data)=="Thiết bị vệ sinh","Sanitary precedence");
                snapshot=FrameReader.Read(db,tx,frame,true);
                foreach(var entity in new Entity[]{furniture,fixture})RegistrationService.Upsert(data,SuiteKinds.Register(db,tx,data,snapshot,snapshot.Candidates.Single(c=>c.Handle==entity.Handle.ToString())));
                var hatch=new Hatch();Add(tx,model,hatch);hatch.SetHatchPattern(HatchPatternType.PreDefined,"SOLID");hatch.Associative=false;
                var points=new Point2dCollection(new[]{new Point2d(10050,20100),new Point2d(10070,20100),new Point2d(10070,20120),new Point2d(10050,20120)});
                hatch.AppendLoop(HatchLoopTypes.External,points,new DoubleCollection(new[]{0d,0d,0d,0d}));hatch.EvaluateHatch(true);
                hatch.LayerId=interior;
                snapshot=FrameReader.Read(db,tx,frame,true);
                RegistrationService.Upsert(data,SuiteKinds.Register(db,tx,data,snapshot,snapshot.Candidates.Single(c=>c.Handle==hatch.Handle.ToString())));
                var secondFloor=new FloorDefinition{Name="T2",HeightMm=4200,BeamMm=500};data.Floors.Add(secondFloor);
                var secondFrame=new DrawingFrame{Level="T2"};SuiteKinds.BindFloor(data,secondFrame);
                Check(SuiteKinds.Floor(data,secondFrame).WallHeightMm==3700&&SuiteKinds.Floor(data,frame).WallHeightMm==3000,"Different floor heights mixed");
                bool invalidFloor=false;try{new FloorDefinition{Name="Bad",HeightMm=3000,BeamMm=3000}.Validate();}catch(InvalidOperationException){invalidFloor=true;}Check(invalidFloor,"Invalid beam height accepted");
                int sources=data.Registrations.Count;
                var target=new Point3d(20000,20000,0);
                bool overlap=false;try{DrawingLinks.Create(db,tx,data,frame,"Overlap","BAD-01",frameBlock.Position,new[]{"Tường"});}catch(InvalidOperationException){overlap=true;}Check(overlap&&data.Frames.Count==1,"Overlapping target accepted");
                frame.Columns.Reverse();
                var drawing=DrawingLinks.Create(db,tx,data,frame,"Mặt bằng nội thất","NT-01",target,new[]{"Tường","Nội thất","Thiết bị vệ sinh"});
                frame.Columns.Reverse();
                Check(FrameReader.Read(db,tx,data.Frames.Single(f=>f.Id==drawing.TargetFrameId),false).Code=="NT-01","Code tag depends on ATT column order");
                Check(data.Frames.Count==2&&data.Registrations.Count==sources&&data.Frames.Single(f=>f.Id==drawing.TargetFrameId).IsDerived,"Derived drawings double-register source entities");
                var signatureMethod=typeof(DrawingLinks).GetMethod("Signature",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                foreach(var group in drawing.Groups)
                {
                    Func<string,string[]> signatures=handle=>{
                        var id=db.GetObjectId(false,new Handle(Convert.ToInt64(handle,16)),0);
                        return ((BlockTableRecord)tx.GetObject(id,OpenMode.ForRead)).Cast<ObjectId>().Select(i=>tx.GetObject(i,OpenMode.ForRead)).OfType<Entity>().Select(e=>(string)signatureMethod.Invoke(null,new object[]{e})).ToArray();};
                    File.WriteAllLines(Path.Combine(Root,"signature-"+group.Kind+".txt"),new[]{"CACHE"}.Concat(signatures(group.CacheHandle)).Concat(new[]{"CONTENT"}).Concat(signatures(group.ContentDefinitionHandle)));
                }
                Check(!DrawingLinks.Protect(db,tx,data),"Unchanged linked geometry considered tampered");
                var content=(BlockReference)Model.Resolve(tx,db,drawing.Groups[0].ContentHandle);
                var layer=(LayerTableRecord)tx.GetObject(content.LayerId,OpenMode.ForWrite);layer.IsLocked=false;content.UpgradeOpen();content.Position+=new Vector3d(123,0,0);
                Check(DrawingLinks.Protect(db,tx,data),"Manual target transform not restored");
                Check(content.Position.DistanceTo(new Point3d(drawing.Groups[0].Offset[0],drawing.Groups[0].Offset[1],drawing.Groups[0].Offset[2]))<1e-8,"Target transform repair failed");
                var copiedDefinition=(BlockTableRecord)tx.GetObject(content.BlockTableRecord,OpenMode.ForWrite);
                var copied=(Entity)tx.GetObject(copiedDefinition.Cast<ObjectId>().First(),OpenMode.ForWrite);copied.TransformBy(Matrix3d.Displacement(new Vector3d(99,0,0)));
                Check(DrawingLinks.Protect(db,tx,data),"Block definition edit not repaired from approved snapshot");
                Check(!DrawingLinks.Protect(db,tx,data),"Repair not stable");
                using(var nested=db.TransactionManager.StartTransaction())
                {
                    var duplicate=new BlockReference(content.Position,content.BlockTableRecord){LayerId=content.LayerId};
                    Add(nested,(BlockTableRecord)nested.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db),OpenMode.ForWrite),duplicate);
                    DrawingLinks.CheckCopies(db,nested,data);Check(drawing.UnmanagedCopiesCount==1&&!duplicate.IsErased,"Duplicate was not reported or was deleted");
                    bool paused=false;try{DrawingLinks.Preview(db,nested,data,frame,new[]{"Tường"},drawing);}catch(InvalidOperationException){paused=true;}
                    Check(paused,"Sync preview ignores unmanaged shared-definition copy");
                    // Deliberately abort this synthetic duplicate fixture; production never deletes copies.
                }
                DrawingLinks.CheckCopies(db,tx,data);
                var sourceWall=(Polyline)Model.Resolve(tx,db,wall.Handle);sourceWall.UpgradeOpen();sourceWall.SetPointAt(0,sourceWall.GetPoint2dAt(0)+new Vector2d(10,0));
                Check(DrawingLinks.Preview(db,tx,data,frame,new[]{"Tường"},drawing).Contains("Cần cập nhật"),"Source geometry change not detected");
                DrawingLinks.Sync(db,tx,data,drawing,new[]{"Tường","Nội thất"});
                Check(drawing.Groups.Single(g=>g.Kind=="Thiết bị vệ sinh").Linked==false,"Removed group not detached");
                Check(Model.Resolve(tx,db,drawing.Groups.Single(g=>g.Kind=="Thiết bị vệ sinh").ContentHandle)!=null,"Detached geometry lost");
                DrawingLinks.Move(db,tx,data,drawing,new Point3d(25000,25000,0));Check(!DrawingLinks.Protect(db,tx,data),"Managed whole-drawing move considered tampering");
                floor.HeightMm=4000;Check(DrawingLinks.Preview(db,tx,data,frame,new[]{"Tường"},drawing).Contains("Cần cập nhật"),"Floor height metadata change not detected");
                DrawingLinks.Sync(db,tx,data,drawing,new[]{"Tường","Nội thất"});
                Check(data.Registrations.Single(r=>r.Kind=="Tường").HeightMm==3200,"Beam override lost after sync");
                furniture.UpgradeOpen();furniture.Erase();
                Check(DrawingLinks.Preview(db,tx,data,frame,new[]{"Nội thất"},drawing).Contains("Cần cập nhật"),"Deleted source not detected");
                DrawingLinks.Sync(db,tx,data,drawing,new[]{"Tường","Nội thất"});
                Check(!data.Registrations.Any(r=>r.Handle==furniture.Handle.ToString()),"Deleted source remains in quantities registry");
                data.Save(db,tx);tx.Commit();
            }
            // Reader pending must stop all reads; the consumer must not inspect storage itself.
            var mock=Path.Combine(Root,"reader-pending");Directory.CreateDirectory(Path.Combine(mock,"Catalog","qa-transactions"));
            File.WriteAllText(Path.Combine(mock,"Catalog","qa-transactions","ISO_QUANGANH.pending.json"),"{}");
            bool blocked=false;try{ResourceLayers.ReadOptions(mock);}catch(LibraryReadException e){blocked=e.Code=="LIBRARY_PENDING";}Check(blocked,"Reader pending guard bypassed");
            File.WriteAllText(Path.Combine(Root,"suite050.txt"),"PASS: two floor heights/beam override, invalid beam and overlapping target rejected; block insertion layer and sanitary priority; source-only registration; clone/private title frame; source change preview; one-way target transform/definition repair; detached geometry retained; whole-frame move; floor metadata update and source deletion propagation; Reader pending error code.");
        }
        catch(System.Exception error){File.WriteAllText(Path.Combine(Root,"suite050.txt"),error.ToString());}
    }
    [CommandMethod("AS050REOPEN")]
    public void Reopen()
    {
        try
        {
            var db=App.DocumentManager.MdiActiveDocument.Database;
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var data=FrameDocument.Load(db,tx);Check(data.Schema==2&&data.Floors.Count==2&&data.Drawings.Count==1,"New model lost after copy/reopen");
                Check(!DrawingLinks.Protect(db,tx,data),"Stable output changed on reopened DWG");
                var cacheIds=new ObjectIdCollection(data.Drawings.SelectMany(d=>d.Groups).Where(g=>g.Linked).Select(g=>db.GetObjectId(false,new Handle(Convert.ToInt64(g.CacheHandle,16)),0)).ToArray());
                db.Purge(cacheIds);Check(cacheIds.Count==0,"PURGE can remove approved cache definitions");
                Check(data.Registrations.All(r=>r.Level=="T1"),"Registration floor metadata lost");
                var drawing=data.Drawings.Single();DrawingLinks.Unlink(db,tx,drawing);
                Check(drawing.Groups.All(g=>!g.Linked)&&drawing.Groups.All(g=>Model.Resolve(tx,db,g.ContentHandle)!=null),"Full unlink lost geometry");
                // Abort; leave saved linked fixture intact for UI regression.
            }
            File.WriteAllText(Path.Combine(Root,"suite050-reopen.txt"),"PASS: schema2, floors, groups and links survive copy/reopen; protection stable; PURGE cannot remove approved caches; unlink preserves geometry.");
        }
        catch(System.Exception error){File.WriteAllText(Path.Combine(Root,"suite050-reopen.txt"),error.ToString());}
    }
    [CommandMethod("AS050TAMPER")]
    public void Tamper()
    {
        new Host().Initialize();
        var db=App.DocumentManager.MdiActiveDocument.Database;
        using(var tx=db.TransactionManager.StartTransaction())
        {
            var data=FrameDocument.Load(db,tx);var group=data.Drawings.Single().Groups.First(g=>g.Linked);
            var entity=(BlockReference)Model.Resolve(tx,db,group.ContentHandle);
            var layer=(LayerTableRecord)tx.GetObject(entity.LayerId,OpenMode.ForWrite);layer.IsLocked=false;
            entity.UpgradeOpen();entity.Position+=new Vector3d(100,0,0);tx.Commit();
        }
    }
    [CommandMethod("AS050VERIFY")]
    public void Verify()
    {
        try
        {
            var db=App.DocumentManager.MdiActiveDocument.Database;
            using(var tx=db.TransactionManager.StartOpenCloseTransaction())
            {
                var data=FrameDocument.Load(db,tx);var group=data.Drawings.Single().Groups.First(g=>g.Linked);
                var entity=(BlockReference)Model.Resolve(tx,db,group.ContentHandle);
                Check(entity.Position.DistanceTo(new Point3d(group.Offset[0],group.Offset[1],group.Offset[2]))<1e-8,"CommandEnded reactor did not repair target");
                Check(((LayerTableRecord)tx.GetObject(entity.LayerId,OpenMode.ForRead)).IsLocked,"Protection layer not relocked");
            }
            File.WriteAllText(Path.Combine(Root,"gui-protection.txt"),"PASS: actual AutoCAD CommandEnded repaired a target transform and relocked the layer with Palette closed.");
        }
        catch(System.Exception error){File.WriteAllText(Path.Combine(Root,"gui-protection.txt"),error.ToString());}
        finally{new Host().Terminate();}
    }
    [CommandMethod("AS050LIBRARY")]
    public void Library()
    {
        try
        {
            string appRoot=Environment.GetEnvironmentVariable("ARCH_SUITE_TEST_APP");
            var options=ResourceLayers.ReadOptions(appRoot);Check(options.Count>0,"No Reader resources");
            var db=App.DocumentManager.MdiActiveDocument.Database;
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var table=(LayerTable)tx.GetObject(db.LayerTableId,OpenMode.ForRead);
                var layer=options.First(r=>r.Kind=="layers"&&!table.Has(r.Name));
                var extra=new Dictionary<string,LayerRole>();foreach(string kind in new[]{"blocks","hatches","texts","dims"})extra[kind]=options.FirstOrDefault(r=>r.Kind==kind);
                var model=Model.Load(tx,db);ResourceLayers.ApplyResources(App.DocumentManager.MdiActiveDocument,tx,model,appRoot,layer,layer,extra);
                Check(model.WallLayer.Id==layer.Id&&model.Styles.Count==extra.Count(p=>p.Value!=null),"Reader resource import did not preserve selections");
                tx.Commit(); // Commit only to the owned synthetic test drawing.
            }
            var mock=Path.Combine(Root,"preferences");var preferences=LibraryPreferences.Load(mock);int revision=(int)preferences["Revision"];
            LibraryPreferences.Save(mock,revision,new Dictionary<string,LayerRole>{{"wall",options.First(r=>r.Kind=="layers")}});
            Check(LibraryPreferences.Resolve(LibraryPreferences.Load(mock),"wall",options)!=null,"Default selection reload failed");
            bool stale=false;try{LibraryPreferences.Save(mock,revision,new Dictionary<string,LayerRole>());}catch(InvalidOperationException){stale=true;}Check(stale,"Stale preferences write accepted");
            File.WriteAllText(Path.Combine(Root,"reader-integration.txt"),"PASS: real published Reader snapshot, layer/block/text/dim/hatch resolution and CAD import in owned synthetic DWG; current defaults retain stable IDs, reload and reject stale revision.");
        }
        catch(System.Exception error){File.WriteAllText(Path.Combine(Root,"reader-integration.txt"),error.ToString());}
    }
    [CommandMethod("AS050MIGRATION")]
    public void Migration()
    {
        try
        {
            using(var db=new Database(true,true))using(var tx=db.TransactionManager.StartTransaction())
            {
                var nod=(DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId,OpenMode.ForWrite);
                var frames=new Xrecord();nod.SetAt("ARCH_SUITE_FRAMES_V1",frames);tx.AddNewlyCreatedDBObject(frames,true);
                using(var buffer=new ResultBuffer(new TypedValue((int)DxfCode.Text,"{\"Schema\":1,\"Revision\":1,\"Frames\":[],\"DoorRules\":[{\"BlockName\":\"LEGACY_DOOR\",\"WidthProperty\":\"Width\",\"HeightMm\":2200}]}")))frames.Data=buffer;
                var old=new Xrecord();nod.SetAt("ARCH_SUITE_V1",old);tx.AddNewlyCreatedDBObject(old,true);using(var buffer=new ResultBuffer(new TypedValue((int)DxfCode.Text,"unchanged legacy marker")))old.Data=buffer;
                var data=FrameDocument.Load(db,tx);Check(data.DoorRules.Single().NeedsKindConfirmation,"0.4 door silently classified as door/window");
                data.Save(db,tx);Check(FrameDocument.Load(db,tx).Schema==2,"Schema2 migration failed");
                using(var buffer=old.Data)Check((string)buffer.AsArray()[0].Value=="unchanged legacy marker","0.2 data altered by migration");
            }
            File.WriteAllText(Path.Combine(Root,"migration050.txt"),"PASS: loads schema1, flags old door types for explicit confirmation, writes schema2 and preserves legacy NOD data.");
        }
        catch(System.Exception error){File.WriteAllText(Path.Combine(Root,"migration050.txt"),error.ToString());}
    }
}
