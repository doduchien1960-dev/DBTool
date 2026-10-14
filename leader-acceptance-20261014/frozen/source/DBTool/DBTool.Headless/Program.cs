using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Drawing;
using DBTool.Infrastructure.Excel;

using var cancellation=new CancellationTokenSource();
Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;cancellation.Cancel();};
return await DBTool.Headless.HeadlessRunner.RunAsync(args,Console.Out,cancellation.Token);

namespace DBTool.Headless
{
    public static class HeadlessRunner
    {
        public static async Task<int> RunAsync(string[] args,TextWriter output,CancellationToken token,
            ISteelWorkbookWriter? writer=null, IQuantityWorkbookWriter? quantityWriter=null)
        {
            string? staging=null;
            try
            {
                if(args.Length==1&&args[0]=="--help")
                {
                    await output.WriteLineAsync("DBTool.Headless --project NAME --input FILE --profile PROFILE --sheet SHEET --id NAME [--id NAME] --scope BaseRebarOnly --policy S1-Analytic-1 --allow-experimental true --output DIRECTORY [--scale 20|30|40]");
                    return 0;
                }
                var allowed=new HashSet<string>(["--project","--input","--profile","--sheet","--id","--scope","--policy","--allow-experimental","--output","--scale"],StringComparer.Ordinal);
                var options=new Dictionary<string,string>(StringComparer.Ordinal);var names=new List<string>();
                for(int i=0;i<args.Length;i+=2)
                {
                    if(i+1>=args.Length||!allowed.Contains(args[i])||string.IsNullOrWhiteSpace(args[i+1]))throw new ArgumentException("Expected known --name value pairs; use --help.");
                    if(args[i]=="--id")names.Add(args[i+1]);
                    else if(!options.TryAdd(args[i],args[i+1]))throw new ArgumentException("Duplicate argument: "+args[i]);
                }
                double drawingScale=20;
                if(options.TryGetValue("--scale",out var scaleText) &&
                    (!double.TryParse(scaleText,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out drawingScale) ||
                    drawingScale is not (20 or 30 or 40))) throw new ArgumentException("Scale must be 20, 30 or 40.");
                if(options.Count-(options.ContainsKey("--scale")?1:0)!=8||names.Count==0)throw new ArgumentException("All required arguments must be supplied; use --help.");
                if(!Enum.TryParse<Profile>(options["--profile"],false,out var profile)||!Enum.IsDefined(profile)||
                    !Enum.TryParse<CalculationScope>(options["--scope"],false,out var scope)||!Enum.IsDefined(scope)||
                    !bool.TryParse(options["--allow-experimental"],out bool allow))throw new ArgumentException("Invalid profile/scope/allow-experimental.");
                token.ThrowIfCancellationRequested();
                if(names.Count!=1){await output.WriteLineAsync("S1_SINGLE_MANHOLE_ONLY: exactly one --id is required.");return 1;}
                var numeric=new NumericPolicy("en-US",false,false);
                var calculator=new CalculateManholeUseCase(new WorkbookImporter(),new CatalogReader(),
                    new ManholeValidator(new NumericCellReader(),numeric),numeric,
                    new CalculationPipeline(new GeometryStageAdapter(),new FullRebarStage(),new SteelScheduleStageAdapter(),new DrawingStageAdapter(),
                        new ConcreteQuantityStageAdapter(),new FormworkQuantityStageAdapter()));
                var request=new CalculationRequest(options["--project"],names.Select(n=>ManholeIdentity.Create(options["--project"],profile,n)),scope,options["--policy"],allow);
                bool includeDrawing = scope is CalculationScope.BaseRebarOnly or CalculationScope.FullRebar or CalculationScope.Full;
                var result=await calculator.CalculateAsync(new(options["--project"],options["--input"],profile,options["--sheet"]),request,
                    new PresentationRequest(drawingScale,ElevationMode.Symbolic,null,scope == CalculationScope.BaseRebarOnly ? "S1" : SheetOnePresentationPolicy.Family21Theme,includeDrawing),token);
                if(!result.Success){await Diagnostics(result.Diagnostics,output);return 1;}
                var package=result.Value!;
                token.ThrowIfCancellationRequested();
                string root=Path.GetFullPath(options["--output"]);
                Directory.CreateDirectory(root);
                string runName="run-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+"-"+Guid.NewGuid().ToString("N");
                staging=Path.Combine(root,"."+runName+".staging");Directory.CreateDirectory(staging);
                await File.WriteAllTextAsync(Path.Combine(staging,"package.json"),SerializePackage(package),token);
                if (package.Scope is CalculationScope.ConcreteOnly or CalculationScope.FormworkOnly)
                {
                    IEnumerable<QuantityLine> quantityLines = package.Scope == CalculationScope.ConcreteOnly
                        ? package.ConcreteLines! : package.FormworkLines!;
                    await File.WriteAllTextAsync(Path.Combine(staging,"trace.json"),JsonSerializer.Serialize(
                        quantityLines.Select(line => new { line.Id, line.ComponentId, line.MaterialId, line.Unit,
                            line.RuleId, line.TraceIds, Sources = line.Sources.Select(source => new {
                                source.RelativePath, source.Sha256, source.SheetName, source.CellAddress, source.RawText }) })), token);
                    await File.WriteAllTextAsync(Path.Combine(staging,"delta.json"),JsonSerializer.Serialize(new {
                        Status = "NotRun", Reason = "No ReferenceFixture attached to this quantity run.", Scope = package.Scope.ToString()
                    }), token);
                }
                if (package.DrawingPackage is not null)
                    await File.WriteAllTextAsync(Path.Combine(staging,"drawing.json"),DrawingPackageJson.Serialize(package.DrawingPackage,true),token);
                var exporter = new ExportCalculationUseCase(writer??new SteelWorkbookWriter(), quantityWriter??new QuantityWorkbookWriter());
                bool quantityScope = package.Scope is CalculationScope.ConcreteOnly or CalculationScope.FormworkOnly;
                var exported = quantityScope
                    ? await exporter.ExportQuantityAsync(package,new(staging,null,"quantity","result"),token)
                    : await exporter.ExportAsync(package,new(staging,null,"steel","result"),token);
                if(exported.Diagnostics.Any(d=>d.Code=="OPERATION_CANCELLED"))throw new OperationCanceledException();
                token.ThrowIfCancellationRequested();
                if(exported.Success)File.Move(exported.Value!.Path,Path.Combine(staging,quantityScope?"quantity.xlsx":"steel.xlsx"),false);
                string excel=exported.Success?(quantityScope?"QuantityComplete":"Complete"):"Failed";
                string drawing = DrawingStatus(includeDrawing, package.DrawingPackage is not null,
                    package.DrawingPackage is not null && DrawingPackageValidator.ValidatePrimitives(package.DrawingPackage.Views, package.DrawingPackage.Primitives).Success,
                    package.Diagnostics.Any(d => d.Code == "REBAR_COMPOSITION_PARTIAL") ||
                    package.Occurrences.Any(o => o.LengthStatus == LengthStatus.NeedsRule));
                await File.WriteAllTextAsync(Path.Combine(staging,"status.json"),JsonSerializer.Serialize(new{
                    Package="Complete",Drawing=drawing,Excel=excel,Scope=package.Scope.ToString(),PresentationScaleDenominator=drawingScale,Evidence="ExperimentalSoftwareFixture",
                    Recalculation="NotRun",Diagnostics=exported.Diagnostics}),token);
                token.ThrowIfCancellationRequested();
                string final=Path.Combine(root,runName);Directory.Move(staging,final);staging=null;
                await output.WriteLineAsync("RUN_DIRECTORY="+final);
                await output.WriteLineAsync($"Package=Complete Drawing={drawing} Excel={excel} Scope={package.Scope} ExperimentalSoftwareFixture; Concrete={package.ConcreteStatus} Formwork={package.FormworkStatus}; ExcelHostRecalcPending");
                await Diagnostics(package.Diagnostics.Concat(exported.Diagnostics),output);
                return exported.Success?0:2;
            }
            catch(OperationCanceledException){await output.WriteLineAsync("OPERATION_CANCELLED: no final run published.");return 1;}
            catch(ArgumentException ex){await output.WriteLineAsync("CLI_ARGUMENT_INVALID: "+ex.Message);return 1;}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
            {await output.WriteLineAsync("RUN_FAILED: "+ex.Message);return 1;}
            finally
            {
                // Only the private directory allocated by this invocation can be removed.
                if(staging is not null&&Directory.Exists(staging))
                {
                    string full=Path.GetFullPath(staging);
                    if(Path.GetFileName(full).StartsWith(".run-",StringComparison.Ordinal)&&full.EndsWith(".staging",StringComparison.Ordinal))Directory.Delete(full,true);
                }
            }
        }

        public static string DrawingStatus(bool requested, bool hasPayload, bool valid, bool partial) =>
            !requested && !hasPayload ? "NotRequested" : !hasPayload || !valid ? "Failed" : partial ? "Partial" : "Complete";

        private static async Task Diagnostics(IEnumerable<Diagnostic> diagnostics,TextWriter output)
        {foreach(var d in diagnostics)await output.WriteLineAsync($"{d.Severity} {d.Code}: {d.Message} [expected={d.Expected}; actual={d.Actual}; source={d.Source?.CellAddress}]");}

        private static string SerializePackage(CalculationPackage package)
        {
            var resolver=new DefaultJsonTypeInfoResolver();
            resolver.Modifiers.Add(info=>
            {
                Type[] types=info.Type==typeof(PathSegment)?[typeof(LineSegment3Mm),typeof(ArcSegment3Mm)]:
                    info.Type==typeof(Profile2Mm)?[typeof(CircleProfile),typeof(RectangleProfile),typeof(RoundedRectangleProfile),typeof(PolygonProfile),typeof(MultiCellProfile),typeof(OpenTopProfile)]:
                    info.Type==typeof(SolidDescriptor)?[typeof(ExtrusionSolid),typeof(UnionSolid),typeof(DifferenceSolid),typeof(HaunchSolid),typeof(NeckSeatCutSolid)]:[];
                if(types.Length==0)return;
                info.PolymorphismOptions=new JsonPolymorphismOptions{TypeDiscriminatorPropertyName="$type"};
                foreach(var type in types)info.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(type,type.Name));
            });
            return JsonSerializer.Serialize(package,new JsonSerializerOptions{WriteIndented=true,TypeInfoResolver=resolver,Converters={new JsonStringEnumConverter()}});
        }
    }
}

