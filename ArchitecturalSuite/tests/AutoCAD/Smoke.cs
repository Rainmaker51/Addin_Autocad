using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Arch.Suite.Host;

public class Smoke
{
    [CommandMethod("ASLIBTEST")]
    public void Library()
    {
        var doc=Application.DocumentManager.MdiActiveDocument;
        if(!doc.Name.EndsWith("suite-smoke.dwg",StringComparison.OrdinalIgnoreCase))return;
        var root=Environment.GetEnvironmentVariable("ARCH_SUITE_TEST_APP");
        var choices=ResourceLayers.ReadOptions(root);
        var layer=choices.Single(i=>i.Kind=="layers" && i.Name=="QA_KT_Dim");
        var styles=new Dictionary<string,LayerRole>{
            {"blocks",choices.Single(i=>i.Kind=="blocks" && i.Name=="GC_CTCUA")},
            {"texts",choices.Single(i=>i.Kind=="texts" && i.Name=="QA_Arial")},
            {"dims",choices.Single(i=>i.Kind=="dims" && i.Name=="DIM")},
            {"hatches",choices.First(i=>i.Kind=="hatches")}};
        using(var tx=doc.Database.TransactionManager.StartTransaction()){
            var m=Model.Load(tx,doc.Database);
            ResourceLayers.ApplyResources(doc,tx,m,root,layer,layer,styles);
            if(m.Styles.Count!=4 || ResourceLayers.Demand(doc.Database,tx,m.WallLayer)!=layer.Name)throw new InvalidOperationException("Unresolved warehouse roles");
            tx.Commit();
        }
        doc.Editor.WriteMessage("\nARCH_SUITE_WAREHOUSE_PASS\n");
    }
    [CommandMethod("ASTESTPALETTE")]
    public void Palette()
    {
        var doc=Application.DocumentManager.MdiActiveDocument;
        if(!doc.Name.EndsWith("suite-smoke.dwg",StringComparison.OrdinalIgnoreCase))return;
        var host=new Host();
        try{
            if(host.ManagedCommands.Count!=1 || host.ManagedCommands[0].Command!="QAARCHOPEN")throw new InvalidOperationException("Wrong entry");
            host.Open();host.Open();
            var field=typeof(Host).GetField("palette",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var palette=(Autodesk.AutoCAD.Windows.PaletteSet)field.GetValue(null);
            if(!palette.Visible || palette.Count!=1)throw new InvalidOperationException("Palette not visible or duplicated");
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(doc.Name),"palette-result.txt"),"PASS · opened twice, single visible PaletteSet");
        }
        catch(System.Exception ex){System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(doc.Name),"palette-result.txt"),ex.ToString());}
        finally{host.Terminate();}
    }
    [CommandMethod("ASREOPEN")]
    public void Reopened()
    {
        var doc=Application.DocumentManager.MdiActiveDocument;
        using(var tx=doc.Database.TransactionManager.StartOpenCloseTransaction()){
            var model=Model.Load(tx,doc.Database);
            if(model.Parts.Count!=2 || model.Parts.Single(p=>p.Kind=="Door").HostId!="wall" || model.Outputs.Count!=8 || model.Tables.Count!=2)throw new InvalidOperationException("Lost links");
            if(model.Outputs.Values.Concat(model.Tables.Values).Any(o=>Model.Resolve(tx,doc.Database,o.Handle)==null))throw new InvalidOperationException("Lost handles");
            if(model.Details.Count!=5 || model.Details.Values.Any(o=>Model.Resolve(tx,doc.Database,o.Handle)==null))throw new InvalidOperationException("Lost annotations");
        }
        doc.Editor.WriteMessage("\nARCH_SUITE_REOPEN_PASS\n");
    }
    [CommandMethod("ASTESTUNDOEDIT")]
    public void UndoEdit()
    {
        var doc=Application.DocumentManager.MdiActiveDocument;
        if(!doc.Name.EndsWith("suite-smoke.dwg",StringComparison.OrdinalIgnoreCase))return;
        using(var tx=doc.Database.TransactionManager.StartTransaction()){
            var model=Model.Load(tx,doc.Database);
            model.Parts.Single(p=>p.Kind=="Door").Width=1100;model.Save(tx,doc.Database);tx.Commit();
        }
    }
    [CommandMethod("ASCHECKWIDTH",CommandFlags.NoUndoMarker)]
    public void CheckWidth()
    {
        var doc=Application.DocumentManager.MdiActiveDocument;
        using(var tx=doc.Database.TransactionManager.StartOpenCloseTransaction())
            doc.Editor.WriteMessage("\nARCH_SUITE_WIDTH_"+Model.Load(tx,doc.Database).Parts.Single(p=>p.Kind=="Door").Width+"\n");
    }
    [CommandMethod("ASREGISTERTEST")]
    public void Registration()
    {
        var doc=Application.DocumentManager.MdiActiveDocument;
        if(!doc.Name.EndsWith("suite-smoke.dwg",StringComparison.OrdinalIgnoreCase))return;
        using(var tx=doc.Database.TransactionManager.StartTransaction()){
            var model=Model.Load(tx,doc.Database);model.Bounds=new[]{100000d,0d,104000d,3000d};model.LevelName="Test";
            var space=(BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(doc.Database),OpenMode.ForWrite);
            foreach(var y in new[]{100,300}){
                var edge=new Line(new Point3d(100010,y,0),new Point3d(103000,y,0));space.AppendEntity(edge);tx.AddNewlyCreatedDBObject(edge,true);
            }
            var found=Candidates.Search(doc,tx,model);var pair=found.Single(p=>p.Kind=="Tường");
            model.Register(doc,tx,false,pair.Handles);
            var part=model.Find(pair.Handles[0]);
            if(part==null || part.Level!="Test" || !Guid.TryParseExact(part.Id,"N",out _))throw new InvalidOperationException("Registration failed");
            model.Unregister(tx,doc.Database,part);
            if(Model.Resolve(tx,doc.Database,pair.Handles[0])==null)throw new InvalidOperationException("Removed source geometry");
            // This probe leaves the fixture unchanged.
            tx.Abort();
        }
        doc.Editor.WriteMessage("\nARCH_SUITE_REGISTRATION_PASS\n");
    }
    [CommandMethod("ASTEST")]
    public void Run()
    {
        var doc=Application.DocumentManager.MdiActiveDocument;
        if(!doc.Name.EndsWith("suite-smoke.dwg",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Synthetic fixture only");
        var db=doc.Database;
        using(var tx=db.TransactionManager.StartTransaction())
        {
            var space=(BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db),OpenMode.ForWrite);
            var handles=new List<string>();
            foreach(var y in new[]{0,100}){
                var l=new Line(new Point3d(0,y,0),new Point3d(5000,y,0));space.AppendEntity(l);tx.AddNewlyCreatedDBObject(l,true);handles.Add(l.Handle.ToString());
            }
            var bt=(BlockTable)tx.GetObject(db.BlockTableId,OpenMode.ForWrite);
            var def=new BlockTableRecord{Name="TEST_DOOR"};bt.Add(def);tx.AddNewlyCreatedDBObject(def,true);
            var br=new BlockReference(new Point3d(1800,50,0),def.ObjectId);space.AppendEntity(br);tx.AddNewlyCreatedDBObject(br,true);
            var m=new Model{Masonry=new[]{0d,5000d,0d},Doors=new[]{0d,10000d,0d}};
            var fixtureLayer=(LayerTableRecord)tx.GetObject(db.LayerZero,OpenMode.ForRead);
            var role=new LayerRole{Code="TEST",LocalHandle=fixtureLayer.Handle.ToString(),Appearance=ResourceLayers.Appearance(tx,fixtureLayer)};
            m.WallLayer=role;m.DoorLayer=role;
            m.Styles["blocks"]=new LayerRole{Id="fixture-block",LocalHandle=def.Handle.ToString()};
            m.Styles["dims"]=new LayerRole{Id="fixture-dim",LocalHandle=db.Dimstyle.Handle.ToString()};
            m.Styles["texts"]=new LayerRole{Id="fixture-text",LocalHandle=db.Textstyle.Handle.ToString()};
            m.Styles["hatches"]=new LayerRole{Id="fixture-hatch",Settings="{\"pattern\":\"ANSI31\",\"type\":\"PreDefined\",\"scale\":1,\"angle\":0}"};
            m.Parts.Add(new Part{Kind="Wall",Code="W01",Id="wall",Level="T1",Height=3200,Handles=handles.ToArray()});
            m.Parts.Add(new Part{Kind="Door",Code="D01",Id="door",HostId="wall",Level="T1",Width=900,Height=2200,Offset=1800,Handles=new[]{br.Handle.ToString()}});
            m.Save(tx,db);tx.Commit();
        }
        Workspace.Run(doc,"preview");Workspace.Run(doc,"apply");Workspace.Run(doc,"apply");
        Workspace.Run(doc,"walltable");Workspace.Run(doc,"doortable");Workspace.Run(doc,"walltable");Workspace.Run(doc,"doortable");
        using(var tx=db.TransactionManager.StartTransaction()){
            var model=Model.Load(tx,db);model.Parts.Single(p=>p.Kind=="Door").Width=1000;model.Save(tx,db);tx.Commit();
        }
        Workspace.Run(doc,"apply");
        string editedHandle;
        Point3d editedPoint;
        using(var tx=db.TransactionManager.StartTransaction())
        {
            var m=Model.Load(tx,db);
            if(m.Outputs.Count!=8)throw new InvalidOperationException("Expected eight linked segments");
            if(m.Details.Count!=5)throw new InvalidOperationException("Expected symbol, tag, dimension, two hatches");
            if(m.Tables.Count!=2)throw new InvalidOperationException("Expected two linked schedules");
            var wallTable=(Table)Model.Resolve(tx,db,m.Tables["WALL_SCHEDULE"].Handle);
            if(wallTable.Cells[1,5].TextString!="13.8")throw new InvalidOperationException("Incorrect net area "+wallTable.Cells[1,5].TextString);
            var item=m.Outputs.Values.First();
            var l=(Line)Model.Resolve(tx,db,item.Handle);l.UpgradeOpen();l.EndPoint+=new Vector3d(12,0,0);editedPoint=l.EndPoint;editedHandle=item.Handle;tx.Commit();
        }
        Workspace.Run(doc,"apply");
        using(var tx=db.TransactionManager.StartOpenCloseTransaction()){
            var m=Model.Load(tx,db);if(m.Outputs.Count!=8)throw new InvalidOperationException("Conflict changed outputs");
            if(((Line)Model.Resolve(tx,db,editedHandle)).EndPoint!=editedPoint)throw new InvalidOperationException("Manual edit lost");
        }
        doc.Editor.WriteMessage("\nARCH_SUITE_SMOKE_PASS\n");
    }
}
