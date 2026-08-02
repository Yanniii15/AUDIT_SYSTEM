using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AuditCkDayo.Controllers
{
    public class AccountController : Controller
    {
        private readonly AuditDbContext _context;

        public AccountController(AuditDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                if (user != null && BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.Name),
                        new Claim(ClaimTypes.Role, user.Role.ToString())
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return RedirectToAction("Index", "Home");
                }
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            var managers = await _context.Users
                .Where(u => u.Role == UserRole.Manager)
                .ToListAsync();
            ViewBag.Managers = managers;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var existingUser = await _context.Users.AnyAsync(u => u.Email == model.Email);
                if (existingUser)
                {
                    ModelState.AddModelError("Email", "Email address is already in use.");
                }
                else
                {
                    var user = new User
                    {
                        Name = model.Name,
                        Email = model.Email,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                        Role = model.Role,
                        ManagerId = model.Role == UserRole.Buyer ? model.ManagerId : null,
                        PcfBalance = 0.00m,
                        DailyStartingFloat = 0.00m
                    };

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Login));
                }
            }

            var managers = await _context.Users
                .Where(u => u.Role == UserRole.Manager)
                .ToListAsync();
            ViewBag.Managers = managers;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
