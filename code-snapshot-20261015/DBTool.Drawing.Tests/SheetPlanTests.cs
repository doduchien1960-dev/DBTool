using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;
public sealed class SheetPlanTests
{
    [Fact]
    public void PhysicalBendingAndSchedulePagesSpillWithoutDroppingOrDuplicatingViews()
    {
        var views=Enumerable.Range(1,12).Select(i=>new ViewDefinition($"Bending/D{i:000}",ViewPurpose.Schematic,
            new(0,0),new Bounds2Mm(-500,1000,2500,4000),30)).Concat(
            Enumerable.Range(1,2).Select(i=>new ViewDefinition(i==1?"SteelSchedule":"SteelSchedule/Page/2",ViewPurpose.SteelTable,
                new(0,0),new Bounds2Mm(0,0,7500,7200),30))).ToArray();
        var package=new DrawingPackage(new string('a',64),"test",views,[],new(-500,0,7500,7200),CalculationScope.FullRebar);
        var plan=SheetPlan.Create(VhgSheetFamily.Visit,[],new Dictionary<SheetContent,IReadOnlyList<string>>{
            [SheetContent.Tables]=views.Where(v=>v.Purpose==ViewPurpose.SteelTable).Select(v=>v.Id).ToArray(),
            [SheetContent.Bending]=views.Where(v=>v.Purpose==ViewPurpose.Schematic).Select(v=>v.Id).ToArray()});
        var profile=new PresentationProfile(420,297,10,10,10,10,0,5,[30],30,[2],"test");
        var result=SheetLayoutPlanner.Arrange(package,profile,plan,true);
        Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code)));
        var sheets=result.Value!.Sheets;
        Assert.Equal(7,sheets.Length);
        Assert.All(sheets,s=>Assert.Equal(sheets.Length,s.PageCount));
        Assert.All(sheets.Where(s=>s.Frames.Any(f=>f.ViewId.StartsWith("SteelSchedule"))),s=>Assert.Equal("Primary/4",s.SourcePlanPageId));
        Assert.All(sheets.Where(s=>s.Frames.Any(f=>f.ViewId.StartsWith("Bending/"))),s=>Assert.Equal("Primary/5",s.SourcePlanPageId));
        Assert.Equal(views.Select(v=>v.Id).Order().ToArray(),sheets.SelectMany(s=>s.Frames).Select(f=>f.ViewId).Order().ToArray());
        Assert.All(sheets.SelectMany(s=>s.Frames),f=>Assert.Equal(30,f.Transform.UniformScaleDenominator));
    }

    [Fact]
    public void MeasuredNeckAndScheduleFitBesideEachOtherWithoutShelfOverflow()
    {
        var views=new[]{
            new ViewDefinition("Reinforcement/Neck/Plan",ViewPurpose.Plan,new(0,0),new Bounds2Mm(-355,-3504,1255,302),30),
            new ViewDefinition("Reinforcement/Neck/AA",ViewPurpose.Section,new(0,0),new Bounds2Mm(-780,296,830,2275),30),
            new ViewDefinition("Reinforcement/Neck/BB",ViewPurpose.Section,new(0,0),new Bounds2Mm(-355,296,1255,2275),30),
            new ViewDefinition("SteelSchedule",ViewPurpose.SteelTable,new(0,0),new Bounds2Mm(0,0,7500,7200),30)};
        var package=new DrawingPackage(new string('a',64),"test",views,[],new(-780,-3504,7500,7200),CalculationScope.FullRebar);
        var plan=SheetPlan.Create(VhgSheetFamily.Visit,[],new Dictionary<SheetContent,IReadOnlyList<string>>{
            [SheetContent.Tables]=views.Select(v=>v.Id).ToArray()});
        var profile=new PresentationProfile(420,297,10,10,10,10,0,5,[30],30,[2],"test");
        var result=SheetLayoutPlanner.Arrange(package,profile,plan,true);
        Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code)));
        var frames=result.Value!.Sheets.Single(s=>s.PageIndex==4).Frames;
        Assert.Equal(4,frames.Length);
        foreach(var frame in frames)
        {
            Assert.Equal(views.Single(v=>v.Id==frame.ViewId).Bounds,frame.LogicalBounds);
            Assert.Equal(30,frame.Transform.UniformScaleDenominator);
            Assert.InRange(frame.PaperBounds.MinX,10,410);Assert.InRange(frame.PaperBounds.MaxX,10,410);
            Assert.InRange(frame.PaperBounds.MinY,10,287);Assert.InRange(frame.PaperBounds.MaxY,10,287);
        }
        for(int i=0;i<frames.Length;i++)for(int j=i+1;j<frames.Length;j++)
        {
            var a=frames[i].PaperBounds;var b=frames[j].PaperBounds;
            Assert.True(a.MaxX+5<=b.MinX+1e-9 || b.MaxX+5<=a.MinX+1e-9 || a.MaxY+5<=b.MinY+1e-9 || b.MaxY+5<=a.MinY+1e-9);
        }
        var again=SheetLayoutPlanner.Arrange(package,profile,plan,true).Value!;
        Assert.Equal(frames.Select(f=>(f.ViewId,f.LogicalBounds,f.PaperBounds,f.Transform)).ToArray(),
            again.Sheets.Single(s=>s.PageIndex==4).Frames.Select(f=>(f.ViewId,f.LogicalBounds,f.PaperBounds,f.Transform)).ToArray());
    }

    [Theory]
    [InlineData(20,0)]
    [InlineData(30,50000)]
    [InlineData(40,-30000)]
    public void PhysicalNeckPageNormalizesWorldCoordinatesAtRequestedScale(double scale,double offset)
    {
        var bounds=new Bounds2Mm(offset-600,offset+1400,offset+950,offset+2200);
        var view=new ViewDefinition(SheetFourViews.AAViewId,ViewPurpose.Section,new(0,0),bounds,scale);
        var package=new DrawingPackage(new string('a',64),"test",[view],[],bounds,CalculationScope.FullRebar);
        var plan=SheetPlan.Create(VhgSheetFamily.VisitAndCatch,[],new Dictionary<SheetContent,IReadOnlyList<string>> {
            [SheetContent.NeckAndCatch]=[view.Id] });
        var profile=new PresentationProfile(420,297,10,10,10,10,0,5,[scale],scale,[2],"test");
        var result=SheetLayoutPlanner.Arrange(package,profile,plan,true);
        Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code)));
        var frame=Assert.Single(result.Value!.Sheets[3].Frames);
        Assert.Equal(scale,frame.Transform.UniformScaleDenominator);
        Assert.Equal(new Point2Mm(frame.PaperBounds.MinX,frame.PaperBounds.MinY),frame.Transform.Apply(new(bounds.MinX,bounds.MinY),bounds));
        Assert.InRange(frame.PaperBounds.MinX,10,410);Assert.InRange(frame.PaperBounds.MaxX,10,410);
        Assert.InRange(frame.PaperBounds.MinY,10,287);Assert.InRange(frame.PaperBounds.MaxY,10,287);
    }

    [Theory]
    [InlineData(VhgSheetFamily.Visit,5,4,5)]
    [InlineData(VhgSheetFamily.VisitAndCatch,6,5,6)]
    public void PrimarySequenceKeepsTablesAndBendingLast(VhgSheetFamily family,int count,int table,int bending)
    {
        var plan=SheetPlan.Create(family,[]);
        Assert.Equal(count,plan.Pages.Length);
        Assert.Equal(SheetContent.Tables,plan.Pages[table-1].Content);
        Assert.Equal(SheetContent.Bending,plan.Pages[bending-1].Content);
        if(count==6) Assert.Equal(SheetContent.NeckAndCatch,plan.Pages[3].Content);
    }
    [Fact]
    public void ExplicitLayoutRetainsFivePagesAndBlocksMissingAssignments()
    {
        var view=new ViewDefinition("V",ViewPurpose.Plan,new(0,0),new Bounds2Mm(0,0,1000,1000),30);
        var package=new DrawingPackage(new string('a',64),"test",[view],[],new Bounds2Mm(0,0,1000,1000),DBTool.Domain.CalculationScope.FullRebar);
        var profile=new PresentationProfile(420,297,10,10,10,10,0,5,[30],30,[2],"test");
        var missing=SheetLayoutPlanner.Arrange(package,profile,SheetPlan.Create(VhgSheetFamily.Visit,[]));
        Assert.False(missing.Success);
        Assert.Contains(missing.Diagnostics,d=>d.Code=="SHEET_PLAN_VIEW_COVERAGE");
        var plan=SheetPlan.Create(VhgSheetFamily.Visit,[],new Dictionary<SheetContent,IReadOnlyList<string>>{[SheetContent.Structure]=["V"]});
        var result=SheetLayoutPlanner.Arrange(package,profile,plan);
        Assert.True(result.Success);
        Assert.Equal(5,result.Value!.Sheets.Length);
        Assert.Equal(5,result.Value.Sheets[0].PageCount);
        Assert.Single(result.Value.Sheets.SelectMany(s=>s.Frames));
    }
    [Fact]
    public void VisitAndCatchLayoutRetainsSixPagesWithTablesOnFiveAndBendingOnSix()
    {
        var views = new[] {
            new ViewDefinition("Catch",ViewPurpose.Plan,new(0,0),new Bounds2Mm(0,0,1000,1000),30),
            new ViewDefinition("Table",ViewPurpose.SteelTable,new(0,0),new Bounds2Mm(0,0,1000,1000),30),
            new ViewDefinition("Bending",ViewPurpose.Schematic,new(0,0),new Bounds2Mm(0,0,1000,1000),30)
        };
        var package = new DrawingPackage(new string('a',64),"test",views,[],new Bounds2Mm(0,0,1000,1000),CalculationScope.FullRebar);
        var plan = SheetPlan.Create(VhgSheetFamily.VisitAndCatch,[],new Dictionary<SheetContent,IReadOnlyList<string>> {
            [SheetContent.NeckAndCatch]=["Catch"], [SheetContent.Tables]=["Table"], [SheetContent.Bending]=["Bending"]
        });
        var result = SheetLayoutPlanner.Arrange(package,new PresentationProfile(420,297,10,10,10,10,0,5,[30],30,[2],"test"),plan);
        Assert.True(result.Success);
        Assert.Equal(6,result.Value!.Sheets.Length);
        Assert.All(result.Value.Sheets,s=>Assert.Equal(6,s.PageCount));
        Assert.Equal("Catch",Assert.Single(result.Value.Sheets[3].Frames).ViewId);
        Assert.Equal("Table",Assert.Single(result.Value.Sheets[4].Frames).ViewId);
        Assert.Equal("Bending",Assert.Single(result.Value.Sheets[5].Frames).ViewId);
    }
    [Fact]
    public void AuxiliaryPagesRetainManifestOrderAndIdentity()
    {
        var plan=SheetPlan.Create(VhgSheetFamily.Visit,[new("aux-9","Golden/Model-9",["Panel"]),new("aux-7","Golden/Model-7",["Cover"])]);
        Assert.Equal(7,plan.Pages.Length);
        Assert.Equal("aux-9",plan.Pages[5].Id);
        Assert.Equal("Golden/Model-7",plan.Pages[6].ManifestId);
        Assert.Equal("Cover",Assert.Single(plan.Pages[6].ViewIds));
    }
    [Theory]
    [InlineData(VhgSheetFamily.Visit,8)]
    [InlineData(VhgSheetFamily.VisitAndCatch,9)]
    public void LargePlanAddsOnlyStructureSteel(VhgSheetFamily family,int count)
    {
        var plan=SheetPlan.Create(family,[],isLarge:true);
        Assert.Equal(count,plan.Pages.Length);
        Assert.Equal(SheetContent.StructureSteel,plan.Pages[1].Content);
        Assert.Equal(SheetContent.BaseSlab,plan.Pages[2].Content);
        Assert.Equal(SheetContent.Walls12,plan.Pages[3].Content);
        Assert.Equal(SheetContent.Walls34,plan.Pages[4].Content);
        Assert.Equal(SheetContent.Roof,plan.Pages[5].Content);
        Assert.Equal(SheetContent.Tables,plan.Pages[^2].Content);
        Assert.Equal(SheetContent.Bending,plan.Pages[^1].Content);
    }
    [Fact]
    public void AuxiliaryBendingViewsRemainManifestOnly()
    {
        var v=new ViewDefinition("Bending/D001",ViewPurpose.Schematic,new(0,0),new Bounds2Mm(0,0,1000,1000),30);
        var package=new DrawingPackage(new string('a',64),"test",[v],[],v.Bounds,CalculationScope.FullRebar);
        var plan=SheetPlan.Create(VhgSheetFamily.Visit,[new("aux","detail",[v.Id])]);
        var result=SheetLayoutPlanner.Arrange(package,new PresentationProfile(420,297,10,10,10,10,0,5,[30],30,[2],"test"),plan,true);
        Assert.True(result.Success);
        Assert.Equal(5,result.Value!.Sheets.Length);
        Assert.Empty(result.Value.Sheets.SelectMany(s=>s.Frames));
    }
    [Theory]
    [InlineData(2200,3500,false)]
    [InlineData(2201,1800,true)]
    public void ThresholdUsesBaseWidthOnly(double width,double length,bool expected)
    {
        var view=new ViewDefinition("Plan",ViewPurpose.Plan,new(0,0),new Bounds2Mm(0,0,width,length),30);
        var outline=new PolylinePrimitive("Base/Outline","Geometry/Outline","Plan","Base",null,null,[new(0,0),new(width,0),new(width,length),new(0,length)],true);
        var package=new DrawingPackage(new string('a',64),"test",[view],[outline],view.Bounds,CalculationScope.FullRebar);
        Assert.Equal(expected,SheetLayoutPlanner.RequiresAdditionalSheets(package));
    }
    [Fact]
    public void OrdinaryStructureRetainsSixNamedViewsOnOneThreeColumnSheet()
    {
        var names=new[]{"Plan","AA","BB","WallMeshPlan","SteelAA","SteelBB"};
        var views=names.Select(id=>new ViewDefinition(id,ViewPurpose.Plan,new(0,0),new Bounds2Mm(-1692.5,-500,1692.5,3285),30)).ToArray();
        var package=new DrawingPackage(new string('a',64),"test",views,[],new Bounds2Mm(-1692.5,-500,1692.5,3285),CalculationScope.FullRebar);
        var result=SheetOnePlacement.Arrange(package,new PresentationProfile(420,297,10,10,10,10,0,5,[30],30,[2],"test"),5);
        Assert.True(result.Success);
        Assert.Equal(6,result.Value!.Frames.Length);
        Assert.All(result.Value.Frames,f=> {Assert.Equal(30,f.Transform.UniformScaleDenominator);Assert.InRange(f.PaperBounds.MinX,10,410);Assert.InRange(f.PaperBounds.MaxX,10,410);Assert.InRange(f.PaperBounds.MinY,10,287);Assert.InRange(f.PaperBounds.MaxY,10,287);});
    }
    [Fact]
    public void LargeStructureUsesTwoNamedSheetsAndNeverPrintsAuxiliaryBending()
    {
        var names=new[]{"Plan","AA","BB","WallMeshPlan","SteelAA","SteelBB","AngleSchedule","AccessoryDetail/Collar","Bending/D001"};
        var views=names.Select(id=>new ViewDefinition(id,ViewPurpose.Plan,new(0,0),new Bounds2Mm(-500,-500,500,500),30)).ToArray();
        var package=new DrawingPackage(new string('a',64),"test",views,[],views[0].Bounds,CalculationScope.FullRebar);
        var plan=SheetPlan.Create(VhgSheetFamily.Visit,[new("aux","source-only",["Bending/D001"])],new Dictionary<SheetContent,IReadOnlyList<string>> {
            [SheetContent.Structure]=["Plan","AA","BB","WallMeshPlan"],
            [SheetContent.StructureSteel]=["SteelAA","SteelBB","AngleSchedule","AccessoryDetail/Collar"]},true);
        var result=SheetLayoutPlanner.Arrange(package,new PresentationProfile(420,297,10,10,10,10,0,5,[30],30,[2],"test"),plan,true);
        Assert.True(result.Success);
        Assert.Equal(8,result.Value!.Sheets.Length);
        Assert.Equal(4,result.Value.Sheets[0].Frames.Length);
        Assert.Equal(4,result.Value.Sheets[1].Frames.Length);
        Assert.DoesNotContain(result.Value.Sheets.SelectMany(s=>s.Frames),f=>f.ViewId=="Bending/D001");
        Assert.All(result.Value.Sheets.SelectMany(s=>s.Frames),f=>Assert.Equal(30,f.Transform.UniformScaleDenominator));
    }
    [Fact]
    public void LargeColumnBlocksWhenNativeInkExceedsItsHeight()
    {
        var v=new ViewDefinition("Plan",ViewPurpose.Plan,new(0,0),new Bounds2Mm(0,0,3263,4800),30);
        var package=new DrawingPackage(new string('a',64),"test",[v],[],v.Bounds,CalculationScope.FullRebar);
        var result=SheetOnePlacement.ArrangeSheet1A(package,new PresentationProfile(420,297,10,10,10,10,0,5,[30],30,[2],"test"),8);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="SHEET_PLAN_PAGE_OVERFLOW");
    }
}
