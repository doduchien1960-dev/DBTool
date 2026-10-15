using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Drawing;
namespace DBTool.Application;
public interface IManholeValidator
{
    OperationResult<ValidatedProjectInput> Validate(RawProjectInput input, CatalogSet catalogs, CalculationRequest request, CalculationPolicy policy);
}

