using System.Reflection;
using DBTool.Domain;
using DBTool.Drawing;
using Xunit;
namespace DBTool.Application.Tests;
public class ArchitectureContractTests
{
    [Fact] public void AssemblyDependenciesRemainOneWayAndHostFree()
    {
        var domain=typeof(CalculationPolicy).Assembly;
        var drawing=typeof(DrawingPackage).Assembly;
        var application=typeof(CalculationPackage).Assembly;
        Assert.DoesNotContain(domain.GetReferencedAssemblies(),a=>a.Name is "DBTool.Application" or "DBTool.Drawing");
        Assert.DoesNotContain(drawing.GetReferencedAssemblies(),a=>a.Name=="DBTool.Application");
        foreach(var assembly in new[]{domain,drawing,application})
        {
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(),a=>a.Name!.Contains("Autodesk") || a.Name.Contains("AcMgd") || a.Name.Contains("ClosedXML") || a.Name.Contains("PresentationFramework"));
            foreach(var type in assembly.GetExportedTypes().Where(t=>t.Namespace is "DBTool.Domain" or "DBTool.Drawing" or "DBTool.Application"))
                Assert.DoesNotContain(type.GetProperties(BindingFlags.Public|BindingFlags.Instance),p=>p.SetMethod?.IsPublic==true);
        }
    }
    [Fact] public void AllNinePortsArePureContracts()
    {
        var ports=new[]{typeof(IWorkbookImporter),typeof(IRawValueParser),typeof(ICatalogReader),typeof(IManholeValidator),typeof(ISteelWorkbookWriter),typeof(IGeometryStage),typeof(IBaseRebarStage),typeof(ISteelScheduleStage),typeof(IDrawingStage)};
        Assert.All(ports,t=>Assert.True(t.IsInterface));
        foreach(var method in ports.SelectMany(t=>t.GetMethods()))
        {
            Assert.Equal(typeof(OperationResult<>),method.ReturnType.GetGenericTypeDefinition());
            Assert.DoesNotContain(method.GetParameters(),p=>p.ParameterType.Name is "IXLWorkbook" or "ObjectId");
        }
    }
}
