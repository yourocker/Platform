using Core.Entities.Company;
using CRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<Employee> _userManager;
        private readonly SignInManager<Employee> _signInManager;

        public ProfileController(UserManager<Employee> userManager, SignInManager<Employee> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var model = new UserProfileViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                MiddleName = user.MiddleName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                TimezoneId = user.TimezoneId ?? "Russian Standard Time",
                
                NotifySoundEnabled = user.NotifySoundEnabled,
                NotifyDesktopEnabled = user.NotifyDesktopEnabled,
                IsAdvancedSettings = user.IsAdvancedSettings,
                NotifyTaskGeneral = user.NotifyTaskGeneral,
                NotifyTaskAssigned = user.NotifyTaskAssigned,
                NotifyTaskComment = user.NotifyTaskComment
            };

            return PartialView("_ProfileSettings", model);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateInfo(UserProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.MiddleName = model.MiddleName;
            user.TimezoneId = model.TimezoneId;
            user.PhoneNumber = model.PhoneNumber;

            user.NotifySoundEnabled = model.NotifySoundEnabled;
            user.NotifyDesktopEnabled = model.NotifyDesktopEnabled;
            user.IsAdvancedSettings = model.IsAdvancedSettings;
            user.NotifyTaskGeneral = model.NotifyTaskGeneral;
            user.NotifyTaskAssigned = model.NotifyTaskAssigned;
            user.NotifyTaskComment = model.NotifyTaskComment;

            var result = await _userManager.UpdateAsync(user);
            return result.Succeeded ? Ok() : BadRequest("Ошибка при обновлении данных");
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
            if (result.Succeeded)
            {
                await _signInManager.SignOutAsync();
                return Ok();
            }
            return BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}