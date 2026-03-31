using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Core.Entities.Company;
using Core.Data;
using System.Security.Claims;

namespace CRM.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<Employee> _userManager;
        private readonly SignInManager<Employee> _signInManager;
        private readonly AppDbContext _context;

        public AccountController(UserManager<Employee> userManager, SignInManager<Employee> signInManager, AppDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string login, string password, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError(string.Empty, "Введите логин и пароль");
                return View();
            }

            var cleanLogin = login.Trim();
            var normalizedLogin = cleanLogin.ToUpperInvariant();
            var phoneLogin = cleanLogin.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

            var user = await _context.Employees
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u =>
                    u.UserName == cleanLogin ||
                    u.NormalizedUserName == normalizedLogin ||
                    u.Email == cleanLogin ||
                    u.NormalizedEmail == normalizedLogin ||
                    u.PhoneNumber == phoneLogin ||
                    u.PhoneNumber == cleanLogin);

            // 3. САМОЛЕЧЕНИЕ (ИСПРАВЛЕННОЕ)
            // Если нашли пользователя, проверяем критические поля Identity
            if (user != null)
            {
                var needUpdate = false;

                // А. Если UserName пустой - заполняем его из Email или Телефона
                if (string.IsNullOrEmpty(user.UserName))
                {
                    user.UserName = !string.IsNullOrEmpty(user.Email) ? user.Email : login;
                    needUpdate = true;
                }

                // Б. Если NormalizedUserName пустой - обновляем
                if (string.IsNullOrEmpty(user.NormalizedUserName))
                {
                    await _userManager.UpdateNormalizedUserNameAsync(user);
                    needUpdate = true; // UpdateNormalizedUserNameAsync сам сохраняет, но флаг оставим для логики
                }

                // В. Если SecurityStamp пустой - генерируем (ИНАЧЕ БУДЕТ ОШИБКА ВХОДА)
                if (string.IsNullOrEmpty(user.SecurityStamp))
                {
                    await _userManager.UpdateSecurityStampAsync(user);
                    needUpdate = true;
                }

                // Г. Нормализация Email
                if (string.IsNullOrEmpty(user.NormalizedEmail) && !string.IsNullOrEmpty(user.Email))
                {
                    await _userManager.UpdateNormalizedEmailAsync(user);
                    needUpdate = true;
                }
                
                // Если были изменения полей, которые не сохраняет UserManager автоматически
                if (needUpdate)
                {
                   // На всякий случай сохраняем контекст, хотя методы UserManager обычно делают это сами
                   if (_context.ChangeTracker.HasChanges())
                   {
                       await _context.SaveChangesAsync();
                   }
                }
            }

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Неверный логин или пароль");
                return View();
            }

            if (user.IsDismissed)
            {
                ModelState.AddModelError(string.Empty, "Доступ запрещен");
                return View();
            }

            var membership = await ResolveLoginMembershipAsync(user);
            if (membership == null)
            {
                ModelState.AddModelError(string.Empty, "У пользователя нет активного доступа ни к одному tenant.");
                return View();
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                await SignInWithTenantAsync(user, membership, isPersistent: true);
                return RedirectToLocal(returnUrl);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Аккаунт временно заблокирован после нескольких неудачных попыток входа.");
                return View();
            }
            
            ModelState.AddModelError(string.Empty, "Неверный логин или пароль");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SwitchTenant(Guid tenantId, string? returnUrl = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var membership = await _context.EmployeeTenantMemberships
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x =>
                    x.EmployeeId == user.Id &&
                    x.TenantId == tenantId &&
                    x.IsActive &&
                    x.Tenant.IsActive);

            if (membership == null)
            {
                return Forbid();
            }

            await SignInWithTenantAsync(user, membership, isPersistent: true);
            return RedirectToLocal(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        private async Task<EmployeeTenantMembership?> ResolveLoginMembershipAsync(Employee user)
        {
            var currentTenantClaim = User.FindFirstValue("tenant_id");
            if (Guid.TryParse(currentTenantClaim, out var currentTenantId))
            {
                var membershipByClaim = await _context.EmployeeTenantMemberships
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Include(x => x.Tenant)
                    .FirstOrDefaultAsync(x =>
                        x.EmployeeId == user.Id &&
                        x.TenantId == currentTenantId &&
                        x.IsActive &&
                        x.Tenant.IsActive);

                if (membershipByClaim != null)
                {
                    return membershipByClaim;
                }
            }

            return await _context.EmployeeTenantMemberships
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(x => x.Tenant)
                .Where(x => x.EmployeeId == user.Id && x.IsActive && x.Tenant.IsActive)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.JoinedAt)
                .FirstOrDefaultAsync();
        }

        private async Task SignInWithTenantAsync(Employee user, EmployeeTenantMembership membership, bool isPersistent)
        {
            var claims = new List<Claim>
            {
                new("tenant_id", membership.TenantId.ToString()),
                new("tenant_membership_id", membership.Id.ToString()),
                new("tenant_role", membership.RoleCode),
                new(ClaimTypes.Role, membership.RoleCode)
            };

            await _signInManager.SignOutAsync();
            await _signInManager.SignInWithClaimsAsync(user, isPersistent, claims);
        }
    }
}
