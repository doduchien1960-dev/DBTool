using System.Text;
using DBTool.Domain;
namespace DBTool.Application;
public static class ManholeIdentity
{
    public static string CanonicalText(string text)=>ContractGuard.Text(text,nameof(text)).Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    public static string Create(string projectId,Profile profile,string name)=>
        $"{Uri.EscapeDataString(CanonicalText(projectId))}/{ContractGuard.EnumValue(profile)}/{Uri.EscapeDataString(CanonicalText(name))}";
}
