using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace FaceScan.Web.Helpers;

public static class EnumExtensions
{
    public static string GetDisplayName(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
    }
}
