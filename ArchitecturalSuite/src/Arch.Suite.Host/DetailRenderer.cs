using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;

namespace Arch.Suite.Host
{
    internal sealed class DetailRenderer
    {
        private sealed class Item
        {
            public string Input,Layer;
            public Func<Entity> Create;
        }
        private readonly Dictionary<string,Item> desired=new Dictionary<string,Item>();
        private readonly List<string> refresh=new List<string>(),remove=new List<string>();
        internal readonly List<string> Conflicts=new List<string>();
        internal int Count=>refresh.Count+remove.Count;
        private static string N(double n)=>n.ToString("R",CultureInfo.InvariantCulture);
        private static string P(Point3d p)=>N(p.X)+","+N(p.Y)+","+N(p.Z);
        private static ObjectId Symbol(Database db,LayerRole style)
        {
            ObjectId id;if(!db.TryGetObjectId(new Handle(Convert.ToInt64(style.LocalHandle,16)),out id)||id.IsErased)throw new InvalidOperationException("Chọn lại "+style.Code+" trong bộ tài nguyên.");
            return id;
        }
        private void Put(string key,string input,LayerRole style,string layer,Func<Entity> create)
        {
            desired.Add(key,new Item{Input=input+"|"+style.Id+"|"+style.SourceHash+"|"+style.LocalHandle+"|"+layer,Layer=layer,Create=create});
        }
        internal static string State(Entity entity,Transaction tx)
        {
            string common=entity.Layer+"|"+entity.Color+"|"+entity.LineWeight+"|"+entity.Linetype+"|"+entity.Visible+"|"+N(entity.LinetypeScale);
            var text=entity as DBText;
            if(text!=null)return common+"|"+P(text.Position)+"|"+text.TextString+"|"+N(text.Height)+"|"+N(text.Rotation)+"|"+text.TextStyleId.Handle+"|"+N(text.WidthFactor);
            var block=entity as BlockReference;
            if(block!=null)return common+"|"+block.BlockTableRecord.Handle+"|"+P(block.Position)+"|"+N(block.Rotation)+"|"+block.ScaleFactors+"|"+string.Join("|",block.AttributeCollection.Cast<ObjectId>().Select(id=>((AttributeReference)tx.GetObject(id,OpenMode.ForRead)).TextString));
            var dim=entity as AlignedDimension;
            if(dim!=null)return common+"|"+P(dim.XLine1Point)+"|"+P(dim.XLine2Point)+"|"+P(dim.DimLinePoint)+"|"+dim.DimensionText+"|"+dim.DimensionStyle.Handle+"|"+P(dim.TextPosition);
            var hatch=entity as Hatch;
            if(hatch!=null){
                var loops=new List<string>();
                for(int i=0;i<hatch.NumberOfLoops;i++){
                    var loop=hatch.GetLoopAt(i);if(!loop.IsPolyline)return common+"|edited-boundary";
                    loops.Add(string.Join(";",loop.Polyline.Cast<BulgeVertex>().Select(v=>N(v.Vertex.X)+","+N(v.Vertex.Y)+","+N(v.Bulge))));
                }
                return common+"|"+hatch.PatternName+"|"+N(hatch.PatternScale)+"|"+N(hatch.PatternAngle)+"|"+string.Join("/",loops);
            }
            return null;
        }
        internal void Prepare(Database db,Transaction tx,Model model,string wallLayer,string doorLayer)
        {
            foreach(var wall in model.Parts.Where(p=>p.Kind=="Wall")){
                Point3d a,b;double thickness;Model.Geometry(tx,db,wall,out a,out b,out thickness);
                var along=(b-a).GetNormal();var perp=new Vector3d(-along.Y,along.X,0);
                var doorShift=new Vector3d(model.Doors);var wallShift=new Vector3d(model.Masonry);
                var doors=model.Parts.Where(p=>p.HostId==wall.Id).OrderBy(p=>p.Offset).ToList();
                double cursor=0;string previous="start";
                Action<double,string> hatchSpan=(end,key)=>{
                    LayerRole style;
                    if(end-cursor<0.001 || !model.Styles.TryGetValue("hatches",out style))return;
                    var p=a+along*cursor+wallShift;var q=a+along*end+wallShift;var half=perp*thickness/2;
                    var corners=new[]{p-half,q-half,q+half,p+half};var values=JObject.Parse(style.Settings);
                    Put(wall.Id+"/h/"+key,string.Join(";",corners.Select(P))+values.ToString(),style,wallLayer,()=>{
                        var hatch=new Hatch();hatch.SetDatabaseDefaults(db);hatch.Elevation=p.Z;
                        HatchPatternType type;if(!Enum.TryParse((string)values["type"],true,out type))throw new InvalidOperationException("Kiểu hatch chưa hỗ trợ: "+style.Code);
                        hatch.SetHatchPattern(type,(string)values["pattern"]);hatch.PatternScale=(double?)values["scale"]??1;hatch.PatternAngle=(double?)values["angle"]??0;
                        hatch.ColorIndex=(int?)values["color"]??256;
                        var vertices=new Point2dCollection(corners.Select(pt=>new Point2d(pt.X,pt.Y)).ToArray());
                        hatch.AppendLoop(HatchLoopTypes.Polyline,vertices,new DoubleCollection(new double[4]));return hatch;
                    });
                };
                foreach(var door in doors){
                    var center=a+along*door.Offset+doorShift;var left=center-along*door.Width/2;
                    hatchSpan(door.Offset-door.Width/2,previous+"-"+door.Id);cursor=door.Offset+door.Width/2;previous=door.Id;
                    var angle=Math.Atan2(along.Y,along.X);
                    LayerRole block,text,dim;
                    if(model.Styles.TryGetValue("blocks",out block)){
                        var id=Symbol(db,block);var definition=(BlockTableRecord)tx.GetObject(id,OpenMode.ForRead);
                        var scale=definition.Units==UnitsValue.Undefined?1:UnitsConverter.GetConversionFactor(definition.Units,db.Insunits);
                        var rotation=angle+(door.ReverseSwing?Math.PI:0);
                        Put(door.Id+"/symbol",P(center)+N(rotation)+N(scale),block,doorLayer,()=>new BlockReference(center,id){Rotation=rotation,ScaleFactors=new Scale3d(scale)});
                    }
                    if(model.Styles.TryGetValue("texts",out text)){
                        var id=Symbol(db,text);var pos=center+perp*(thickness/2+150);var style=(TextStyleTableRecord)tx.GetObject(id,OpenMode.ForRead);
                        var height=style.TextSize>0?style.TextSize:150;
                        Put(door.Id+"/tag",P(pos)+door.Code+N(angle)+N(height),text,doorLayer,()=>new DBText{TextStyleId=id,Position=pos,TextString=door.Code,Height=height,Rotation=angle});
                    }
                    if(model.Styles.TryGetValue("dims",out dim)){
                        var id=Symbol(db,dim);var start=a+doorShift;var pos=left-perp*(thickness/2+400);
                        if(start.DistanceTo(left)>0.001)Put(door.Id+"/dimension",P(start)+P(left)+P(pos),dim,doorLayer,()=>new AlignedDimension(start,left,pos,"",id));
                    }
                }
                hatchSpan(a.DistanceTo(b),previous+"-end");
            }
            foreach(var pair in model.Details){
                var entity=Model.Resolve(tx,db,pair.Value.Handle);
                if(entity==null || State(entity,tx)!=pair.Value.Signature)Conflicts.Add(pair.Value.Handle);
                else if(!desired.ContainsKey(pair.Key))remove.Add(pair.Key);
                else if(desired[pair.Key].Input!=pair.Value.Input)refresh.Add(pair.Key);
            }
            refresh.AddRange(desired.Keys.Except(model.Details.Keys));
        }
        internal void Apply(Database db,Transaction tx,Model model)
        {
            var space=(BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db),OpenMode.ForWrite);
            foreach(var key in remove.Concat(refresh)){
                Output old;
                if(model.Details.TryGetValue(key,out old)){
                    var entity=Model.Resolve(tx,db,old.Handle);entity.UpgradeOpen();entity.Erase();model.Details.Remove(key);
                }
                Item item;if(!desired.TryGetValue(key,out item))continue;
                var next=item.Create();space.AppendEntity(next);tx.AddNewlyCreatedDBObject(next,true);next.Layer=item.Layer;
                var block=next as BlockReference;
                if(block!=null){
                    var definition=(BlockTableRecord)tx.GetObject(block.BlockTableRecord,OpenMode.ForRead);
                    foreach(ObjectId id in definition){
                        var att=tx.GetObject(id,OpenMode.ForRead) as AttributeDefinition;
                        if(att==null || att.Constant)continue;
                        var value=new AttributeReference();value.SetAttributeFromBlock(att,block.BlockTransform);
                        block.AttributeCollection.AppendAttribute(value);tx.AddNewlyCreatedDBObject(value,true);
                    }
                }
                var hatch=next as Hatch;if(hatch!=null)hatch.EvaluateHatch(true);
                var dim=next as Dimension;if(dim!=null)dim.RecomputeDimensionBlock(true);
                Model.Tag(tx,next,"ARCH_SUITE_VIEW",key.Split('/')[0],key);
                model.Details[key]=new Output{Handle=next.Handle.ToString(),Signature=State(next,tx),Input=item.Input};
            }
        }
    }
}
