using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Drawing;
namespace DBTool.Application;
public interface IDrawingStage
{
    OperationResult<DrawingPackage> Build(ValidatedManholeSpec spec, DrawingInputs inputs, PresentationRequest presentation, PackageIdentity identity);
}
