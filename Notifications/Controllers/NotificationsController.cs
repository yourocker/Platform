using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notifications.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Notifications.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly NotificationDbContext _context;

        public Task<IActionResult> Index() => throw new NotImplementedException("GetHistory");

        public NotificationsController(NotificationDbContext context)
        {
            _context = context;
        }

        // Получение последних 20 уведомлений для пользователя
        [HttpGet("history/{userId}")]
        public async Task<IActionResult> GetHistory(Guid userId)
        {
            var notifications = await _context.UserNotifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(20)
                .ToListAsync();

            return Ok(notifications);
        }

        // Пометка всех уведомлений как прочитанных
        [HttpPost("mark-as-read/{userId}")]
        public async Task<IActionResult> MarkAsRead(Guid userId)
        {
            var unread = await _context.UserNotifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
            {
                n.IsRead = true;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}