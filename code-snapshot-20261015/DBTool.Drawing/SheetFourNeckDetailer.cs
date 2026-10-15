using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

public sealed record NeckDetailViews
{
    public NeckDetailViews(IEnumerable<DrawingPrimitive> plan,IEnumerable<DrawingPrimitive> aa,IEnumerable<DrawingPrimitive> bb)
    { Plan=ContractGuard.Copy(plan);AA=ContractGuard.Copy(aa);BB=ContractGuard.Copy(bb); }
    public System.Collections.Immutable.ImmutableArray<DrawingPrimitive> Plan { get; }
    public System.Collections.Immutable.ImmutableArray<DrawingPrimitive> AA { get; }
    public System.Collections.Immutable.ImmutableArray<DrawingPrimitive> BB { get; }
}

/// <summary>Neck geometry and steel are projected from their actual owners and paths, never a named sheet store.</summary>
public static class SheetFourNeckDetailer
{
    public static (IReadOnlyList<DrawingPrimitive> Plan,IReadOnlyList<DrawingPrimitive> AA,
        IReadOnlyList<DrawingPrimitive> BB,IReadOnlyList<DrawingPrimitive> Schedule) Build(
        DrawingInputs inputs,IReadOnlyDictionary<string,string> marks,string? activeManhole,PresentationRequest presentation)
    {
        var result=TryBuild(inputs,marks,presentation);
        if(!result.Success) throw new InvalidOperationException(string.Join(";",result.Diagnostics.Select(d=>d.Code)));
        return (result.Value!.Plan,result.Value.AA,result.Value.BB,[]);
    }

    public static OperationResult<NeckDetailViews> TryBuild(DrawingInputs inputs,
        IReadOnlyDictionary<string,string> marks,PresentationRequest presentation)
    {
        var necks=inputs.Geometry.Components.Where(c=>c.Kind==ComponentKind.Neck && c.ZRange.Length>0).ToArray();
        if(necks.Length==0) return new(true,new([],[],[]),[]);
        if(necks.Length!=1) return Fail("NECK_DETAIL_ASSEMBLY_UNSUPPORTED","One physical neck owner is required.");
        var neck=necks[0];
        var model=new GeometryModel(necks,inputs.Geometry.Openings.Where(o=>o.OwnerId==neck.Id));
        var plan=new List<DrawingPrimitive>();
        var aa=new List<DrawingPrimitive>();var bb=new List<DrawingPrimitive>();
        var outer=neck.Footprint.Outer.Vertices;
        double cx=(outer.Min(p=>p.X)+outer.Max(p=>p.X))/2;
        double cy=(outer.Min(p=>p.Y)+outer.Max(p=>p.Y))/2;
        plan.Add(new PolylinePrimitive("Plan/Poly/OuterOutline","Geometry/BaseOutline",SheetFourViews.PlanViewId,
            neck.Id,null,null,outer,true));
        for(int i=0;i<neck.Footprint.InnerLoops.Length;i++)
        {
            var hole=neck.Footprint.InnerLoops[i];
            plan.Add(new PolylinePrimitive(i==0?"Plan/Poly/InnerOpening":"Plan/Poly/InnerOpening/"+i,
                "Geometry/RoofOpening",SheetFourViews.PlanViewId,neck.Id,null,null,hole.Vertices,true));
            double x0=hole.Vertices.Min(p=>p.X),x1=hole.Vertices.Max(p=>p.X);
            double y0=hole.Vertices.Min(p=>p.Y),y1=hole.Vertices.Max(p=>p.Y);
            AddDimension(plan,SheetFourViews.PlanViewId,"Opening/"+i+"/X",new(x0,y1),new(x1,y1),0);
            AddDimension(plan,SheetFourViews.PlanViewId,"Opening/"+i+"/Y",new(x1,y0),new(x1,y1),Math.PI/2);
            AddDimension(aa,SheetFourViews.AAViewId,"Opening/"+i,new(y0,neck.ZRange.End),new(y1,neck.ZRange.End),0);
            AddDimension(bb,SheetFourViews.BBViewId,"Opening/"+i,new(x0,neck.ZRange.End),new(x1,neck.ZRange.End),0);
        }
        var aaProfile=SectionProjector.BuildSection(model,new SectionDefinition(SheetFourViews.AAViewId,SectionAxis.AlongY,cx,SectionMode.TrueSection));
        var bbProfile=SectionProjector.BuildSection(model,new SectionDefinition(SheetFourViews.BBViewId,SectionAxis.AlongX,cy,SectionMode.TrueSection));
        if(!aaProfile.Success) return new(false,null,aaProfile.Diagnostics);
        if(!bbProfile.Success) return new(false,null,bbProfile.Diagnostics);
        AddRegions(aa,aaProfile.Value!);AddRegions(bb,bbProfile.Value!);
        var bars=inputs.RebarLayout.Occurrences.Where(b=>b.ComponentId==neck.Id && b.ComponentKind==ComponentKind.Neck).ToArray();
        foreach(var bar in bars)
        {
            if(!marks.TryGetValue(bar.Id,out var mark)) return Fail("NECK_DETAIL_MARK_UNRESOLVED",bar.Id);
            var projected=PlanBarProjector.Build(bar,SheetFourViews.PlanViewId,"Rebar/Main",mark);
            if(!projected.Success) return new(false,null,projected.Diagnostics);
            plan.AddRange(projected.Value!);
        }
        if(bars.Length>0)
        {
            var sections=StructuralRebarProjector.Build(new RebarLayout(bars,[],CalculationScope.FullRebar),marks,aaProfile.Value,bbProfile.Value);
            if(!sections.Success) return new(false,null,sections.Diagnostics);
            foreach(var p in sections.Value!)
            {
                string? view=p.ViewId=="SteelAA"?SheetFourViews.AAViewId:p.ViewId=="SteelBB"?SheetFourViews.BBViewId:null;
                if(view is null) continue;
                var destination=p.ViewId=="SteelAA"?aa:bb;
                if(p is PolylinePrimitive line)
                    destination.Add(new PolylinePrimitive(view+"/"+p.SemanticId,p.Role,view,p.ComponentId,p.OccurrenceId,p.ScheduleMark,
                        line.Points,line.Closed,line.LogicalLengthMm,line.MaterialKind,line.Bulges));
                else if(p is CirclePrimitive dot)
                    destination.Add(new CirclePrimitive(view+"/"+p.SemanticId,p.Role,view,p.ComponentId,p.OccurrenceId,p.ScheduleMark,dot.Center,dot.RadiusMm));
            }
            AddLabels(plan,SheetFourViews.PlanViewId);AddLabels(aa,SheetFourViews.AAViewId);AddLabels(bb,SheetFourViews.BBViewId);
        }
        return new(true,new(plan,aa,bb),[]);

        void AddRegions(List<DrawingPrimitive> list,ViewProfile profile)
        {
            foreach(var region in profile.Regions)
            {
                list.Add(new PolylinePrimitive(profile.Id+"/"+region.Id+"/Outer","Geometry/Material",profile.Id,
                    neck.Id,null,null,region.Footprint.Outer.Vertices,true));
                foreach(var hole in region.Footprint.InnerLoops.Select((p,i)=>(Polygon:p,Index:i)))
                    list.Add(new PolylinePrimitive(profile.Id+"/"+region.Id+"/Inner/"+hole.Index,"Geometry/InnerLoop",profile.Id,
                        neck.Id,null,null,hole.Polygon.Vertices,true));
            }
        }
        void AddDimension(List<DrawingPrimitive> list,string view,string id,Point2Mm a,Point2Mm b,double rotation)
        {
            double length=rotation==0?Math.Abs(b.X-a.X):Math.Abs(b.Y-a.Y);
            if(length<=0) return;
            list.Add(new DimensionIntentPrimitive(view+"/Dimension/"+id,"Dimension/Neck",view,neck.Id,null,null,
                a,b,length,"<>",3*presentation.ScaleDenominator,rotation));
        }
        void AddLabels(List<DrawingPrimitive> list,string view)
        {
            var candidates=list.Where(p=>p.OccurrenceId is not null && p.ScheduleMark is not null && p.Role=="Rebar/Main").ToArray();
            int leftSlot=0, rightSlot=0;
            double midX = view == SheetFourViews.AAViewId ? cy : cx;
            double scale = presentation.ScaleDenominator;
            double blockScale = presentation.ScaleDenominator / 100.0;

            foreach(var family in candidates.GroupBy(p=>p.ScheduleMark).OrderBy(g=>g.Key,StringComparer.Ordinal))
            {
                var p=family.OrderBy(p=>p.SemanticId,StringComparer.Ordinal).First();
                var bar=bars.Single(b=>b.Id==p.OccurrenceId);
                Point2Mm? tip=p switch {PolylinePrimitive l=>l.Points[0],CirclePrimitive c=>c.Center,_=>null};
                if(tip is null) continue;
                bool isLeft = tip.Value.X < midX;
                double laneX = isLeft
                    ? (outer.Min(v=>view==SheetFourViews.AAViewId?v.Y:v.X) - 8 * scale)
                    : (outer.Max(v=>view==SheetFourViews.AAViewId?v.Y:v.X) + 8 * scale);
                int slot = isLeft ? leftSlot++ : rightSlot++;
                var at = new Point2Mm(laneX,
                    (view==SheetFourViews.PlanViewId?outer.Max(v=>v.Y):neck.ZRange.End) - slot * 8 * scale);
                double? pitch=inputs.NeckMesh is { } mesh ? bar.Direction switch
                { RebarDirection.Vertical=>mesh.VerticalSpacingMm,
                  RebarDirection.Horizontal=>mesh.VerticalSpacingMm+mesh.HorizontalSpacingDeltaMm,_=>null } : null;
                string id=view+"/Callout/"+family.Key;
                list.Add(new PolylinePrimitive(id+"/Leader","Rebar/Leader",view,neck.Id,bar.Id,family.Key,[tip.Value,at],false));
                list.Add(new BlockPrimitive(id,SheetTwoBarAnnotations.Role,view,neck.Id,"Block:BLT1",at,blockScale,
                    [new("SH",family.Key!),new("DK","%%c"+bar.DiameterMm),new("A",pitch is >0?FormattableString.Invariant($"@{pitch:0.###}"):" ")],
                    occurrenceId:bar.Id,scheduleMark:family.Key));
            }
        }
        OperationResult<NeckDetailViews> Fail(string code,string actual)=>new(false,null,
            [new Diagnostic(code,DiagnosticSeverity.Block,DiagnosticScope.Component,inputs.ManholeId,"Neck",necks.FirstOrDefault()?.Sources.FirstOrDefault(),
                "physical neck geometry and occurrence projection",actual,"Neck detail was not replaced by a template.")]);
    }
}
