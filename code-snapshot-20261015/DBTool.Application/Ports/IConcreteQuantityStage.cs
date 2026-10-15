using DBTool.Domain;

namespace DBTool.Application;

public interface IConcreteQuantityStage
{
    OperationResult<ConcreteQuantityResult> Calculate(ValidatedManholeSpec spec,
        GeometryModel geometry, QuantityPolicy policy);
}
