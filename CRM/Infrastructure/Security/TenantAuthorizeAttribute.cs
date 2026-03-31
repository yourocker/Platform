using Microsoft.AspNetCore.Mvc;

namespace CRM.Infrastructure.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class TenantAuthorizeAttribute : TypeFilterAttribute
{
    public TenantAuthorizeAttribute(string permission)
        : base(typeof(TenantPermissionFilter))
    {
        Arguments = new object[] { permission };
        Order = int.MinValue;
    }
}
