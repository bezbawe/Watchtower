using Microsoft.AspNetCore.SignalR;

namespace Watchtower.Web.Hubs;

// SignalR-хаб live-обновления дашборда. Сервер шлёт клиентам событие "AlertRaised"
// с полезной нагрузкой алерта; клиенты (Blazor-компонент) только слушают.
public class AlertsHub : Hub;
