using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Drawing;
namespace DBTool.Application;
public interface IRawValueParser
{
    OperationResult<NumericValue> ReadNumber(RawCell cell, NumericPolicy policy);
}

