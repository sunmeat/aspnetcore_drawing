using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace CollaborativeDrawingBoard.Hubs
{
    // структура для одного штриха
    public class DrawAction
    {
        public List<object> Points { get; set; } = new();
        public string Color { get; set; } = "red";
        public int LineWidth { get; set; } = 5;
    }

    public class DrawHub : Hub
    {
        // історія малюнків (спільна для всіх)
        private static readonly List<DrawAction> DrawHistory = new();

        private static readonly ConcurrentDictionary<string, HashSet<string>> OnlineUsers = new();

        public async Task Connect(string userName)
        {
            userName = string.IsNullOrWhiteSpace(userName) ? "Анонім" : userName.Trim();

            var connectionId = Context.ConnectionId;
            var userSet = OnlineUsers.GetOrAdd(userName, _ => new HashSet<string>());

            lock (userSet)
            {
                userSet.Add(connectionId);
            }

            // відправка новому користувачу всієї історії малюнків
            await Clients.Caller.SendAsync("LoadHistory", DrawHistory);

            await Clients.All.SendAsync("UserConnected", userName);
            await UpdateOnlineUsersList();
        }

        // клієнт надсилає свій новий штрих — розсилаємо іншим
        public async Task DrawPath(List<object> points, string color, int lineWidth = 5)
        {
            var action = new DrawAction
            {
                Points = points,
                Color = color,
                LineWidth = lineWidth
            };

            // пишемо до історії
            lock (DrawHistory)
            {
                DrawHistory.Add(action);
            }

            // надсилаємо ТІЛЬКИ іншим клієнтам (відправник вже собі намалював локально)
            await Clients.Others.SendAsync("DrawPath", points, color, lineWidth);
        }

        public async Task ClearCanvas()
        {
            lock (DrawHistory)
            {
                DrawHistory.Clear();
            }

            await Clients.All.SendAsync("ClearCanvas");
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            string? disconnectedUser = null;

            foreach (var pair in OnlineUsers)
            {
                lock (pair.Value)
                {
                    if (pair.Value.Remove(connectionId) && pair.Value.Count == 0)
                    {
                        OnlineUsers.TryRemove(pair.Key, out _);
                        disconnectedUser = pair.Key;
                        break;
                    }
                }
            }

            if (disconnectedUser != null)
            {
                await Clients.All.SendAsync("UserDisconnected", disconnectedUser);
                await UpdateOnlineUsersList();
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task UpdateOnlineUsersList()
        {
            var users = OnlineUsers.Keys.OrderBy(u => u).ToList();
            await Clients.All.SendAsync("UpdateOnlineUsers", users);
        }
    }
}