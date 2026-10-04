using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TeamTracker.Models;

namespace TeamTracker.Services;

[Authorize]
public class BoardHub : Hub
{
    private readonly ITaskRepository _repo;
    public BoardHub(ITaskRepository repo) => _repo = repo;

    public static string TeamGroup(int leaderId) => $"team-{leaderId}";

    public override async Task OnConnectedAsync()
    {
        if (int.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) &&
            _repo.GetUser(id) is { } user)
        {
            var leaderId = user.Role == Roles.Leader ? user.Id : user.LeaderId;
            if (leaderId is not null)
                await Groups.AddToGroupAsync(Context.ConnectionId, TeamGroup(leaderId.Value));
        }
        await base.OnConnectedAsync();
    }
}

public record BoardChange(int TaskId, string Kind, int ActorId, int AssigneeId, string Message);

public interface IBoardNotifier
{
    Task TaskChanged(TaskItem task, string kind, int actorId, string message);
    Task EveryoneReload();
}

public class SignalRBoardNotifier : IBoardNotifier
{
    private readonly IHubContext<BoardHub> _hub;
    public SignalRBoardNotifier(IHubContext<BoardHub> hub) => _hub = hub;

    public Task TaskChanged(TaskItem task, string kind, int actorId, string message) =>
        _hub.Clients.Group(BoardHub.TeamGroup(task.CreatedById))
            .SendAsync("changed", new BoardChange(task.Id, kind, actorId, task.AssignedToId, message));

    public Task EveryoneReload() => _hub.Clients.All.SendAsync("reload");
}
