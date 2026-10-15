using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DBTool.Domain;
using DBTool.Domain.Geometry;
namespace DBTool.Application;

internal static class ValidationInputHash
{
    internal static string Compute(IEnumerable<ValidatedManholeSpec> specs,CalculationPolicy policy,CalculationScope scope)
    {
        // Explicit projection excludes source paths/hashes/raw spelling and all presentation metadata.
        var values=specs.OrderBy(s=>s.Input.Id,StringComparer.Ordinal).Select(s=>
        {
            var i=s.Input;
            return new {
                i.Id,i.Profile,i.L,i.B,i.H,i.BaseThicknessMm,i.WallThicknessMm,i.RoofThicknessMm,i.PadThicknessMm,
                i.BaseExpansionMm,i.PadExpansionMm,i.BeddingMortarMm,i.BaseHaunch,i.TopHaunch,i.NeckHeight,i.RoofOpening,
                Structure = i.BaseHaunchSpec is null && i.TopHaunchSpec is null && i.NeckSpec is null && i.NeckSeatSpec is null && i.CollarSpec is null && i.CoverPanelSpec is null && i.PurchasedCoverSpec is null && i.CatchBasinSpec is null ? null : new {
                    BaseHaunch=i.BaseHaunchSpec is null?null:new {i.BaseHaunchSpec.WidthMm,i.BaseHaunchSpec.HeightMm,i.BaseHaunchSpec.Owner,i.BaseHaunchSpec.Rebar},
                    TopHaunch=i.TopHaunchSpec is null?null:new {i.TopHaunchSpec.WidthMm,i.TopHaunchSpec.HeightMm,i.TopHaunchSpec.Owner,i.TopHaunchSpec.Rebar},
                    Neck=i.NeckSpec is null?null:new {i.NeckSpec.InnerWidthMm,i.NeckSpec.InnerLengthMm,i.NeckSpec.ThicknessMm,i.NeckSpec.HeightMm,i.NeckSpec.Position},
                    Seat=i.NeckSeatSpec is null?null:new {i.NeckSeatSpec.Kind,i.NeckSeatSpec.ExpansionMm,i.NeckSeatSpec.HeightMm,i.NeckSeatSpec.SeatElevationMm},
                    Collar=i.CollarSpec is null?null:new {OuterProfile=ProfileProjection(i.CollarSpec.OuterProfile),OpeningProfile=ProfileProjection(i.CollarSpec.OpeningProfile),i.CollarSpec.ZRange,i.CollarSpec.MaterialId},
                    Panel=i.CoverPanelSpec is null?null:new {i.CoverPanelSpec.Count,i.CoverPanelSpec.GapMm,i.CoverPanelSpec.SplitAxis,i.CoverPanelSpec.SupportMode,i.CoverPanelSpec.CapHeightMm},
                    Purchased=i.PurchasedCoverSpec is null?null:new {i.PurchasedCoverSpec.CatalogId,i.PurchasedCoverSpec.Anchor,i.PurchasedCoverSpec.PhysicalSize,i.PurchasedCoverSpec.RotationDegrees},
                    Catch=i.CatchBasinSpec is null?null:new {i.CatchBasinSpec.Owner,i.CatchBasinSpec.Wall,i.CatchBasinSpec.Position,
                        i.CatchBasinSpec.OpeningWidthMm,i.CatchBasinSpec.OpeningHeightMm,
                        i.CatchBasinSpec.ChamberClearWidthMm,i.CatchBasinSpec.ChamberClearDepthMm,
                        i.CatchBasinSpec.ChamberClearHeightMm,i.CatchBasinSpec.WallThicknessMm,
                        i.CatchBasinSpec.BaseThicknessMm,i.CatchBasinSpec.OffsetMm}
                },
                i.BaseBottomMesh,i.BaseTopMesh,i.CoverMm,i.ElevationMode,i.BaseElevationM,i.PipeInvertReference,
                i.HookBaseBottom,i.HookBaseTop,i.DistributionMode,i.MeshJunctionPolicy,i.LayerStackPolicy,
                MemberRebar=i.MemberRebarSpec is null ? null : new {
                    i.MemberRebarSpec.ComponentId,i.MemberRebarSpec.CoverMm,
                    Layers=i.MemberRebarSpec.Layers.OrderBy(x=>x.Id,StringComparer.Ordinal).Select(x=>new {
                        x.Id,x.Layer,x.Axis,x.Direction,x.DiameterMm,x.SpacingMm,x.FrameId,x.Surface })
                },
                MainRebar=policy.PolicyId!="S2Rebar-1" || i.MainRebar is null ? null : new {
                    FabricationFallbackRule=NominalFabricationLength.RuleId,
                    i.MainRebar.WallSupportCoverMm,
                    OpeningCompositionRule=VhgOpeningCompositionPlanner.RuleId,
                    AngleFrame=i.MainRebar.AngleFrame is null?null:new {i.MainRebar.AngleFrame.CatalogCode,i.MainRebar.AngleFrame.Positions.Positions},
                    Accessories=i.MainRebar.Accessories is null?null:new {
                        Rule=AccessoryRebarInput.RuleVersion,
                        Anchor=i.MainRebar.Accessories.Anchor,
                        Dowel=i.MainRebar.Accessories.Dowel is null?null:new {i.MainRebar.Accessories.Dowel.StepMm,i.MainRebar.Accessories.Dowel.DiameterMm,i.MainRebar.Accessories.Dowel.LengthMm,i.MainRebar.Accessories.DowelHoleDiameterMm},
                        Lifting=i.MainRebar.Accessories.Lifting is null?null:new {i.MainRebar.Accessories.Lifting.Count,i.MainRebar.Accessories.Lifting.Diameter,i.MainRebar.Accessories.Lifting.Width,i.MainRebar.Accessories.Lifting.Height,i.MainRebar.Accessories.Lifting.Foot},
                        Ties=i.MainRebar.Accessories.Ties is null?null:new {i.MainRebar.Accessories.Ties.Mesh,i.MainRebar.Accessories.Ties.Staggered,i.MainRebar.Accessories.Ties.Shapes,i.MainRebar.Accessories.Ties.HookC,i.MainRebar.Accessories.Ties.HookZU,i.MainRebar.Accessories.Ties.MiddleU},
                        Pipe=OpeningAccessoryProjection(i.MainRebar.Accessories.Pipe),Roof=OpeningAccessoryProjection(i.MainRebar.Accessories.Roof)
                    },
                    WallOuter=FaceProjection(i.MainRebar.WallOuter),WallInner=FaceProjection(i.MainRebar.WallInner),
                    i.MainRebar.RoofTop,i.MainRebar.RoofBottom,i.MainRebar.RoofCoverMm,
                    NeckOuter=FaceProjection(i.MainRebar.NeckOuter),NeckInner=FaceProjection(i.MainRebar.NeckInner),i.MainRebar.NeckStructure,i.MainRebar.Hooks,i.MainRebar.HookMode,i.MainRebar.HookMultiple,
                    Ladder=i.MainRebar.Ladder is null?null:new {i.MainRebar.Ladder.TopOffsetMm,i.MainRebar.Ladder.PitchMm,i.MainRebar.Ladder.MinZMm,i.MainRebar.Ladder.WidthMm,i.MainRebar.Ladder.LegHeightMm,i.MainRebar.Ladder.HookMm,i.MainRebar.Ladder.DiameterMm,i.MainRebar.Ladder.OpeningBehavior}
                },
                OpeningRebar=policy.PolicyId=="S2Rebar-1" ? i.OpeningReinforcementSpecs.Select(x=>new {
                    x.Shape,x.CountPerSideK,x.LayerSet,x.DiameterMm,x.ExtensionMm,x.LapDiameterMultiple,
                    x.ParallelSpacingMm,x.EndAnchoragePolicy,x.InnerRadiusMm,x.CoverMm
                }) : null,
                Features=policy.PolicyId=="S2Rebar-1" ? i.FeatureRequests.OrderBy(x=>x.FeatureId,StringComparer.Ordinal).Select(x=>new {
                    x.FeatureId,x.Requested,Parameters=x.Parameters.Where(p=>i.MainRebar is null || x.FeatureId is not ("WallRebar" or "RoofRebar" or "NeckRebar" or "Ladder")).OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new {
                        Key=p.Key,Value=new {p.Value.Kind,p.Value.Text,p.Value.Number,p.Value.Boolean,p.Value.Formula,p.Value.CachedText}
                    })
                }) : null,
                Pipes=i.PipeConnections.OrderBy(p=>p.WallId).ThenBy(p=>p.CatalogCode,StringComparer.Ordinal)
                    .Select(p=>policy.PolicyId is "S2Geometry-1" or "S2Rebar-1"
                        ? (object)new{p.Id,p.WallId,p.CatalogCode,p.InvertMm,p.OffsetMm,p.MortarMm}
                        : new{p.WallId,p.CatalogCode,p.InvertMm,p.OffsetMm,p.MortarMm}),
                Catalogs=i.PipeConnections.Select(p=>p.CatalogCode).Distinct().OrderBy(x=>x,StringComparer.Ordinal).Select(code=>s.Catalogs.Pipes[code]),
                Angles=policy.PolicyId=="S2Rebar-1" ? s.Catalogs.Angles.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Value) : null,
                CoverCatalog=i.PurchasedCoverSpec is null?null:CoverProjection(s.Catalogs.Covers.GetValueOrDefault(i.PurchasedCoverSpec.CatalogId))
                // Optional feature payloads are not consumed by BaseRebarOnly. Retain them in the snapshot for
                // capability validation, but never hash raw cell spelling or use a cache hit to bypass T9.
            };
        });
        var projection=new {ContractVersion=policy.PolicyId=="S2Geometry-1"?"contract-v2-geometry":policy.PolicyId=="S2Rebar-1"?"contract-v3-rebar":"contract-v1.1",EngineVersion=policy.PolicyId=="S2Geometry-1"?"validation-s2-v1":policy.PolicyId=="S2Rebar-1"?"validation-s3-v1":"validation-v1",Scope=scope,
            Policy=new{policy.PolicyId,policy.Version,policy.PipeInvertReference,policy.DistributionMode,policy.MeshJunctionPolicy,policy.HookPolicy,policy.BendPolicy,policy.LayerStackPolicy,policy.MassPolicy,policy.TolerancePolicy,Rules=policy.Rules.Select(r=>new{r.RuleId,r.RuleVersion})},Manholes=values};
        var options=new JsonSerializerOptions(); options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        using var document=JsonDocument.Parse(JsonSerializer.Serialize(projection,options));
        using var stream=new MemoryStream();
        using(var writer=new Utf8JsonWriter(stream)) Write(document.RootElement,writer);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    private static object? OpeningAccessoryProjection(VhgOpeningInput? i)=>i is null?null:new {i.Diameter,i.Count,i.Extension,i.LapMultiple,i.Cover,i.Composition};
    private static object? FaceProjection(WallOrNeckRebarSpec? face)=>face is null?null:new {face.VerticalDiameterMm,face.VerticalSpacingMm,face.HorizontalDiameterStepDown,face.HorizontalSpacingDeltaMm,face.Structure,face.CoverMm,face.InnerLayer,face.OuterLayer};
    private static object? CoverProjection(CoverCatalogEntry? entry)=>entry is null ? null : new
    {
        entry.Code,entry.PhysicalSize,entry.InsertionOffset,entry.RotationDegrees,entry.PlacementDatum,entry.LocalBounds,
        SectionOutline=entry.BlockSet?.SectionOutlineRule is not { } outline?null:new {outline.SideExtensionMm,outline.DepthMm},
        NominalOpening=entry.BlockSet?.NominalOpening?.Size,
        NativeAlignment=entry.BlockSet is null?null:entry.BlockSet.Views.OrderBy(v=>v.View,StringComparer.Ordinal)
            .Select(v=>new {v.Name,v.View,v.AlignmentPoint}),
        NativeContact=entry.BlockSet is null || entry.BlockSet.ContactSections.IsEmpty?null:
            entry.BlockSet.ContactSections.OrderBy(s=>s.View,StringComparer.Ordinal).Select(s=>new {
                s.View,Edges=s.Segments.OrderBy(e=>e.Start.X).ThenBy(e=>e.Start.Y)
                    .ThenBy(e=>e.End.X).ThenBy(e=>e.End.Y).Select(e=>new {e.Start,e.End})}),
        Assembly = entry.OpeningContract is null ? null : new {
            Profile = ProfileProjection(entry.OpeningContract.ClearProfile), entry.OpeningContract.MinimumBearingMm }
    };
    private static object ProfileProjection(Profile2Mm profile) => profile switch
    {
        RectangleProfile r => new {Kind="Rectangle",r.Rectangle},
        RoundedRectangleProfile r => new {Kind="RoundedRectangle",r.Center,r.Size,r.CornerRadiusMm,r.RotationDegrees},
        CircleProfile c => new {Kind="Circle",c.Center,c.RadiusMm},
        PolygonProfile p => new {Kind="Polygon",p.Polygon.Vertices},
        OpenTopProfile o => new {Kind="OpenTop",o.Polygon.Vertices},
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };
    private static void Write(JsonElement node,Utf8JsonWriter writer)
    {
        switch(node.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach(var p in node.EnumerateObject().OrderBy(p=>p.Name,StringComparer.Ordinal)) { if((p.Name=="MainRebar" || p.Name=="Structure" || p.Name=="CoverCatalog" || p.Name=="Catch") && p.Value.ValueKind==JsonValueKind.Null) continue; writer.WritePropertyName(p.Name);Write(p.Value,writer); }
                writer.WriteEndObject();break;
            case JsonValueKind.Array:
                writer.WriteStartArray();foreach(var item in node.EnumerateArray()) Write(item,writer);writer.WriteEndArray();break;
            case JsonValueKind.String: writer.WriteStringValue(node.GetString()!.Normalize(NormalizationForm.FormC));break;
            case JsonValueKind.Number: double value=node.GetDouble();writer.WriteNumberValue(value==0?0:value);break;
            default: node.WriteTo(writer);break;
        }
    }
}
