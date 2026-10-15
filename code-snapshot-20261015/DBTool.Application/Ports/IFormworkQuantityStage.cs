using DBTool.Domain;

namespace DBTool.Application;

public interface IFormworkQuantityStage
{
    OperationResult<FormworkResult> Calculate(ValidatedManholeSpec spec,
        GeometryModel geometry, FormworkPolicy policy);
}
