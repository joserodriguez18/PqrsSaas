using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PqrsSaas.Api.Hubs;

/// <summary>
/// Hub de notificaciones en tiempo real para los agentes. Solo los agentes
/// autenticados se conectan (el widget radica tickets pero no recibe avisos).
/// Cada conexión se agrupa por tenantId para respetar el aislamiento multi-tenant:
/// los eventos se emiten al grupo "tenant-&lt;tenantId&gt;".
/// </summary>
[Authorize]
public class TicketsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst("tenantId")?.Value;
        if (Guid.TryParse(tenantId, out _))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GrupoTenant(tenantId!));
        }
        await base.OnConnectedAsync();
    }

    public static string GrupoTenant(string tenantId) => $"tenant-{tenantId}";
}
