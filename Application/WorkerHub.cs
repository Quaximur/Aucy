//using Aucy.Application.Helpers;
//using Aucy.Contracts.OnionClient;
//using MagicOnion.Server.Hubs;
//using Microsoft.AspNetCore.Authorization;

//namespace Aucy.Application;

//[Authorize]
//public class WorkerHub(WorkerGroupService groupService) : StreamingHubBase<IWorkerHub, IWorkerHubReceiver>,
//#pragma warning disable CS8644 
//    IWorkerHub
//#pragma warning restore CS8644 
//{
//    protected override async ValueTask OnConnected()
//    {
//        var userId = Context.GetUserId();

//        await groupService.AddMember(userId, Client);
//    }

//    protected override async ValueTask OnDisconnected()
//    {
//        var userId = Context.GetUserId();

//        await groupService.RemoveMember(userId);
//    }
//}