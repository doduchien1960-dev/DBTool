using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Annotation only: dimensions measure the existing rectangular geometry snapshot.</summary>
public static class SheetOneDimensionPlanner
{
    public static IReadOnlyList<DrawingPrimitive>? Build(GeometryModel geometry, string view,double? assembledTopMm=null)
    {
        if (view is not ("Plan" or "AA" or "BB" or "SteelAA" or "SteelBB" or "WallMeshPlan")) return null;
        var walls = geometry.Components.Where(c => c.Kind == ComponentKind.Wall && c.Id != "Wall/Mortar").ToArray();
        if (walls.Length != 4) return null;
        Bounds2Mm Bounds(ComponentGeometry c) => new(c.Footprint.Outer.Vertices.Min(p => p.X), c.Footprint.Outer.Vertices.Min(p => p.Y),
            c.Footprint.Outer.Vertices.Max(p => p.X), c.Footprint.Outer.Vertices.Max(p => p.Y));
        var wb = walls.Select(Bounds).ToArray();
        var vertical = wb.Where(b => b.MaxX-b.MinX < b.MaxY-b.MinY).OrderBy(b => b.MinX).ToArray();
        var horizontal = wb.Where(b => b.MaxX-b.MinX > b.MaxY-b.MinY).OrderBy(b => b.MinY).ToArray();
        if (vertical.Length != 2 || horizontal.Length != 2) return null;
        var inner = new Bounds2Mm(vertical[0].MaxX, horizontal[0].MaxY, vertical[1].MinX, horizontal[1].MinY);
        var outer = new Bounds2Mm(wb.Min(b => b.MinX), wb.Min(b => b.MinY), wb.Max(b => b.MaxX), wb.Max(b => b.MaxY));
        var pad = geometry.Components.SingleOrDefault(c => c.Id == "Pad");
        var basis = geometry.Components.Single(c => c.Id == "Base"); var bb = Bounds(basis);
        // L06/G10: dimension the actual lowest support. With pad OFF this
        // is the concrete base itself, without inventing a bedding component.
        var lowestSupport = pad ?? basis;
        var pb = Bounds(lowestSupport);
        var roof = geometry.Components.Single(c => c.Id == "Roof");
        var neck = geometry.Components.FirstOrDefault(c => c.Id == "Neck");
        var result = new List<DrawingPrimitive>();
        void Dim(string id, Point2Mm a, Point2Mm b, Point2Mm line, bool y, string label = "<>")
        {
            double measure = Math.Abs(y ? b.Y-a.Y : b.X-a.X);
            if (measure == 0) return;
            if(view=="WallMeshPlan")
            {
                a=WallMeshPresentation.GraphicPoint(a);b=WallMeshPresentation.GraphicPoint(b);
                line=WallMeshPresentation.GraphicPoint(line);
            }
            result.Add(new DimensionIntentPrimitive($"{view}/Chain/{id}", "Dimension/Linear", view, null, null, null,
                a, b, measure, label, 0, y ? Math.PI/2 : 0, line));
        }
        if (view is "Plan" or "WallMeshPlan")
        {
            pb = bb; // Published plan dimensions follow the base, not the buried pad.
            if (view == "WallMeshPlan")
            {
                var xStations = new[] {bb.MinX,outer.MinX,outer.MaxX,bb.MaxX}.Distinct().Order().ToArray();
                var yStations = new[] {bb.MinY,outer.MinY,outer.MaxY,bb.MaxY}.Distinct().Order().ToArray();
                for(int i=1;i<xStations.Length;i++) Dim($"X/{i}",new(xStations[i-1],bb.MaxY),new(xStations[i],bb.MaxY),new(xStations[i],bb.MaxY+180),false);
                for(int i=1;i<yStations.Length;i++) Dim($"Y/{i}",new(bb.MaxX,yStations[i-1]),new(bb.MaxX,yStations[i]),new(bb.MaxX+180,yStations[i]),true);
                Dim("OverallX",new(bb.MinX,bb.MaxY),new(bb.MaxX,bb.MaxY),new(0,bb.MaxY+360),false);
                Dim("OverallY",new(bb.MaxX,bb.MinY),new(bb.MaxX,bb.MaxY),new(bb.MaxX+360,0),true);
                return result;
            }
            var xs = new[] { new Point2Mm(bb.MinX,bb.MinY),new(outer.MinX,outer.MinY),new(inner.MinX,inner.MinY),
                new(inner.MaxX,inner.MinY),new(outer.MaxX,outer.MinY),new(bb.MaxX,bb.MinY) };
            var ys = new[] { new Point2Mm(bb.MinX,bb.MaxY),new(outer.MinX,outer.MaxY),new(inner.MinX,inner.MaxY),
                new(inner.MinX,inner.MinY),new(outer.MinX,outer.MinY),new(bb.MinX,bb.MinY) };
            for (int i=1;i<xs.Length;i++) Dim($"X/{i}",xs[i-1],xs[i],new(xs[i].X,pb.MinY-180),false);
            for (int i=1;i<ys.Length;i++) Dim($"Y/{i}",ys[i-1],ys[i],new(pb.MinX-180,ys[i].Y),true);
            Dim("OverallX", new(pb.MinX,pb.MinY), new(pb.MaxX,pb.MinY), new(0,pb.MinY-360), false);
            Dim("OverallY",new(pb.MinX,pb.MaxY),new(pb.MinX,pb.MinY),new(pb.MinX-360,pb.MinY),true);
            foreach(var opening in geometry.Openings)
            {
                if(opening.WallFrame is not {} frame || opening.Inner is not CircleProfile pipe ||
                    opening.PipeOuter is not CircleProfile pipeOuter) continue;
                bool horizontalPipe=frame.WallId is WallId.T1 or WallId.T3;
                double half=horizontalPipe?(inner.MaxX-inner.MinX)/2:(inner.MaxY-inner.MinY)/2;
                double tip=opening.ExtrusionRange.End+SheetOnePipeViews.PlanStubLengthMm;
                var stations=new[]{(-half,0d),(pipe.Center.X-pipeOuter.RadiusMm,tip),
                    (pipe.Center.X-pipe.RadiusMm,tip),(pipe.Center.X+pipe.RadiusMm,tip),
                    (pipe.Center.X+pipeOuter.RadiusMm,tip),(half,0d)};
                Point2Mm At(double u,double depth) { var p=frame.ToGlobal(u,depth,0);return new(p.X,p.Y); }
                for(int i=1;i<stations.Length;i++)
                    Dim($"Pipe/{opening.Id}/{i}",At(stations[i-1].Item1,stations[i-1].Item2),
                        At(stations[i].Item1,stations[i].Item2),At(stations[i].Item1,tip+180),!horizontalPipe);
            }
            return result;
        }
        bool alongY = view is "AA" or "SteelAA";
        double Min(Bounds2Mm b) => alongY ? b.MinY : b.MinX;
        double Max(Bounds2Mm b) => alongY ? b.MaxY : b.MaxX;
        var haunch=geometry.Components.FirstOrDefault(c=>c.Id=="Base/Haunch");
        if((view is "AA" or "BB") && haunch is not null && haunch.Footprint.InnerLoops.Length>0)
        {
            double outerLeft=Min(Bounds(haunch));
            double innerLeft=haunch.Footprint.InnerLoops.SelectMany(l=>l.Vertices).Min(p=>alongY?p.Y:p.X);
            var toe=new Point2Mm(innerLeft,haunch.ZRange.Start);
            var topCorner=new Point2Mm(outerLeft,haunch.ZRange.End);
            Dim("Haunch/Width",toe,topCorner,new(outerLeft,topCorner.Y+120),false);
            Dim("Haunch/Height",toe,topCorner,new(outerLeft+220,topCorner.Y),true);
        }
        // Default measures the physical seat; a supported native assembly measures its drawn plate/cap top.
        double floor = basis.ZRange.End, bottom = lowestSupport.ZRange.Start,
            top = assembledTopMm ?? geometry.Components.FirstOrDefault(c=>c.Id=="Collar")?.ZRange.Start ?? neck?.ZRange.End ?? roof.ZRange.End;
        var points = new[] { new Point2Mm(Min(pb),bottom), new(Min(bb),basis.ZRange.Start), new(Min(outer),floor),
            new(Min(inner),floor), new(Max(inner),floor), new(Max(outer),floor), new(Max(bb),basis.ZRange.Start), new(Max(pb),bottom) }
            .GroupBy(p=>p.X).Select(g=>g.MaxBy(p=>p.Y)).OrderBy(p=>p.X).ToArray();
        for (int i=1;i<points.Length;i++) Dim($"Base/{i}", points[i-1], points[i], new(points[i].X,bottom-180), false);
        // Both source structural and steel sections use one overall bedding dimension below the chain.
        Dim("Overall", new(Min(pb),bottom), new(Max(pb),bottom), new(0,bottom-360), false);
        var levels = new[] { bottom, basis.ZRange.Start, floor, roof.ZRange.Start, roof.ZRange.End, top }.Distinct().Order().ToArray();
        foreach (int side in new[] {-1,1})
        {
            double x = side < 0 ? Min(outer) : Max(outer);
            double line = side < 0 ? Min(pb)-180 : Max(pb)+180;
            var sideLevels=levels.ToList();
            foreach(var opening in geometry.Openings)
            {
                if(opening.WallFrame is not {} frame || opening.Inner is not CircleProfile pipe ||
                    opening.PipeOuter is not CircleProfile pipeOuter ||
                    alongY!=(frame.WallId is WallId.T1 or WallId.T3)) continue;
                var tip=frame.ToGlobal(pipe.Center.X,opening.ExtrusionRange.End+SheetOnePipeViews.StubLengthMm,0);
                double coordinate=alongY?tip.Y:tip.X;
                if(Math.Sign(coordinate)!=side) continue;
                line=side<0?Math.Min(line,coordinate-300):Math.Max(line,coordinate+300);
                sideLevels.AddRange(new[]{pipe.Center.Y-pipeOuter.RadiusMm,pipe.Center.Y-pipe.RadiusMm,
                    pipe.Center.Y+pipe.RadiusMm,pipe.Center.Y+pipeOuter.RadiusMm}.Where(z=>z>floor && z<roof.ZRange.Start));
            }
            var measuredLevels=sideLevels.Distinct().Order().ToArray();
            var capHole=neck?.Footprint.InnerLoops.SelectMany(l=>l.Vertices).ToArray();
            double capCenter=capHole is {Length:>0}
                ?(capHole.Min(p=>alongY?p.Y:p.X)+capHole.Max(p=>alongY?p.Y:p.X))/2:x;
            Point2Mm AtLevel(double z)=>new(z==top?capCenter:x,z);
            for (int i=1;i<measuredLevels.Length;i++)
                Dim($"Side/{side}/{i}",AtLevel(measuredLevels[i-1]),AtLevel(measuredLevels[i]),new(line,measuredLevels[i]),true,
                    measuredLevels[i-1]==roof.ZRange.End ? "Ctb=<>" : measuredLevels[i-1]==floor && measuredLevels[i]==roof.ZRange.Start ? "Htb=<>" : "<>");
            Dim($"Height/{side}",new(x,bottom),AtLevel(top),new(line+side*180,top),true,"Htb=<>");
        }
        if (neck is not null)
        {
            var nb = Bounds(neck);
            var openingStations = neck.Footprint.InnerLoops.SelectMany(loop => loop.Vertices)
                .Select(p => alongY ? p.Y : p.X);
            var xs = new[] { Min(outer), Min(nb), Max(nb), Max(outer) }
                .Concat(openingStations).Distinct().Order().ToArray();
            for (int i=1;i<xs.Length;i++) Dim($"Neck/{i}",new(xs[i-1],roof.ZRange.End),new(xs[i],roof.ZRange.End),new(xs[i],top+180),false);
            Dim("RoofOverall",new(Min(outer),roof.ZRange.End),new(Max(outer),roof.ZRange.End),new(0,top+360),false);
        }
        return result;
    }
}
