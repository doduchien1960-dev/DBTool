using System.Globalization;
using DBTool.Domain;
using DBTool.Domain.Geometry;
namespace DBTool.Drawing;

/// <summary>
/// Visible pipe projections from typed opening frames and OpeningViewMap, in logical millimetres.
/// Displays inner, outer, and cutout loops with appropriate roles and preserves host inverts without circle-fitting box pipes.
/// </summary>
public static class SheetOnePipeViews
{
    // Published diagram stub and centerline extents are presentation dimensions,
    // independent of the pipe diameter, manhole code and model insertion point.
    public const double StubLengthMm=300;
    public const double PlanStubLengthMm=400;
    public const double CenterlineExtensionMm=150;
    public static IReadOnlyList<DrawingPrimitive> Build(GeometryModel geometry,PresentationRequest presentation)
    {
        var result=new List<DrawingPrimitive>();
        foreach(var op in geometry.Openings)
        {
            if(op.WallFrame is not {} wall) continue;
            var innerB = GetBounds(op.Inner);
            var outerB = op.PipeOuter is not null ? GetBounds(op.PipeOuter) : null;
            var cutoutB = GetBounds(op.ConcreteCutout);
            if(innerB is null || outerB is null || cutoutB is null) continue;

            var (innerCenter, innerHalfW, innerHalfH) = innerB.Value;
            var (outerCenter, outerHalfW, outerHalfH) = outerB.Value;
            var (cutoutCenter, cutoutHalfW, cutoutHalfH) = cutoutB.Value;

            var center=wall.ToGlobal(innerCenter.X,0,innerCenter.Y);
            bool alongY=wall.WallId is WallId.T1 or WallId.T3;
            string sideView=alongY?"AA":"BB";
            Point2Mm Side(double depth,double z)
            {
                var p=wall.ToGlobal(innerCenter.X,depth,z);
                return new(alongY?p.Y:p.X,p.Z);
            }
            double endDepth=op.ExtrusionRange.End+StubLengthMm;
            double planEndDepth=op.ExtrusionRange.End+PlanStubLengthMm;
            Point2Mm Plan(double u,double depth)
            {
                var p=wall.ToGlobal(u,depth,innerCenter.Y);
                return new(p.X,p.Y);
            }
            string planId=$"Plan/{op.Id}/Stub";
            foreach(var (halfW,role,label) in new[]{(innerHalfW,"Geometry/PipeInner","Inner"),(outerHalfW,"Geometry/PipeOuter","Outer")})
                foreach(int sign in new[]{-1,1})
                    result.Add(new PolylinePrimitive($"{planId}/{label}/{sign}","Geometry/PlanPipeHidden","Plan",op.OwnerId,null,null,
                        [Plan(innerCenter.X+sign*halfW,op.ExtrusionRange.Start),Plan(innerCenter.X+sign*halfW,planEndDepth)],false));
            foreach(int sign in new[]{-1,1})
                result.Add(new PolylinePrimitive($"{planId}/VisibleOuter/{sign}","Geometry/PlanPipeVisible","Plan",op.OwnerId,null,null,
                    [Plan(innerCenter.X+sign*outerHalfW,op.ExtrusionRange.End),Plan(innerCenter.X+sign*outerHalfW,planEndDepth)],false));
            result.Add(new PolylinePrimitive(planId+"/Center","Geometry/PlanPipeCenter","Plan",op.OwnerId,null,null,
                [Plan(innerCenter.X,op.ExtrusionRange.Start),Plan(innerCenter.X,planEndDepth)],false));
            // Conventional break dimensions are presentation sizes, independent of pipe diameter.
            const double breakHalfWidth=30, breakDepth=60, breakExtension=60;
            result.Add(new PolylinePrimitive(planId+"/Break","Geometry/NeckBreak","Plan",op.OwnerId,null,null,
                [Plan(innerCenter.X+outerHalfW+breakExtension,planEndDepth),
                 Plan(innerCenter.X+breakHalfWidth,planEndDepth),
                 Plan(innerCenter.X+breakHalfWidth,planEndDepth+breakDepth),
                 Plan(innerCenter.X-breakHalfWidth,planEndDepth-breakDepth),
                 Plan(innerCenter.X-breakHalfWidth,planEndDepth),
                 Plan(innerCenter.X-outerHalfW-breakExtension,planEndDepth)],false));
            foreach(var view in new[]{sideView,"Steel"+sideView})
            {
                string id=$"{view}/{op.Id}/Stub";
                foreach(int sign in new[]{-1,1})
                {
                    result.Add(new HatchPrimitive(id+$"/PipeMaterial/{sign}",SectionMaterialPolicy.SheetOnePipeRole,view,op.OwnerId,
                        [Side(op.ExtrusionRange.Start,innerCenter.Y+sign*innerHalfH),
                         Side(endDepth,innerCenter.Y+sign*innerHalfH),
                         Side(endDepth,innerCenter.Y+sign*outerHalfH),
                         Side(op.ExtrusionRange.Start,innerCenter.Y+sign*outerHalfH)],[],DrawingMaterialKind.Concrete,nativeBoundaryLoops:true));
                    if(cutoutHalfH>outerHalfH)
                        result.Add(new HatchPrimitive(id+$"/Grout/{sign}","Material/Mortar",view,op.OwnerId,
                            [Side(op.ExtrusionRange.Start,innerCenter.Y+sign*outerHalfH),
                             Side(op.ExtrusionRange.End,innerCenter.Y+sign*outerHalfH),
                             Side(op.ExtrusionRange.End,innerCenter.Y+sign*cutoutHalfH),
                             Side(op.ExtrusionRange.Start,innerCenter.Y+sign*cutoutHalfH)],[],DrawingMaterialKind.Concrete,nativeBoundaryLoops:true));
                }
                foreach(var (halfH,role,label) in new[]{(innerHalfH,"Geometry/PipeInner","Inner"),(outerHalfH,"Geometry/PipeOuter","Outer")})
                    foreach(int sign in new[]{-1,1})
                        result.Add(new PolylinePrimitive($"{id}/{label}/{sign}",role,view,op.OwnerId,null,null,
                            [Side(op.ExtrusionRange.Start,innerCenter.Y+sign*halfH),Side(endDepth,innerCenter.Y+sign*halfH)],false));
                result.Add(new PolylinePrimitive(id+"/End","Geometry/NeckBreak",view,op.OwnerId,null,null,
                    [Side(endDepth,innerCenter.Y+outerHalfH+breakExtension),
                     Side(endDepth,innerCenter.Y+breakHalfWidth),
                     Side(endDepth+breakDepth,innerCenter.Y+breakHalfWidth),
                     Side(endDepth-breakDepth,innerCenter.Y-breakHalfWidth),
                     Side(endDepth,innerCenter.Y-breakHalfWidth),
                     Side(endDepth,innerCenter.Y-outerHalfH-breakExtension)],false));
                result.Add(new PolylinePrimitive(id+"/Center","Geometry/PlanPipeCenter",view,op.OwnerId,null,null,
                    [Side(op.ExtrusionRange.Start,innerCenter.Y),Side(endDepth,innerCenter.Y)],false));
                var edge=Side(op.ExtrusionRange.End,innerCenter.Y-innerHalfH);
                result.Add(Level(id+"/Invert",view,edge));
                string dimLabel = op.Inner is CircleProfile
                    ? $"D{2*innerHalfW:0.###}"
                    : $"{2*innerHalfW:0.###}x{2*innerHalfH:0.###}";
                result.Add(new TextPrimitive(id+"/Diameter","DimensionText",view,op.OwnerId,null,null,
                    new(edge.X-120,innerCenter.Y+30),dimLabel,60,0));
            }
            // Conventional cut arrows look toward T1 in BB and T4 in AA.
            string? frontView=wall.WallId==WallId.T1?"BB":wall.WallId==WallId.T4?"AA":null;
            if(frontView is not null)
            foreach(var view in new[]{frontView,"Steel"+frontView})
            {
                var point=new Point2Mm(frontView=="BB"?center.X:center.Y,center.Z);
                string id=$"{view}/{op.Id}/Face";
                if(op.Inner is CircleProfile && op.PipeOuter is CircleProfile && op.ConcreteCutout is CircleProfile)
                {
                    result.Add(new CirclePrimitive(id+"/Inner","Geometry/PipeInner",view,op.OwnerId,null,null,point,innerHalfW));
                    result.Add(new CirclePrimitive(id+"/Outer","Geometry/PipeOuter",view,op.OwnerId,null,null,point,outerHalfW));
                    result.Add(new CirclePrimitive(id+"/Cutout","Geometry/NeckBreak",view,op.OwnerId,null,null,point,cutoutHalfW));
                    result.Add(new DiameterDimensionPrimitive(id+"/Diameter","Dimension/PipeDiameter",view,op.OwnerId,point,2*innerHalfW));
                }
                else
                {
                    result.Add(BoxRect(id+"/Inner","Geometry/PipeInner",view,op.OwnerId,point,innerHalfW,innerHalfH));
                    result.Add(BoxRect(id+"/Outer","Geometry/PipeOuter",view,op.OwnerId,point,outerHalfW,outerHalfH));
                    result.Add(BoxRect(id+"/Cutout","Geometry/NeckBreak",view,op.OwnerId,point,cutoutHalfW,cutoutHalfH));
                }
                result.Add(Level(id+"/Invert",view,new(point.X,point.Y-innerHalfH)));
            }
            BlockPrimitive Level(string id,string view,Point2Mm position)
            {
                string label=presentation.ElevationMode==ElevationMode.Absolute
                    ? (presentation.BaseElevationM!.Value.Value+(innerCenter.Y-innerHalfH)/1000).ToString("0.00",CultureInfo.InvariantCulture)
                    : $"Z/{op.Id}/Invert";
                return new(id,"Elevation/SheetOneFloor",view,op.OwnerId,"Block:CT",position,.3,[new("CT",label)]);
            }
        }
        return result;
    }

    private static (Point2Mm Center,double HalfWidth,double HalfHeight)? GetBounds(Profile2Mm profile) => profile switch
    {
        CircleProfile circle => (circle.Center,circle.RadiusMm,circle.RadiusMm),
        RectangleProfile rect => (new((rect.Rectangle.MinX+rect.Rectangle.MaxX)/2,(rect.Rectangle.MinY+rect.Rectangle.MaxY)/2),
                                  (rect.Rectangle.MaxX-rect.Rectangle.MinX)/2,
                                  (rect.Rectangle.MaxY-rect.Rectangle.MinY)/2),
        PolygonProfile poly => (new((poly.Polygon.Vertices.Min(p=>p.X)+poly.Polygon.Vertices.Max(p=>p.X))/2,
                                    (poly.Polygon.Vertices.Min(p=>p.Y)+poly.Polygon.Vertices.Max(p=>p.Y))/2),
                                (poly.Polygon.Vertices.Max(p=>p.X)-poly.Polygon.Vertices.Min(p=>p.X))/2,
                                (poly.Polygon.Vertices.Max(p=>p.Y)-poly.Polygon.Vertices.Min(p=>p.Y))/2),
        MultiCellProfile multi => (new((multi.Cells.SelectMany(c=>c.Polygon.Vertices).Min(p=>p.X)+multi.Cells.SelectMany(c=>c.Polygon.Vertices).Max(p=>p.X))/2,
                                       (multi.Cells.SelectMany(c=>c.Polygon.Vertices).Min(p=>p.Y)+multi.Cells.SelectMany(c=>c.Polygon.Vertices).Max(p=>p.Y))/2),
                                   (multi.Cells.SelectMany(c=>c.Polygon.Vertices).Max(p=>p.X)-multi.Cells.SelectMany(c=>c.Polygon.Vertices).Min(p=>p.X))/2,
                                   (multi.Cells.SelectMany(c=>c.Polygon.Vertices).Max(p=>p.Y)-multi.Cells.SelectMany(c=>c.Polygon.Vertices).Min(p=>p.Y))/2),
        _ => null
    };

    private static PolylinePrimitive BoxRect(string id,string role,string view,string ownerId,Point2Mm center,double hw,double hh) =>
        new(id,role,view,ownerId,null,null,
            [new(center.X-hw,center.Y-hh),
             new(center.X+hw,center.Y-hh),
             new(center.X+hw,center.Y+hh),
             new(center.X-hw,center.Y+hh)],true);
}
