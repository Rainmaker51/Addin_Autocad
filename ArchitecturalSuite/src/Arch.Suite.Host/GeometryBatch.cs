using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Arch.Suite.Host
{
    // All normalization, hashing and cloning use resident entities in the same DWG.
    // Scratch ownership is removed before the caller transaction completes.
    internal sealed class GeometryBatch : IDisposable
    {
        internal readonly List<Entity> Entities=new List<Entity>();
        private Database database;private ObjectId scratchId;
        internal string Fingerprint;
        internal int Count=>Entities.Count;
        public void Dispose()
        {
            if(database!=null&&!scratchId.IsNull&&scratchId.IsValid&&!scratchId.IsErased)
            {
                var tx=database.TransactionManager.TopTransaction;
                if(tx!=null){var scratch=(BlockTableRecord)tx.GetObject(scratchId,OpenMode.ForWrite);scratch.Erase();}
            }
            Entities.Clear();
        }
        private static void Attach(Transaction tx,BlockTableRecord owner,Entity entity)
        {
            owner.AppendEntity(entity);tx.AddNewlyCreatedDBObject(entity,true);
        }
        private static DBText Text(Transaction tx,BlockTableRecord owner,DBText source)
        {
            var text=new DBText();Attach(tx,owner,text);text.SetPropertiesFrom(source);
            text.TextStyleId=source.TextStyleId;text.Normal=source.Normal;text.Height=source.Height;text.WidthFactor=source.WidthFactor;
            text.Oblique=source.Oblique;text.Rotation=source.Rotation;text.TextString=source.TextString;
            text.IsMirroredInX=source.IsMirroredInX;text.IsMirroredInY=source.IsMirroredInY;
            text.HorizontalMode=source.HorizontalMode;text.VerticalMode=source.VerticalMode;text.Position=source.Position;
            if(source.HorizontalMode!=TextHorizontalMode.TextLeft||source.VerticalMode!=TextVerticalMode.TextBase)text.AlignmentPoint=source.AlignmentPoint;
            return text;
        }
        private static void Flatten(Database db,Transaction tx,BlockTableRecord owner,Entity entity,List<Entity> leaves,int depth,ref string path)
        {
            if(depth>24||leaves.Count>100000)throw new InvalidOperationException("Block quá sâu hoặc nhóm vượt 100.000 thực thể.");
            if(!entity.Visible)return;
            if(entity is AttributeDefinition definition)
            {
                if(definition.Constant&&!definition.Invisible)leaves.Add(Text(tx,owner,definition));return;
            }
            if(entity is BlockReference block)
            {
                var record=(BlockTableRecord)tx.GetObject(block.BlockTableRecord,OpenMode.ForRead);
                if(record.IsFromExternalReference)throw new InvalidOperationException("Chưa sao chép nội dung Xref; cần xử lý nguồn tham chiếu trước.");
                path+=" / "+record.Name;
                foreach(ObjectId id in block.AttributeCollection)
                {var attribute=(AttributeReference)tx.GetObject(id,OpenMode.ForRead);if(!attribute.Invisible)leaves.Add(Text(tx,owner,attribute));}
            }
            if(entity is BlockReference||entity is Dimension)
            {
                var objects=new DBObjectCollection();
                try
                {
                    entity.Explode(objects);
                    foreach(DBObject item in objects)
                    {
                        if(!(item is Entity child))continue;
                        Attach(tx,owner,child);string previous=path;path+=" / "+child.GetType().Name;
                        Flatten(db,tx,owner,child,leaves,depth+1,ref path);path=previous;
                    }
                }
                finally{foreach(DBObject item in objects)if(item.ObjectId.IsNull)item.Dispose();objects.Dispose();}
                return;
            }
            if(!DrawingLinks.Supported(entity))throw new InvalidOperationException("Chưa hỗ trợ sao chép có bảo vệ: "+entity.GetType().Name);
            if(entity is Hatch hatch){if(!hatch.IsWriteEnabled)hatch.UpgradeOpen();hatch.Associative=false;}
            leaves.Add(entity);
        }
        internal static GeometryBatch Build(Database db,IEnumerable<SourceRegistration> sources,string kind)
        {
            var batch=new GeometryBatch{database=db};
            try
            {
                using(var nested=db.TransactionManager.StartTransaction())
                {
                    var blocks=(BlockTable)nested.GetObject(db.BlockTableId,OpenMode.ForWrite);
                    var scratch=new BlockTableRecord{Name="ARCH_TEMP_"+Guid.NewGuid().ToString("N")};blocks.Add(scratch);nested.AddNewlyCreatedDBObject(scratch,true);batch.scratchId=scratch.ObjectId;
                    var leaves=new List<Entity>();var signatures=new List<string>();
                    foreach(var source in sources)
                    {
                        var original=Model.Resolve(nested,db,source.Handle);if(original==null)throw new InvalidOperationException("Nguồn đã mất #"+source.Handle);
                        string path=original.GetType().Name;
                        try
                        {
                            var map=new IdMapping();db.DeepCloneObjects(new ObjectIdCollection{original.ObjectId},scratch.ObjectId,map,false);
                            var clone=(Entity)nested.GetObject(map[original.ObjectId].Value,OpenMode.ForRead);
                            int start=leaves.Count;Flatten(db,nested,scratch,clone,leaves,0,ref path);
                            foreach(var leaf in leaves.Skip(start))signatures.Add(DrawingLinks.Signature(leaf));
                        }
                        catch(Exception error){throw new SourceFailure("Chuẩn bị hình",kind,original,path,error);}
                    }
                    batch.Fingerprint=DrawingLinks.HashSignatures(signatures);
                    batch.Entities.AddRange(leaves);
                    // Transfer temporary ownership to the caller transaction. The batch erases
                    // only its own scratch record; previews abort the outer transaction.
                    nested.Commit();
                }
                return batch;
            }
            catch{batch.Dispose();throw;}
        }
    }
}
