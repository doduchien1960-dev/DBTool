using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public sealed record FormworkFaceTrace
{
    public FormworkFaceTrace(string faceId, string ownerId, bool included, double areaM2,
        string reason, string policyId)
    {
        FaceId = ContractGuard.Text(faceId, nameof(faceId));
        OwnerId = ContractGuard.Text(ownerId, nameof(ownerId));
        Included = included;
        AreaM2 = ContractGuard.NonNegative(areaM2, nameof(areaM2));
        Reason = ContractGuard.Text(reason, nameof(reason));
        PolicyId = ContractGuard.Text(policyId, nameof(policyId));
    }
    public string FaceId { get; }
    public string OwnerId { get; }
    public bool Included { get; }
    public double AreaM2 { get; }
    public string Reason { get; }
    public string PolicyId { get; }
}

public sealed record FormworkResult
{
    public FormworkResult(IEnumerable<QuantityLine> lines, IEnumerable<FormworkFaceTrace> faceTrace)
    {
        Lines = ContractGuard.Copy(lines);
        FaceTrace = ContractGuard.Copy(faceTrace);
    }
    public ImmutableArray<QuantityLine> Lines { get; }
    public ImmutableArray<FormworkFaceTrace> FaceTrace { get; }
}

public sealed class FormworkCalculator
{
    public OperationResult<FormworkResult> Calculate(GeometryModel geometry, FormworkPolicy policy,
        TolerancePolicy tolerance)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(tolerance);
        var diagnostics = new List<Diagnostic>();
        var trace = new List<FormworkFaceTrace>();
        var states = new Dictionary<string, State>(StringComparer.Ordinal);

        foreach (var component in geometry.Components.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            var state = states.TryGetValue(component.Id, out var existing)
                ? existing : states[component.Id] = new State(component.Id);
            var selectedFaces = new HashSet<string>(StringComparer.Ordinal);
            foreach (var face in component.FacePatches)
            {
                var decision = Decide(component, face.Role, policy);
                double area = face.AreaMm2.Value / 1_000_000d;
                if (decision.Include)
                {
                    double factor = component.Kind == ComponentKind.Roof && face.Role == FaceRole.Bottom &&
                        policy.RoofCastMode == RoofCastMode.InSitu ? 0.5 : 1;
                    Include(state, trace, face.Id, component.Id, area * factor, decision.Reason, policy.PolicyId);
                    selectedFaces.Add(face.Role.ToString());
                }
                else
                {
                    Exclude(trace, face.Id, component.Id, area, decision.Reason, policy.PolicyId);
                    if (decision.NeedsRule)
                        diagnostics.Add(Review("FORMWORK_NEEDS_RULE", component.Id, face.Id, decision.Reason));
                }
            }

            if (component.Kind is ComponentKind.Pad or ComponentKind.Base)
            {
                double sideArea = Perimeter(component.Footprint.Outer) * component.ZRange.Length / 1_000_000d;
                Include(state, trace, component.Id + "/Sides", component.Id, sideArea, "Perimeter side formwork", policy.PolicyId);
                double bottom = FootprintArea(component.Footprint) / 1_000_000d;
                bool includeBottom = component.Kind == ComponentKind.Base && policy.BaseBottomRequired && !policy.CastAgainstGround;
                if (includeBottom)
                    Include(state, trace, component.Id + "/Bottom", component.Id, bottom, "Base bottom required", policy.PolicyId);
                else
                    Exclude(trace, component.Id + "/Bottom", component.Id, bottom,
                        policy.CastAgainstGround ? "CastAgainstGround" : "BaseBottomNotRequired", policy.PolicyId);
            }
            if (component.Kind == ComponentKind.Roof)
            {
                double bottom = FootprintArea(component.Footprint) / 1_000_000d;
                double soffit = policy.RoofCastMode == RoofCastMode.InSitu
                    ? Math.Round(bottom * 0.5, 2, MidpointRounding.AwayFromZero) : bottom;
                Include(state, trace, component.Id + "/Soffit", component.Id, soffit,
                    policy.RoofCastMode == RoofCastMode.InSitu ? "InSitu soffit" : "PrecastBed bottom", policy.PolicyId);
                Include(state, trace, component.Id + "/OuterEdge", component.Id,
                    Perimeter(component.Footprint.Outer) * component.ZRange.Length / 1_000_000d,
                    "Roof outer edge", policy.PolicyId);
                foreach (var hole in component.Footprint.InnerLoops)
                {
                    double reveal = Perimeter(hole) * component.ZRange.Length / 1_000_000d;
                    if (policy.OpeningRevealRequired == true)
                        Include(state, trace, component.Id + "/Reveal/" + trace.Count, component.Id, reveal,
                            "Closed opening reveal", policy.PolicyId);
                    else
                    {
                        string reason = policy.OpeningRevealRequired is null ? "NeedsRule" : "OpeningRevealExcluded";
                        Exclude(trace, component.Id + "/Reveal/" + trace.Count, component.Id, reveal, reason, policy.PolicyId);
                        if (policy.OpeningRevealRequired is null) diagnostics.Add(Review("FORMWORK_NEEDS_RULE", component.Id, component.Id, reason));
                    }
                }
            }
            if (component.Kind == ComponentKind.Panel && policy.PanelBedRequired == true)
            {
                Include(state, trace, component.Id + "/Bed", component.Id,
                    FootprintArea(component.Footprint) / 1_000_000d, "Panel bed required", policy.PolicyId);
            }

            var openings = component.Voids.GroupBy(x => OpeningSignature(x), StringComparer.Ordinal).Select(x => x.First()).ToArray();
            if (openings.Length > 0 && component.Kind == ComponentKind.Wall && (policy.WallInnerRequired == true || policy.WallOuterRequired == true))
            {
                double openingFace = OpeningFaceUnion(component, openings, tolerance);
                if (openingFace > 0)
                {
                    if (policy.WallInnerRequired == true)
                        Deduct(state, trace, component.Id + "/InnerOpening", component.Id, openingFace, "Opening cut", policy.PolicyId);
                    if (policy.WallOuterRequired == true)
                        Deduct(state, trace, component.Id + "/OuterOpening", component.Id, openingFace, "Opening cut", policy.PolicyId);
                }
            }
            if (openings.Length > 0 && policy.OpeningRevealRequired == true && component.Kind != ComponentKind.Roof)
            {
                foreach (var opening in openings)
                {
                    double reveal = OpeningReveal(opening, component);
                    if (reveal <= 0) continue;
                    Include(state, trace, opening.Id + "/Reveal", component.Id, reveal,
                        opening.Kind == OpeningKind.OpenTop ? "Open-top three-edge reveal" : "Closed opening reveal", policy.PolicyId);
                }
            }
            else if (openings.Length > 0 && policy.OpeningRevealRequired is null)
                diagnostics.Add(Review("FORMWORK_NEEDS_RULE", component.Id, component.Id, "OpeningRevealRequired"));
        }

        var lines = new List<QuantityLine>();
        foreach (var state in states.Values.OrderBy(x => x.ComponentId, StringComparer.Ordinal))
        {
            double net = state.GrossM2 - state.DeductionsM2;
            if (!double.IsFinite(net) || net < 0)
            {
                diagnostics.Add(new Diagnostic("FORMWORK_NEGATIVE_NET", DiagnosticSeverity.Block, DiagnosticScope.Global,
                    null, null, geometry.Components.FirstOrDefault(x => x.Id == state.ComponentId)?.Sources.FirstOrDefault(),
                    ">= 0", state.ComponentId + ": " + net, "Formwork net is negative."));
                continue;
            }
            lines.Add(new QuantityLine("Formwork/" + state.ComponentId, state.ComponentId, "Formwork",
                state.GrossM2, 0, state.DeductionsM2, net, QuantityUnit.M2, policy.PolicyId, [],
                trace.Where(x => x.OwnerId == state.ComponentId).Select(x => x.FaceId)));
        }
        if (diagnostics.Any(x => x.Severity == DiagnosticSeverity.Block)) return new(false, null, diagnostics);
        return new(true, new FormworkResult(lines, trace), diagnostics);
    }

    private static Decision Decide(ComponentGeometry component, FaceRole role, FormworkPolicy policy) =>
        role switch
        {
            FaceRole.Contact => policy.ContactExcluded ? Decision.Exclude("ContactExcluded") : Decision.IncludeAs("Contact formwork required"),
            FaceRole.Inner when component.Kind == ComponentKind.Wall => Optional(policy.WallInnerRequired, "WallInnerRequired"),
            FaceRole.Outer when component.Kind == ComponentKind.Wall => Optional(policy.WallOuterRequired, "WallOuterRequired"),
            FaceRole.Bottom when component.Kind == ComponentKind.Base => policy.BaseBottomRequired && !policy.CastAgainstGround
                ? Decision.IncludeAs("Base bottom required") : Decision.Exclude(policy.CastAgainstGround ? "CastAgainstGround" : "BaseBottomNotRequired"),
            FaceRole.Bottom when component.Kind == ComponentKind.Panel => Optional(policy.PanelBedRequired, "PanelBedRequired"),
            FaceRole.Bottom when component.Kind == ComponentKind.Roof => Decision.IncludeAs(policy.RoofCastMode.ToString()),
            FaceRole.OpeningReveal => Optional(policy.OpeningRevealRequired, "OpeningRevealRequired"),
            FaceRole.Top => Decision.Exclude("TopNotIncludedByDefault"),
            _ => Decision.Exclude("FaceNotSelected")
        };

    private static Decision Optional(bool? value, string name) => value switch
    {
        true => Decision.IncludeAs(name), false => Decision.Exclude(name + "=false"), _ => Decision.Needs(name)
    };

    private static void Include(State state, ICollection<FormworkFaceTrace> trace, string faceId, string ownerId,
        double area, string reason, string policyId)
    {
        if (area <= 0) return;
        state.GrossM2 += area;
        trace.Add(new FormworkFaceTrace(faceId, ownerId, true, area, reason, policyId));
    }
    private static void Deduct(State state, ICollection<FormworkFaceTrace> trace, string faceId, string ownerId,
        double area, string reason, string policyId)
    {
        if (area <= 0) return;
        state.DeductionsM2 += area;
        trace.Add(new FormworkFaceTrace(faceId, ownerId, false, area, reason, policyId));
    }
    private static void Exclude(ICollection<FormworkFaceTrace> trace, string faceId, string ownerId,
        double area, string reason, string policyId) => trace.Add(new FormworkFaceTrace(faceId, ownerId, false, area, reason, policyId));

    private static double OpeningFaceUnion(ComponentGeometry component, IReadOnlyList<OpeningProfile> openings, TolerancePolicy tolerance)
    {
        double u0 = component.Footprint.Outer.Vertices.Min(x => x.X), u1 = component.Footprint.Outer.Vertices.Max(x => x.X);
        double z0 = component.ZRange.Start, z1 = component.ZRange.End;
        if (openings.Any(x => x.WallFrame is not null))
        {
            var frame = openings.First(x => x.WallFrame is not null).WallFrame!;
            var local = component.Footprint.Outer.Vertices.Select(p => frame.ToLocal(new Point3Mm(p.X, p.Y, 0))).ToArray();
            u0 = local.Min(x => x.U); u1 = local.Max(x => x.U);
        }
        var profiles = openings.Select(x => x.ConcreteCutout).ToArray();
        return AreaUnion(profiles, u0, u1, z0, z1, tolerance) / 1_000_000d;
    }

    private static double OpeningReveal(OpeningProfile opening, ComponentGeometry component)
    {
        double depth = opening.ExtrusionRange.Length;
        return opening.ConcreteCutout switch
        {
            CircleProfile circle => 2 * Math.PI * circle.RadiusMm * depth / 1_000_000d,
            RectangleProfile rectangle => ((opening.Kind == OpeningKind.OpenTop
                ? 2 * (rectangle.Rectangle.MaxY - rectangle.Rectangle.MinY) + rectangle.Rectangle.MaxX - rectangle.Rectangle.MinX
                : 2 * ((rectangle.Rectangle.MaxX - rectangle.Rectangle.MinX) + (rectangle.Rectangle.MaxY - rectangle.Rectangle.MinY))) * depth) / 1_000_000d,
            PolygonProfile polygon => Perimeter(polygon.Polygon) * depth / 1_000_000d,
            OpenTopProfile openTop => (2 * (openTop.Polygon.Vertices.Max(x => x.Y) - openTop.Polygon.Vertices.Min(x => x.Y)) +
                openTop.Polygon.Vertices.Max(x => x.X) - openTop.Polygon.Vertices.Min(x => x.X)) * depth / 1_000_000d,
            _ => 0
        };
    }

    private static double AreaUnion(IReadOnlyList<Profile2Mm> shapes, double x0, double x1, double y0, double y1, TolerancePolicy tolerance)
    {
        var events = new List<double> { x0, x1 };
        foreach (var shape in shapes)
        {
            switch (shape)
            {
                case CircleProfile circle:
                    events.Add(circle.Center.X - circle.RadiusMm); events.Add(circle.Center.X + circle.RadiusMm); break;
                case RectangleProfile rectangle:
                    events.Add(rectangle.Rectangle.MinX); events.Add(rectangle.Rectangle.MaxX); break;
                case PolygonProfile polygon:
                    events.AddRange(polygon.Polygon.Vertices.Select(x => x.X)); break;
            }
        }
        var ordered = events.Where(x => x >= x0 && x <= x1).Distinct().Order().ToArray();
        double area = 0;
        for (int i = 0; i < ordered.Length - 1; i++)
            area += SimpsonAdaptive(x => Height(shapes, x, y0, y1), ordered[i], ordered[i + 1],
                Math.Max(1e-8, tolerance.AreaMm2.GetValueOrDefault(1) / 100));
        return area;
    }

    private static double Height(IReadOnlyList<Profile2Mm> shapes, double x, double y0, double y1)
    {
        var intervals = new List<(double Low, double High)>();
        foreach (var shape in shapes)
        {
            switch (shape)
            {
                case CircleProfile circle:
                    double dx = x - circle.Center.X;
                    if (Math.Abs(dx) < circle.RadiusMm)
                    {
                        double h = Math.Sqrt(Math.Max(0, circle.RadiusMm * circle.RadiusMm - dx * dx));
                        intervals.Add((circle.Center.Y - h, circle.Center.Y + h));
                    }
                    break;
                case RectangleProfile rectangle when x > rectangle.Rectangle.MinX && x < rectangle.Rectangle.MaxX:
                    intervals.Add((rectangle.Rectangle.MinY, rectangle.Rectangle.MaxY)); break;
            }
        }
        var merged = intervals.Where(x => x.High > y0 && x.Low < y1).OrderBy(x => x.Low).ToList();
        double total = 0, low = 0, high = 0;
        foreach (var item in merged)
        {
            double a = Math.Max(y0, item.Low), b = Math.Min(y1, item.High);
            if (b <= a) continue;
            if (total == 0 || a > high) { total += b - a; low = a; high = b; }
            else if (b > high) { total += b - high; high = b; }
        }
        return total;
    }

    private static double SimpsonAdaptive(Func<double, double> f, double a, double b, double eps)
    {
        double c = (a + b) / 2, fa = f(a), fb = f(b), fc = f(c), whole = (b - a) * (fa + 4 * fc + fb) / 6;
        return Recurse(f, a, b, eps, whole, fa, fb, fc, 0);
    }
    private static double Recurse(Func<double, double> f, double a, double b, double eps, double whole,
        double fa, double fb, double fc, int depth)
    {
        double c = (a + b) / 2, l = (a + c) / 2, r = (c + b) / 2, fl = f(l), fr = f(r);
        double left = (c - a) * (fa + 4 * fl + fc) / 6, right = (b - c) * (fc + 4 * fr + fb) / 6;
        if (depth >= 24 || Math.Abs(left + right - whole) <= 15 * eps) return left + right + (left + right - whole) / 15;
        return Recurse(f, a, c, eps / 2, left, fa, fc, fl, depth + 1) + Recurse(f, c, b, eps / 2, right, fc, fb, fr, depth + 1);
    }

    private static double FootprintArea(Footprint footprint) => Math.Abs(SignedArea(footprint.Outer)) - footprint.InnerLoops.Sum(x => Math.Abs(SignedArea(x)));
    private static double Perimeter(Polygon2Mm polygon) => Enumerable.Range(0, polygon.Vertices.Length).Sum(i =>
        Math.Sqrt(Math.Pow(polygon.Vertices[i].X - polygon.Vertices[(i + 1) % polygon.Vertices.Length].X, 2) +
                  Math.Pow(polygon.Vertices[i].Y - polygon.Vertices[(i + 1) % polygon.Vertices.Length].Y, 2)));
    private static double SignedArea(Polygon2Mm polygon) => Enumerable.Range(0, polygon.Vertices.Length).Sum(i =>
        polygon.Vertices[i].X * polygon.Vertices[(i + 1) % polygon.Vertices.Length].Y - polygon.Vertices[(i + 1) % polygon.Vertices.Length].X * polygon.Vertices[i].Y) / 2;
    private static string OpeningSignature(OpeningProfile opening) => opening.Id + "|" + opening.Kind + "|" + opening.ExtrusionRange;
    private static Diagnostic Review(string code, string componentId, string faceId, string reason) =>
        new(code, DiagnosticSeverity.Review, DiagnosticScope.Global, null, null, null,
            "explicit construction policy", $"{componentId}/{faceId}", reason);

    private sealed class State
    {
        public State(string componentId) { ComponentId = componentId; }
        public string ComponentId { get; }
        public double GrossM2 { get; set; }
        public double DeductionsM2 { get; set; }
    }
    private readonly record struct Decision(bool Include, bool NeedsRule, string Reason)
    {
        public static Decision IncludeAs(string reason) => new(true, false, reason);
        public static Decision Exclude(string reason) => new(false, false, reason);
        public static Decision Needs(string reason) => new(false, true, reason + ": NeedsRule");
    }
}
