using System;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using App=Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Arch.Suite.Host;

public class DefinitionSmoke
{
    static string Root=>Path.GetDirectoryName(App.DocumentManager.MdiActiveDocument.Name);
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    [CommandMethod("ASDEFTEST")]
    public void Run()
    {
        try
        {
            var db=App.DocumentManager.MdiActiveDocument.Database;
            using(var tx=db.TransactionManager.StartTransaction())
            {
                var data=FrameDocument.Load(db,tx);var frame=data.Frames.Single();
                Check(data.Registrations.Count==0 && data.DoorRules.Count==0,"Migration defaults");
                data.Profile=FrameProfile.From(frame);data.WallRule.Confirmed=true;
                var second=data.Profile.ForBlock("ABC");
                Check(second.Id!=frame.Id && second.NameTag==frame.NameTag && second.BoundaryLayer==frame.BoundaryLayer,"Profile clone");
                second.Columns[0].Label="Other";Check(frame.Columns[0].Label!="Other","Shared mutable columns");
                var snapshot=FrameReader.Read(db,tx,frame,true);
                var candidate=snapshot.Candidates.Single(c=>c.Status!="Cần xác nhận");
                var source=RegistrationService.ReadWall(db,tx,snapshot,candidate,data.WallRule);
                Check(Math.Abs(source.AreaMm2-40000)<0.1 && source.OriginalBoundary.Length==12,"Wall original region and scale");
                RegistrationService.Upsert(data,source);string id=source.Id;
                RegistrationService.Upsert(data,RegistrationService.ReadWall(db,tx,snapshot,candidate,data.WallRule));
                Check(data.Registrations.Count==1 && data.Registrations.Single().Id==id,"Repeated registration IDs");
                source=RegistrationService.ReadWall(db,tx,snapshot,candidate,data.WallRule);source.FrameId=second.Id;
                bool rejected=false;try{RegistrationService.Upsert(data,source);}catch(InvalidOperationException){rejected=true;}
                Check(rejected && data.Registrations.Count==1,"Overlapping frame registration");
                foreach(var invalid in snapshot.Candidates.Where(c=>c.Status=="Cần xác nhận"))
                {
                    rejected=false;try{RegistrationService.ReadWall(db,tx,snapshot,invalid,data.WallRule);}catch(InvalidOperationException){rejected=true;}
                    Check(rejected,"Open/crossing wall accepted");
                }
                Host.MarkRegistrations(snapshot,data);Check(candidate.Registered=="Đã đăng ký","Candidate registration state");
                var mapping=new IdMapping();db.DeepCloneObjects(new ObjectIdCollection{FrameReader.Resolve(db,tx,frame.Handle).ObjectId},SymbolUtilityServices.GetBlockModelSpaceId(db),mapping,false);
                var copied=(BlockReference)tx.GetObject(mapping[FrameReader.Resolve(db,tx,frame.Handle).ObjectId].Value,OpenMode.ForWrite);
                copied.TransformBy(Autodesk.AutoCAD.Geometry.Matrix3d.Displacement(new Autodesk.AutoCAD.Geometry.Vector3d(5000,0,0)));
                var another=data.Profile.ForBlock(copied.Handle.ToString());another.Level="T2";
                RegistrationService.ValidateFrame(another,FrameReader.Read(db,tx,another,false));
                data.Frames.Add(another);
                // Repeat the same frame update exactly as the batch commit does.
                data.Frames.RemoveAll(f=>f.Id==another.Id);data.Frames.Add(another);
                Check(data.Frames.Count==2 && data.Frames.Select(f=>f.Handle).Distinct().Count()==2,"Duplicate batch frame registration");
                data.Save(db,tx);tx.Commit();
            }
            int doors=0;
            using(var fixture=new Database(false,true))
            {
                fixture.ReadDwgFile(Path.Combine(Root,"door-fixture.dwg"),FileOpenMode.OpenForReadAndAllShare,false,null);
                fixture.CloseInput(true);fixture.Insunits=UnitsValue.Millimeters;
                using(var tx=fixture.TransactionManager.StartTransaction())
                {
                    var model=(BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(fixture),OpenMode.ForRead);
                    foreach(ObjectId objectId in model)
                    {
                        var block=tx.GetObject(objectId,OpenMode.ForRead) as BlockReference;
                        if(block==null||!block.IsDynamicBlock)continue;
                        var sample=RegistrationService.Describe(tx,block);
                        if(sample.Properties.Count==0)continue;
                        var property=sample.Properties.First();
                        var rule=new DoorDefinition{BlockName=sample.BlockName,WidthProperty=property.Key,HeightMm=2200};
                        var item=RegistrationService.ReadDoor(fixture,tx,new FrameSnapshot{Frame=new DrawingFrame()},
                            new FrameCandidate{Handle=block.Handle.ToString()},new[]{rule});
                        Check(Math.Abs(item.WidthMm-property.Value*Math.Abs(block.ScaleFactors.X))<1e-6 && item.HeightMm==2200,"Dynamic property scale/height");
                        rule.HeightProperty=property.Key;
                        var mapped=RegistrationService.ReadDoor(fixture,tx,new FrameSnapshot{Frame=new DrawingFrame()},new FrameCandidate{Handle=block.Handle.ToString()},new[]{rule});
                        Check(Math.Abs(mapped.HeightMm-item.WidthMm)<1e-6,"Dynamic height mapping");
                        rule.HeightProperty=null;rule.HeightTag="AS_TEST_HEIGHT";bool noHeight=false;
                        try{RegistrationService.ReadDoor(fixture,tx,new FrameSnapshot{Frame=new DrawingFrame()},new FrameCandidate{Handle=block.Handle.ToString()},new[]{rule});}
                        catch(InvalidOperationException){noHeight=true;}
                        Check(noHeight,"Missing height ATT accepted");rule.HeightTag=null;
                        rule.WidthProperty="MISSING";bool rejected=false;
                        try{RegistrationService.ReadDoor(fixture,tx,new FrameSnapshot{Frame=new DrawingFrame()},new FrameCandidate{Handle=block.Handle.ToString()},new[]{rule});}
                        catch(InvalidOperationException){rejected=true;}
                        Check(rejected,"Missing dynamic property accepted");doors++;
                    }
                }
            }
            Check(doors>0,"No dynamic block properties exercised");
            var catalog=Path.Combine(Root,"mock-warehouse","Catalog");
            Directory.CreateDirectory(Path.Combine(catalog,"qa-transactions"));Directory.CreateDirectory(Path.Combine(catalog,"sync"));
            File.WriteAllText(Path.Combine(catalog,"qa-transactions","ISO_QUANGANH.pending.json"),"{\"operation\":\"merge-hatches\",\"phase\":\"PREPARED\"}");
            File.WriteAllText(Path.Combine(catalog,"sync","state.json"),"{\"Items\":[{\"Id\":\"0123456789abcdef0123456789abcdef\",\"Kind\":\"layers\",\"Name\":\"QA_KT_Tuong\",\"Draft\":false,\"Values\":{}}]}");
            File.WriteAllText(Path.Combine(catalog,"resource-codes.json"),"{\"SchemaVersion\":1,\"Revision\":1,\"Items\":[{\"SourceId\":\"ISO_QUANGANH\",\"Kind\":\"layers\",\"Id\":\"0123456789abcdef0123456789abcdef\",\"Code\":\"LAY-0001\",\"Name\":\"QA_KT_Tuong\"}]}");
            var mock=Path.GetDirectoryName(catalog);Check(ResourceLayers.PendingStatus(mock).Contains("merge-hatches"),"Missing journal detail");
            Check(ResourceLayers.ReadOptions(mock,true).Single().Name=="QA_KT_Tuong","Published read-only choices blocked");
            bool blocked=false;try{ResourceLayers.ReadOptions(mock);}catch(InvalidOperationException){blocked=true;}Check(blocked,"Pending resource apply not blocked");
            File.WriteAllText(Path.Combine(Root,"definitions.txt"),"PASS: migration/profile clone and two real frames; wall area/original boundary; repeat IDs; overlapping/open/crossing rejected; "+doors+" dynamic blocks read with scaled distance property and height; height property and missing property/ATT rejected; pending QA readable with apply blocked.");
        }
        catch(System.Exception error){File.WriteAllText(Path.Combine(Root,"definitions.txt"),error.ToString());}
    }
    [CommandMethod("ASDEFREOPEN")]
    public void Reopen()
    {
        try
        {
            var db=App.DocumentManager.MdiActiveDocument.Database;
            using(var tx=db.TransactionManager.StartOpenCloseTransaction())
            {
                var data=FrameDocument.Load(db,tx);
                Check(data.Frames.Count==2 && data.Frames.Single(f=>f.Level=="T2").BlockName=="FRAME_TEST" && data.Profile!=null && data.Profile.NameTag=="TENBANVE" && data.Registrations.Count==1 && data.Registrations.Single().OriginalBoundary.Length==12,"Definition registration/profile lost after copy/reopen");
                Check(Model.Load(tx,db).ViewName=="Existing 0.2","Legacy data lost");
            }
            File.WriteAllText(Path.Combine(Root,"definitions-reopen.txt"),"PASS: profile, region, registration ID and legacy model persisted after save/copy/reopen.");
        }
        catch(System.Exception error){File.WriteAllText(Path.Combine(Root,"definitions-reopen.txt"),error.ToString());}
    }
}
